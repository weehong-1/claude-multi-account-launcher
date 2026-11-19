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
- First sign-in caveat (verified 2026-11-19): **Continue with Google**
  returns through a `claude://` link. Windows always opens it as
  `Claude.exe -- "claude://..."` from the MSIX package, with no
  `--user-data-dir`, so it lands in the Default instance (starting one
  if needed), never in the profile. Closing other Claude windows does not
  help, and a per-user `HKCU\Software\Classes\claude` handler is ignored
  in favour of the package's. **Continue with email** signs in inside the
  profile's own window and works, also for accounts created with Google
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
- Status: `Running` or `Not running` (FR-7). No sign-in state is stored:
  the launcher can't see it, and asking the user added a step for nothing

### FR-2 Add profile

- Input: display name (e.g. "Resetrix", "Personal")
- App derives a kebab-case slug from the name, unique among profiles
- Paths per profile:
  - Data: `%APPDATA%\Claude-<slug>` (directly under `%APPDATA%`, see section 8)
  - Claude Code config: `%USERPROFILE%\.claude-<slug>`
- Create folders if missing, in the real `%APPDATA%` and `%USERPROFILE%`
  (the launcher is unpackaged, so it is not redirected)
- Then offer to open it for sign-in (FR-4)
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

### FR-4 First sign-in

Revised 2026-11-19 after a real test: the original flow (close every
Claude, launch the profile alone, ask "Signed in?") could not work,
because Google sign-in always returns to Default (section 2).

- Right after Add, one dialog: `Open Claude` / `Later`. It says that
  Claude opens next to the current one and nothing is closed, and to
  sign in with **Continue with email**, not Google
- `Open Claude` launches the profile per FR-3. The user signs in there
- Nothing is ever closed, and there is no "signed in?" question. Launch
  works the same for every profile, first time or not

### FR-5 Rename and remove

- Rename changes the display name only. Slug and folders stay
- Remove takes the profile off the list. A second confirmation asks
  whether to also delete its data and config folders (default: keep)

### FR-6 Exe resolution

- Result has two parts: the launch path, and the install folder used to
  recognise Claude Desktop processes (FR-7)
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
  lines for `--user-data-dir=<data folder>`; no flag means Default
- Pick Claude Desktop processes by executable path (the install folder
  from FR-6), never by name alone: the Claude Code CLI is also
  `claude.exe` (Desktop's bundled CLI runs from
  `%LOCALAPPDATA%\Claude-Data\claude_pzs8sxrjxfjjc\claude-code\...\claude.exe`).
  For MSIX, match any `Claude_*__pzs8sxrjxfjjc` package folder, so a
  process from the previous version after an update still counts
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
- Fields per profile: `name`, `slug`, `createdAt` (a `status` field from
  the first build is ignored when read and dropped on the next save)
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
- [ ] After adding "Personal", `Open Claude` opens it next to the running
      Claude without closing anything, and email sign-in completes in
      that window
- [ ] With "Default" and "Personal" both running, each window shows
      its own account and chat history
- [ ] Claude Code sessions in "Personal" write to `.claude-personal`
- [ ] Desktop shortcut for "Personal" opens that profile in one click
- [ ] Shortcut icon still shows after a Claude Desktop update
- [ ] Running profiles show `Running`; closing Claude clears it on
      next window focus
- [ ] Removing a profile keeps its folders unless deletion is confirmed
- [ ] Works on both MSIX and standalone installs
- [ ] The Claude Code CLI is never counted as a running profile (FR-7)

## 8. Known risks

- Unofficial: a Claude Desktop update may change or block the flag
- Claude's own update relaunch has been reported to drop
  `CLAUDE_CONFIG_DIR` (sessions fall back to `~/.claude`). After an
  update, relaunch the profile from this app
- Connectors (GitLab, Vilix, etc.) must be reconnected per profile.
  Connectors that finish their sign-in through a `claude://` link will
  hit the same problem as Google sign-in and land in Default (not yet
  tested)
- Google sign-in can't be used in a profile (section 2); email only
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
  registry handler. Tested here 2026-11-19 against the MSIX install:
  Windows ignores the per-user handler and uses the package's, so it
  doesn't help. Email sign-in is used instead (FR-4)

## 9. Open questions

None. FR-1 to FR-8 are all in the first version.
