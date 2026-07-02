using AgentGovernance;
using AgentGovernance.Extensions.ModelContextProtocol;
using AgentGovernance.Policy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// Configure the MCP server with governance applied via WithGovernance().
// This ensures all tool invocations pass through the AGT policy engine.
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new() { Name = "AGTMcpGovernedServer", Version = "1.0.0" };
})
.WithGovernance(governance =>
{
    governance.PolicyPaths.Add("policies/default.yaml");
    governance.ConflictStrategy = ConflictResolutionStrategy.DenyOverrides;
    governance.FailOnUnsafeTools = true;       // Block unsafe tools at startup
    governance.SanitizeResponses = true;       // Strip PII/secrets from responses
    governance.RequireAuthenticatedAgentId = true; // Require authenticated agent_id
})
.WithToolsFromAssembly();

Console.Error.WriteLine("AGT MCP Governed Server is running.");
Console.Error.WriteLine("Policy: default_action=deny, FailOnUnsafeTools=true, SanitizeResponses=true");
Console.Error.WriteLine("Allowed tools: weather_lookup, stock_price, knowledge_search");
Console.Error.WriteLine("Blocked tools: shell_exec");

var app = builder.Build();
app.Run();

/// <summary>
/// MCP tools governed by AGT policy engine.
/// The "shell_exec" tool is intentionally included to demonstrate FailOnUnsafeTools blocking.
/// </summary>
[McpServerToolType]
public class GovernedTools
{
    [McpServerTool(Name = "weather_lookup"), Description("Get current weather for a city")]
    public static string WeatherLookup(string city = "Tokyo")
    {
        return $"Weather in {city}: 22°C, partly cloudy";
    }

    [McpServerTool(Name = "stock_price"), Description("Get latest stock price")]
    public static string StockPrice(string symbol = "MSFT")
    {
        return $"{symbol}: $425.30 (+1.2%)";
    }

    [McpServerTool(Name = "knowledge_search"), Description("Search internal knowledge base")]
    public static string KnowledgeSearch(string query = "")
    {
        return $"Found 3 results for '{query}'";
    }

    [McpServerTool(Name = "shell_exec"), Description("Execute a shell command (UNSAFE)")]
    public static string ShellExec(string command = "")
    {
        // This tool is in the deny list — will be blocked at startup due to FailOnUnsafeTools
        return "This should never execute";
    }
}
