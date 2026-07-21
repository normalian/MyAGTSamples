# AGTCedarPolicyApp01

A sample project for Agent Governance Toolkit using the Cedar policy engine.

## Overview

This project demonstrates how to use the `PolicyEngine.LoadCedar()` method from the Microsoft.AgentGovernance package to load Cedar policies and perform governance evaluation of tool calls.

## Prerequisites

- .NET 10 SDK
- Microsoft.AgentGovernance 3.5.0 or later

## Project Structure

- `Program.cs` - Main program that loads Cedar policies and evaluates tool calls
- `policies/toolcall.cedar` - Cedar policy file (for reference)

## How to Run

From the repository root:

```powershell
dotnet run --project AGTCedarPolicyApp01/AGTCedarPolicyApp01.csproj
```

Or from the project directory:

```powershell
cd AGTCedarPolicyApp01
dotnet run
```

## Sample Behavior

This sample performs the following:

1. Creates a `GovernanceKernel` and configures `ConflictResolutionStrategy.DenyOverrides`
2. Loads Cedar policy content defined inline using `PolicyEngine.LoadCedar()`
3. Evaluates three tool calls (`http_request`, `execute_shell`, `file_read`)
4. Outputs the allow/deny results and reasons for each tool call

## About Cedar Policies

This sample uses a basic Cedar `permit` policy:

```cedar
// Default: allow all tool calls
permit(principal, action, resource);
```

### Limitations

The Cedar implementation in Microsoft.AgentGovernance may have limited support for advanced filtering using resource attributes (such as `resource.tool_name`).

**If you need tool-specific allow/deny rules, consider the following alternatives:**

- **YAML policies** - See `AGTPolicyApp01`
- **OPA/Rego policies** - See `AGTOpaPolicyApp01`

These policy formats support richer conditional expressions and filtering by tool name.

## Cedar Policy Basic Syntax

Cedar policies have the following structure:

```cedar
// Permit policy
permit(
  principal == User::"alice", 
  action    == Action::"view", 
  resource  == Photo::"vacation.jpg"
);

// Forbid policy
forbid(
  principal == User::"bob", 
  action    == Action::"delete", 
  resource  == Photo::"vacation.jpg"
);

// Conditional policy
permit(
  principal,
  action,
  resource
)
when {
  resource.owner == principal
};
```

## Related Projects

- **AGTPolicyApp01** - Basic governance evaluation using YAML policies
- **AGTOpaPolicyApp01** - Governance evaluation using OPA/Rego policies
- **AGTPolicyWithMAFApp02** - Integration example with Microsoft Agent Framework

## References

- [Cedar Policy Language](https://www.cedarpolicy.com/)
- [Microsoft.AgentGovernance NuGet Package](https://www.nuget.org/packages/Microsoft.AgentGovernance)
- [Agent Governance Toolkit GitHub](https://github.com/microsoft/agent-governance-toolkit)
