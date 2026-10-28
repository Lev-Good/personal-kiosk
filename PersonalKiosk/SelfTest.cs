using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalKiosk;

internal static class SelfTest
{
    internal static int Run(string? output)
    {
        var results = new List<string>(); int failures = 0;
        void Check(string name, Func<bool> test)
        {
            try { if (!test()) throw new Exception("assertion failed"); results.Add("PASS " + name); }
            catch (Exception ex) { failures++; results.Add("FAIL " + name + ": " + ex.Message); }
        }
        var dir = Path.Combine(output == null ? Path.GetTempPath() : Path.GetDirectoryName(Path.GetFullPath(output))!, "PersonalKiosk-Test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var settings = new Settings(); Passwords.Set(settings, "sample-test-password");
            Check("password accepts correct input", () => Passwords.Verify(settings, "sample-test-password"));
            Check("password rejects incorrect input", () => !Passwords.Verify(settings, "wrong"));
            Check("password is case sensitive", () => !Passwords.Verify(settings, "Sample-test-password"));
            var second = new Settings(); Passwords.Set(second, "sample-test-password");
            Check("password salt is random", () => settings.Salt != second.Salt && settings.PasswordHash != second.PasswordHash);
            Check("malformed hash is rejected", () => !Passwords.Verify(new Settings { Salt = "broken", PasswordHash = "broken" }, "pass"));
            Check("short password is rejected", () => { try { Passwords.Set(new Settings(), "123"); return false; } catch (ArgumentException) { return true; } });
            Check("USB root permits child", () => Policy.WithinRoot(@"E:\folder\file.pdf", @"E:\"));
            Check("USB path traversal rejected", () => !Policy.WithinRoot(@"E:\folder\..\..\Windows\file.pdf", @"E:\folder"));
            Check("USB sibling prefix rejected", () => !Policy.WithinRoot(@"E:\folder-other\file.pdf", @"E:\folder"));
            Check("USB other drive rejected", () => !Policy.WithinRoot(@"C:\private.pdf", @"E:\"));
            Check("executable rejected", () => !Policy.IsDocument("file.exe"));
            Check("shortcut rejected", () => !Policy.IsDocument("file.lnk"));
            Check("script rejected", () => !Policy.IsDocument("file.ps1"));
            Check("macro format rejected", () => !Policy.IsDocument("file.docm"));
            Check("extension spoof rejected", () => !Policy.IsDocument("file.pdf.exe"));
            Check("PDF case insensitive", () => Policy.IsDocument("FILE.PDF"));
            var reader = new AllowedApp { Name = "Test Reader", Path = Environment.ProcessPath!, Extensions = ".pdf,txt" };
            Check("extension association accepted", () => Policy.Accepts(reader, "document.PDF") && Policy.Accepts(reader, "note.txt"));
            Check("unknown association rejected", () => !Policy.Accepts(reader, "document.docx"));
            var store = new Store(Path.Combine(dir, "settings.json")); settings.KioskActive = true; settings.Apps.Add(reader); store.Save(settings);
            Check("active state persists", () => store.Load().KioskActive && Passwords.Verify(store.Load(), "sample-test-password"));
            settings.KioskActive = false; store.Save(settings);
            Check("inactive state persists", () => !store.Load().KioskActive);
            Check("atomic save retains backup", () => File.Exists(store.FilePath + ".bak"));
            Check("password plaintext absent", () => !File.ReadAllText(store.FilePath).Contains("sample-test-password"));
            File.WriteAllText(store.FilePath, "invalid-json");
            Check("corrupt settings do not silently reset", () => { try { store.Load(); return false; } catch { return true; } });
            File.Delete(store.FilePath);
            Check("missing settings with backup do not reset", () => { try { store.Load(); return false; } catch (IOException) { return true; } });
            var doc = Path.Combine(dir, "safe.txt"); File.WriteAllText(doc, "test");
            Check("existing normal path validated", () => { Policy.ValidateUsbPath(doc, dir); return true; });
            Check("outside root validation fails", () => { try { Policy.ValidateUsbPath(doc, Path.Combine(dir, "other")); return false; } catch (IOException) { return true; } });
            Check("discovery paths exist and unique", () => { var apps = Discovery.Find(); return apps.All(a => File.Exists(a.Path)) && apps.Select(a => a.Path.ToLowerInvariant()).Distinct().Count() == apps.Count; });
            Check("keyboard INPUT struct size x64", () => System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>() == 40);
            var notepad = new AllowedApp { Name = "Notepad", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe") };
            var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            Check("modern Notepad system redirect accepted", () => Policy.AllowedImage(notepad, Path.Combine(packages, "Microsoft.WindowsNotepad_11.0.0.0_x64__8wekyb3d8bbwe", "Notepad", "Notepad.exe")));
            Check("untrusted Notepad package rejected", () => !Policy.AllowedImage(notepad, Path.Combine(packages, "Microsoft.WindowsNotepad_11.0.0.0_x64__untrusted", "Notepad", "Notepad.exe")));
            Check("same filename outside configured path rejected", () => !Policy.AllowedImage(reader, Path.Combine(dir, Path.GetFileName(reader.Path))));
            Check("empty image is rejected", () => !Policy.AllowedImage(reader, ""));
            Check("embedded installer resources exist", () => new[] { "RuntimeLicense.txt", "RuntimeNotices.txt", "DesktopLicense.txt", "Guide.md" }.All(file => System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("PersonalKiosk.Resources." + file)));
            var seder = new WeeklySchedule { Name = "סדר א׳", Start = "09:00", End = "13:00", Days = new() { 0 } };
            var sunday = new DateTime(2026, 10, 25);
            var scheduled = new Settings { ScheduleEnabled = true, PasswordHash = settings.PasswordHash, Salt = settings.Salt, Schedules = new() { seder }, Apps = new() { reader } };
            Check("schedule before start is not due", () => SchedulePolicy.Due(scheduled, sunday.AddHours(8).AddMinutes(59)).Count == 0);
            Check("schedule start inclusive", () => SchedulePolicy.Due(scheduled, sunday.AddHours(9)).Count == 1);
            Check("schedule catches login during window", () => SchedulePolicy.Due(scheduled, sunday.AddHours(11)).Count == 1);
            Check("schedule end exclusive", () => SchedulePolicy.Due(scheduled, sunday.AddHours(13)).Count == 0);
            Check("schedule does not catch up after window", () => SchedulePolicy.Due(scheduled, sunday.AddHours(15)).Count == 0);
            Check("unselected weekday excluded", () => SchedulePolicy.Due(scheduled, sunday.AddDays(1).AddHours(10)).Count == 0);
            Check("next weekly occurrence calculated", () => SchedulePolicy.Next(scheduled, sunday.AddHours(14))?.Time == sunday.AddDays(7).AddHours(9));
            SchedulePolicy.MarkCurrentHandled(scheduled, sunday.AddHours(10));
            Check("password exit prevents current window retrigger", () => SchedulePolicy.Due(scheduled, sunday.AddHours(11)).Count == 0);
            Check("clock rollback within same window does not retrigger", () => SchedulePolicy.Due(scheduled, sunday.AddHours(9)).Count == 0);
            Check("next week still activates", () => SchedulePolicy.Due(scheduled, sunday.AddDays(7).AddHours(9)).Count == 1);
            var scheduledStore = new Store(Path.Combine(dir, "scheduled.json")); scheduledStore.Save(scheduled);
            Check("schedule and handled occurrence persist", () => scheduledStore.Load().ScheduleEnabled && SchedulePolicy.Due(scheduledStore.Load(), sunday.AddHours(11)).Count == 0);
            Check("handled schedule next start skips current window", () => SchedulePolicy.Next(scheduled, sunday.AddHours(11))?.Time == sunday.AddDays(7).AddHours(9));
            var evening = new WeeklySchedule { Start = "23:00", End = "01:30", Days = new() { 0 } };
            Check("overnight start included", () => SchedulePolicy.Current(evening, sunday.AddHours(23)).HasValue);
            Check("overnight after midnight belongs to start date", () => SchedulePolicy.Current(evening, sunday.AddDays(1).AddHours(1))?.StartDate == "2026-10-25");
            Check("overnight end excluded", () => SchedulePolicy.Current(evening, sunday.AddDays(1).AddHours(1.5)) == null);
            Check("overnight wrong start weekday excluded", () => SchedulePolicy.Current(evening, sunday.AddHours(1)) == null);
            scheduled.Schedules.Add(new WeeklySchedule { Start = "10:00", End = "14:00", Days = new() { 0 } });
            Check("overlapping newly starting window is due", () => SchedulePolicy.Due(scheduled, sunday.AddHours(12)).Count == 1);
            SchedulePolicy.MarkCurrentHandled(scheduled, sunday.AddHours(12));
            Check("all overlapping windows handled together", () => SchedulePolicy.Due(scheduled, sunday.AddHours(12)).Count == 0);
            Check("marking never exits kiosk", () => { scheduled.KioskActive = true; SchedulePolicy.MarkCurrentHandled(scheduled, sunday.AddHours(14)); return scheduled.KioskActive; });
            Check("master schedule switch off", () => { scheduled.ScheduleEnabled = false; return SchedulePolicy.Due(scheduled, sunday.AddDays(7).AddHours(10)).Count == 0 && SchedulePolicy.Next(scheduled, sunday) == null; });
            Check("individual entry switch off", () => { var disabled = new WeeklySchedule { Enabled = false }; return SchedulePolicy.Current(disabled, sunday.AddHours(10)) == null; });
            bool Invalid(WeeklySchedule entry) { try { SchedulePolicy.Validate(new Settings { Schedules = new() { entry } }); return false; } catch (IOException) { return true; } }
            Check("equal schedule boundaries rejected", () => Invalid(new WeeklySchedule { Start = "09:00", End = "09:00" }));
            Check("invalid schedule hour rejected", () => Invalid(new WeeklySchedule { Start = "25:00" }));
            Check("missing schedule weekday rejected", () => Invalid(new WeeklySchedule { Days = new() }));
            Check("invalid schedule weekday rejected", () => Invalid(new WeeklySchedule { Days = new() { 7 } }));
            Check("enabled schedules require password", () => { try { SchedulePolicy.Validate(new Settings { ScheduleEnabled = true, Schedules = new() { new WeeklySchedule() } }); return false; } catch (IOException) { return true; } });
            Check("enabled empty schedule rejected", () => { try { SchedulePolicy.Validate(new Settings { ScheduleEnabled = true }); return false; } catch (IOException) { return true; } });
            Check("inactive schedule keeps login runner enabled", () => Startup.ShouldAutoStart(new Settings { ScheduleEnabled = true, Schedules = new() { new WeeklySchedule() } }));
            Check("active kiosk keeps login runner enabled", () => Startup.ShouldAutoStart(new Settings { KioskActive = true }));
            Check("inactive kiosk without schedules disables login runner", () => !Startup.ShouldAutoStart(new Settings()));
            Check("old settings migrate with scheduling disabled", () => { var oldPath = Path.Combine(dir, "old.json"); File.WriteAllText(oldPath, "{\"Version\":1,\"Apps\":[]}"); var old = new Store(oldPath).Load(); return !old.ScheduleEnabled && old.Schedules.Count == 0; });
            Check("Bar Ilan Hebrew product name recognized", () => ResponsaDiscovery.IsProductName("פרויקט השו״ת בר-אילן 34"));
            Check("Bar Ilan versioned English folder recognized", () => ResponsaDiscovery.IsProductName("ResponsaCD32"));
            Check("Bar Ilan helper and uninstaller excluded", () => !ResponsaDiscovery.IsProductName("Responsa helper") && !ResponsaDiscovery.IsProductName("הסרת בר אילן"));
            Check("unrelated product excluded", () => !ResponsaDiscovery.IsProductName("Adobe Acrobat"));
            var responsaFolder = Path.Combine(dir, "ResponsaCD-fixture"); Directory.CreateDirectory(responsaFolder);
            try
            {
                File.WriteAllText(Path.Combine(responsaFolder, "RESPONSA.exe"), "test fixture, never executed");
                File.WriteAllText(Path.Combine(responsaFolder, "unins000.exe"), "test fixture, never executed");
                Check("Bar Ilan discovery finds executable without installation", () => ResponsaDiscovery.FindInFolders(new[] { responsaFolder, responsaFolder }).SequenceEqual(new[] { Path.Combine(responsaFolder, "RESPONSA.exe") }));
                Check("absent Bar Ilan executable is not fabricated", () => ResponsaDiscovery.FindInFolders(new[] { Path.Combine(dir, "not-installed") }).Count == 0);
            }
            finally { foreach (var file in Directory.EnumerateFiles(responsaFolder)) File.Delete(file); Directory.Delete(responsaFolder); }
        }
        finally
        {
            // Only files created by this test; never traverse arbitrary directories.
            foreach (var file in Directory.EnumerateFiles(dir)) File.Delete(file);
            Directory.Delete(dir);
        }
        results.Add($"TOTAL {results.Count}, FAILED {failures}");
        File.WriteAllLines(output ?? Path.Combine(Path.GetTempPath(), "PersonalKiosk-SelfTest.txt"), results);
        return failures == 0 ? 0 : 1;
    }
}
