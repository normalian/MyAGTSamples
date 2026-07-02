# Audit to Blob + Telemetry to Application Insights

A runnable console sample that starts from the same Microsoft Agent Framework
shape as `AGTIdentityWithMAFApp02` and shows how to split governance output
across two destinations:

- **Audit**: append every `GovernanceEvent` to an Azure Blob Storage append blob
  as newline-delimited JSON.
- **Telemetry**: export `GovernanceMetrics` to Application Insights through the
  Azure Monitor OpenTelemetry exporter.

## What it shows

- Creating a `GovernanceKernel` with a YAML policy file
- Subscribing to all audit events and writing them to Blob Storage
- Exporting `agent_governance.*` metrics to Application Insights
- Wiring `GovernanceKernel` into a Microsoft Agent Framework agent
- Running one blocked governance call so you can see the audit path fire

## Required environment variables

Set these before you run the sample:

- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT_NAME` or leave it unset to use `gpt-4.1-mini`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `AZURE_STORAGE_CONNECTION_STRING`

Optional:

- `AUDIT_STORAGE_CONTAINER` default: `agt-audit`

## Run it

From this directory:

```powershell
dotnet run
```

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | Creates the agent, kernel, Blob audit sink, and Application Insights exporter. |
| `BlobAuditSink.cs` | Synchronous append-blob writer used by the audit callback. |
| `policies/default.yaml` | Policy file that allows the weather/time tools and blocks a shell tool. |

### Governance Policy

Tool invocations are controlled by `policies/default.yaml` using a **deny-by-default** strategy with explicit allow rules:

| Tool | Action | Description |
|------|--------|-------------|
| `GetWeather` | Allow | Returns weather information for a given city |
| `GetTime` | Allow | Returns the current time for a given timezone |
| `GetLocation` | Allow | Returns location coordinates for a given city |
| `execute_shell` | Deny | Shell command execution (demo of blocked tool) |

### Output Destinations

| Data | Destination | Details |
|------|------------|---------|
| Audit logs | Azure Blob Storage | `governance-audit-YYYYMMDD.jsonl` (Append Blob) |
| Metrics | Application Insights | `customMetrics` table |
| Traces | Application Insights | `traces` / `dependencies` tables |
| Logs | Application Insights | `traces` table (`severityLevel` column) |

### Dependencies

| Package | Version |
|---------|---------|
| `Azure.AI.OpenAI` | 2.9.0-beta.1 |
| `Azure.Identity` | 1.21.0 |
| `Azure.Monitor.OpenTelemetry.Exporter` | 1.8.2 |
| `Azure.Storage.Blobs` | 12.29.1 |
| `Microsoft.AgentGovernance` | 4.0.0 |
| `Microsoft.AgentGovernance.Extensions.Microsoft.Agents` | 4.0.0 |
| `Microsoft.Agents.AI.OpenAI` | 1.12.0 |
| `OpenTelemetry` | 1.16.0 |

## Target Framework

- `.NET 10`

## License

MIT License
