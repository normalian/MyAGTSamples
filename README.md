# MyAGTSamples

A minimal .NET 9 console sample that demonstrates policy-driven tool governance using `Microsoft.AgentGovernance`.

## Overview

This repository contains a single application:

- `AGTPolicyApp01`: Evaluates tool calls against a YAML governance policy before execution.

The sample shows how to:

- Load governance policy files from disk.
- Configure governance runtime options (rings, prompt-injection detection, circuit breaker).
- Evaluate tool calls and inspect allow/deny outcomes.

## Tech Stack

- .NET SDK: 9.0
- Language: C#
- Package: `Microsoft.AgentGovernance` (3.5.0)
- Policy format: YAML

## Repository Structure

- `MyAGTSamples.sln`: Solution file.
- `AGTPolicyApp01/AGTPolicyApp01.csproj`: Console app project.
- `AGTPolicyApp01/Program.cs`: Sample governance runtime setup and evaluation calls.
- `AGTPolicyApp01/policies/default.yaml`: Default governance policy.

## Prerequisites

1. Install .NET 9 SDK.
2. Use PowerShell, Terminal, or Command Prompt on Windows/macOS/Linux.

Check your SDK:

```powershell
dotnet --version
```

## Build and Run

From the repository root:

```powershell
dotnet restore
```

```powershell
dotnet build MyAGTSamples.sln
```

Run the sample app:

```powershell
dotnet run --project AGTPolicyApp01/AGTPolicyApp01.csproj
```

## Policy Notes

The default policy file is at:

- `AGTPolicyApp01/policies/default.yaml`

Current policy behavior includes:

- Default action is `deny`.
- A rule to allow tools in `allowed_tools`.
- A rule to deny tools in `blocked_tools`.
- A rate-limit rule for `http_request` (`100/minute`).

The project copies `policies/default.yaml` to output on build, so runtime policy loading works from the app output folder.

## Development Workflow

Typical inner loop:

```powershell
dotnet build AGTPolicyApp01/AGTPolicyApp01.csproj
dotnet run --project AGTPolicyApp01/AGTPolicyApp01.csproj
```

Optional formatting and checks:

```powershell
dotnet format
```

## Contributing

1. Create a feature branch.
2. Make focused changes with clear commit messages.
3. Ensure build passes locally:

```powershell
dotnet build MyAGTSamples.sln
```

4. Open a pull request with:

- What changed
- Why it changed
- How it was tested

## Troubleshooting

- SDK not found:
  - Install .NET 9 SDK and restart terminal.
- Policy file not loaded:
  - Confirm `AGTPolicyApp01/policies/default.yaml` exists.
  - Rebuild to ensure file is copied to output.
- Package restore issues:
  - Verify internet access to NuGet.
  - Run `dotnet nuget locals all --clear` and retry restore.

## License

Add your preferred license in a `LICENSE` file.
