using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace PersonalKiosk;

public static class BuildInfo
{
    public static string Version => typeof(BuildInfo).Assembly.GetName().Version!.ToString(3);
    public static string ReleaseVersion => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(BuildInfo).Assembly)!.InformationalVersion.Split('+')[0];
}

public sealed class AllowedApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Extensions { get; set; } = "";
    public bool TimeRestricted { get; set; }
    public List<WeeklySchedule> Availability { get; set; } = new();
}

public sealed class Settings
{
    public int Version { get; set; } = 1;
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool KioskActive { get; set; }
    public bool UsbEnabled { get; set; } = true;
    public bool GateDialogs { get; set; } = true;
    public List<AllowedApp> Apps { get; set; } = new();
    public bool ScheduleEnabled { get; set; }
    public List<WeeklySchedule> Schedules { get; set; } = new();
    public bool ScheduleWarningsEnabled { get; set; } = true;
    public int ScheduleWarningMinutes { get; set; } = 5;
    public string PasswordHint { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
    public bool IncludePrereleaseUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string LatestUpdateTag { get; set; } = "";
    public string LatestUpdateUrl { get; set; } = "";
    public KioskVisualSettings Appearance { get; set; } = new();
    public bool AutoUnlockAtScheduleEnd { get; set; } = true;
    public DateTime? SessionEndLocal { get; set; }
}

public static class Passwords
{
    public static void Set(Settings settings, string password)
    {
        if (password.Length < 4) throw new ArgumentException("יש לבחור סיסמה באורך 4 תווים לפחות.");
        var salt = RandomNumberGenerator.GetBytes(24);
        settings.Salt = Convert.ToBase64String(salt);
        settings.PasswordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32));
    }
    public static bool Verify(Settings settings, string password)
    {
        try
        {
            var expected = Convert.FromBase64String(settings.PasswordHash);
            if (expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(settings.Salt), 210000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
    }
}

public sealed class Store
{
    public static string DirectoryPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalKiosk");
    public string FilePath { get; }
    public Store(string? path = null) { FilePath = path ?? System.IO.Path.Combine(DirectoryPath, "settings.json"); }
    public Settings Load()
    {
        if (!File.Exists(FilePath))
        {
            if (File.Exists(FilePath + ".bak")) throw new IOException("קובץ ההגדרות חסר. יש לשחזר את settings.json.bak לפני השימוש.");
            return new Settings { Apps = Discovery.Find() };
        }
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new IOException("קובץ ההגדרות אינו תקין.");
        if (s.Version != 1 || s.Apps == null || (s.KioskActive && string.IsNullOrEmpty(s.PasswordHash)) || s.Apps.Any(a => a == null || string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Path) || a.Extensions == null)) throw new IOException("הגדרות הקיוסק אינן תקינות. אין להפעיל חסימה עם קובץ זה.");
        if (!string.IsNullOrEmpty(s.PasswordHash) || !string.IsNullOrEmpty(s.Salt))
        {
            try { if (Convert.FromBase64String(s.Salt).Length != 24 || Convert.FromBase64String(s.PasswordHash).Length != 32) throw new FormatException(); }
            catch (Exception ex) when (ex is FormatException or ArgumentException) { throw new IOException("נתוני הסיסמה בקובץ ההגדרות אינם תקינים."); }
        }
        SchedulePolicy.Validate(s);
        FeaturePolicy.Validate(s);
        return s;
    }
    public void Save(Settings settings)
    {
        SchedulePolicy.Validate(settings);
        FeaturePolicy.Validate(settings);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
            stream.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }
}

public static class Policy
{
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase) { ".txt", ".pdf", ".docx", ".doc", ".rtf", ".odt" };
    public static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    public static bool IsDocument(string file) => Documents.Contains(Path.GetExtension(file));
    public static bool AllowedImage(AllowedApp app, string image)
    {
        if (string.IsNullOrWhiteSpace(image)) return false;
        if (SamePath(app.Path, image)) return true;
        // Windows 11 can redirect the system Notepad stub into the Microsoft Store package.
        var systemNotepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
        if (!SamePath(app.Path, systemNotepad)) return false;
        var packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
        if (!WithinRoot(image, packageRoot)) return false;
        var relative = Path.GetRelativePath(packageRoot, image).Split(Path.DirectorySeparatorChar);
        return relative.Length == 3 && relative[0].StartsWith("Microsoft.WindowsNotepad_", StringComparison.OrdinalIgnoreCase) && relative[0].EndsWith("__8wekyb3d8bbwe", StringComparison.OrdinalIgnoreCase) && string.Equals(relative[1], "Notepad", StringComparison.OrdinalIgnoreCase) && string.Equals(relative[2], "Notepad.exe", StringComparison.OrdinalIgnoreCase);
    }
    public static bool Accepts(AllowedApp app, string file) => IsDocument(file) && app.Extensions.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(e => string.Equals(e.StartsWith('.') ? e : "." + e, Path.GetExtension(file), StringComparison.OrdinalIgnoreCase));
    public static bool WithinRoot(string file, string root)
    {
        try
        {
            var full = Path.GetFullPath(file);
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || SamePath(full, fullRoot);
        }
        catch { return false; }
    }
    public static void ValidateUsbPath(string file, string root)
    {
        if (!WithinRoot(file, root)) throw new IOException("הנתיב נמצא מחוץ לכונן המורשה.");
        var current = Path.GetFullPath(file);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("לא ניתן לפתוח קישורים או תיקיות מקושרות בקיוסק.");
            if (SamePath(current, root)) return;
            current = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current)) ?? "";
        }
        throw new IOException("לא ניתן לאמת את מיקום הקובץ.");
    }
}

public static class Discovery
{
    public static List<AllowedApp> Find()
    {
        var result = new List<AllowedApp>();
        void Add(string name, string extensions, params string[] paths)
        {
            var path = paths.FirstOrDefault(File.Exists);
            if (path != null && !result.Any(a => Policy.SamePath(a.Path, path))) result.Add(new AllowedApp { Name = name, Extensions = extensions, Path = path });
        }
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Add("Word", ".docx,.doc,.rtf,.odt", Path.Combine(pf, "Microsoft Office", "root", "Office16", "WINWORD.EXE"), Path.Combine(pf86, "Microsoft Office", "root", "Office16", "WINWORD.EXE"));
        Add("פנקס רשימות", ".txt", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe"));
        Add("אוצריא", "", Path.Combine(pf, "Otzaria", "otzaria.exe"));
        Add("זית", "", Path.Combine(local, "Programs", "zayit", "zayit.exe"));
        Add("אוצר החכמה", "", @"C:\OtzarApp\OtzarLocal\launcher\bin\x64\app\otzar.exe");
        foreach (var baseDir in new[] { pf, pf86 })
        {
            Add("PDF-XChange Editor", ".pdf", Path.Combine(baseDir, "Tracker Software", "PDF Editor", "PDFXEdit.exe"));
            Add("Adobe Acrobat", ".pdf", Path.Combine(baseDir, "Adobe", "Acrobat DC", "Acrobat", "Acrobat.exe"), Path.Combine(baseDir, "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"));
            Add("Foxit PDF Reader", ".pdf", Path.Combine(baseDir, "Foxit Software", "Foxit PDF Reader", "FoxitPDFReader.exe"));
            Add("SumatraPDF", ".pdf", Path.Combine(baseDir, "SumatraPDF", "SumatraPDF.exe"));
        }
        Add("SumatraPDF", ".pdf", Path.Combine(local, "SumatraPDF", "SumatraPDF.exe"));
        // App Paths advertises installed readers without walking personal folders.
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var view in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\" })
        foreach (var exe in new[] { "Acrobat.exe", "AcroRd32.exe", "FoxitPDFReader.exe", "SumatraPDF.exe", "PDFXEdit.exe", "WINWORD.EXE" })
        {
            try { using var key = hive.OpenSubKey(view + exe); if (key?.GetValue(null) is string value) Add(exe == "WINWORD.EXE" ? "Word" : Path.GetFileNameWithoutExtension(exe), exe == "WINWORD.EXE" ? ".docx,.doc,.rtf,.odt" : ".pdf", value.Trim('"')); }
            catch (System.Security.SecurityException) { }
        }
        foreach (var path in ResponsaDiscovery.Find()) Add("בר־אילן — פרויקט השו״ת", "", path);
        return result;
    }
}

public static class Startup
{
    public static bool ShouldAutoStart(Settings settings) => settings.KioskActive || SchedulePolicy.HasSchedule(settings);
    public static void Apply(Settings settings) { if (ShouldAutoStart(settings)) Enable(); else Disable(); }
    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        key.SetValue("PersonalKiosk", "\"" + Environment.ProcessPath + "\" --resume");
    }
    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        key?.DeleteValue("PersonalKiosk", false);
    }
    public static Process? Launch(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return Process.Start(psi);
    }
}
