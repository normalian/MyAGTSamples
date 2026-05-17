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

        // Configuration from environment variables
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") 
            ?? "gpt-4o-mini";
        var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") 
            ?? throw new InvalidOperationException("APPLICATIONINSIGHTS_CONNECTION_STRING environment variable is not set.");
        var storageAccountName = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_NAME") 
            ?? throw new InvalidOperationException("AZURE_STORAGE_ACCOUNT_NAME environment variable is not set.");
        var storageAccountUri = $"https://{storageAccountName}.blob.core.windows.net/";
        var auditContainerName = Environment.GetEnvironmentVariable("AUDIT_STORAGE_CONTAINER") ?? "agt-audit";

        // Azure AD Tenant ID (optional - uses default tenant if not specified)
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

        // Shared Azure CLI credential for both OpenAI and Blob Storage
        var credential = tenantId is not null
            ? new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId })
            : new AzureCliCredential();

        var agent = AgentIdentity.Create(
            name: "WeatherAgent",
            sponsor: "alice@company.com",
            capabilities: ["read:weather", "read:time"],
            organization: "Analytics");

        Console.WriteLine($"Agent DID: {agent.Did}");
        Console.WriteLine($"Agent Status: {agent.Status}");
        Console.WriteLine($"Telemetry: Application Insights via {nameof(AzureMonitorExporterOptions)}");
        Console.WriteLine($"Audit: Azure Blob Storage container '{auditContainerName}'");
        Console.WriteLine();

        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.AddAzureMonitorLogExporter(options =>
                {
                    options.ConnectionString = applicationInsightsConnectionString;
                });
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("AgentGovernance.Examples");

        // Configure OpenTelemetry with Metrics and Traces
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("AuditBlobTelemetry", serviceVersion: "1.0.0");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(GovernanceMetrics.MeterName)
            .AddAzureMonitorMetricExporter(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
            })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(GovernanceMetrics.MeterName)
            .AddSource(ActivitySource.Name)
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = applicationInsightsConnectionString;
            })
            .Build();

        logger.LogInformation("✓ OpenTelemetry providers initialized");
        logger.LogInformation("  - Metrics: {MeterName}", GovernanceMetrics.MeterName);
        logger.LogInformation("  - Traces: {ActivitySourceName}", ActivitySource.Name);
        logger.LogInformation("  - Logs: AgentGovernance.Examples");

        Console.WriteLine("✓ OpenTelemetry providers initialized");
        Console.WriteLine($"  - Metrics: {GovernanceMetrics.MeterName}");
        Console.WriteLine($"  - Traces: {ActivitySource.Name}");
        Console.WriteLine($"  - Logs: AgentGovernance.Examples");
        Console.WriteLine();

        using var auditSink = new BlobAuditSink(
            storageAccountUri,
            credential,
            auditContainerName,
            blobName: $"governance-audit-{DateTime.UtcNow:yyyyMMdd}.jsonl");

        var kernel = new GovernanceKernel(new GovernanceOptions
        {
            PolicyPaths = [Path.Join(AppContext.BaseDirectory, "policies", "default.yaml")],
            ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
            EnablePromptInjectionDetection = true,
        });

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

        var chatClient = new AzureOpenAIClient(new Uri(endpoint), credential)
            .GetChatClient(deploymentName)
            .AsIChatClient();

        AIAgent CreateAgent()
        {
            return chatClient
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
        }

        var aiAgent = CreateAgent();

        Console.WriteLine("\n=== Agent Run 1 ===");
        using (var activity = ActivitySource.StartActivity("AgentRun", ActivityKind.Client))
        {
            activity?.SetTag("query", "What is the weather in Seattle?");
            activity?.SetTag("run.number", 1);
            logger.LogInformation("Starting Agent Run 1: What is the weather in Seattle?");
            var response1 = await aiAgent.RunAsync("What is the weather in Seattle?");
            PrintResponse(response1);
            logger.LogInformation("Completed Agent Run 1");
        }

        Console.WriteLine("\n=== Agent Run 2 ===");
        using (var activity = ActivitySource.StartActivity("AgentRun", ActivityKind.Client))
        {
            activity?.SetTag("query", "What time is it in Tokyo?");
            activity?.SetTag("run.number", 2);
            logger.LogInformation("Starting Agent Run 2: What time is it in Tokyo?");
            var response2 = await aiAgent.RunAsync("What time is it in Tokyo?");
            PrintResponse(response2);
            logger.LogInformation("Completed Agent Run 2");
        }

        Console.WriteLine("\n=== Agent Run 3 (New Allow Example) ===");
        using (var activity = ActivitySource.StartActivity("AgentRun", ActivityKind.Client))
        {
            activity?.SetTag("query", "Where is Paris located?");
            activity?.SetTag("run.number", 3);
            logger.LogInformation("Starting Agent Run 3: Where is Paris located?");
            var response3 = await aiAgent.RunAsync("Where is Paris located?");
            PrintResponse(response3);
            logger.LogInformation("Completed Agent Run 3");
        }

        Console.WriteLine("\n=== Direct Governance Demo - Allow Example ===");
        using (var activity = ActivitySource.StartActivity("DirectGovernanceEvaluation", ActivityKind.Internal))
        {
            activity?.SetTag("tool.name", "GetLocation");
            activity?.SetTag("decision", "allow");
            var allowed = kernel.EvaluateToolCall(
                agent.Did,
                "GetLocation",
                new Dictionary<string, object>
                {
                    ["city"] = "London"
                });

            logger.LogInformation(
                "Direct governance evaluation: GetLocation allowed={Allowed} reason={Reason}",
                allowed.Allowed,
                allowed.Reason);
            Console.WriteLine($"GetLocation call allowed? {allowed.Allowed}");
            Console.WriteLine($"GetLocation call reason: {allowed.Reason}");
        }

        Console.WriteLine("\n=== Direct Governance Demo - Deny Example ===");
        using (var activity = ActivitySource.StartActivity("DirectGovernanceEvaluation", ActivityKind.Internal))
        {
            activity?.SetTag("tool.name", "execute_shell");
            activity?.SetTag("decision", "deny");
            var blocked = kernel.EvaluateToolCall(
                agent.Did,
                "execute_shell",
                new Dictionary<string, object>
                {
                    ["cmd"] = "rm -rf /"
                });

            logger.LogWarning(
                "Direct governance evaluation: execute_shell allowed={Allowed} reason={Reason}",
                blocked.Allowed,
                blocked.Reason);
            Console.WriteLine($"Blocked call allowed? {blocked.Allowed}");
            Console.WriteLine($"Blocked call reason: {blocked.Reason}");
        }

        Console.WriteLine();
        Console.WriteLine("Telemetry is exported through the Azure Monitor OpenTelemetry exporter.");
        Console.WriteLine("Audit events are appended to a JSONL blob in Azure Storage.");

        // Flush telemetry to Application Insights and wait for completion
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
        Console.WriteLine($"  - Metrics: customMetrics table");
        Console.WriteLine($"  - Traces: traces/dependencies tables");
        Console.WriteLine($"  - Logs: traces table (severityLevel column)");
        Console.WriteLine($"  - Time range: Last 30 minutes");
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

    private static string GetRequiredEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is not set.");
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
