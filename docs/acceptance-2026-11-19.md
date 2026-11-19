# Acceptance status: claude-multi-account-launcher 0.1.0

- Date: 2026-11-19
- Against: [spec section 7](spec-2026-11-19-claude-multi-account-launcher.md#7-acceptance-criteria)
- Build: branch `feat/launcher`, Windows 11 Pro 10.0.26300, Claude
  Desktop MSIX 6.6395.0.0, .NET SDK 11.0.100, Windows App SDK 2.7.0
- Automated tests: 115 passing (`dotnet test --project tests/ClaudeMultiAccountLauncher.Core.Tests`)
- Revised 2026-11-19 after FR-4 was rewritten (email sign-in, nothing
  closed, no stored status)

How each item was checked:

- **UI run**: the real window driven through UI Automation on this PC
- **Manual**: Vernon, by hand. Signing in can't be automated: Claude
  can't control its own window
- **Tests**: unit or integration tests in the repo

| # | Criterion | Status | How |
|---|---|---|---|
| 1 | Adding "Personal" creates both folders and a list row | Pass | UI run (Phase 2): added a profile; `%APPDATA%\Claude-<slug>`, `%USERPROFILE%\.claude-<slug>` and the row appeared; tests |
| 2 | After adding "Personal", `Open Claude` opens it next to the running Claude without closing anything, and email sign-in completes there | Pass (manual) | Manual 2026-11-19: profile opened beside Default, signed in with Continue with email in its own window. The **original** flow (close all Claude, then sign in) **failed** in the same test: Google sign-in's `claude://` link started a new Default instance, seen in its command line (`Claude.exe -- "claude://login/google-auth?..."`, no `--user-data-dir`). An earlier "Pass" recorded here was based on a report, not this test, and was wrong |
| 3 | "Default" and "Personal" both running, each shows its own account and history | Pass (manual) | Manual 2026-11-19: both windows open at once, Default on the original account and the profile on the other one |
| 4 | Claude Code sessions in "Personal" write to `.claude-personal` | **Not verified** | Spike confirmed `CLAUDE_CONFIG_DIR` reaches the Claude process through the MSIX alias; no Claude Code session has been run in a profile yet |
| 5 | Desktop shortcut for "Personal" opens that profile in one click | Pass | UI run (Phase 4): shortcut created from the menu; double-clicking the Default shortcut started Claude with no launcher window, in about 1 s. Profile path: tests |
| 6 | Shortcut icon still shows after a Claude Desktop update | **Open** | Design only: the icon is a copy in `%APPDATA%\claude-multi-account-launcher\claude.ico`, not a path into the versioned MSIX folder; tests confirm the copy is reused when Claude can't be found. Needs a real Claude update to confirm |
| 7 | Running profiles show `Running`; when Claude exits it clears without refocusing the launcher | Pass | UI run 2026-11-19 with the launcher in the background: a profile started outside the launcher showed Running after 2.6 s; after its process ended the row showed Not running 0.21 s later; tests for the exit watcher |
| 8 | Removing a profile keeps its folders unless deletion is confirmed | Pass | Tests (keep by default, delete, locked-file reporting); UI run: "Keep folders" is the default button, Delete removed all folders |
| 9 | Works on both MSIX and standalone installs | **Partly** | MSIX: everything above. Standalone: lookup order and process matching covered by tests only; no standalone install was available |
| 11 | `Uninstall and remove everything…` removes every listed item and the installed app, and leaves Default untouched | Pass | Tests (10, incl. a bug they caught: the launcher's own folder matched the old-profile pattern). On this PC: real plan listed exactly the profile's two folders, the launcher folder, the MSI and the temp runtime, without Default; UI refused while a profile was running; deletion run on a temp copy, then the real after-exit command uninstalled the installed MSI (Apps entry, exe, Start menu shortcut gone) and removed the runtime folder, with the real profile data untouched |
| 10 | The Claude Code CLI is never counted as a running profile | Pass | Read-only run against all real `claude.exe` processes: 15 Desktop processes picked, 3 CLI processes excluded; tests. (The launcher no longer closes any process, so the old "CLI survives a close" check no longer applies) |

## Clean-PC test (Windows Sandbox, v0.1.0 release files)

Fresh Windows 11 Enterprise 10.0.26100 with networking off: no .NET, no
Windows App SDK runtime of ours, no VC++ redistributable, no Claude.

- MSI install: Pass. Exit 0, exe identical to the portable download,
  Start menu shortcut and Apps entry present
- Launching the app (installed or portable): **Blocked by Smart App
  Control**, which is on in the sandbox. CodeIntegrity event 3118 "Smart
  App Control Block" / 3077 for `claude-multi-account-launcher.exe`,
  because the exe is unsigned
- Decision (Vernon, 2026-11-19): no code signing for now. New PCs turn
  Smart App Control off before installing; documented in the README.
  If signing becomes necessary, use SignPath Foundation (free for open
  source). Before applying, the repo needs an OSI-approved license (there
  is no LICENSE file yet) and release builds on GitHub Actions, since
  SignPath checks the binaries are built from the public source. The
  publisher shown will be "SignPath Foundation"

Not yet shown on a clean PC: that the app starts and runs with nothing
else installed. That needs a clean PC with Smart App Control off.

## Google sign-in through Finish Google sign-in (FR-4)

- Routing: Pass. With a harmless `claude://login/cmal-routing-test` on the
  clipboard and a test profile running, the launcher started
  `--user-data-dir=...\Claude-link-test -- claude://login/cmal-routing-test`;
  that process handed off and exited, the profile's main process stayed
  the same, and no new Default instance appeared. Clipboard cleared after
- Without a link on the clipboard: shows the four steps instead
- **Not verified yet:** that Claude completes a real Google sign-in when
  it receives the link this way. Needs one real sign-in by Vernon

## Open items

1. **#6**: after the next Claude Desktop update, check that `Claude - *`
   desktop shortcuts still show Claude's icon.
2. **#9**: if a standalone install is ever available, run add → sign in →
   launch once on it.
3. **#4**: start a Claude Code session inside a profile and check it
   writes to `%USERPROFILE%\.claude-<slug>`.
4. Connectors in a profile: check whether their sign-in also returns
   through `claude://` and lands in Default (spec section 8).
