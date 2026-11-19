# Acceptance status: claude-multi-account-launcher 0.1.0

- Date: 2026-11-19
- Against: [spec section 7](spec-2026-11-19-claude-multi-account-launcher.md#7-acceptance-criteria)
- Build: branch `feat/launcher`, Windows 11 Pro 10.0.26300, Claude
  Desktop MSIX 6.6395.0.0, .NET SDK 11.0.100, Windows App SDK 2.7.0
- Automated tests: 95 passing (`dotnet test --project tests/ClaudeMultiAccountLauncher.Core.Tests`)

How each item was checked:

- **UI run**: the real window driven through UI Automation on this PC
- **Manual**: Vernon, by hand. These steps close Claude Desktop, which
  hosts the Claude Code session that built this, so they couldn't be
  automated
- **Tests**: unit or integration tests in the repo

| # | Criterion | Status | How |
|---|---|---|---|
| 1 | Adding "Personal" creates both folders and a list row | Pass | UI run (Phase 2): added a profile; `%APPDATA%\Claude-<slug>`, `%USERPROFILE%\.claude-<slug>` and the row appeared; tests |
| 2 | First launch closes running Claude after confirm, opens a fresh Claude, sign-in completes, status becomes `Ready` | Pass (manual) | Manual (after Phase 3): Vernon reported the sign-in flow tested. UI run covered the confirm dialog and Cancel (no process closed) |
| 3 | "Default" and "Personal" both running, each shows its own account and history | Pass (manual) | Manual (after Phase 3, step 4 of the test list) |
| 4 | Claude Code sessions in "Personal" write to `.claude-personal` | Pass (manual) | Manual (after Phase 3, step 5). Spike confirmed `CLAUDE_CONFIG_DIR` reaches the Claude process through the MSIX alias |
| 5 | Desktop shortcut for "Personal" opens that profile in one click | Pass | UI run (Phase 4): shortcut created from the menu; double-clicking the Default shortcut started Claude with no launcher window, in about 1 s; a not-signed-in profile's shortcut opened the sign-in flow. Ready-profile path: tests |
| 6 | Shortcut icon still shows after a Claude Desktop update | **Open** | Design only: the icon is a copy in `%APPDATA%\claude-multi-account-launcher\claude.ico`, not a path into the versioned MSIX folder; tests confirm the copy is reused when Claude can't be found. Needs a real Claude update to confirm |
| 7 | Running profiles show `Running`; closing Claude clears it on next window focus | Pass | UI run (Phase 2): launched a profile, row showed Running; closed it, refocused, row went back |
| 8 | Removing a profile keeps its folders unless deletion is confirmed | Pass | Tests (keep by default, delete, locked-file reporting); UI run: "Keep folders" is the default button, Delete removed all folders |
| 9 | Works on both MSIX and standalone installs | **Partly** | MSIX: everything above. Standalone: lookup order and process matching covered by tests only; no standalone install was available |
| 10 | FR-4 leaves a running Claude Code CLI session untouched | Pass | Read-only run against all real `claude.exe` processes: 15 Desktop processes picked, 3 CLI processes kept, path re-checked on the handle that would be terminated; tests. Not watched live with a terminal session during a real close |

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

## Open items

1. **#6**: after the next Claude Desktop update, check that `Claude - *`
   desktop shortcuts still show Claude's icon.
2. **#9**: if a standalone install is ever available, run add → sign in →
   launch once on it.
3. **#10, live**: during a real sign-in flow, keep `claude` running in
   Windows Terminal and confirm it survives.
