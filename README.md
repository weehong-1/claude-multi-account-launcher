# claude-multi-account-launcher

A small Windows app that runs several Claude Desktop accounts side by side,
each in its own profile. See
[docs/spec-2026-11-19-claude-multi-account-launcher.md](docs/spec-2026-11-19-claude-multi-account-launcher.md).

## Requirements

- Windows 10 2004 (19041) or later, x64
- .NET 11 SDK (pinned in `global.json`) to build

Visual Studio is not needed. The Windows App SDK and its build tools come
from NuGet.

## Build and run

```powershell
dotnet build
```

```powershell
dotnet run --project src/ClaudeMultiAccountLauncher
```

## Publish a single self-contained exe

```powershell
dotnet publish src/ClaudeMultiAccountLauncher -c Release -o artifacts/publish
```

The output is `artifacts/publish/claude-multi-account-launcher.exe`. It bundles
the .NET and Windows App SDK runtimes, so it runs without installing anything.
