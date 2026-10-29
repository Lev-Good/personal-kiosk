# PersonalKiosk / קיוסק אישי

Created by **לב טוב דיגיטל**. © 2026 לב טוב דיגיטל. כל הזכויות שמורות.

[אתר הבית — תוכנות חינמיות נוספות](https://digital.levtov.uk/index.html). Original code/content rights are reserved; bundled third-party runtime licenses remain in effect.

Local Windows desktop kiosk in C# / WPF. Same-account, casual-use restriction; no separate Windows account, service, driver, internet filtering or OS security policy changes.

## Build

Requires Windows x64 and .NET SDK 10. Run `pwsh -File ./build.ps1` from this directory. The published executable bundles the .NET and Windows Desktop runtimes, so the destination does not need the SDK or a separate .NET installation. No application-level NuGet dependencies.

Outputs in `dist/`:

- `PersonalKiosk-1.6.1-win-x64/PersonalKioskSetup.exe`: per-user installer. Installer mode is selected by this exact filename or `--install`.
- `app/PersonalKiosk.exe`: portable application.
- `PersonalKiosk-1.6.1-win-x64.zip`: distribution candidate, guide, licenses, test results and checksums.
- `PersonalKiosk-1.6.1-source.zip`: source-only archive, without settings or build output.

The portable executable must stay at the same path while the kiosk is active; the startup registration and watchdog refer to that path. Installation places it under `%LOCALAPPDATA%\Programs\PersonalKiosk`.

## Entry points

- Default: load settings and show the normal UI or resume active kiosk.
- `--preview`: discover apps and show the home screen without loading/saving settings or activating restrictions. Alternatively rename a copy to `PersonalKioskPreview.exe`.
- `--self-test <report-path>`: run policy, password, persistence, native structure and resource checks. Writes a report; does not activate the kiosk or change startup registration.
- `--resume`: resume a persisted active kiosk, or run in the tray waiting for enabled weekly schedules. Exit immediately when both are disabled.
- `--preview-schedule`: display sample schedule settings entirely in memory; no startup registration, password or persisted settings changes. A copy named `PersonalKioskSchedulePreview.exe` opens the same preview.
- `--watch <pid>`: internal watchdog process, monitors the UI process and relaunches an active kiosk after unexpected exit.
- `--install` / `--uninstall`: interactive per-user deployment.
- `--render-previews <directory>`: off-screen WPF layout renders, without showing windows, loading settings, making update requests or activating the kiosk.
- `--probe-updates <report-path>`: one explicit public GitHub metadata request and a JSON report, without changing settings or launching the GUI.

## Implementation

`Core.cs`: settings, PBKDF2 password derivation, atomic persistence, discovery, path policy and startup registration.

`Scheduling.cs` / `ScheduleEditor.cs`: optional weekly activation windows with names, selected weekdays, start/end wall-clock times and overnight support. The end boundary is exclusive. From 1.3, an owner-configured automatic opening is enabled by default at the end of the continuous study session. A password exit consumes all currently active occurrences; later windows still fire. The 10-second dispatcher check catches login/wake inside a window and skips fully missed windows. Occurrence history is persisted, preventing the current window from re-triggering after a normal restart or a repeated wall-clock hour. Settings dialogs defer activation until closed. Scheduling itself defaults off for both new and older settings files.

When schedules are enabled, the normal Close button hides to a tray icon and HKCU Run remains registered after password exit. The tray menu can terminate the resident runner after password confirmation; it returns on the next application launch/login. Opening another normal instance signals the existing process to show its window. No wake timers, Task Scheduler jobs, services or automatic Windows login are installed. The computer must be awake with the user logged in.

`ResponsaDiscovery.cs`: detects Bar-Ilan Responsa through product-named installation directories, uninstall/App Paths registrations and product-named shortcuts. Only existing executable paths are added; uninstaller/helper shortcuts are excluded. Tested with fixture files because the product is not installed on the development computer. Custom shortcuts requiring command arguments may require manual application selection.

`Guard.cs` / `Native.cs`: keyboard hook, 200 ms window checks, common dialog gating, path-based executable approval, USB bus detection. Window overlays cover all attached displays. Unapproved visible top-level windows with titles are minimized and restored on exit; no processes are killed.

`MainWindow.cs` / `Ui.cs`: Hebrew RTL UI, settings and USB document browser. `InstallerWindow.cs`: installer and narrowly scoped uninstall script. `SelfTest.cs`: automated checks.

## Version 1.2 features

- `Features.cs` / `NoticeWindow.cs`: configurable 1–60-minute advance notice (5 by default), plus a final-minute notice. Persistent per-occurrence history avoids repeat notifications; entering inside the final minute does not show two notices. Non-modal windows do not block scheduled entry. Notice failures have their own cooldown and do not suppress activation retries.
- Each allowed app has optional weekly availability windows. Launch buttons, USB app selection and the guard all apply the same current-time policy. At expiry, visible windows are minimized rather than processes killed; background activity can continue. The restriction applies only during active kiosk mode.
- `Updates.cs`: automatic public GitHub releases metadata check, at most once per successful 24-hour interval while running, with one-hour failure cooldown and a manual check. Stable/prerelease channel selection, semantic-version ordering including RC numbers, bounded 2 MiB responses, 15-second timeout, trusted repository URLs and uploaded installer checks. No credentials or settings are sent and nothing is installed automatically. Reference: [GitHub Releases API](https://docs.github.com/en/rest/releases/releases#list-releases).
- Optional 200-character plain-text password hint, revealed only on request. It never unlocks/resets the password; the UI refuses a hint equal to the password.
- Creator/copyright/homepage attribution in the app, installer, Windows publisher metadata and embedded rights notice. Runtime licenses are retained.

## Version 1.3 dashboard and session end

`KioskDashboard.cs`, `Appearance.cs`, `AppearanceEditor.cs`: live clock/date/countdown, rounded tiles, light/dark/warm presets, private imported wallpaper (PNG/JPEG/BMP, max 20 MiB), hover/entry/ambient animations and a motion-off option. Missing/corrupt photos fall back to a built-in background. The public package contains no personal wallpapers. Settings import copies images locally; cancellation may leave an unused private copy, which is never uploaded.

`LogoImage.cs`: the original 76-frame GIF is embedded unchanged, decoded once into shared frozen 160×90 frames and animated only while visible and motion is enabled. Header and credit placements use the same original asset. OS client-area animation preference is respected; disabling motion shows a static frame.

`SessionPolicy.cs`: merge overlapping and touching weekly intervals, including midnight and week boundaries; full-week continuous coverage has no artificial finite deadline. Persist the session deadline at entry and preserve it on resume. Default automatic end unlocks without a password, restores minimized windows, closes only kiosk-owned dialogs and hides the kiosk to its existing tray runner until the next session. Manual entry outside an active schedule has no automatic deadline. Password-only ending remains selectable in protected appearance settings. A late automatic end does not mark a future occurrence handled. Failed persistence leaves the existing restriction in place and reports an error.

The one-second UI timer updates existing text controls instead of rebuilding the dashboard, and checks automatic end independently of the ten-second activation timer. Off-screen previews cover light/dark/warm kiosk states, countdown and appearance settings without activating restrictions.

## Version 1.4 Fluent interface

The complete WPF interface now shares one RTL-first design system: `#F8FAFC` window backgrounds, white 12-pixel cards, `#E2E8F0` borders, deep-teal `#0F766E` actions and pale-teal secondary surfaces. Buttons, fields, password dialogs, installer and settings use the same Windows 11-inspired sizing, typography and hover states. Scroll viewers use a rounded 6-pixel track without arrow buttons.

The legacy settings tabs were replaced by a clean right-side navigation rail. Each setting is presented as a compact title/description/action row; Boolean options use accessible toggle switches and day selection uses compact selection pills. The primary kiosk action is visually distinct. Program cards now include a 44×44 teal icon tile, pointer cursor, teal hover border, lift and shadow. Hebrew text wraps in RTL and action controls retain content-sized widths.

## Version 1.5 refinements

- Manual update checking and the trusted release link now live directly in the protected password/update settings page. The main header remains focused on kiosk activation, settings and help.
- Three offline landscape backgrounds are embedded in the executable, with thumbnail selection and bundled source/license information in `LandscapeLicenses.txt`: two CC0 images and one public-domain panorama from Wikimedia Commons.
- Allowed-app settings rows show the executable icon and name on the RTL start side, actions on the opposite side, and both upward and downward ordering controls.
- The dashboard header always places the product logo/name at the far RTL start, followed by its actions.
- Window restoration now rejects cloaked system windows and recycled window handles by verifying the original process ID and executable path before restoring a window. This addresses a case where Windows Settings could appear after leaving kiosk mode.
- On resume from sleep, the app rechecks the scheduled deadline, rebuilds monitor shields and immediately refreshes the guard. Desktop applications normally retain their in-memory windows across sleep. Selecting an allowed app after resume now restores and activates its existing exact-path window when possible instead of always starting another process. A reboot, application crash/update or application-specific recovery behavior can still require reopening the app.

## Version 1.6 landscape collection

The built-in landscape selector now contains ten offline backgrounds, all visually inspected to contain no people: tropical lake, desert, mountain lake, forest waterfall, snowy mountains, northern lights, ocean sunset, tea fields, alpine lakes and Moraine Lake. The earlier Peyto Lake panorama containing visitors was removed from the project and executable rather than merely hidden. Every file is CC0 or public domain; exact authors, source pages and license terms are bundled in `LandscapeLicenses.txt`.

### Version 1.6.1 interface corrections

- The password-change action is located in the **Password and updates** settings page.
- Program cards on the home and kiosk dashboards use the executable's real icon, with the same safe fallback used by the settings list.

## Boundaries and release status

This is a packaged release candidate, not a verified security boundary. Do not advertise complete personal-file protection or complete per-action authorization. Approved applications retain the Windows user's permissions; their internal UI and approved file dialogs can expose personal files. Ctrl+Alt+Delete, elevated Windows surfaces, arbitrary injected keyboard input, configuration tampering and shutdown/login gaps are outside the boundary.

The owner explicitly chose same-account operation in exchange for weaker restriction. Dialog approval is based on common window classes, not comprehensive per-application integration. Some operations bypass it and some non-file dialogs trigger it. A shortcut approval can be handed to the same process's next recognized dialog for 3 seconds; the approved dialog remains usable until it closes.

Runtime notices are embedded and installed alongside the executable. The binary is unsigned. Full live acceptance, installer/uninstaller checks, physical USB, mixed-DPI multi-monitor, reboot/watchdog and app-specific workflows remain to be verified; see `RELEASE-CHECKLIST.md`. No startup or kiosk activation was performed on the owner's live desktop during development.

Microsoft references: [single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/) and [Windows support](https://learn.microsoft.com/dotnet/core/install/windows).
