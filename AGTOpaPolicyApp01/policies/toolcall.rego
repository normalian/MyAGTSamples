package agentgovernance.toolcall

default allow := false

# Allow all tools except "execute_shell"
allow {
    input.tool_name != "execute_shell"
}

# Deny reason when blocked
reason := "execute_shell is not allowed by OPA policy" {
    input.tool_name == "execute_shell"
}
