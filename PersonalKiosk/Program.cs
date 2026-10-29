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
    private static EventWaitHandle? showRequest;
    private static RegisteredWaitHandle? showListener;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTest.Run(args.SkipWhile(a => a != "--self-test").Skip(1).FirstOrDefault());
        if (args.Length == 2 && args[0] == "--probe-updates")
        {
            try
            {
                var release = UpdatePolicy.FetchAsync(true).GetAwaiter().GetResult();
                File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { Success = true, Current = BuildInfo.ReleaseVersion, Latest = release?.Tag, Url = release?.Url, Newer = release != null && UpdatePolicy.Newer(release.Tag, BuildInfo.ReleaseVersion) })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(new { Success = false, Error = ex.GetType().Name })); return 1; }
        }
        if (args.Length == 2 && args[0] == "--watch" && int.TryParse(args[1], out var pid)) return Watch(pid);
        bool setup = args.Contains("--install") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("PersonalKioskSetup", StringComparison.OrdinalIgnoreCase);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Ui.Theme(app);
        if (args.Length == 2 && args[0] == "--render-previews")
        {
            PreviewRenderer.Render(args[1]); return 0;
        }
        app.DispatcherUnhandledException += (_, e) =>
        {
            // Keep the persisted active flag for the watchdog if the UI cannot recover.
            try { Directory.CreateDirectory(Store.DirectoryPath); File.AppendAllText(Path.Combine(Store.DirectoryPath, "errors.log"), DateTimeOffset.Now.ToString("O") + " " + e.Exception.GetType().Name + ": " + e.Exception.Message + Environment.NewLine); } catch { }
            e.Handled = false;
        };
        try
        {
            mutex = new Mutex(true, @"Local\PersonalKiosk_" + Environment.UserName, out bool created);
            if (!created)
            {
                if (setup || args.Contains("--uninstall")) { MessageBox.Show("יש לסגור את קיוסק אישי לפני התקנה או הסרה. אם התזמון מופעל, אפשר לסגור את התוכנה מהסמל שלה ליד השעון.", "קיוסק אישי"); return 1; }
                if (!args.Contains("--resume"))
                {
                    try { using var request = EventWaitHandle.OpenExisting(@"Local\PersonalKiosk_Show_" + Environment.UserName); request.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("קיוסק אישי כבר פועל. יש לעבור לחלון הקיים.", "קיוסק אישי"); }
                }
                return 0;
            }
            if (setup) { app.Run(new InstallerWindow()); return 0; }
            if (args.Contains("--uninstall")) { app.Run(new InstallerWindow(true)); return 0; }
            if (args.Contains("--preview-schedule") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("PersonalKioskSchedulePreview", StringComparison.OrdinalIgnoreCase))
            {
                var previewWindow = new Window { Title = "לוח זמנים — תצוגה מקדימה", Width = 780, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft, Background = Ui.Background, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI") };
                var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
                panel.Children.Add(Ui.Text("תצוגה מקדימה בלבד — השינויים אינם נשמרים", 14, Ui.Accent));
                panel.Children.Add(ScheduleEditor.Create(previewWindow, new Settings { Schedules = new() { new WeeklySchedule { Name = "סדר א׳", Start = "09:00", End = "13:00" }, new WeeklySchedule { Name = "סדר ב׳", Start = "15:30", End = "19:00" }, new WeeklySchedule { Name = "סדר ג׳", Start = "20:30", End = "22:30" } } }));
                previewWindow.Content = new System.Windows.Controls.ScrollViewer { Content = panel }; app.Run(previewWindow); return 0;
            }
            bool preview = args.Contains("--preview") || Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("PersonalKioskPreview", StringComparison.OrdinalIgnoreCase);
            var store = new Store(); var settings = preview ? new Settings { Apps = Discovery.Find() } : store.Load();
            if (args.Contains("--resume") && !Startup.ShouldAutoStart(settings)) return 0;
            var window = new MainWindow(store, settings, preview, args.Contains("--resume"));
            showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PersonalKiosk_Show_" + Environment.UserName);
            showListener = ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) => { if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(window.ShowMain); }, null, Timeout.Infinite, false);
            app.Run(window);
            return 0;
        }
        catch (Exception ex) { MessageBox.Show("לא ניתן להפעיל את קיוסק אישי.\n" + ex.Message, "קיוסק אישי", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
        finally { showListener?.Unregister(null); showRequest?.Dispose(); mutex?.Dispose(); }
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
