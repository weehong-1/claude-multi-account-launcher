# claude-multi-account-launcher

Windows launcher to run several Claude Desktop accounts side by side, each
in its own profile, and open any of them with one click.

> **Unofficial.** It starts Claude Desktop with Electron's
> `--user-data-dir` flag. Anthropic doesn't support this, and a Claude
> Desktop update can break it.

Spec: [docs/spec-2026-11-19-claude-multi-account-launcher.md](docs/spec-2026-11-19-claude-multi-account-launcher.md)
· Acceptance status: [docs/acceptance-2026-11-19.md](docs/acceptance-2026-11-19.md)

## Requirements

- Windows 10 2004 (19041) or later, x64
- Claude Desktop, Microsoft Store (MSIX) or the older standalone install

Nothing else. The exe bundles .NET and the Windows App SDK, needs no
installer and no admin rights, and makes no network calls.

## Install

Put `claude-multi-account-launcher.exe` in a folder it will stay in, for
example:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Programs\claude-multi-account-launcher"
```

Desktop shortcuts point at the exe's location. If you move it, create the
shortcuts again.

## Use

- **Default** is your existing Claude account. It is always there.
- **Add profile**: give it a name, e.g. "Personal". The launcher creates
  two folders for it (see below). Its status is *Not signed in yet*.
- **Launch** a *Not signed in yet* profile to sign in:
  1. The launcher asks to close every Claude Desktop window. Sign-in comes
     back through a `claude://` link, and Windows sends that link to
     whichever Claude is already running.
  2. It closes Claude Desktop and opens a fresh Claude for the new profile.
     Claude Code running in a terminal is not touched. Claude Code
     sessions inside Claude Desktop stop with it.
  3. Sign in, come back, and answer **Yes**. The profile becomes *Ready*.
- **Launch** a *Ready* profile or Default: opens it, or brings it to the
  front if it's already running. Several profiles can run at once.
- **⋯ menu**: Create desktop shortcut, Rename, Reset to *Not signed in
  yet*, Remove. Remove asks a second time before deleting the profile's
  folders; keeping them is the default. A profile can't be removed while
  it's running.
- **Desktop shortcut** `Claude - <name>` opens that profile in one click,
  without showing the launcher. It runs
  `claude-multi-account-launcher.exe --launch <slug>`
  (`--launch default` for Default).
- *Running* refreshes when the launcher window gets focus.

## Where things live

| What | Where |
|---|---|
| Profile list | `%APPDATA%\claude-multi-account-launcher\profiles.json` (names, slugs, status; no secrets) |
| Shortcut icon | `%APPDATA%\claude-multi-account-launcher\claude.ico`, copied once from Claude |
| Profile's Claude Desktop data | `%APPDATA%\Claude-<slug>` |
| Profile's Claude Code config | `%USERPROFILE%\.claude-<slug>` (set as `CLAUDE_CONFIG_DIR`) |
| Default's data | Claude's own; the MSIX version keeps it in `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude` |
| Unpacked runtime | `%TEMP%\.net\claude-multi-account-launcher\` (about 160 MB, reused between runs) |

## Known limits

- After Claude updates itself and relaunches, Claude Code may fall back to
  `~/.claude`. Close Claude and launch the profile again from here.
- Connectors (GitLab etc.) must be connected again in each profile.
- Only one profile at a time can run Cowork's VM.
- Profile data folders must stay directly under `%APPDATA%` for Cowork.
  The launcher creates a profile's folder before every launch. MSIX
  Claude would otherwise put a missing folder's data in its package
  cache instead.
- Each new launcher version unpacks into a new folder under
  `%TEMP%\.net\claude-multi-account-launcher\`. Old ones can be deleted.

## Build

Needs the .NET 11 SDK (pinned in `global.json`). Visual Studio is not
needed; the Windows App SDK comes from NuGet.

```powershell
dotnet build
```

```powershell
dotnet run --project src/ClaudeMultiAccountLauncher
```

```powershell
dotnet test --project tests/ClaudeMultiAccountLauncher.Core.Tests
```

Single self-contained exe (about 70 MB, compressed):

```powershell
dotnet publish src/ClaudeMultiAccountLauncher -c Release -o artifacts/publish
```

Don't turn on trimming or Native AOT: WMI process lookup and `.lnk`
writing use built-in COM.

## Layout

| Path | What |
|---|---|
| `src/ClaudeMultiAccountLauncher.Core` | Everything testable: profiles, Claude lookup, process scan/close, launch, shortcuts, icons |
| `src/ClaudeMultiAccountLauncher` | WinUI 3 window and startup (`--launch`) |
| `tests/ClaudeMultiAccountLauncher.Core.Tests` | xUnit v3 tests |
| `tools/make-icon.ps1` | Draws the launcher icon (`Assets/AppIcon.ico`) |

## Third-party

JetBrains Mono (Regular, v2.304) is bundled for paths. It is licensed
under the SIL Open Font License 1.1; see
[src/ClaudeMultiAccountLauncher/Assets/Fonts/JetBrainsMono-OFL.txt](src/ClaudeMultiAccountLauncher/Assets/Fonts/JetBrainsMono-OFL.txt).
