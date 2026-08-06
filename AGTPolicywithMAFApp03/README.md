# AGTPolicywithMAFApp03

This sample combines the Agent Framework Harness `FileAccessProvider` with the
Agent Governance Toolkit. It uses the `Microsoft.Agents.AI.Harness` 1.17.0
package, based on the Harness implementation in:

`C:\opt\workspace\agent-framework.git\dotnet\src\Microsoft.Agents.AI\Harness`

The sample's file store contains `working/sample.txt` and `working/notes/details.txt`.
The governance policy allows `file_access_ls`, `file_access_read`, and
`file_access_grep`, while denying `file_access_write`, `file_access_delete`,
`file_access_replace`, and `file_access_replace_lines`.

## Run

Set `AZURE_OPENAI_ENDPOINT`, sign in with Azure CLI, then run:

```powershell
dotnet run --project AGTPolicywithMAFApp03/AGTPolicywithMAFApp03.csproj
```

The agent reads `sample.txt`, then attempts a write and a delete. The read
succeeds; the write and delete are returned as governance-blocked tool results.

## Agent identity settings

The sample uses two separate identifiers:

- `HarnessAgentOptions.Name = "GovernedFileAccessAgent"` is the Agent Framework
  / Harness agent name.
- `AgentFrameworkGovernanceOptions.DefaultAgentId = "governed-file-access-agent"`
  is the Agent Governance Toolkit identifier used for policy evaluation and
  audit events.

These values are intentionally different and are not treated as aliases.
