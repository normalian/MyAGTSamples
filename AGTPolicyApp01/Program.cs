using AgentGovernance;
using AgentGovernance.Policy;

var kernel = new GovernanceKernel(new GovernanceOptions
{
    PolicyPaths = new() { "policies/default.yaml" },
    ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
    EnableRings = true,                       // Execution ring enforcement
    EnablePromptInjectionDetection = true,    // Scan inputs for injection attacks
    EnableCircuitBreaker = true,              // Resilience for governance evaluations
});

// Evaluate a tool call before execution
var result1 = kernel.EvaluateToolCall(
    agentId: "did:mesh:analyst-001",
    toolName: "file_write",
    args: new() { ["path"] = "/etc/config" }
);
Console.WriteLine($"Allowed:{result1.Allowed}, Blocked: {result1.Reason}");

// Evaluate a tool call before execution
var result2 = kernel.EvaluateToolCall(
    agentId: "did:mesh:analyst-002",
    toolName: "http_request",
    args: new() { ["url"] = "http://example.com" }
);
Console.WriteLine($"Allowed:{result2.Allowed}, Blocked: {result2.Reason}");
