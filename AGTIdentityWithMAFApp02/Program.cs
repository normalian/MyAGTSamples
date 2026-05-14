using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using AgentGovernance.Trust;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace AGTIdentityWithMAFApp02;

class Program
{
    // Trust score threshold for tool access (Ring 2)
    private const double TrustScoreThreshold = 500.0;

    // Penalty applied after first execution (simulating a risk signal)
    private const double TrustPenalty = 150.0;

    // Shared trust store instance
    private static FileTrustStore? _trustStore;
    private static string? _agentDid;

    [Description("Get weather information for a location.")]
    static string GetWeather([Description("Target city name")] string city)
    {
        // Check trust score before executing the tool
        if (_trustStore != null && _agentDid != null)
        {
            var currentScore = _trustStore.GetScore(_agentDid);
            Console.WriteLine($"  [Trust Check in Tool] Score: {currentScore:F0}, Threshold: {TrustScoreThreshold}");

            if (currentScore < TrustScoreThreshold)
            {
                return $"[BLOCKED] Tool execution denied. Trust score ({currentScore:F0}) is below threshold ({TrustScoreThreshold}).";
            }
        }

        return $"{city} is sunny, 22°C";
    }

    [Description("Get current time for a timezone.")]
    static string GetTime([Description("Timezone name")] string timezone)
    {
        // Check trust score before executing the tool
        if (_trustStore != null && _agentDid != null)
        {
            var currentScore = _trustStore.GetScore(_agentDid);
            Console.WriteLine($"  [Trust Check in Tool] Score: {currentScore:F0}, Threshold: {TrustScoreThreshold}");

            if (currentScore < TrustScoreThreshold)
            {
                return $"[BLOCKED] Tool execution denied. Trust score ({currentScore:F0}) is below threshold ({TrustScoreThreshold}).";
            }
        }

        return $"Current time in {timezone}: {DateTime.UtcNow:HH:mm:ss} UTC";
    }

    static async Task Main(string[] args)
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4.1-mini";

        // ========================================
        // 1. Create Agent Identity with Trust Score
        // ========================================
        Console.WriteLine("=== Creating Agent Identity ===");

        var agent = AgentIdentity.Create(
            name: "WeatherAgent",
            sponsor: "alice@company.com",
            capabilities: ["read:weather", "read:time"],
            organization: "Analytics"
        );

        _agentDid = agent.Did;

        Console.WriteLine($"Agent DID: {agent.Did}");
        Console.WriteLine($"Agent Status: {agent.Status}");
        Console.WriteLine($"Sponsor: {agent.SponsorEmail}");
        Console.WriteLine($"Capabilities: {string.Join(", ", agent.Capabilities)}");

        // ========================================
        // 2. Initialize Trust Store with default score (500)
        // ========================================
        var trustStorePath = "trust-scores.json";

        // Clean up previous trust store for demo
        if (File.Exists(trustStorePath))
        {
            File.Delete(trustStorePath);
        }

        _trustStore = new FileTrustStore(trustStorePath, defaultScore: 500);

        var initialScore = _trustStore.GetScore(agent.Did);
        Console.WriteLine($"\nInitial Trust Score: {initialScore}");
        Console.WriteLine($"Trust Level: {GetTrustLevel(initialScore)}");

        // ========================================
        // 3. Setup Governance Kernel
        // ========================================
        var kernel = new GovernanceKernel(new GovernanceOptions
        {
            PolicyPaths = ["policies/trust-based.yaml"],
            EnablePromptInjectionDetection = true,
        });

        // Subscribe to governance events
        kernel.OnAllEvents(evt =>
        {
            Console.WriteLine($"[Governance] Type: {evt.Type}, Tool: {evt.PolicyName}, Agent: {evt.AgentId}");
        });

        // ========================================
        // 4. Create MAF Agent
        // ========================================
        var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            .GetChatClient(deploymentName)
            .AsIChatClient();

        AIAgent CreateAgent()
        {
            return chatClient
                .AsAIAgent(
                    name: agent.Name,
                    instructions: "You are a helpful assistant that provides weather and time information.",
                    tools:
                    [
                        AIFunctionFactory.Create(GetWeather, name: "GetWeather"),
                        AIFunctionFactory.Create(GetTime, name: "GetTime")
                    ]
                )
                .WithGovernance(
                    kernel,
                    new AgentFrameworkGovernanceOptions
                    {
                        DefaultAgentId = agent.Did,
                        EnableFunctionMiddleware = true,
                        BlockedToolResultFactory = toolResult =>
                        {
                            Console.WriteLine($"[BLOCKED by Policy] {toolResult.AuditEntry.PolicyName}: {toolResult.Reason}");
                            return $"Tool call blocked by governance policy.";
                        }
                    });
        }

        // ========================================
        // 5. First Execution (Score = 500, should PASS)
        // ========================================
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("=== FIRST EXECUTION (Trust Score >= 500) ===");
        Console.WriteLine(new string('=', 50));

        var currentScore = _trustStore.GetScore(agent.Did);
        Console.WriteLine($"Current Trust Score: {currentScore:F0}");
        Console.WriteLine($"Trust Level: {GetTrustLevel(currentScore)}");
        Console.WriteLine($"Expected Result: ALLOWED (score >= {TrustScoreThreshold})");
        Console.WriteLine();

        var aiAgent1 = CreateAgent();
        var response1 = await aiAgent1.RunAsync("What is the weather in Tokyo?");

        Console.WriteLine("\nAgent Response 1:");
        PrintResponse(response1);

        // ========================================
        // 6. Apply Trust Penalty (Simulating Risk Signal)
        // ========================================
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("=== APPLYING TRUST PENALTY (Risk Signal) ===");
        Console.WriteLine(new string('=', 50));

        var scoreBefore = _trustStore.GetScore(agent.Did);
        Console.WriteLine($"Score Before Penalty: {scoreBefore:F0}");
        Console.WriteLine($"Applying Penalty: -{TrustPenalty} (Reason: Unusual data access pattern detected)");

        // Use SetScore to reduce the trust score (simulating RecordPenalty)
        var newScore = scoreBefore - TrustPenalty;
        _trustStore.SetScore(agent.Did, newScore);

        var scoreAfter = _trustStore.GetScore(agent.Did);
        Console.WriteLine($"Score After Penalty: {scoreAfter:F0}");
        Console.WriteLine($"Trust Level: {GetTrustLevel(scoreAfter)}");

        // ========================================
        // 7. Second Execution (Score < 500, should BLOCK)
        // ========================================
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("=== SECOND EXECUTION (Trust Score < 500) ===");
        Console.WriteLine(new string('=', 50));

        currentScore = _trustStore.GetScore(agent.Did);
        Console.WriteLine($"Current Trust Score: {currentScore:F0}");
        Console.WriteLine($"Trust Level: {GetTrustLevel(currentScore)}");
        Console.WriteLine($"Expected Result: BLOCKED (score < {TrustScoreThreshold})");
        Console.WriteLine();

        var aiAgent2 = CreateAgent();
        var response2 = await aiAgent2.RunAsync("What is the weather in Seattle?");

        Console.WriteLine("\nAgent Response 2:");
        PrintResponse(response2);

        // ========================================
        // 8. Summary
        // ========================================
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"Agent DID: {agent.Did}");
        Console.WriteLine($"Initial Score: 500 (Standard - Ring 2)");
        Console.WriteLine($"Penalty Applied: -{TrustPenalty}");
        Console.WriteLine($"Final Score: {_trustStore.GetScore(agent.Did):F0} (Restricted - Ring 3)");
        Console.WriteLine($"First Call: ALLOWED (score >= {TrustScoreThreshold})");
        Console.WriteLine($"Second Call: BLOCKED (score < {TrustScoreThreshold})");
    }

    static string GetTrustLevel(double score) => score switch
    {
        >= 900 => "Ring 0 - Verified Partner (Full Access)",
        >= 700 => "Ring 1 - Trusted (Standard Operations)",
        >= 500 => "Ring 2 - Standard (Limited Operations)",
        _ => "Ring 3 - Restricted (Requires Approval)"
    };

    static void PrintResponse(AgentResponse response)
    {
        foreach (var message in response.Messages)
        {
            Console.WriteLine($"  Role: {message.Role}");
            foreach (var content in message.Contents)
            {
                var contentText = content switch
                {
                    TextContent tc => $"    Text: {tc.Text}",
                    FunctionCallContent fc => $"    FunctionCall: {fc.Name}({JsonSerializer.Serialize(fc.Arguments)})",
                    FunctionResultContent fr => $"    FunctionResult: {fr.CallId} = {fr.Result}",
                    _ => $"    {content.GetType().Name}: {content}"
                };
                Console.WriteLine(contentText);
            }
        }
    }
}
