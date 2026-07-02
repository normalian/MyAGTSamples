using AgentGovernance;
using AgentGovernance.Audit;
using AgentGovernance.Extensions.Microsoft.Agents;
using AgentGovernance.Policy;
using AgentGovernance.Telemetry;
using AgentGovernance.Trust;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.ComponentModel;
using System.Text.Json;
using System.Diagnostics;

namespace AgentGovernance.Examples.AuditBlobTelemetry;

internal static class Program
{
    private static readonly ActivitySource ActivitySource = new("AgentGovernance.Examples.AuditBlobTelemetry", "1.0.0");

    private static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Banner("AGT Audit to Blob + Telemetry to Application Insights");

        var config = AppConfiguration.LoadFromEnvironment();
        var credential = config.CreateCredential();

        var agent = CreateAgentIdentity();
        DisplayAgentInfo(agent, config);

        using var loggerFactory = CreateLoggerFactory(config.ApplicationInsightsConnectionString);
        var logger = loggerFactory.CreateLogger("AgentGovernance.Examples");

        var telemetryProviders = CreateTelemetryProviders(config.ApplicationInsightsConnectionString);
        using var meterProvider = telemetryProviders.MeterProvider;
        using var tracerProvider = telemetryProviders.TracerProvider;
        LogTelemetryInitialization(logger);

        using var auditSink = new BlobAuditSink(
            config.StorageAccountUri,
            credential,
            config.AuditContainerName,
            blobName: $"governance-audit-{DateTime.UtcNow:yyyyMMdd}.jsonl");

        var kernel = CreateGovernanceKernel();
        RegisterEventHandler(kernel, auditSink, logger);

        var chatClient = new AzureOpenAIClient(new Uri(config.OpenAIEndpoint), credential)
            .GetChatClient(config.DeploymentName)
            .AsIChatClient();

        var aiAgent = CreateAIAgent(chatClient, agent, kernel, logger);

        await RunAgentQuery(aiAgent, "What is the weather in Seattle?", 1, logger);
        await RunAgentQuery(aiAgent, "What time is it in Tokyo?", 2, logger);
        await RunAgentQuery(aiAgent, "Where is Paris located?", 3, logger);

        EvaluateDirectGovernance(kernel, agent.Did, "GetLocation", new() { ["city"] = "London" }, logger, expectAllow: true);
        EvaluateDirectGovernance(kernel, agent.Did, "execute_shell", new() { ["cmd"] = "rm -rf /" }, logger, expectAllow: false);

        await FlushTelemetry(meterProvider, tracerProvider, loggerFactory, logger);
    }

    private static AgentIdentity CreateAgentIdentity() =>
        AgentIdentity.Create(
            name: "WeatherAgent",
            sponsor: "alice@company.com",
            capabilities: ["read:weather", "read:time"],
            organization: "Analytics");

    private static void DisplayAgentInfo(AgentIdentity agent, AppConfiguration config)
    {
        Console.WriteLine($"Agent DID: {agent.Did}");
        Console.WriteLine($"Agent Status: {agent.Status}");
        Console.WriteLine($"Telemetry: Application Insights via {nameof(AzureMonitorExporterOptions)}");
        Console.WriteLine($"Audit: Azure Blob Storage container '{config.AuditContainerName}'");
        Console.WriteLine();
    }

    private static ILoggerFactory CreateLoggerFactory(string connectionString) =>
        LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
                logging.AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString));
            builder.SetMinimumLevel(LogLevel.Information);
        });

    private static TelemetryProviders CreateTelemetryProviders(string connectionString)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("AuditBlobTelemetry", serviceVersion: "1.0.0");

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(GovernanceMetrics.MeterName)
            .AddAzureMonitorMetricExporter(options => options.ConnectionString = connectionString)
            .Build();

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(GovernanceMetrics.MeterName)
            .AddSource(ActivitySource.Name)
            .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString)
            .Build();

        return new TelemetryProviders(meterProvider, tracerProvider);
    }

    private sealed record TelemetryProviders(MeterProvider MeterProvider, TracerProvider TracerProvider);

    private static void LogTelemetryInitialization(ILogger logger)
    {
        logger.LogInformation("✓ OpenTelemetry providers initialized");
        logger.LogInformation("  - Metrics: {MeterName}", GovernanceMetrics.MeterName);
        logger.LogInformation("  - Traces: {ActivitySourceName}", ActivitySource.Name);
        logger.LogInformation("  - Logs: AgentGovernance.Examples");

        Console.WriteLine("✓ OpenTelemetry providers initialized");
        Console.WriteLine($"  - Metrics: {GovernanceMetrics.MeterName}");
        Console.WriteLine($"  - Traces: {ActivitySource.Name}");
        Console.WriteLine($"  - Logs: AgentGovernance.Examples");
        Console.WriteLine();
    }

    private static GovernanceKernel CreateGovernanceKernel() =>
        new(new GovernanceOptions
        {
            PolicyPaths = [Path.Join(AppContext.BaseDirectory, "policies", "default.yaml")],
            ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
            EnablePromptInjectionDetection = true,
        });

    private static void RegisterEventHandler(GovernanceKernel kernel, BlobAuditSink auditSink, ILogger logger)
    {
        kernel.OnAllEvents(evt =>
        {
            using var activity = ActivitySource.StartActivity($"GovernanceEvent.{evt.Type}", ActivityKind.Internal);
            activity?.SetTag("event.type", evt.Type.ToString());
            activity?.SetTag("agent.id", evt.AgentId);
            activity?.SetTag("policy.name", evt.PolicyName ?? "(none)");
            activity?.SetTag("tool.name", evt.Data.GetValueOrDefault("tool_name", "(unknown)"));

            auditSink.Append(evt);
            logger.LogInformation(
                "[Audit -> Blob] {EventType} policy={PolicyName} tool={ToolName} agentId={AgentId}",
                evt.Type,
                evt.PolicyName ?? "(none)",
                evt.Data.GetValueOrDefault("tool_name", "(unknown)"),
                evt.AgentId);
            Console.WriteLine($"[Audit -> Blob] {evt.Type} policy={evt.PolicyName ?? "(none)"} tool={evt.Data.GetValueOrDefault("tool_name", "(unknown)")}");
        });
    }

    private static AIAgent CreateAIAgent(IChatClient chatClient, AgentIdentity agent, GovernanceKernel kernel, ILogger logger) =>
        chatClient
            .AsAIAgent(
                name: agent.Name,
                instructions: "You are a helpful assistant that provides weather, time, and location information.",
                tools:
                [
                    AIFunctionFactory.Create(GetWeather, name: "GetWeather"),
                    AIFunctionFactory.Create(GetTime, name: "GetTime"),
                    AIFunctionFactory.Create(GetLocation, name: "GetLocation")
                ])
            .WithGovernance(
                kernel,
                new AgentFrameworkGovernanceOptions
                {
                    DefaultAgentId = agent.Did,
                    EnableFunctionMiddleware = true,
                    BlockedToolResultFactory = toolResult =>
                    {
                        logger.LogWarning(
                            "[BLOCKED TOOL] {PolicyName}: {Reason}",
                            toolResult.AuditEntry.PolicyName,
                            toolResult.Reason);
                        Console.WriteLine($"[BLOCKED TOOL] {toolResult.AuditEntry.PolicyName}: {toolResult.Reason}");
                        return "Tool call blocked by governance policy.";
                    }
                });

    private static async Task RunAgentQuery(AIAgent agent, string query, int runNumber, ILogger logger)
    {
        Console.WriteLine($"\n=== Agent Run {runNumber} ===");
        using var activity = ActivitySource.StartActivity("AgentRun", ActivityKind.Client);
        activity?.SetTag("query", query);
        activity?.SetTag("run.number", runNumber);
        logger.LogInformation("Starting Agent Run {RunNumber}: {Query}", runNumber, query);
        var response = await agent.RunAsync(query);
        PrintResponse(response);
        logger.LogInformation("Completed Agent Run {RunNumber}", runNumber);
    }

    private static void EvaluateDirectGovernance(
        GovernanceKernel kernel,
        string agentDid,
        string toolName,
        Dictionary<string, object> args,
        ILogger logger,
        bool expectAllow)
    {
        var title = expectAllow ? "Allow Example" : "Deny Example";
        Console.WriteLine($"\n=== Direct Governance Demo - {title} ===");

        using var activity = ActivitySource.StartActivity("DirectGovernanceEvaluation", ActivityKind.Internal);
        activity?.SetTag("tool.name", toolName);
        activity?.SetTag("decision", expectAllow ? "allow" : "deny");

        var result = kernel.EvaluateToolCall(agentDid, toolName, args);

        var logAction = expectAllow
            ? (Action<string, bool, string>)((msg, allowed, reason) => logger.LogInformation(msg, allowed, reason))
            : ((msg, allowed, reason) => logger.LogWarning(msg, allowed, reason));

        logAction(
            $"Direct governance evaluation: {toolName} allowed={{Allowed}} reason={{Reason}}",
            result.Allowed,
            result.Reason);

        Console.WriteLine($"{toolName} call allowed? {result.Allowed}");
        Console.WriteLine($"{toolName} call reason: {result.Reason}");
    }

    private static async Task FlushTelemetry(
        MeterProvider? meterProvider,
        TracerProvider? tracerProvider,
        ILoggerFactory? loggerFactory,
        ILogger logger)
    {
        Console.WriteLine();
        Console.WriteLine("Telemetry is exported through the Azure Monitor OpenTelemetry exporter.");
        Console.WriteLine("Audit events are appended to a JSONL blob in Azure Storage.");

        Console.WriteLine("\n=== Flushing Telemetry to Application Insights ===");
        logger.LogInformation("Starting telemetry flush to Application Insights");

        Console.WriteLine("Flushing metrics...");
        meterProvider?.ForceFlush();
        Console.WriteLine("Flushing traces...");
        tracerProvider?.ForceFlush();
        Console.WriteLine("Flushing logs...");
        loggerFactory?.Dispose();
        Console.WriteLine("Waiting 5 seconds for data transmission...");
        await Task.Delay(TimeSpan.FromSeconds(5));
        Console.WriteLine("✓ Telemetry flush complete.");

        Console.WriteLine("\nCheck Application Insights for:");
        Console.WriteLine("  - Metrics: customMetrics table");
        Console.WriteLine("  - Traces: traces/dependencies tables");
        Console.WriteLine("  - Logs: traces table (severityLevel column)");
        Console.WriteLine("  - Time range: Last 30 minutes");
    }

    [Description("Get weather information for a location.")]
    private static string GetWeather([Description("Target city name")] string city)
    {
        return $"{city} is sunny, 22°C";
    }

    [Description("Get current time for a timezone.")]
    private static string GetTime([Description("Timezone name")] string timezone)
    {
        return $"Current time in {timezone}: {DateTime.UtcNow:HH:mm:ss} UTC";
    }

    [Description("Get location information for a city.")]
    private static string GetLocation([Description("Target city name")] string city)
    {
        return $"{city} is located at coordinates (48.8566° N, 2.3522° E)";
    }

    private static void PrintResponse(AgentResponse response)
    {
        foreach (var message in response.Messages)
        {
            Console.WriteLine($"  Role: {message.Role}");
            foreach (var content in message.Contents)
            {
                var contentText = content switch
                {
                    TextContent textContent => $"    Text: {textContent.Text}",
                    FunctionCallContent functionCallContent => $"    FunctionCall: {functionCallContent.Name}({JsonSerializer.Serialize(functionCallContent.Arguments)})",
                    FunctionResultContent functionResultContent => $"    FunctionResult: {functionResultContent.CallId} = {functionResultContent.Result}",
                    _ => $"    {content.GetType().Name}: {content}"
                };

                Console.WriteLine(contentText);
            }
        }
    }

    private static void Banner(string title)
    {
        var bar = new string('=', title.Length + 4);
        Console.WriteLine(bar);
        Console.WriteLine($"  {title}");
        Console.WriteLine(bar);
        Console.WriteLine();
    }
}
