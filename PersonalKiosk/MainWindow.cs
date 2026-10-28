using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace PersonalKiosk;

internal sealed class MainWindow : Window
{
    private readonly Store store;
    private Settings settings;
    private Guard? guard;
    private readonly List<Window> shields = new();
    private bool active, exiting, transition, prompting, sessionEnding;
    private string fault = "";
    private Rect normalBounds;
    private readonly bool preview;
    private readonly bool backgroundStart;
    private readonly DispatcherTimer scheduleTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private Forms.NotifyIcon? tray;
    private string scheduleError = "";
    private DateTime retryScheduleAfter;
    internal MainWindow(Store store, Settings settings, bool preview = false, bool backgroundStart = false)
    {
        this.store = store; this.settings = settings; this.preview = preview; this.backgroundStart = backgroundStart;
        Title = "קיוסק אישי"; Width = 1060; Height = 740; MinWidth = 780; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FlowDirection = FlowDirection.RightToLeft;
        FontFamily = new FontFamily("Segoe UI"); Background = Ui.Background; Foreground = Ui.Ink;
        Build(); Closing += OnClosing;
        SourceInitialized += (_, _) => { var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook(WindowMessage); };
        Loaded += (_, _) =>
        {
            if (preview) { Title += " — תצוגה מקדימה"; return; }
            UpdateResidentMode();
            if (settings.KioskActive) Enter(true);
            else if (this.backgroundStart && SchedulePolicy.HasSchedule(settings)) { ShowInTaskbar = false; Hide(); }
            ScheduleTick();
        };
        scheduleTimer.Tick += (_, _) => ScheduleTick();
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        Closed += (_, _) => { SystemEvents.DisplaySettingsChanged -= DisplaysChanged; scheduleTimer.Stop(); tray?.Dispose(); guard?.Dispose(); CloseShields(); };
    }
    internal void Reload(Settings updated) { settings = updated; scheduleError = ""; UpdateResidentMode(); Build(); Dispatcher.BeginInvoke(ScheduleTick); }
    private void UpdateResidentMode()
    {
        if (preview) return;
        scheduleTimer.Stop();
        if (SchedulePolicy.HasSchedule(settings))
        {
            if (tray == null)
            {
                tray = new Forms.NotifyIcon { Text = "קיוסק אישי — תזמון פעיל", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application };
                var menu = new Forms.ContextMenuStrip { RightToLeft = Forms.RightToLeft.Yes };
                menu.Items.Add("פתיחת קיוסק אישי", null, (_, _) => Dispatcher.BeginInvoke(ShowMain));
                menu.Items.Add("סגירת התוכנה", null, (_, _) => Dispatcher.BeginInvoke(QuitResident));
                tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowMain);
            }
            tray.Visible = true; scheduleTimer.Start();
        }
        else if (tray != null) { tray.Visible = false; }
    }
    internal void ShowMain() { ShowInTaskbar = true; Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); if (active) Home(); }
    private void QuitResident()
    {
        ShowMain();
        if (active) { Ui.Message(this, "הקיוסק פעיל", "יש לצאת מהקיוסק בסיסמה לפני סגירת התוכנה."); return; }
        if (!Ask("סגירת התוכנה — התזמון ייעצר עד להפעלה הבאה")) return;
        exiting = true; Close();
    }
    private void ScheduleTick()
    {
        if (preview || transition || prompting || sessionEnding || DateTime.UtcNow < retryScheduleAfter || OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) return;
        var now = DateTime.Now;
        var due = SchedulePolicy.Due(settings, now);
        if (due.Count == 0) return;
        var previousDates = due.Select(o => (o.Schedule, o.Schedule.LastHandledDate)).ToList();
        try
        {
            SchedulePolicy.MarkCurrentHandled(settings, now);
            scheduleError = "";
            if (!active)
            {
                ShowMain(); Enter(true);
                if (!active)
                {
                    foreach (var (schedule, date) in previousDates) schedule.LastHandledDate = date;
                    store.Save(settings); retryScheduleAfter = DateTime.UtcNow.AddMinutes(1);
                    scheduleError = "הקיוסק לא הופעל. ייעשה ניסיון נוסף בעוד דקה."; Build();
                }
            }
            else { store.Save(settings); Build(); }
        }
        catch (Exception ex)
        {
            foreach (var (schedule, date) in previousDates) schedule.LastHandledDate = date;
            scheduleError = "ההפעלה המתוזמנת נכשלה: " + ex.Message;
            retryScheduleAfter = DateTime.UtcNow.AddMinutes(1); ShowMain(); Build();
        }
    }
    private void Build()
    {
        var outer = new DockPanel { Margin = new Thickness(36, 22, 36, 22) };
        var header = new DockPanel(); DockPanel.SetDock(header, Dock.Top); outer.Children.Add(header);
        var badge = new Border { Background = active ? new SolidColorBrush(Color.FromRgb(220, 240, 234)) : Brushes.White, CornerRadius = new CornerRadius(20), Padding = new Thickness(18, 7, 18, 7), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Text(!string.IsNullOrEmpty(fault) ? "תקלה בחסימה" : active ? "●  הקיוסק פעיל" : preview ? "תצוגה מקדימה · ללא חסימה" : "○  המחשב במצב רגיל", 13, !string.IsNullOrEmpty(fault) ? Brushes.Firebrick : active ? Ui.Accent : Ui.Muted) };
        DockPanel.SetDock(badge, Dock.Left); header.Children.Add(badge);
        var title = new StackPanel(); title.Children.Add(Ui.Text("קיוסק אישי", 34)); title.Children.Add(Ui.Text(active ? "סביבת עבודה פשוטה. כל מה שצריך, במקום אחד." : "בוחרים תוכנות. מפעילים. עובדים בראש שקט.", 15, Ui.Muted)); header.Children.Add(title);
        var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); outer.Children.Add(footer);
        var controls = new WrapPanel(); DockPanel.SetDock(controls, Dock.Right); footer.Children.Add(controls);
        if (active) controls.Children.Add(Ui.Button("יציאה מהקיוסק", Exit, true));
        else
        {
            controls.Children.Add(Ui.Button("הפעלת הקיוסק", () => { if (!preview) Enter(false); }, true));
            controls.Children.Add(Ui.Button("הגדרות", () =>
            {
                if (preview) return;
                if (string.IsNullOrEmpty(settings.PasswordHash)) { if (!PasswordPrompt.Set(this, settings, store)) return; }
                else if (!Ask("אישור כניסה להגדרות")) return;
                SettingsEditor.Show(this, settings, store);
            }));
        }
        controls.Children.Add(Ui.Button("מידע ועזרה", Help));
        footer.Children.Add(Ui.Text(active ? "חזרה לבית הקיוסק: Ctrl + Alt + Home" : "גרסה " + BuildInfo.Version + "  ·  Windows 10 / 11", 12, Ui.Muted));
        var body = new StackPanel();
        if (!string.IsNullOrEmpty(scheduleError)) body.Children.Add(Ui.Card(Ui.Text(scheduleError, 15, Brushes.Firebrick)));
        if (!active && SchedulePolicy.HasSchedule(settings))
        {
            var next = SchedulePolicy.Next(settings, DateTime.Now);
            body.Children.Add(Ui.Card(Ui.Text(next.HasValue ? "הפעלה מתוזמנת הבאה: " + next.Value.Schedule.Name + " · " + next.Value.Time.ToString("dd/MM HH:mm") + "\nסגירת החלון תשאיר את התוכנה פועלת ברקע." : "לוח הזמנים מופעל. התוכנה ממשיכה לפעול ברקע.", 14, Ui.Accent)));
        }
        if (!string.IsNullOrEmpty(fault)) body.Children.Add(Ui.Card(Ui.Text("תקלה — החסימה אינה תקינה: " + fault + " יש לצאת מהקיוסק באמצעות הסיסמה.", 16, Brushes.Firebrick)));
        body.Children.Add(Ui.Text("התוכנות שלך", 23));
        body.Children.Add(Ui.Text(active ? "לבחירת תוכנה, יש ללחוץ על הכרטיס שלה." : "התוכנות שיהיו זמינות לאחר הפעלת הקיוסק", 14, Ui.Muted));
        var apps = new WrapPanel();
        foreach (var app in settings.Apps)
        {
            var card = new StackPanel(); card.Children.Add(Ui.Text(app.Name.EndsWith("Word") ? "W" : app.Extensions.Contains(".pdf") ? "PDF" : app.Name == "פנקס רשימות" ? "Aa" : "▤", 28, Ui.Accent));
            card.Children.Add(Ui.Text(app.Name, 18)); card.Children.Add(Ui.Text(File.Exists(app.Path) ? active ? "פתיחת התוכנה  ←" : "מוכנה לשימוש" : "קובץ התוכנה לא נמצא", 12, Ui.Muted));
            var button = Ui.Button("", () => { if (active) Launch(app); }); button.Content = card; button.Width = 218; button.Height = 154; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.IsEnabled = File.Exists(app.Path); System.Windows.Automation.AutomationProperties.SetName(button, app.Name); apps.Children.Add(button);
        }
        body.Children.Add(apps);
        if (settings.Apps.Count == 0) body.Children.Add(Ui.Text("עדיין אין תוכנות ברשימה. יש להוסיף תוכנות דרך ההגדרות."));
        if (settings.UsbEnabled)
        {
            var usb = new DockPanel(); var open = Ui.Button("עיון בכוננים", () => { if (active) UsbBrowser.Show(this, settings); }); DockPanel.SetDock(open, Dock.Left); usb.Children.Add(open);
            var description = new StackPanel(); description.Children.Add(Ui.Text("קבצים מכונן USB", 19)); description.Children.Add(Ui.Text("פתיחת מסמכים בכוננים חיצוניים, באישור סיסמה", 13, Ui.Muted)); usb.Children.Add(description); body.Children.Add(Ui.Card(usb));
        }
        if (!active) body.Children.Add(Ui.Text("הקיוסק מגביל שימוש מזדמן בחשבון הקיים. הוא אינו מבודד קבצים אישיים; אישור פעולות בתוך תוכנות חיצוניות הוא חלקי. פרטים במידע ועזרה.", 13, Ui.Muted));
        outer.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = outer;
    }
    internal bool Ask(string title)
    {
        if (prompting) return false;
        prompting = true;
        try { return PasswordPrompt.Ask(this, settings, title); }
        finally { prompting = false; }
    }
    private void Enter(bool resume)
    {
        if (active || transition || preview) return;
        transition = true;
        bool previouslyActive = settings.KioskActive;
        try
        {
            if (!resume)
            {
                if (string.IsNullOrEmpty(settings.PasswordHash) && !PasswordPrompt.Set(this, settings, store)) return;
                var consent = Ui.Dialog(this, "לפני הפעלת הקיוסק", 570, 450); var panel = new StackPanel { Margin = new Thickness(24) };
                panel.Children.Add(Ui.Text("לפני שמתחילים", 27));
                panel.Children.Add(Ui.Text("הקיוסק יפעל בחשבון הנוכחי. תוכנות מאושרות עשויות להגיע לקבצים אישיים ולבצע פעולות ללא סיסמה דרך ממשק פנימי. מסכי מערכת מאובטחים אינם נחסמים."));
                panel.Children.Add(Ui.Text("תתווסף הפעלה אוטומטית בכניסה לחשבון. לאחר יציאה בסיסמה היא תישאר רק אם לוח הזמנים מופעל. חלונות אחרים ימוזערו ויוחזרו ביציאה. מסמכים לא ייסגרו.", 14, Ui.Muted));
                panel.Children.Add(Ui.Text("ליציאה דרושה הסיסמה. אין שחזור סיסמה.", 15));
                panel.Children.Add(Ui.Button("הפעלת הקיוסק", () => consent.DialogResult = true, true)); panel.Children.Add(Ui.Button("ביטול", () => consent.DialogResult = false)); consent.Content = new ScrollViewer { Content = panel };
                if (consent.ShowDialog() != true) return;
            }
            if (settings.Apps.Count == 0 || string.IsNullOrEmpty(settings.PasswordHash)) throw new IOException("יש להגדיר סיסמה ולפחות תוכנה אחת לפני ההפעלה.");
            normalBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
            Startup.Enable(); settings.KioskActive = true; SchedulePolicy.MarkCurrentHandled(settings, DateTime.Now); store.Save(settings);
            active = true; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            RebuildScreens(); Build(); Home();
            guard = new Guard(settings, Home, Ask, GuardFault, Dispatcher) { ApprovedForeground = LowerShields };
            guard.Start();
            Startup.Launch(Environment.ProcessPath!, "--watch", Environment.ProcessId.ToString());
        }
        catch (Exception ex)
        {
            guard?.Dispose(); guard = null;
            if (previouslyActive || active)
            {
                active = true; fault = ex.Message; Build(); Home();
            }
            else
            {
                settings.KioskActive = false;
                try { store.Save(settings); Startup.Apply(settings); } catch { }
                Ui.Message(this, "הקיוסק לא הופעל", ex.Message);
            }
        }
        finally { transition = false; }
    }
    private void Exit()
    {
        if (!active || transition || !Ask("יציאה מהקיוסק")) return;
        transition = true;
        try
        {
            settings.KioskActive = false;
            var previousDates = settings.Schedules.Select(s => (s, s.LastHandledDate)).ToList();
            SchedulePolicy.MarkCurrentHandled(settings, DateTime.Now);
            try { store.Save(settings); }
            catch { settings.KioskActive = true; foreach (var (schedule, date) in previousDates) schedule.LastHandledDate = date; throw; }
            guard?.Dispose(); guard = null; active = false; fault = ""; CloseShields();
            Topmost = false; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
            Width = normalBounds.Width > 0 ? normalBounds.Width : 1060; Height = normalBounds.Height > 0 ? normalBounds.Height : 740;
            Left = normalBounds.X; Top = normalBounds.Y;
            Build();
            try { Startup.Apply(settings); } catch (Exception ex) { Ui.Message(this, "הקיוסק הופסק", "המצב נשמר ככבוי, אך לא ניתן לעדכן את רישום ההפעלה האוטומטית: " + ex.Message); }
        }
        catch (Exception ex) { Ui.Message(this, "לא ניתן לצאת", ex.Message); }
        finally { transition = false; }
    }
    internal void Launch(AllowedApp app, string? file = null)
    {
        try
        {
            if (!File.Exists(app.Path)) throw new IOException("קובץ התוכנה לא נמצא. יש לעדכן אותו בהגדרות לאחר היציאה.");
            LowerShields();
            using var process = file == null ? Startup.Launch(app.Path) : Startup.Launch(app.Path, file);
        }
        catch (Exception ex) { Home(); Ui.Message(this, "לא ניתן לפתוח", ex.Message); }
    }
    private void GuardFault(string message) { fault = message; Build(); Home(); }
    private void Home()
    {
        if (!active) return;
        foreach (var shield in shields) shield.Topmost = true;
        Topmost = true; if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        // Do not steal focus from password prompts and other owned kiosk windows.
        if (!OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) { Activate(); Native.SetForegroundWindow(new WindowInteropHelper(this).Handle); }
    }
    private void LowerShields() { if (!active) return; Topmost = false; foreach (var shield in shields) shield.Topmost = false; }
    private void RebuildScreens()
    {
        CloseShields();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (screen.Primary) continue;
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 440 };
            panel.Children.Add(Ui.Text("קיוסק אישי", 38)); panel.Children.Add(Ui.Text("אפשר להעביר לכאן תוכנה מאושרת. לחזרה לרשימת התוכנות:", 17, Ui.Muted)); panel.Children.Add(Ui.Button("בית הקיוסק", Home, true));
            var shield = new Window { Title = "קיוסק אישי — מסך נוסף", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Ui.Background, FlowDirection = FlowDirection.RightToLeft, Content = panel, Topmost = true };
            shield.Closing += (_, e) => { if (active && !exiting && !transition) { e.Cancel = true; Home(); } };
            shield.Show(); Place(shield, screen); shields.Add(shield);
        }
        Place(this, Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]);
    }
    private static void Place(Window window, Forms.Screen screen)
    {
        // PerMonitorV2 makes these physical pixels, including negative monitor origins.
        // Converting using the primary monitor's WPF transform leaves gaps at mixed DPI.
        var bounds = screen.Bounds;
        if (!Native.SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0014)) throw new IOException("לא ניתן לכסות את כל שטח המסך.");
    }
    private void CloseShields() { var wasExiting = exiting; exiting = true; foreach (var w in shields.ToArray()) w.Close(); shields.Clear(); exiting = wasExiting; }
    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { if (active) { RebuildScreens(); Home(); } });
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (msg == 0x0011) { sessionEnding = true; if (active) { try { store.Save(settings); } catch { } } }
        if (msg == 0x0016) sessionEnding = wparam != IntPtr.Zero;
        return IntPtr.Zero;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (active && !exiting && !sessionEnding) { e.Cancel = true; if (!transition) Exit(); }
        else if (!exiting && !sessionEnding && SchedulePolicy.HasSchedule(settings)) { e.Cancel = true; ShowInTaskbar = false; Hide(); }
    }
    private void Help() => Ui.Message(this, "מידע ועזרה", "קיוסק אישי " + BuildInfo.Version + "\n\nחזרה למסך הבית: Ctrl + Alt + Home. יציאה ושינוי הגדרות מחייבים סיסמה.\n\nבלשונית לוח זמנים מגדירים ימים ושעות לסדרים. התוכנה נשארת ברקע, והקיוסק מופעל בתוך טווח הסדר. יציאה בסיסמה מדלגת על שאר הסדר הנוכחי. סוף הטווח אינו מוציא מהקיוסק. המחשב חייב להיות ער והחשבון מחובר.\n\nהחסימה מיועדת לשימוש מזדמן. אין בידוד קבצים בחשבון הקיים. Ctrl+Alt+Delete, הרשאות מנהל ומסכי Windows מאובטחים אינם נחסמים.\n\nנדרשת סיסמה לקיצורי Ctrl+O/S/P/K ולחלונות מזוהים, אך לא לכל פעולה פנימית. אישור חלון מאפשר להשתמש בו עד סגירתו, לרבות ניווט במחשב. אין אפשרות לשחזר סיסמה.\n\nאין סינון אינטרנט. מסמכים שכבר פתוחים נשארים פתוחים. כונני USB מזוהים לפי סוג החיבור; קוראי כרטיסים ומתאמים מסוימים עשויים להופיע כ־USB.\n\nמומלץ לשמור מסמכים אישיים ולסגור אותם לפני השימוש. מדריך מלא מצורף לחבילה.");
}
