using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PersonalKiosk;

internal sealed class InstallerWindow : Window
{
    internal static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PersonalKiosk");
    internal static string InstalledExe => Path.Combine(InstallDir, "PersonalKiosk.exe");
    private readonly TextBlock status;
    private readonly Button install;
    internal InstallerWindow(bool uninstall = false)
    {
        Title = uninstall ? "הסרת קיוסק אישי" : "התקנת קיוסק אישי"; Width = 620; Height = 560; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FlowDirection = FlowDirection.RightToLeft; Background = Ui.Background; FontFamily = new FontFamily("Segoe UI");
        var p = new StackPanel { Margin = new Thickness(34) };
        p.Children.Add(Ui.Text("קיוסק אישי", 36, Ui.Accent)); p.Children.Add(Ui.Text(uninstall ? "הסרת התוכנה מהמחשב" : "סביבת עבודה פשוטה למחשב שלך", 21));
        p.Children.Add(Ui.Text(uninstall ? "ההסרה תמחק את התוכנה וקיצורי הדרך. ההגדרות יישארו במחשב. יש לצאת מהקיוסק בסיסמה לפני הסרה." : "התקנה לחשבון הנוכחי, ללא צורך בהרשאת מנהל. התוכנה כוללת את רכיבי ההפעלה שלה; אין צורך להתקין ‎.NET בנפרד.", 15, Ui.Muted));
        p.Children.Add(Ui.Text(uninstall ? InstallDir : "התוכנה תותקן כאן:", 14));
        if (!uninstall) p.Children.Add(new TextBlock { Text = InstallDir, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.LeftToRight, FontSize = 13, Foreground = Ui.Muted, Margin = new Thickness(4, 10, 4, 16) });
        if (!uninstall) p.Children.Add(Ui.Text("Windows 10 מגרסה 1809 ומעלה / Windows 11, במחשב x64. החסימה מיועדת לשימוש מזדמן ואינה מבודדת קבצים אישיים.", 14));
        status = Ui.Text("", 14); p.Children.Add(status);
        install = Ui.Button(uninstall ? "הסרת התוכנה" : "התקנה", () => { if (uninstall) Uninstall(); else Install(); }, true); p.Children.Add(install);
        p.Children.Add(Ui.Button("סגירה", Close)); Content = new ScrollViewer { Content = p };
        p.Children.Add(Ui.BrandFooter(this));
    }
    private void Install()
    {
        try
        {
            if (!Environment.Is64BitOperatingSystem) throw new IOException("גרסה זו מיועדת למערכת Windows של 64 סיביות.");
            Directory.CreateDirectory(InstallDir);
            if (!Policy.SamePath(Environment.ProcessPath!, InstalledExe)) File.Copy(Environment.ProcessPath!, InstalledExe, true);
            foreach (var file in new[] { "RuntimeLicense.txt", "RuntimeNotices.txt", "DesktopLicense.txt", "Rights.txt", "Guide.md" })
            {
                using var resource = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PersonalKiosk.Resources." + file) ?? throw new IOException("משאב התקנה חסר: " + file);
                using var target = File.Create(Path.Combine(InstallDir, file)); resource.CopyTo(target);
            }
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "קיוסק אישי.lnk"), "");
            var menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "קיוסק אישי"); Directory.CreateDirectory(menu);
            Shortcut(Path.Combine(menu, "קיוסק אישי.lnk"), ""); Shortcut(Path.Combine(menu, "הסרת קיוסק אישי.lnk"), "--uninstall");
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PersonalKiosk");
            key.SetValue("DisplayName", "קיוסק אישי"); key.SetValue("DisplayVersion", BuildInfo.Version); key.SetValue("Publisher", Brand.Owner); key.SetValue("URLInfoAbout", Brand.HomeUrl); key.SetValue("InstallLocation", InstallDir); key.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall"); key.SetValue("DisplayIcon", InstalledExe); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            status.Text = "ההתקנה הושלמה. אפשר לפתוח את התוכנה מקיצור הדרך בשולחן העבודה. מצב קיוסק לא הופעל.";
            install.IsEnabled = false;
        }
        catch (Exception ex) { status.Text = "ההתקנה לא הושלמה: " + ex.Message; }
    }
    private static void Shortcut(string path, string args)
    {
        // Use the Windows shell COM API; no external script or shell command is evaluated.
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("לא ניתן ליצור קיצור דרך.");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic link = shell.CreateShortcut(path);
        try { link.TargetPath = InstalledExe; link.Arguments = args; link.WorkingDirectory = InstallDir; link.Description = "קיוסק אישי"; link.Save(); }
        finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
    }
    private void Uninstall()
    {
        try
        {
            if (new Store().Load().KioskActive) throw new IOException("יש לצאת מהקיוסק באמצעות הסיסמה לפני הסרת התוכנה.");
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PersonalKiosk");
            if (!Policy.SamePath(InstallDir, expected) || !Policy.SamePath(Environment.ProcessPath!, InstalledExe)) throw new IOException("ההסרה זמינה רק מהעותק המותקן.");
            Startup.Disable();
            var scriptPath = Path.Combine(Path.GetTempPath(), "PersonalKiosk-Uninstall-" + Guid.NewGuid().ToString("N") + ".ps1");
            static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
            var menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "קיוסק אישי");
            var script = "$ErrorActionPreference = 'Stop'\n" +
                "Wait-Process -Id " + Environment.ProcessId + " -ErrorAction SilentlyContinue\n" +
                "$target = " + Quote(InstallDir) + "\n" +
                "$expected = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\\PersonalKiosk'\n" +
                "if ([IO.Path]::GetFullPath($target) -ne [IO.Path]::GetFullPath($expected)) { throw 'Unexpected target' }\n" +
                "Remove-Item -LiteralPath (Join-Path $target 'PersonalKiosk.exe') -Force -ErrorAction SilentlyContinue\n" +
                "foreach ($file in @('RuntimeLicense.txt','RuntimeNotices.txt','DesktopLicense.txt','Rights.txt','Guide.md')) { Remove-Item -LiteralPath (Join-Path $target $file) -Force -ErrorAction SilentlyContinue }\n" +
                "if ((Get-ChildItem -LiteralPath $target -Force | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath $target -Force }\n" +
                "Remove-Item -LiteralPath " + Quote(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "קיוסק אישי.lnk")) + " -Force -ErrorAction SilentlyContinue\n" +
                "Remove-Item -LiteralPath " + Quote(Path.Combine(menu, "קיוסק אישי.lnk")) + " -Force -ErrorAction SilentlyContinue\n" +
                "Remove-Item -LiteralPath " + Quote(Path.Combine(menu, "הסרת קיוסק אישי.lnk")) + " -Force -ErrorAction SilentlyContinue\n" +
                "if ((Get-ChildItem -LiteralPath " + Quote(menu) + " -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath " + Quote(menu) + " -Force -ErrorAction SilentlyContinue }\n" +
                "Remove-Item -LiteralPath 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\PersonalKiosk' -ErrorAction SilentlyContinue\n" +
                "Remove-Item -LiteralPath $PSCommandPath -Force\n";
            File.WriteAllText(scriptPath, script, new UTF8Encoding(true));
            var psi = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath }) psi.ArgumentList.Add(arg);
            Process.Start(psi); Close();
        }
        catch (Exception ex) { status.Text = "לא ניתן להסיר: " + ex.Message; }
    }
}
