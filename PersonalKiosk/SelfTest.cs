using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

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
            Check("embedded installer resources exist", () => new[] { "RuntimeLicense.txt", "RuntimeNotices.txt", "DesktopLicense.txt", "Rights.txt", "Guide.md" }.All(file => System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("PersonalKiosk.Resources." + file)));
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
            var warnings = new Settings { ScheduleEnabled = true, Schedules = new() { new WeeklySchedule { Start = "09:00", End = "13:00", Days = new() { 0 } } } };
            Check("warning does not appear before lead time", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(54)).Count == 0);
            Check("advance warning starts at configured lead time", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(55)).Single().FinalMinute == false);
            NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(55)).Single().MarkShown();
            Check("advance warning not repeated", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(56)).Count == 0);
            Check("last minute warning appears separately", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(59)).Single().FinalMinute);
            NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(59)).Single().MarkShown();
            Check("last minute warning not repeated", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(59.5)).Count == 0);
            Check("warning never replaces scheduled activation", () => NoticePolicy.Pending(warnings, sunday.AddHours(9)).Count == 0 && SchedulePolicy.Due(warnings, sunday.AddHours(9)).Count == 1);
            Check("warning suppressed after handled occurrence", () => { warnings.Schedules[0].LastHandledDate = "2026-10-25"; warnings.Schedules[0].LastAdvanceNoticeDate = ""; return NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(55)).Count == 0; });
            warnings.Schedules[0].LastHandledDate = ""; warnings.ScheduleWarningsEnabled = false;
            Check("warnings can be disabled without disabling schedule", () => NoticePolicy.Pending(warnings, sunday.AddHours(8).AddMinutes(59)).Count == 0 && SchedulePolicy.Due(warnings, sunday.AddHours(10)).Count == 1);
            warnings.ScheduleWarningsEnabled = true; warnings.Schedules[0].Start = "00:02"; warnings.Schedules[0].End = "01:00"; warnings.Schedules[0].Days = new() { 1 };
            Check("advance warning crosses midnight on correct start day", () => NoticePolicy.Pending(warnings, sunday.AddHours(23).AddMinutes(58)).Single().Date == "2026-10-26");
            warnings.PasswordHash = settings.PasswordHash; warnings.Salt = settings.Salt;
            NoticePolicy.Pending(warnings, sunday.AddHours(23).AddMinutes(58)).Single().MarkShown(); scheduledStore.Save(warnings);
            Check("warning history survives restart", () => NoticePolicy.Pending(scheduledStore.Load(), sunday.AddHours(23).AddMinutes(59)).Count == 0);
            var timedApp = new AllowedApp { Name = "Word", Path = reader.Path, TimeRestricted = true, Availability = new() { new WeeklySchedule { Start = "09:00", End = "13:00", Days = new() { 0 } } } };
            Check("application unavailable before allowed hours", () => !FeaturePolicy.Available(timedApp, sunday.AddHours(8)));
            Check("application availability start inclusive", () => FeaturePolicy.Available(timedApp, sunday.AddHours(9)));
            Check("application availability end exclusive", () => !FeaturePolicy.Available(timedApp, sunday.AddHours(13)));
            Check("application unavailable on unselected day", () => !FeaturePolicy.Available(timedApp, sunday.AddDays(1).AddHours(10)));
            timedApp.Availability.Add(new WeeklySchedule { Start = "23:00", End = "01:00", Days = new() { 0 } });
            Check("application availability supports overnight window", () => FeaturePolicy.Available(timedApp, sunday.AddDays(1).AddMinutes(30)));
            Check("application remains blocked between windows", () => !FeaturePolicy.Available(timedApp, sunday.AddHours(20)));
            Check("unrestricted apps remain available", () => FeaturePolicy.Available(reader, sunday.AddHours(3)));
            Check("application restriction requires active window", () => { try { FeaturePolicy.Validate(new Settings { Apps = new() { new AllowedApp { TimeRestricted = true } } }); return false; } catch (IOException) { return true; } });
            Check("availability does not depend on kiosk schedule master", () => FeaturePolicy.Available(timedApp, sunday.AddHours(10)) && !SchedulePolicy.HasSchedule(new Settings()));
            Check("optional empty hint accepted", () => { FeaturePolicy.ValidateHint(settings, ""); return true; });
            Check("hint cannot equal actual password", () => { try { FeaturePolicy.ValidateHint(settings, "sample-test-password"); return false; } catch (IOException) { return true; } });
            Check("oversized hint rejected", () => { try { FeaturePolicy.ValidateHint(settings, new string('x', 201)); return false; } catch (IOException) { return true; } });
            settings.PasswordHint = "רמז לבדיקה"; settings.Apps.Add(timedApp); store.Save(settings);
            Check("hint and app availability persist", () => store.Load().PasswordHint == "רמז לבדיקה" && store.Load().Apps.Last().TimeRestricted && store.Load().Apps.Last().Availability.Count == 2);
            Check("legacy settings retain unrestricted apps and empty hint", () => { var oldPath = Path.Combine(dir, "old-features.json"); File.WriteAllText(oldPath, JsonSerializer.Serialize(new { Version = 1, Apps = new[] { new { Name = "Reader", Path = reader.Path, Extensions = ".pdf" } } })); var legacy = new Store(oldPath).Load(); return legacy.PasswordHint == "" && !legacy.Apps[0].TimeRestricted && legacy.AutoCheckUpdates && legacy.ScheduleWarningsEnabled; });
            Check("update numeric versions compare correctly", () => UpdatePolicy.Newer("v1.10.0", "1.9.9"));
            Check("update RC numbers compare numerically", () => UpdatePolicy.Newer("v1.2.0-rc.10", "1.2.0-rc.2"));
            Check("stable release supersedes its RC", () => UpdatePolicy.Newer("v1.2.0", "1.2.0-rc.1"));
            Check("same version is not an update", () => !UpdatePolicy.Newer("v1.2.0-rc.1", "1.2.0-rc.1"));
            Check("build metadata is ignored for precedence", () => !UpdatePolicy.Newer("1.2.0+new", "1.2.0+old"));
            Check("older release never offered as downgrade", () => !UpdatePolicy.Newer("v1.1.0", "1.2.0-rc.1"));
            Check("malformed release version rejected", () => !UpdatePolicy.Newer("1.02.0", "1.0.0") && !ReleaseNumber.TryParse("1.2.0-rc.01", out _));
            Check("trusted GitHub release link accepted", () => UpdatePolicy.TrustedReleaseUrl("https://github.com/Lev-Good/personal-kiosk/releases/tag/v1.3.0"));
            Check("untrusted update links rejected", () => !UpdatePolicy.TrustedReleaseUrl("https://github.com.evil.invalid/Lev-Good/personal-kiosk/releases/tag/v1.3.0") && !UpdatePolicy.TrustedReleaseUrl("http://github.com/Lev-Good/personal-kiosk/releases/tag/v1.3.0") && !UpdatePolicy.TrustedReleaseUrl("https://github.com/Other/repo/releases/tag/v1.3.0"));
            string Release(string tag, bool pre = false, bool draft = false, string? url = null, bool installer = true) => JsonSerializer.Serialize(new { tag_name = tag, prerelease = pre, draft, html_url = url ?? "https://github.com/Lev-Good/personal-kiosk/releases/tag/" + tag, assets = installer ? new object[] { new { name = "PersonalKioskSetup.exe", state = "uploaded" } } : Array.Empty<object>() });
            var releases = "[" + string.Join(',', new[] { Release("v1.3.0-rc.1", true), Release("v1.2.0"), Release("v9.0.0", draft: true), Release("v8.0.0", url: "https://evil.invalid/"), Release("v7.0.0", installer: false) }) + "]";
            Check("updater selects highest permitted release", () => UpdatePolicy.SelectLatest(releases, true)?.Tag == "v1.3.0-rc.1");
            Check("stable channel excludes prereleases", () => UpdatePolicy.SelectLatest(releases, false)?.Tag == "v1.2.0");
            Check("mismatched release tag URL rejected", () => UpdatePolicy.SelectLatest("[" + Release("v2.0.0", url: "https://github.com/Lev-Good/personal-kiosk/releases/tag/v1.0.0") + "]", true) == null);
            Check("empty release list handled", () => UpdatePolicy.SelectLatest("[]", true) == null);
            Check("invalid release response fails visibly", () => { try { UpdatePolicy.SelectLatest("{}", true); return false; } catch (FormatException) { return true; } });
            var utcNow = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
            Check("automatic updates limited to once daily", () => !UpdatePolicy.CheckDue(utcNow.AddHours(-23), utcNow) && UpdatePolicy.CheckDue(utcNow.AddDays(-1), utcNow));
            Check("clock rollback cannot suppress checks forever", () => UpdatePolicy.CheckDue(utcNow.AddDays(2), utcNow));
            bool requestSafe = false;
            var handler = new StubHandler(request => { requestSafe = request.Method == HttpMethod.Get && request.RequestUri?.ToString() == UpdatePolicy.ApiUrl && request.Headers.Authorization == null && request.Content == null; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releases) }; });
            Check("updater transport sends metadata request without secrets", () => UpdatePolicy.FetchAsync(true, handler).GetAwaiter().GetResult()?.Tag == "v1.3.0-rc.1" && requestSafe);
            Check("HTTP error does not claim up-to-date", () => { try { UpdatePolicy.FetchAsync(true, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))).GetAwaiter().GetResult(); return false; } catch (HttpRequestException) { return true; } });
            Check("oversized update response rejected", () => { try { UpdatePolicy.FetchAsync(true, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 3 * 1024 * 1024)) })).GetAwaiter().GetResult(); return false; } catch (HttpRequestException) { return true; } });
            Check("branding and exact home link embedded", () => Brand.Owner == "לב טוב דיגיטל" && Brand.HomeUrl == "https://digital.levtov.uk/index.html" && Brand.Rights.Contains("כל הזכויות שמורות"));
            Check("installed release metadata is valid for update comparison", () => ReleaseNumber.TryParse(BuildInfo.ReleaseVersion, out _) && BuildInfo.ReleaseVersion.StartsWith(BuildInfo.Version, StringComparison.Ordinal));
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
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
        internal StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) { this.respond = respond; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
