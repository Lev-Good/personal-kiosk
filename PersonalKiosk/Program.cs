using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PersonalKiosk;

internal static class Program
{
    private static Mutex? mutex;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTest.Run(args.SkipWhile(a => a != "--self-test").Skip(1).FirstOrDefault());
        if (args.Length == 2 && args[0] == "--watch" && int.TryParse(args[1], out var pid)) return Watch(pid);
        bool setup = args.Contains("--install") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("PersonalKioskSetup", StringComparison.OrdinalIgnoreCase);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Ui.Theme(app);
        app.DispatcherUnhandledException += (_, e) =>
        {
            // Keep the persisted active flag for the watchdog if the UI cannot recover.
            try { Directory.CreateDirectory(Store.DirectoryPath); File.AppendAllText(Path.Combine(Store.DirectoryPath, "errors.log"), DateTimeOffset.Now.ToString("O") + " " + e.Exception.GetType().Name + ": " + e.Exception.Message + Environment.NewLine); } catch { }
            e.Handled = false;
        };
        try
        {
            mutex = new Mutex(true, @"Local\PersonalKiosk_" + Environment.UserName, out bool created);
            if (!created) { MessageBox.Show("קיוסק אישי כבר פועל. יש לעבור לחלון הקיים.", "קיוסק אישי"); return 1; }
            if (setup) { app.Run(new InstallerWindow()); return 0; }
            if (args.Contains("--uninstall")) { app.Run(new InstallerWindow(true)); return 0; }
            bool preview = args.Contains("--preview") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("PersonalKioskPreview", StringComparison.OrdinalIgnoreCase);
            var store = new Store(); var settings = preview ? new Settings { Apps = Discovery.Find() } : store.Load();
            if (args.Contains("--resume") && !settings.KioskActive) return 0;
            app.Run(new MainWindow(store, settings, preview));
            return 0;
        }
        catch (Exception ex) { MessageBox.Show("לא ניתן להפעיל את קיוסק אישי.\n" + ex.Message, "קיוסק אישי", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
        finally { mutex?.Dispose(); }
    }
    private static int Watch(int pid)
    {
        using var watchMutex = new Mutex(true, @"Local\PersonalKiosk_Watch_" + Environment.UserName, out bool created);
        if (!created) return 0;
        try { using var process = Process.GetProcessById(pid); if (!Policy.SamePath(Native.Image((uint)pid), Environment.ProcessPath!)) return 1; process.WaitForExit(); }
        catch (ArgumentException) { }
        // A normal password exit persists inactive before the UI process ends.
        Thread.Sleep(1200);
        try
        {
            if (new Store().Load().KioskActive) Startup.Launch(Environment.ProcessPath!, "--resume");
        }
        catch { return 1; }
        return 0;
    }
}
