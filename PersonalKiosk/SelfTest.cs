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
