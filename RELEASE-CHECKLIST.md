# Release verification — PersonalKiosk 1.2.0

Status: packaged release candidate; NOT certified for unrestricted distribution.

## Completed

- Self-contained Windows x64 release build.
- 115 automated checks pass, including from the self-contained installer executable. They cover existing kiosk policies, weekly scheduling and migration, notification boundaries/history, overnight app availability, optional hint persistence, version/channel selection, trusted release URLs, bounded metadata transport, network failure handling and branding. Bar-Ilan discovery uses inert fixtures, not an installed copy.
- A real public GitHub metadata request through the application's updater succeeded: the current 1.2.0-rc.1 build identified v1.1.0-rc.1 as older, not an update. The probe writes a report only; no settings or startup registration are changed.
- Home, scheduling and app-availability layouts were rendered off-screen in WPF and visually inspected. This does not replace live desktop interaction checks. The prior 1.0.0 home/installer UI was inspected without activating or installing the kiosk.
- Runtime package vulnerability advisory lookup was unavailable because the NuGet service could not be reached. Compilation and local checks passed; no successful online vulnerability audit is claimed.
- No live kiosk activation, registry startup changes or installation performed on the owner's desktop during development.

## Required hands-on acceptance

- [ ] Install and uninstall as a standard user on clean Windows 10 and 11 x64 systems.
- [ ] Start live kiosk, reject wrong password, leave by correct password, restore minimized windows.
- [ ] Open each installed app; check secondary/helper processes and packaged Notepad on the target OS.
- [ ] Check keyboard hook installation and behavior with real keyboard input.
- [ ] Check Ctrl+O/S/P/K and Shift variants in every allowed app.
- [ ] Check menu actions, Word Backstage, recent files, existing documents, auto-save, links, drag/drop and embedded content; record bypasses rather than claim full coverage.
- [ ] Connect/disconnect physical USB, test USB fixed disks and card readers, deny junction/symlink navigation.
- [ ] Connect/disconnect monitors, mixed DPI, primary screen changes and approved app movement.
- [ ] Reboot while active, log back in, verify automatic resume and document startup gap.
- [ ] Terminate kiosk UI in a test environment, verify watchdog relaunch, then normal password exit.
- [ ] Configure sample schedules, close to tray, relaunch shortcut and verify the existing window is restored; authenticate tray quit and verify scheduling resumes next login.
- [ ] Inspect the new schedule tab and editor visually on target displays.
- [ ] Verify real timed activation, password exit mid-window, overlapping/overnight entries, sleep/wake, login catch-up and disabled schedules on Windows 10/11.
- [ ] Check spring-forward/fall-back transitions in the target Windows time zone; verify no duplicate activation of a handled date.
- [ ] Verify Bar-Ilan discovery and launch with real installations, including different versions and non-default install folders.
- [ ] Verify advance and final-minute notices on the real desktop, dismissal without delaying activation and persistence after restart.
- [ ] Verify active app windows minimize when availability ends, can be used again in the next window, and documents remain open.
- [ ] Verify automatic and manual update UI with a newer release, stable/prerelease filtering, offline failure and browser download link; no automatic installation.
- [ ] Verify hint reveal, hint removal, password change and all creator/homepage placements in the installed app.
- [ ] Verify elevated/secure Windows UI limitations are visible to operator.
- [ ] Review with owner that approved app dialogs can expose personal files; same-account protection remains partial.
- [ ] Sign executable with publisher certificate, rebuild checksums, review bundled runtime notices, then distribute publicly.

No claim is made that a desktop overlay can enforce complete per-action password authorization in arbitrary third-party applications.
