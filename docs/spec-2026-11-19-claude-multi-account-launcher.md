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
  - MSIX install (current): `%LOCALAPPDATA%\Microsoft\WindowsApps\Claude.exe`
  - Older standalone install: `%LOCALAPPDATA%\AnthropicClaude\claude.exe`
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
- Create folders if missing. New profile status: `Not signed in yet`

### FR-3 Launch (one click)

- Resolve exe per FR-6
- Start Claude with `--user-data-dir="<data folder>"` and env var
  `CLAUDE_CONFIG_DIR=<config folder>` (Default profile: neither)
- Launching a profile that is already running only brings Claude to
  the front (Claude's own single-instance behaviour handles this)

### FR-4 First sign-in flow

- When launching a `Not signed in yet` profile:
  1. If any `Claude` process is running, show a dialog explaining that
     all Claude windows must close for sign-in to work, with
     buttons `Close Claude and continue` / `Cancel`
  2. On confirm, end all `Claude` processes, wait until they are gone
  3. Launch the new profile only
  4. Show a dialog: "Signed in successfully?" `Yes` / `Not yet`
  5. `Yes` sets status to `Ready`
- User can also reset a profile to `Not signed in yet` from its menu

### FR-5 Rename and remove

- Rename changes the display name only. Slug and folders stay
- Remove takes the profile off the list. A second confirmation asks
  whether to also delete its data and config folders (default: keep)

### FR-6 Exe resolution

- Try in order: MSIX alias path, then standalone path (section 2)
- If neither exists, try the installed package location
  (`Get-AppxPackage *Claude*` equivalent)
- If still not found, show an error naming the paths tried

### FR-7 Running indicator

- Detect running profiles by reading `Claude` process command lines
  for `--user-data-dir=<data folder>`
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

- Single self-contained `.exe`, no installer, no admin rights
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
