#pragma warning disable MAAI001

using AgentGovernance;
using AgentGovernance.Extensions.Microsoft.Agents;
using AgentGovernance.Policy;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5-mini";
var workingDirectory = Path.Combine(AppContext.BaseDirectory, "working");

var kernel = new GovernanceKernel(new GovernanceOptions
{
    PolicyPaths = [Path.Combine(AppContext.BaseDirectory, "policies", "default.yaml")],
    ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
});

kernel.OnAllEvents(evt =>
{
    Console.WriteLine($"[Governance] Type: {evt.Type}, Tool: {evt.PolicyName}, Agent: {evt.AgentId}");
});

var instructions =
    """
    You are a file access governance demonstration agent.
    Use the file_access_* tools to inspect the sample files.
    Read operations (ls, read, grep) are allowed.
    Write, delete, and replace operations are intentionally denied by governance.
    When a tool is blocked, explain that the Agent Governance Toolkit policy denied it.
    """;

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsHarnessAgent(new HarnessAgentOptions
    {
        Name = "GovernedFileAccessAgent",
        Description = "Demonstrates governed read-only access to sample files.",
        FileAccessStore = new FileSystemAgentFileStore(workingDirectory),
        FileAccessProviderOptions = new FileAccessProviderOptions
        {
            DisableReadOnlyToolApproval = true,
            DisableWriteToolApproval = true,
        },
        ToolApprovalAgentOptions = new ToolApprovalAgentOptions(),
        DisableTodoProvider = true,
        DisableAgentModeProvider = true,
        DisableAgentSkillsProvider = true,
        DisableFileMemory = true,
        DisableWebSearch = true,
        ChatOptions = new ChatOptions { Instructions = instructions },
    })
    .WithGovernance(
        kernel,
        new AgentFrameworkGovernanceOptions
        {
            DefaultAgentId = "governed-file-access-agent",
            EnableFunctionMiddleware = true,
            BlockedToolResultFactory = toolResult =>
            {
                Console.WriteLine($"[BLOCKED by Governance] {toolResult.AuditEntry.PolicyName}: {toolResult.Reason}");
                return $"Tool call blocked by governance policy: {toolResult.Reason}";
            },
        });

Console.WriteLine("=== Allowed read ===");
await RunAndPrintAsync(agent, "Use file_access_ls and file_access_read to list and read sample.txt. Do not modify any files.");

Console.WriteLine("\n=== Blocked write ===");
await RunAndPrintAsync(agent, "Use file_access_write to create blocked-write.txt with the text 'this write must be denied'.");

Console.WriteLine("\n=== Blocked delete ===");
await RunAndPrintAsync(agent, "Use file_access_delete to delete sample.txt. This operation must be denied by governance.");

static async Task RunAndPrintAsync(AIAgent agent, string prompt)
{
    var response = await agent.RunAsync(prompt);
    foreach (var message in response.Messages)
    {
        Console.WriteLine($"  Role: {message.Role}");
        foreach (var content in message.Contents)
        {
            var text = content switch
            {
                TextContent textContent => textContent.Text,
                FunctionCallContent functionCall => $"FunctionCall: {functionCall.Name}({JsonSerializer.Serialize(functionCall.Arguments)})",
                FunctionResultContent functionResult => $"FunctionResult: {functionResult.CallId} = {functionResult.Result}",
                _ => $"{content.GetType().Name}: {content}",
            };
            Console.WriteLine($"    {text}");
        }
    }
}
