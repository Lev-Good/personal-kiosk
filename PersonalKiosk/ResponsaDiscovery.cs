using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PersonalKiosk;

public static class ResponsaDiscovery
{
    public static bool IsProductName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var compact = Regex.Replace(name.ToLowerInvariant(), @"[^\p{L}\p{Nd}]", "");
        if (new[] { "uninstall", "unins", "setup", "helper", "service", "otzaria", "הסרה", "הסרת", "אוצריא", "תוסף", "עזרה" }.Any(compact.Contains)) return false;
        return compact.Contains("responsa") || compact.Contains("barilan") || compact.Contains("בראילן") || compact.Contains("פרויקטהשות") || compact.Contains("פרוייקטהשות");
    }
    public static List<string> FindInFolders(IEnumerable<string> folders)
    {
        var found = new List<string>();
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                foreach (var file in new[] { "RESPONSA.exe", "ResponsaCD.exe" })
                {
                    var path = Path.Combine(folder, file);
                    if (File.Exists(path)) found.Add(Path.GetFullPath(path));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static List<string> Find()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddExe(string? file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            file = Environment.ExpandEnvironmentVariables(file.Trim('"'));
            if (string.Equals(Path.GetExtension(file), ".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file)) found.Add(Path.GetFullPath(file));
        }
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs", Path.GetPathRoot(Environment.SystemDirectory)! }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { foreach (var folder in Directory.EnumerateDirectories(root)) if (IsProductName(Path.GetFileName(folder))) folders.Add(folder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var keyPath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var parent = hive.OpenSubKey(keyPath);
                    foreach (var name in parent?.GetSubKeyNames() ?? Array.Empty<string>())
                    {
                        using var entry = parent!.OpenSubKey(name);
                        if (!IsProductName(entry?.GetValue("DisplayName") as string)) continue;
                        if (entry?.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location)) folders.Add(Environment.ExpandEnvironmentVariables(location.Trim('"')));
                        // Read an icon path only when it names the main program, not an uninstaller.
                        if (entry?.GetValue("DisplayIcon") is string icon)
                        {
                            var path = Regex.Replace(icon, @",\s*-?\d+$", "").Trim('"');
                            if (Path.GetFileName(path).Equals("RESPONSA.exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("ResponsaCD.exe", StringComparison.OrdinalIgnoreCase)) AddExe(path);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
            foreach (var exe in new[] { "RESPONSA.exe", "ResponsaCD.exe" })
            foreach (var prefix in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\" })
            {
                try { using var entry = hive.OpenSubKey(prefix + exe); AddExe(entry?.GetValue(null) as string); }
                catch (System.Security.SecurityException) { }
            }
        }
        foreach (var file in FindInFolders(folders)) found.Add(file);
        // Product-named shortcuts cover custom installation paths and versioned folders.
        // Reading a .lnk does not execute it. Ignore launcher shortcuts with arguments.
        object? shellObject = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type != null) shellObject = Activator.CreateInstance(type);
            foreach (var menu in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) })
            {
                if (shellObject == null || !Directory.Exists(menu)) continue;
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 5 };
                foreach (var shortcut in Directory.EnumerateFiles(menu, "*.lnk", options))
                {
                    if (!IsProductName(Path.GetFileNameWithoutExtension(shortcut))) continue;
                    dynamic link = ((dynamic)shellObject).CreateShortcut(shortcut);
                    try
                    {
                        string target = link.TargetPath; string args = link.Arguments;
                        if (!string.IsNullOrEmpty(args)) continue;
                        var basename = Path.GetFileNameWithoutExtension(target).ToLowerInvariant();
                        if (new[] { "unins", "setup", "helper", "cmd", "powershell", "rundll", "explorer", "msiexec", "wscript", "cscript" }.Any(basename.StartsWith)) continue;
                        AddExe(target);
                    }
                    finally { Marshal.FinalReleaseComObject(link); }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        finally { if (shellObject != null) Marshal.FinalReleaseComObject(shellObject); }
        return found.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
