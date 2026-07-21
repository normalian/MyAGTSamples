// Cedar policy for Agent Governance Toolkit
// This demonstrates basic Cedar policy syntax with permit rules

// Default: allow all tool calls
permit(principal, action, resource);

// Note: Advanced resource attribute filtering may require
// additional schema configuration in Microsoft.AgentGovernance.
// For tool-specific allow/deny rules, consider using YAML policies
// or OPA/Rego which provide richer filtering capabilities.
