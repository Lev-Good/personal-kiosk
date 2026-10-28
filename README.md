# PersonalKiosk / קיוסק אישי

Local Windows desktop kiosk in C# / WPF. Same-account, casual-use restriction; no separate Windows account, service, driver, internet filtering or OS security policy changes.

## Build

Requires Windows x64 and .NET SDK 10. Run `pwsh -File ./build.ps1` from this directory. The published executable bundles the .NET and Windows Desktop runtimes, so the destination does not need the SDK or a separate .NET installation. No application-level NuGet dependencies.

Outputs in `dist/`:

- `PersonalKiosk-1.0.0-win-x64/PersonalKioskSetup.exe`: per-user installer. Installer mode is selected by this exact filename or `--install`.
- `app/PersonalKiosk.exe`: portable application.
- `PersonalKiosk-1.0.0-win-x64.zip`: distribution candidate, guide, licenses, test results and checksums.
- `PersonalKiosk-1.0.0-source.zip`: source-only archive, without settings or build output.

The portable executable must stay at the same path while the kiosk is active; the startup registration and watchdog refer to that path. Installation places it under `%LOCALAPPDATA%\Programs\PersonalKiosk`.

## Entry points

- Default: load settings and show the normal UI or resume active kiosk.
- `--preview`: discover apps and show the home screen without loading/saving settings or activating restrictions. Alternatively rename a copy to `PersonalKioskPreview.exe`.
- `--self-test <report-path>`: run policy, password, persistence, native structure and resource checks. Writes a report; does not activate the kiosk or change startup registration.
- `--resume`: exit immediately if persisted kiosk state is inactive.
- `--watch <pid>`: internal watchdog process, monitors the UI process and relaunches an active kiosk after unexpected exit.
- `--install` / `--uninstall`: interactive per-user deployment.

## Implementation

`Core.cs`: settings, PBKDF2 password derivation, atomic persistence, discovery, path policy and startup registration.

`Guard.cs` / `Native.cs`: keyboard hook, 200 ms window checks, common dialog gating, path-based executable approval, USB bus detection. Window overlays cover all attached displays. Unapproved visible top-level windows with titles are minimized and restored on exit; no processes are killed.

`MainWindow.cs` / `Ui.cs`: Hebrew RTL UI, settings and USB document browser. `InstallerWindow.cs`: installer and narrowly scoped uninstall script. `SelfTest.cs`: automated checks.

## Boundaries and release status

This is a packaged release candidate, not a verified security boundary. Do not advertise complete personal-file protection or complete per-action authorization. Approved applications retain the Windows user's permissions; their internal UI and approved file dialogs can expose personal files. Ctrl+Alt+Delete, elevated Windows surfaces, arbitrary injected keyboard input, configuration tampering and shutdown/login gaps are outside the boundary.

The owner explicitly chose same-account operation in exchange for weaker restriction. Dialog approval is based on common window classes, not comprehensive per-application integration. Some operations bypass it and some non-file dialogs trigger it. A shortcut approval can be handed to the same process's next recognized dialog for 3 seconds; the approved dialog remains usable until it closes.

Runtime notices are embedded and installed alongside the executable. The binary is unsigned. Full live acceptance, installer/uninstaller checks, physical USB, mixed-DPI multi-monitor, reboot/watchdog and app-specific workflows remain to be verified; see `RELEASE-CHECKLIST.md`. No startup or kiosk activation was performed on the owner's live desktop during development.

Microsoft references: [single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/) and [Windows support](https://learn.microsoft.com/dotnet/core/install/windows).
