# MyAGTSamples

Sample .NET console applications for Microsoft Agent Governance scenarios.

This repository includes five focused examples that cover policy evaluation, agent identity and trust, Microsoft Agent Framework (MAF) integration, and audit/telemetry export.

## What's in this repository

| Project | Focus | Target Framework |
|---|---|---|
| `AGTPolicyApp01` | Basic policy-driven tool call evaluation with a local YAML policy | `net9.0` |
| `AGTPolicyWithMAFApp02` | MAF + Azure OpenAI with governance middleware and tool blocking | `net9.0` |
| `AGTIdentityApp01` | Agent identity (DID/public key) and trust score basics | `net9.0` |
| `AGTIdentityWithMAFApp02` | Trust-score-aware tool execution in a MAF agent flow | `net9.0` |
| `AGTAuditBlobTelemetryApp01` | Governance audit to Azure Blob + telemetry to Application Insights | `net8.0` |

## Solution structure

- `MyAGTSamples.sln` contains all five projects above.
- Policy files are under each project's `policies/` folder where applicable.
- `AGTAuditBlobTelemetryApp01` also includes `BlobAuditSink.cs` and a project-level README with deep details.

## Prerequisites

1. .NET SDKs:
   - .NET 9 SDK
2. Azure CLI (`az`) signed in when running MAF/OpenAI samples that use `AzureCliCredential`.
3. Access to Azure OpenAI (for MAF/OpenAI-based projects).

Check installed SDKs:

```powershell
dotnet --list-sdks
```

## Build all projects

From repository root:

```powershell
dotnet restore
dotnet build MyAGTSamples.sln
```

## Run each sample

From repository root:

### 1) AGTPolicyApp01

```powershell
dotnet run --project AGTPolicyApp01/AGTPolicyApp01.csproj
```

Behavior:
- Loads `AGTPolicyApp01/policies/default.yaml`
- Evaluates sample tool calls (for example `file_write`, `http_request`)
- Prints allow/deny results

### 2) AGTIdentityApp01

```powershell
dotnet run --project AGTIdentityApp01/AGTIdentityApp01.csproj
```

Behavior:
- Creates an agent identity
- Prints DID/public key/status
- Loads trust score from a local file trust store

### 3) AGTPolicyWithMAFApp02

Required environment variables:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT_NAME` (optional, default in code: `gpt-4.1-mini`)

Run:

```powershell
dotnet run --project AGTPolicyWithMAFApp02/AGTPolicyWithMAFApp02.csproj
```

Behavior:
- Uses governance middleware with MAF tools
- Applies policy from `AGTPolicyWithMAFApp02/policies/default.yaml`
- Demonstrates a blocked `GetWeather` call and a normal non-tool response

### 4) AGTIdentityWithMAFApp02

Required environment variables:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT_NAME` (optional, default in code: `gpt-4.1-mini`)

Run:

```powershell
dotnet run --project AGTIdentityWithMAFApp02/AGTIdentityWithMAFApp02.csproj
```

Behavior:
- Uses trust-based policy from `AGTIdentityWithMAFApp02/policies/trust-based.yaml`
- Starts at trust score 500, then applies a penalty
- Shows first tool call allowed and second tool call blocked

### 5) AGTAuditBlobTelemetryApp01

Required environment variables:
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT` (optional, default in code: `gpt-4o-mini`)
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `AZURE_STORAGE_ACCOUNT_NAME`

Optional:
- `AZURE_TENANT_ID`
- `AUDIT_STORAGE_CONTAINER` (default: `agt-audit`)

Run:

```powershell
dotnet run --project AGTAuditBlobTelemetryApp01/AGTAuditBlobTelemetryApp01.csproj
```

Behavior:
- Writes governance events to an Azure Blob append blob (JSONL)
- Exports logs/metrics/traces via Azure Monitor OpenTelemetry exporter
- Demonstrates allow/deny policy decisions and direct governance evaluation

## Policy files

| File | Default action | Notable rule |
|---|---|---|
| `AGTPolicyApp01/policies/default.yaml` | `deny` | Rate limits `http_request` at `100/minute` |
| `AGTPolicyWithMAFApp02/policies/default.yaml` | `allow` | Denies `GetWeather` |
| `AGTIdentityWithMAFApp02/policies/trust-based.yaml` | `allow` | Denies calls when `trust_score < 500` |
| `AGTAuditBlobTelemetryApp01/policies/default.yaml` | `deny` | Allows specific tools and denies `execute_shell` |

## Common troubleshooting

- Build fails due to SDK mismatch:
  - Install missing .NET SDK version (8 and/or 9), then re-run `dotnet build`.
- Azure auth errors:
  - Run `az login` and verify the active subscription/tenant.
- Environment variable errors:
  - Confirm required variables are set for the project you run.
- Policy file not found:
  - Build first so policy files are copied to output directories.

## Notes

- This repository intentionally mixes simple local-only samples and Azure-connected samples.
- For deeper audit/telemetry details, see `AGTAuditBlobTelemetryApp01/README.md`.
