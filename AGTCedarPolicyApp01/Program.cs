using AgentGovernance;
using AgentGovernance.Policy;

Console.WriteLine("=== AGTCedarPolicyApp01: PolicyEngine.LoadCedar Sample ===");
Console.WriteLine();

// Example 1: Permit-only policy
Console.WriteLine("--- Example 1: Permit-only policy ---");
var kernel1 = new GovernanceKernel(new GovernanceOptions
{
    ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
});

var permitPolicy = """
    // Allow all tool calls
    permit(principal, action, resource);
    """;

kernel1.PolicyEngine.LoadCedar(permitPolicy);

var result1 = kernel1.EvaluateToolCall(
    agentId: "did:mesh:analyst-001",
    toolName: "http_request",
    args: new() { ["url"] = "https://example.com" }
);
Console.WriteLine($"Tool: http_request  -> Allowed: {result1.Allowed}");

var result2 = kernel1.EvaluateToolCall(
    agentId: "did:mesh:analyst-001",
    toolName: "file_read",
    args: new() { ["path"] = "/tmp/data.txt" }
);
Console.WriteLine($"Tool: file_read     -> Allowed: {result2.Allowed}");

Console.WriteLine();

// Example 2: Permit + Forbid policy (forbid overrides permit)
Console.WriteLine("--- Example 2: Permit + Forbid policy ---");
var kernel2 = new GovernanceKernel(new GovernanceOptions
{
    ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
});

var permitForbidPolicy = """
    // Default: allow all tool calls
    permit(principal, action, resource);

    // But forbid any tool call (demonstrating forbid overrides permit)
    forbid(principal, action, resource);
    """;

kernel2.PolicyEngine.LoadCedar(permitForbidPolicy);

var result3 = kernel2.EvaluateToolCall(
    agentId: "did:mesh:analyst-002",
    toolName: "http_request",
    args: new() { ["url"] = "https://example.com" }
);
Console.WriteLine($"Tool: http_request  -> Allowed: {result3.Allowed}");

var result4 = kernel2.EvaluateToolCall(
    agentId: "did:mesh:analyst-002",
    toolName: "execute_shell",
    args: new() { ["command"] = "ls -la" }
);
Console.WriteLine($"Tool: execute_shell -> Allowed: {result4.Allowed}");

var result5 = kernel2.EvaluateToolCall(
    agentId: "did:mesh:analyst-002",
    toolName: "file_read",
    args: new() { ["path"] = "/tmp/data.txt" }
);
Console.WriteLine($"Tool: file_read     -> Allowed: {result5.Allowed}");

Console.WriteLine();
Console.WriteLine("Note: Cedar 'forbid' policies override 'permit' policies.");
Console.WriteLine("For tool-specific filtering, consider YAML or OPA/Rego policies.");
Console.WriteLine();
Console.WriteLine("Done.");
