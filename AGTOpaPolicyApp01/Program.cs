using AgentGovernance;
using AgentGovernance.Policy;

// Create a GovernanceKernel with basic options
var kernel = new GovernanceKernel(new GovernanceOptions
{
    ConflictStrategy = ConflictResolutionStrategy.DenyOverrides,
});

// Load an OPA/Rego policy from a local .rego file
kernel.PolicyEngine.LoadOpa(
    "policies/toolcall.rego",       // regoPath
    "",                              // opaUrl (empty for local evaluation)
    "agentgovernance.toolcall",      // package
    "allow",                         // rule
    OpaEvaluationMode.Local
);

Console.WriteLine("=== AGTOpaPolicyApp01: PolicyEngine.LoadOpa Sample ===");
Console.WriteLine();

// Test 1: A tool call that should be allowed
var result1 = kernel.EvaluateToolCall(
    agentId: "did:mesh:analyst-001",
    toolName: "http_request",
    args: new() { ["url"] = "https://example.com" }
);
Console.WriteLine($"Tool: http_request  -> Allowed: {result1.Allowed}, Reason: {result1.Reason}");

// Test 2: A tool call that should be denied by the Rego policy
var result2 = kernel.EvaluateToolCall(
    agentId: "did:mesh:analyst-001",
    toolName: "execute_shell",
    args: new() { ["command"] = "rm -rf /" }
);
Console.WriteLine($"Tool: execute_shell -> Allowed: {result2.Allowed}, Reason: {result2.Reason}");

// Test 3: Another allowed tool call
var result3 = kernel.EvaluateToolCall(
    agentId: "did:mesh:analyst-002",
    toolName: "file_read",
    args: new() { ["path"] = "/tmp/data.txt" }
);
Console.WriteLine($"Tool: file_read     -> Allowed: {result3.Allowed}, Reason: {result3.Reason}");

Console.WriteLine();
Console.WriteLine("Done.");
