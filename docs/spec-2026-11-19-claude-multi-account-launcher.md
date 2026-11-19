# Spec: Claude Desktop multi-account launcher (Windows)

- Date: 2026-11-19
- Owner: Vernon KOH
- Status: ready for implementation, pending Vernon's final review
- Implementer: Claude Code account
- Name: `claude-multi-account-launcher`
- Repo: GitHub `weehong-1/claude-multi-account-launcher`

## 1. Problem

Claude Desktop on Windows runs one instance and one signed-in account.
Vernon wants several accounts open side by side and to open any of them
with one click, instead of running commands or batch files by hand.

## 2. How it works (background)

- Claude Desktop is an Electron app. Chromium's single-instance lock is
  keyed on the `--user-data-dir` folder, so a different folder gives a
  second instance with its own sign-in, chats, settings and connectors.
- Launch line per profile:
  `Claude.exe --user-data-dir="<profile data folder>"`
- Claude Code sessions started from Desktop read `~/.claude` unless
  `CLAUDE_CONFIG_DIR` is set in the launch environment, so each profile
  also gets its own `CLAUDE_CONFIG_DIR`.
- First sign-in caveat: sign-in returns through a `claude://` link and
  Windows hands it to whichever Claude is already running. A new profile
  must be signed in while no other Claude process is running.
- Exe location:
  - MSIX install (current): package family `Claude_pzs8sxrjxfjjc`.
    - Execution alias: `%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe`
      (declared in Claude's AppxManifest; there is no `Claude.exe` alias)
    - Real exe: `C:\Program Files\WindowsApps\Claude_<version>_<arch>__pzs8sxrjxfjjc\app\Claude.exe`.
      The versioned folder changes on every update
    - Launch through the alias. It keeps package identity and passes both
      command-line arguments and environment variables through to Claude
      (verified 2026-11-19). The started process is Claude's main process
      itself, with no stub in between
  - Older standalone install: `%LOCALAPPDATA%\AnthropicClaude\claude.exe`
- MSIX file system virtualization (verified 2026-11-19): when MSIX Claude
  writes to a folder under `%APPDATA%` that does not exist yet, Windows
  redirects it to
  `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\<folder>`.
  If the folder already exists in the real `%APPDATA%`, Claude writes there
  directly. The Default profile's data lives in that LocalCache for this
  reason
- This is an unofficial technique. A Claude Desktop update can break it.

## 3. Scope

### In scope

- Small Windows GUI app, one window
- List, add, rename, remove and launch profiles
- Guided first sign-in for a new profile
- Optional desktop shortcut per profile

### Out of scope

- macOS or Linux
- Signing in for the user, storing tokens or passwords
- Rewriting the `claude://` protocol handler in the registry
- Syncing or merging chats between accounts
- Changing Claude Desktop itself

## 4. Functional requirements

### FR-1 Profile list

- Window shows one row per profile: display name, status, Launch button
- A built-in "Default" profile launches Claude with no
  `--user-data-dir` (the existing account). It cannot be removed
- Status values:
  - `Not signed in yet` (new profile, first sign-in not confirmed)
  - `Ready`
  - `Running` (see FR-7)

### FR-2 Add profile

- Input: display name (e.g. "Resetrix", "Personal")
- App derives a kebab-case slug from the name, unique among profiles
- Paths per profile:
  - Data: `%APPDATA%\Claude-<slug>` (directly under `%APPDATA%`, see section 8)
  - Claude Code config: `%USERPROFILE%\.claude-<slug>`
- Create folders if missing, in the real `%APPDATA%` and `%USERPROFILE%`
  (the launcher is unpackaged, so it is not redirected). New profile
  status: `Not signed in yet`
- Slug `default` is reserved for the built-in Default profile

### FR-3 Launch (one click)

- Resolve exe per FR-6
- Before every launch, make sure the profile's data and config folders
  exist (FR-2). If the data folder is missing when MSIX Claude starts,
  its data silently lands in the package LocalCache (section 2) and
  FR-5 deletion would miss it
- Start Claude with `--user-data-dir="<data folder>"` and env var
  `CLAUDE_CONFIG_DIR=<config folder>` (Default profile: neither)
- Launching a profile that is already running only brings Claude to
  the front (Claude's own single-instance behaviour handles this)

### FR-4 First sign-in flow

- When launching a `Not signed in yet` profile:
  1. If any Claude Desktop process is running, show a dialog explaining
     that all Claude windows must close for sign-in to work, with
     buttons `Close Claude and continue` / `Cancel`
  2. On confirm, end all Claude Desktop processes, wait until they are
     gone
     - Identify them by executable path (the resolved Claude Desktop
       install folder from FR-6), never by process name alone: the
       Claude Code CLI is also `claude.exe` and must not be killed.
       The CLI that Desktop bundles runs from
       `%LOCALAPPDATA%\Claude-Data\claude_pzs8sxrjxfjjc\claude-code\...\claude.exe`
     - For MSIX, match any `Claude_*__pzs8sxrjxfjjc` package folder, so a
       process still running from the previous version after an update
       is included
  3. Launch the new profile only
  4. Show a dialog: "Signed in successfully?" `Yes` / `Not yet`
  5. `Yes` sets status to `Ready`
- User can also reset a profile to `Not signed in yet` from its menu

### FR-5 Rename and remove

- Rename changes the display name only. Slug and folders stay
- Remove takes the profile off the list. A second confirmation asks
  whether to also delete its data and config folders (default: keep)

### FR-6 Exe resolution

- Result has two parts: the launch path, and the install folder used to
  recognise Claude Desktop processes (FR-4, FR-7)
- Try in order:
  1. MSIX alias `claude-desktop.exe` (section 2). Its install folder is
     the package location from the package manager (family
     `Claude_pzs8sxrjxfjjc`, `Get-AppxPackage` equivalent)
  2. Standalone path (section 2); install folder `%LOCALAPPDATA%\AnthropicClaude`
  3. MSIX package location without the alias: launch `app\Claude.exe`
     inside it
- If still not found, show an error naming the paths tried

### FR-7 Running indicator

- Detect running profiles by reading Claude Desktop process command
  lines (same executable-path filter as FR-4) for
  `--user-data-dir=<data folder>`; no flag means Default
- Only main processes count. Electron child processes carry `--type=`
  and are skipped
- Refresh when the window gets focus

### FR-8 Desktop shortcut (per profile, user chooses)

- `Create desktop shortcut` makes a `.lnk` named `Claude - <name>`
  that opens the launcher straight into that profile
  (e.g. `claude-multi-account-launcher.exe --launch <slug>`)
- Shortcut icon: extract Claude's icon once to a stable `.ico` next to
  `profiles.json`. Do not point at the MSIX `Claude.exe`: its versioned
  folder is deleted on every Claude update and the icon goes blank

## 5. Storage

- Profile list: `%APPDATA%\claude-multi-account-launcher\profiles.json`
- Fields per profile: `name`, `slug`, `status`, `createdAt`
- No secrets, tokens or passwords stored anywhere

## 6. Non-functional

- Single self-contained `.exe`, no admin rights
- Distributed two ways on GitHub Releases (added 2026-11-19):
  - Per-user MSI installer: installs to
    `%LOCALAPPDATA%\Programs\claude-multi-account-launcher\`, adds a Start
    menu shortcut and an Apps entry, upgrades in place, no admin rights.
    Uninstall leaves profiles and their folders alone
  - The portable `.exe` on its own
- No network calls
- Stack: WinUI 3 (Windows App SDK), C#, .NET 11
- Unpackaged, self-contained (bundle the Windows App SDK runtime) so it
  runs without installing anything else
- Fluent design, follows Windows light/dark mode
- UI text in English; monospace text (paths) uses JetBrains Mono

## 7. Acceptance criteria

- [ ] Adding "Personal" creates both folders and a list row
- [ ] First launch of "Personal" closes running Claude after confirm,
      opens a fresh Claude, sign-in completes, status becomes `Ready`
- [ ] With "Default" and "Personal" both running, each window shows
      its own account and chat history
- [ ] Claude Code sessions in "Personal" write to `.claude-personal`
- [ ] Desktop shortcut for "Personal" opens that profile in one click
- [ ] Shortcut icon still shows after a Claude Desktop update
- [ ] Running profiles show `Running`; closing Claude clears it on
      next window focus
- [ ] Removing a profile keeps its folders unless deletion is confirmed
- [ ] Works on both MSIX and standalone installs
- [ ] FR-4 leaves a running Claude Code CLI session untouched

## 8. Known risks

- Unofficial: a Claude Desktop update may change or block the flag
- Claude's own update relaunch has been reported to drop
  `CLAUDE_CONFIG_DIR` (sessions fall back to `~/.claude`). After an
  update, relaunch the profile from this app
- Connectors (GitLab, Vilix, etc.) must be reconnected per profile
- Cowork's VM service looks for its image at
  `%APPDATA%\<data folder name>\vm_bundles`, ignoring
  `--user-data-dir`. So each data folder must sit directly under
  `%APPDATA%` (not in a subfolder), else "VHDX file not found"
- MSIX file system virtualization (section 2): a profile data folder
  deleted by hand while the profile still exists will be recreated by
  the launcher on next launch; if Claude were started some other way
  first, its data would land in the package LocalCache instead
- Only one profile can run the Cowork VM at a time (second one may
  fail with `HYPERVISOR_SERVICE_ERROR`)
- The `Claude-` prefix on data folders avoids clashing with other
  apps' folders in `%APPDATA%` (e.g. a profile named "Code")

## 8a. Prior art

- vodongha/claude-desktop-clone (MIT, PowerShell + VBScript, last
  commit 2026-06-20): source of the Cowork path and icon findings above.
  No first sign-in handling, MSIX installs only
- LemongrassSteak/claude-accounts (MIT, WinForms, .NET 10, v0.1.0):
  routes `claude://` sign-in by temporarily rewriting the per-user
  registry handler. Not adopted here; FR-4 closes other Claude
  processes instead

## 9. Open questions

None. FR-1 to FR-8 are all in the first version.
