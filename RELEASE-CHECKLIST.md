# Release verification — PersonalKiosk 1.6.1

Status: packaged release candidate; NOT certified for unrestricted distribution.

## Completed

- Self-contained Windows x64 release build.
- 159 automated checks pass. New checks load all ten embedded landscape backgrounds offline and verify that the former people-containing panorama is no longer embedded. Existing process-identity, session, password, file, scheduling, update, hint, wallpaper and 76-frame logo checks remain passing. Bar-Ilan discovery uses inert fixtures, not an installed copy.
- The updater was checked against the real public GitHub API in the previous release. This release continues using the same bounded, read-only transport; no settings are sent by a probe.
- The Fluent-style light/dark/warm kiosk layouts, primary action, 44×44 program icons, thin scrollbars, toggle rows, centered live-clock/countdown values, logo placements, schedule and appearance settings were rendered off-screen in WPF and visually inspected. This does not replace real-time animation and desktop interaction checks.
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
- [ ] Inspect all four settings navigation sections at 100%, 125%, 150% and 200% display scaling; verify keyboard focus, toggle switches, thin scrolling and long Hebrew labels.
- [ ] Verify real timed activation, password exit mid-window, overlapping/overnight entries, sleep/wake, login catch-up and disabled schedules on Windows 10/11.
- [ ] Check spring-forward/fall-back transitions in the target Windows time zone; verify no duplicate activation of a handled date.
- [ ] Verify Bar-Ilan discovery and launch with real installations, including different versions and non-default install folders.
- [ ] Verify advance and final-minute notices on the real desktop, dismissal without delaying activation and persistence after restart.
- [ ] Verify active app windows minimize when availability ends, can be used again in the next window, and documents remain open.
- [ ] Verify automatic and manual update UI with a newer release, stable/prerelease filtering, offline failure and browser download link; no automatic installation.
- [ ] Verify hint reveal, hint removal, password change and all creator/homepage placements in the installed app.
- [ ] Verify one-second clock/date updates, countdown and frame-by-frame GIF animation on actual monitors, including reduced-motion behavior and hidden/background states.
- [ ] Enter sleep with Word, Otzar HaHochma, Otzaria and a PDF reader open; resume before and after the scheduled end, then confirm each surviving exact-path window is restored from its card without a duplicate process.
- [ ] Open and close Windows Settings before and during kiosk mode; verify kiosk exit never opens a cloaked or recycled system window.
- [ ] Review all ten built-in background thumbnails and full-screen crops on monitors with different aspect ratios; confirm no people are visible in any source image.
- [ ] Verify automatic opening at session end (including with kiosk dialogs open), hiding to tray, next automatic start, password-only opt-out and persisted expiry after reboot.
- [ ] Test custom wallpaper selection, persistence when the original is moved, missing-image fallback and mixed-DPI secondary screens.
- [ ] Verify elevated/secure Windows UI limitations are visible to operator.
- [ ] Review with owner that approved app dialogs can expose personal files; same-account protection remains partial.
- [ ] Sign executable with publisher certificate, rebuild checksums, review bundled runtime notices, then distribute publicly.

No claim is made that a desktop overlay can enforce complete per-action password authorization in arbitrary third-party applications.
