using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Net;
using System.Text.Json;


namespace AGTPolicyWithMAFApp02
{


    class Program
    {

        [Description("Get weather information for a location.")]
        static string GetWeather([Description("Target city name")] string city)
        {
            return $"{city} is sunny, 22°C";
        }

        static async Task Main(string[] args)
        {
            string agentName = "myagent";
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";

            var kernel = new GovernanceKernel(new GovernanceOptions
            {
                PolicyPaths = new() { "policies/default.yaml" },
                EnablePromptInjectionDetection = true,
            });

            // Subscribe to all governance events for monitoring and debugging purposes.
            kernel.OnAllEvents(evt =>
            {
                Console.WriteLine($"[Governance Event] Type: {evt.Type}, Tool: {evt.PolicyName}, Agent: {evt.AgentId}");
            });

            AIAgent agent = new AzureOpenAIClient(new Uri(endpoint),
                new AzureCliCredential())
                .GetChatClient(deploymentName)
                .AsIChatClient()
                .AsAIAgent(
                    name: agentName,
                    instructions: "You are an helpful assistant.",
                    tools:[
                        AIFunctionFactory.Create(GetWeather, name: "GetWeather")
                    ]
                ).WithGovernance(
                    kernel,
                    new AgentFrameworkGovernanceOptions
                    {
                        DefaultAgentId = agentName,
                        EnableFunctionMiddleware = true,
                        BlockedToolResultFactory = toolResult =>
                        {
                            Console.WriteLine($"[BLOCKED TOOL] {toolResult.AuditEntry.PolicyName}: {toolResult.Reason}");
                            return $"'{toolResult.AuditEntry.AgentId}' was blocked for tool calling";
                        }
                    });



            // Agent will be block for tool calling and the second question will be allowed.
            var response1 = await agent.RunAsync("What is the weather in Seattle?");
            Console.WriteLine("Agent response1:");
            foreach (var message in response1.Messages)
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

            var response2 = await agent.RunAsync("Who you are?");
            Console.WriteLine("Agent response2:");
            foreach (var message in response2.Messages)
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
}