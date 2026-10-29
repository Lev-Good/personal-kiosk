using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(1) };
    private readonly CancellationTokenSource updateCancellation = new();
    private DateTimeOffset? lastUpdateAttempt;
    private bool updateBusy;
    private string updateStatus = "";
    private string availabilitySignature = "";
    private NoticeWindow? noticeWindow;
    private DateTime retryNoticeAfter;
    private KioskDashboard? dashboard;
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<Action<DateTime>> secondaryClocks = new();
    private DateTime retryUnlockAfter;
    internal MainWindow(Store store, Settings settings, bool preview = false, bool backgroundStart = false)
    {
        this.store = store; this.settings = settings; this.preview = preview; this.backgroundStart = backgroundStart;
        Title = "קיוסק אישי"; Width = Math.Min(1120, SystemParameters.WorkArea.Width - 24); Height = Math.Min(840, SystemParameters.WorkArea.Height - 24); MinWidth = 760; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FlowDirection = FlowDirection.RightToLeft;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"); Background = Ui.Background; Foreground = Ui.Ink;
        Build(); Closing += OnClosing;
        SourceInitialized += (_, _) => { var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook(WindowMessage); };
        Loaded += (_, _) =>
        {
            clockTimer.Start();
            if (preview) { Title += " — תצוגה מקדימה"; return; }
            if (SessionPolicy.ShouldUnlock(settings, DateTime.Now))
            {
                var oldEnd = settings.SessionEndLocal;
                SessionPolicy.End(settings, DateTime.Now, true);
                try { store.Save(settings); }
                catch { settings.KioskActive = true; settings.SessionEndLocal = oldEnd; }
            }
            UpdateResidentMode();
            if (settings.KioskActive) Enter(true);
            else if (this.backgroundStart && SchedulePolicy.HasSchedule(settings)) { ShowInTaskbar = false; Hide(); }
            MaintenanceTick(); _ = CheckUpdatesAsync(false);
        };
        clockTimer.Tick += (_, _) =>
        {
            if (!IsVisible && !active) return;
            TryAutomaticExit();
            var now = DateTime.Now; dashboard?.Update(now); foreach (var update in secondaryClocks) update(now);
        };
        scheduleTimer.Tick += (_, _) => MaintenanceTick();
        updateTimer.Tick += (_, _) => { _ = CheckUpdatesAsync(false); };
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        Closed += (_, _) => { SystemEvents.DisplaySettingsChanged -= DisplaysChanged; clockTimer.Stop(); scheduleTimer.Stop(); updateTimer.Stop(); updateCancellation.Cancel(); noticeWindow?.Close(); tray?.Dispose(); guard?.Dispose(); CloseShields(); };
    }
    internal void Reload(Settings updated) { bool channelChanged = settings.IncludePrereleaseUpdates != updated.IncludePrereleaseUpdates; settings = updated; if (channelChanged) { lastUpdateAttempt = null; updateStatus = ""; } scheduleError = ""; UpdateResidentMode(); Build(); Dispatcher.BeginInvoke(MaintenanceTick); _ = CheckUpdatesAsync(false); }
    private void UpdateResidentMode()
    {
        if (preview) return;
        scheduleTimer.Start();
        if (settings.AutoCheckUpdates) updateTimer.Start(); else updateTimer.Stop();
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
            tray.Visible = true;
        }
        else if (tray != null) { tray.Visible = false; }
    }
    private string AvailabilitySignature() => string.Join(";", settings.Apps.Select(a => a.Id + ":" + FeaturePolicy.Available(a, DateTime.Now)));
    private void MaintenanceTick()
    {
        if (preview || sessionEnding) return;
        TryAutomaticExit();
        bool dialogOpen = OwnedWindows.Cast<Window>().Any(w => w.IsVisible);
        if (active && !dialogOpen && availabilitySignature != AvailabilitySignature()) Build();
        if (!active && !transition && !prompting && !dialogOpen && DateTime.UtcNow >= retryNoticeAfter)
        {
            var notices = NoticePolicy.Pending(settings, DateTime.Now);
            if (notices.Count != 0)
            {
                var prior = notices.Select(n => (n.Schedule, n.Schedule.LastAdvanceNoticeDate, n.Schedule.LastMinuteNoticeDate)).ToList();
                try
                {
                    foreach (var notice in notices) notice.MarkShown();
                    store.Save(settings); noticeWindow?.Close(); noticeWindow = new NoticeWindow(notices); noticeWindow.Show();
                }
                catch (Exception ex)
                {
                    foreach (var (schedule, advance, final) in prior) { schedule.LastAdvanceNoticeDate = advance; schedule.LastMinuteNoticeDate = final; }
                    scheduleError = "לא ניתן להציג התראה: " + ex.Message; retryNoticeAfter = DateTime.UtcNow.AddMinutes(1);
                }
            }
        }
        ScheduleTick();
    }
    private bool HasUpdate => UpdatePolicy.TrustedReleaseUrl(settings.LatestUpdateUrl) && UpdatePolicy.Newer(settings.LatestUpdateTag, BuildInfo.ReleaseVersion) && (settings.IncludePrereleaseUpdates || (ReleaseNumber.TryParse(settings.LatestUpdateTag, out var version) && !version.IsPrerelease));
    private async Task CheckUpdatesAsync(bool manual)
    {
        if (preview || updateBusy || sessionEnding || updateCancellation.IsCancellationRequested) return;
        var now = DateTimeOffset.UtcNow;
        if (!manual && (!settings.AutoCheckUpdates || !UpdatePolicy.CheckDue(settings.LastUpdateCheckUtc, now) || (lastUpdateAttempt.HasValue && now - lastUpdateAttempt.Value < TimeSpan.FromHours(1)))) return;
        updateBusy = true; lastUpdateAttempt = now; updateStatus = "בודק עדכונים…";
        bool channel = settings.IncludePrereleaseUpdates;
        try
        {
            var result = await UpdatePolicy.FetchAsync(channel, token: updateCancellation.Token);
            if (updateCancellation.IsCancellationRequested) return;
            if (channel != settings.IncludePrereleaseUpdates) { lastUpdateAttempt = null; updateStatus = "ערוץ העדכונים השתנה. יש לבדוק שוב."; return; }
            var old = (settings.LastUpdateCheckUtc, settings.LatestUpdateTag, settings.LatestUpdateUrl);
            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow; settings.LatestUpdateTag = result?.Tag ?? ""; settings.LatestUpdateUrl = result?.Url ?? "";
            try { store.Save(settings); }
            catch { (settings.LastUpdateCheckUtc, settings.LatestUpdateTag, settings.LatestUpdateUrl) = old; throw; }
            updateStatus = HasUpdate ? "זמינה גרסה חדשה: " + settings.LatestUpdateTag : result == null ? "לא נמצאה מהדורה מתאימה בערוץ שנבחר." : "לא נמצאה גרסה חדשה יותר.";
        }
        catch (OperationCanceledException) { if (!updateCancellation.IsCancellationRequested) updateStatus = "הבדיקה ארכה זמן רב מדי. ניתן לנסות שוב."; }
        catch (Exception) { updateStatus = "בדיקת העדכונים לא הושלמה. יש לבדוק את החיבור ולנסות שוב."; }
        finally
        {
            updateBusy = false;
            if (!updateCancellation.IsCancellationRequested && !Dispatcher.HasShutdownStarted && IsVisible && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) Build();
        }
    }
    private void ShowUpdates()
    {
        if (active) return;
        var window = Ui.Dialog(this, "עדכוני קיוסק אישי", 580, 480); var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Ui.Text("עדכונים", 28)); panel.Children.Add(Ui.Text("הגרסה המותקנת: " + BuildInfo.ReleaseVersion, 15));
        var status = Ui.Text("", 15);
        panel.Children.Add(status);
        var download = Ui.Button("פתיחת דף ההורדה", () => { try { if (HasUpdate) Brand.OpenLink(settings.LatestUpdateUrl); } catch (Exception ex) { Ui.Message(window, "לא ניתן לפתוח", ex.Message); } }, true);
        void Refresh() { status.Text = updateBusy ? "בודק עדכונים…" : !string.IsNullOrEmpty(updateStatus) ? updateStatus : HasUpdate ? "זמינה גרסה חדשה: " + settings.LatestUpdateTag : settings.LastUpdateCheckUtc.HasValue ? "בדיקה אחרונה: " + settings.LastUpdateCheckUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "עדיין לא בוצעה בדיקת עדכונים."; download.Visibility = HasUpdate ? Visibility.Visible : Visibility.Collapsed; }
        var check = new Button { Content = "בדיקה כעת" };
        check.Click += async (_, _) => { check.IsEnabled = false; status.Text = "בודק עדכונים…"; await CheckUpdatesAsync(true); Refresh(); check.IsEnabled = true; };
        panel.Children.Add(check); panel.Children.Add(download);
        panel.Children.Add(Ui.Text("הבדיקה קוראת פרטי מהדורות מ־GitHub. ההתקנה ידנית בלבד. עדיף לצאת מהקיוסק ולסגור את התוכנה לפני התקנת עדכון.", 13, Ui.Muted));
        panel.Children.Add(Ui.Button("סגירה", window.Close));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += (_, _) => Refresh(); window.Closed += (_, _) => timer.Stop(); timer.Start(); Refresh();
        window.Content = new ScrollViewer { Content = panel }; window.ShowDialog();
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
            else { settings.SessionEndLocal ??= SessionPolicy.CurrentWindow(settings, now)?.End; store.Save(settings); Build(); }
        }
        catch (Exception ex)
        {
            foreach (var (schedule, date) in previousDates) schedule.LastHandledDate = date;
            scheduleError = "ההפעלה המתוזמנת נכשלה: " + ex.Message;
            retryScheduleAfter = DateTime.UtcNow.AddMinutes(1); ShowMain(); Build();
        }
    }
    private bool DisplayKiosk => active || (preview && settings.KioskActive);
    private void Build()
    {
        availabilitySignature = AvailabilitySignature();
        var commands = new DashboardCommands(
            () => { if (!preview) Enter(false); },
            () => { if (!preview) Exit(); }, OpenSettings, Help, ShowUpdates,
            () => { if (active) UsbBrowser.Show(this, settings); },
            app => { if (active) Launch(app); });
        dashboard = new KioskDashboard(this, settings, DisplayKiosk, preview, HasUpdate, string.Join("\n", new[] { fault, scheduleError }.Where(s => !string.IsNullOrEmpty(s))), commands);
        Content = dashboard.View;
    }
    internal void RenderAt(DateTime now) => dashboard?.Update(now);
    private void OpenSettings()
    {
        if (preview || active) return;
        if (string.IsNullOrEmpty(settings.PasswordHash)) { if (!PasswordPrompt.Set(this, settings, store)) return; }
        else if (!Ask("אישור כניסה להגדרות")) return;
        SettingsEditor.Show(this, settings, store);
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
        var previousEnd = settings.SessionEndLocal;
        try
        {
            if (!resume)
            {
                if (string.IsNullOrEmpty(settings.PasswordHash) && !PasswordPrompt.Set(this, settings, store)) return;
                var consent = Ui.Dialog(this, "לפני הפעלת הקיוסק", 570, 450); var panel = new StackPanel { Margin = new Thickness(24) };
                panel.Children.Add(Ui.Text("לפני שמתחילים", 27));
                panel.Children.Add(Ui.Text("הקיוסק יפעל בחשבון הנוכחי. תוכנות מאושרות עשויות להגיע לקבצים אישיים ולבצע פעולות ללא סיסמה דרך ממשק פנימי. מסכי מערכת מאובטחים אינם נחסמים."));
                panel.Children.Add(Ui.Text("תתווסף הפעלה אוטומטית בכניסה לחשבון. לאחר יציאה בסיסמה היא תישאר רק אם לוח הזמנים מופעל. חלונות אחרים ימוזערו ויוחזרו ביציאה. מסמכים לא ייסגרו.", 14, Ui.Muted));
                panel.Children.Add(Ui.Text(settings.AutoUnlockAtScheduleEnd ? "בסוף רצף סדרים מתוזמן החסימה מסתיימת אוטומטית. יציאה מוקדמת או כניסה ידנית ללא זמן סיום דורשות סיסמה." : "ליציאה דרושה הסיסמה. אין שחזור סיסמה.", 15));
                panel.Children.Add(Ui.Button("הפעלת הקיוסק", () => consent.DialogResult = true, true)); panel.Children.Add(Ui.Button("ביטול", () => consent.DialogResult = false)); consent.Content = new ScrollViewer { Content = panel };
                if (consent.ShowDialog() != true) return;
            }
            if (settings.Apps.Count == 0 || string.IsNullOrEmpty(settings.PasswordHash)) throw new IOException("יש להגדיר סיסמה ולפחות תוכנה אחת לפני ההפעלה.");
            noticeWindow?.Close();
            normalBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
            Startup.Enable(); SessionPolicy.Begin(settings, DateTime.Now, resume); store.Save(settings);
            active = true; WindowState = WindowState.Normal; MinWidth = 0; MinHeight = 0; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
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
                settings.SessionEndLocal = previousEnd;
                try { store.Save(settings); Startup.Apply(settings); } catch { }
                Ui.Message(this, "הקיוסק לא הופעל", ex.Message);
            }
        }
        finally { transition = false; }
    }
    private void Exit()
    {
        if (!active || transition || !Ask("יציאה מהקיוסק")) return;
        LeaveKiosk(false);
    }
    private void TryAutomaticExit()
    {
        if (preview || !active || transition || sessionEnding || DateTime.UtcNow < retryUnlockAfter || !SessionPolicy.ShouldUnlock(settings, DateTime.Now)) return;
        LeaveKiosk(true);
    }
    private static void CloseDialogs(Window owner)
    {
        foreach (var window in owner.OwnedWindows.Cast<Window>().ToArray()) { CloseDialogs(window); window.Close(); }
    }
    private void LeaveKiosk(bool automatic)
    {
        transition = true;
        try
        {
            var oldEnd = settings.SessionEndLocal;
            var previousDates = settings.Schedules.Select(s => (s, s.LastHandledDate)).ToList();
            SessionPolicy.End(settings, DateTime.Now, automatic);
            try { store.Save(settings); }
            catch { settings.KioskActive = true; settings.SessionEndLocal = oldEnd; foreach (var (schedule, date) in previousDates) schedule.LastHandledDate = date; throw; }
            guard?.Dispose(); guard = null; active = false; fault = ""; CloseShields();
            if (automatic) CloseDialogs(this);
            Topmost = false; MinWidth = 760; MinHeight = 560; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
            Width = normalBounds.Width > 0 ? normalBounds.Width : 1060; Height = normalBounds.Height > 0 ? normalBounds.Height : 740;
            Left = normalBounds.X; Top = normalBounds.Y;
            Build();
            try { Startup.Apply(settings); } catch (Exception ex) { Ui.Message(this, "הקיוסק הופסק", "המצב נשמר ככבוי, אך לא ניתן לעדכן את רישום ההפעלה האוטומטית: " + ex.Message); }
            if (automatic && SchedulePolicy.HasSchedule(settings)) { ShowInTaskbar = false; Hide(); }
        }
        catch (Exception ex)
        {
            if (automatic) { retryUnlockAfter = DateTime.UtcNow.AddMinutes(1); scheduleError = "לא ניתן לסיים את החסימה אוטומטית: " + ex.Message; Build(); }
            else Ui.Message(this, "לא ניתן לצאת", ex.Message);
        }
        finally { transition = false; }
    }
    internal void Launch(AllowedApp app, string? file = null)
    {
        try
        {
            if (!File.Exists(app.Path)) throw new IOException("קובץ התוכנה לא נמצא. יש לעדכן אותו בהגדרות לאחר היציאה.");
            if (active && !FeaturePolicy.Available(app, DateTime.Now)) throw new IOException("התוכנה אינה זמינה בשעה זו לפי ההגדרות.");
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
            var palette = new KioskPalette(settings.Appearance);
            var panel = new StackPanel();
            panel.Children.Add(new LogoImage(48, AppearancePolicy.Motion(settings.Appearance))); panel.Children.Add(DashboardUi.Text("קיוסק אישי", 34, palette.Ink));
            var clock = DashboardUi.Text(DateTime.Now.ToString("HH:mm:ss"), 60, palette.Accent); clock.FlowDirection = FlowDirection.LeftToRight; panel.Children.Add(clock);
            var date = DashboardUi.Text("", 15, palette.Muted); panel.Children.Add(date);
            var countdownCaption = DashboardUi.Text(settings.AutoUnlockAtScheduleEnd ? "עד לפתיחת המחשב" : "עד סוף הסדר", 13, palette.Muted); panel.Children.Add(countdownCaption);
            var countdown = DashboardUi.Text("", 28, palette.Accent); countdown.FlowDirection = FlowDirection.LeftToRight; panel.Children.Add(countdown);
            Action<DateTime> updateClock = now =>
            {
                clock.Text = now.ToString("HH:mm:ss"); date.Text = now.ToString("dddd, dd MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("he-IL"));
                countdown.Text = settings.SessionEndLocal.HasValue ? SessionPolicy.Countdown(settings.SessionEndLocal.Value - now) : "—";
                countdown.Visibility = countdownCaption.Visibility = settings.Appearance.ShowCountdown ? Visibility.Visible : Visibility.Collapsed;
            };
            secondaryClocks.Add(updateClock); updateClock(DateTime.Now);
            panel.Children.Add(DashboardUi.Text("אפשר להעביר לכאן תוכנה מאושרת, או לחזור לרשימת התוכנות.", 16, palette.Muted)); panel.Children.Add(Ui.Button("בית הקיוסק", Home, true));
            var backdrop = DashboardUi.Background(settings.Appearance); var card = DashboardUi.Surface(panel, palette); card.MaxWidth = 480; card.VerticalAlignment = VerticalAlignment.Center; card.HorizontalAlignment = HorizontalAlignment.Center; backdrop.Children.Add(card);
            var shield = new Window { Title = "קיוסק אישי — מסך נוסף", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Ui.Background, FlowDirection = FlowDirection.RightToLeft, Content = backdrop, Topmost = true };
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
    private void CloseShields() { var wasExiting = exiting; exiting = true; foreach (var w in shields.ToArray()) w.Close(); shields.Clear(); secondaryClocks.Clear(); exiting = wasExiting; }
    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { if (active) { RebuildScreens(); Home(); } });
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (msg == 0x0011) { sessionEnding = true; if (active) { try { store.Save(settings); } catch { } } }
        if (msg == 0x0016) sessionEnding = wparam != IntPtr.Zero;
        return IntPtr.Zero;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (preview) return;
        if (active && !exiting && !sessionEnding) { e.Cancel = true; if (!transition) Exit(); }
        else if (!exiting && !sessionEnding && SchedulePolicy.HasSchedule(settings)) { e.Cancel = true; ShowInTaskbar = false; Hide(); }
    }
    private void Help() => Ui.Message(this, "מידע ועזרה", "קיוסק אישי " + BuildInfo.Version + "\n\nחזרה למסך הבית: Ctrl + Alt + Home. יציאה מוקדמת ושינוי הגדרות מחייבים סיסמה.\n\nבלשונית לוח זמנים מגדירים ימים ושעות לסדרים. התוכנה נשארת ברקע, והקיוסק מופעל בתוך טווח הסדר. יציאה בסיסמה מדלגת על שאר הסדר הנוכחי. כברירת מחדל החסימה מסתיימת בסוף רצף הסדרים. ניתן לשנות זאת בלשונית עיצוב המסך. המחשב חייב להיות ער והחשבון מחובר.\n\nהחסימה מיועדת לשימוש מזדמן. אין בידוד קבצים בחשבון הקיים. Ctrl+Alt+Delete, הרשאות מנהל ומסכי Windows מאובטחים אינם נחסמים.\n\nנדרשת סיסמה לקיצורי Ctrl+O/S/P/K ולחלונות מזוהים, אך לא לכל פעולה פנימית. אישור חלון מאפשר להשתמש בו עד סגירתו, לרבות ניווט במחשב. אין אפשרות לשחזר סיסמה.\n\nאין סינון אינטרנט. מסמכים שכבר פתוחים נשארים פתוחים. כונני USB מזוהים לפי סוג החיבור; קוראי כרטיסים ומתאמים מסוימים עשויים להופיע כ־USB.\n\nמומלץ לשמור מסמכים אישיים ולסגור אותם לפני השימוש. מדריך מלא מצורף לחבילה.");
}
