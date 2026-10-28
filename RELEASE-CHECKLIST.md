# Release verification — PersonalKiosk 1.0.0

Status: packaged release candidate; NOT certified for unrestricted distribution.

## Completed

- Self-contained Windows x64 release build.
- 33 automated checks: password validation and random salt, invalid password/hash handling, atomic state persistence/backup, corruption handling, USB path traversal/root boundaries, file-type allowlist, application discovery, Win32 keyboard input structure, Windows 11 Notepad redirect policy and embedded installation resources.
- The bundled self-contained installer executable also ran all 33 checks successfully (exit code 0); extraction was redirected to a writable build directory for the restricted build environment.
- Desktop UI preview: Hebrew home screen and detected applications, including Otzar HaHochma; installer screen inspected without clicking Install.
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
- [ ] Verify elevated/secure Windows UI limitations are visible to operator.
- [ ] Review with owner that approved app dialogs can expose personal files; same-account protection remains partial.
- [ ] Sign executable with publisher certificate, rebuild checksums, review bundled runtime notices, then distribute publicly.

No claim is made that a desktop overlay can enforce complete per-action password authorization in arbitrary third-party applications.
