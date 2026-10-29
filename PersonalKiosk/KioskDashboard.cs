using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PersonalKiosk;

internal sealed record DashboardCommands(Action Start, Action Exit, Action Settings, Action Help, Action Updates, Action Usb, Action<AllowedApp> Launch);

internal sealed class KioskDashboard
{
    private static readonly CultureInfo Hebrew = new("he-IL", false);
    private readonly Settings settings;
    private readonly bool kiosk;
    private readonly KioskPalette palette;
    private readonly TextBlock clock, date, timerLabel, timerValue, sessionName, sessionHint;
    private readonly ProgressBar progress;
    private readonly Viewbox timerBox;
    internal Grid View { get; }
    internal KioskDashboard(Window owner, Settings settings, bool kiosk, bool preview, bool hasUpdate, string error, DashboardCommands commands)
    {
        this.settings = settings; this.kiosk = kiosk; palette = new KioskPalette(settings.Appearance);
        bool animate = AppearancePolicy.Motion(settings.Appearance);
        View = DashboardUi.Background(settings.Appearance);
        var root = new DockPanel { Margin = new Thickness(32, 24, 32, 20) }; View.Children.Add(root);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18), FlowDirection = FlowDirection.LeftToRight }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, FlowDirection = FlowDirection.RightToLeft }; header.Children.Add(actions);
        void Command(string title, Action action, bool primary = false)
        {
            var b = DashboardUi.Tile(DashboardUi.Text(title, 14, palette.Ink), action, palette, animate, 0); b.Width = double.NaN; b.Height = 43; b.Padding = new Thickness(14, 9, 14, 9); actions.Children.Add(b);
            if (primary) { b.Background = Ui.Accent; b.BorderBrush = Ui.Accent; ((TextBlock)b.Content).Foreground = Brushes.White; }
        }
        if (kiosk) Command("יציאה מהקיוסק", commands.Exit);
        else { Command("הפעלת הקיוסק", commands.Start, true); Command("הגדרות", commands.Settings); }
        Command("עזרה", commands.Help);
        var heading = new StackPanel { FlowDirection = FlowDirection.RightToLeft }; var name = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Background = Brushes.White, Child = new LogoImage(42, animate), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        name.Children.Add(DashboardUi.Text("קיוסק אישי", 29, palette.Ink)); heading.Children.Add(name);
        heading.Children.Add(DashboardUi.Text(preview ? "תצוגה מקדימה · ללא חסימה" : kiosk ? "●  מרחב העבודה שלך פתוח ללימוד" : "מרחב עבודה ממוקד, בקצב שלך", 13, palette.Muted)); Grid.SetColumn(heading, 1); header.Children.Add(heading);

        var footer = new Grid { Margin = new Thickness(0, 15, 0, 0), FlowDirection = FlowDirection.LeftToRight }; footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var version = DashboardUi.Text("גרסה " + BuildInfo.Version + (kiosk ? "\nCtrl + Alt + Home" : "\nWindows 10 / 11"), 11, palette.Muted); version.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(version);
        var creditGroup = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(creditGroup, 2); footer.Children.Add(creditGroup);
        var credit = new StackPanel { VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.RightToLeft };
        credit.Children.Add(DashboardUi.Text(Brand.Credit, 12, palette.Ink)); credit.Children.Add(DashboardUi.Text(Brand.Rights, 11, palette.Muted));
        var linkText = DashboardUi.Text("", 11, palette.Accent); var link = new Hyperlink(new Run("לתוכנות חינמיות נוספות")) { Foreground = palette.Accent };
        link.Click += (_, _) => { if (kiosk) { Ui.Message(owner, "אתר הבית", "ניתן לפתוח את האתר לאחר יציאה מהקיוסק בסיסמה."); return; } try { Brand.OpenLink(Brand.HomeUrl); } catch (Exception ex) { Ui.Message(owner, "לא ניתן לפתוח", ex.Message); } };
        linkText.Inlines.Add(link); credit.Children.Add(linkText); creditGroup.Children.Add(credit);
        creditGroup.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(2), Child = new LogoImage(84, animate), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

        var hero = new Grid { Margin = new Thickness(0, 0, 0, 16) }; hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition()); DockPanel.SetDock(hero, Dock.Top); root.Children.Add(hero);
        var timePanel = new StackPanel(); var clockCaption = DashboardUi.Text("כאן ועכשיו", 12, palette.Muted); clockCaption.TextAlignment = TextAlignment.Center; timePanel.Children.Add(clockCaption);
        clock = DashboardUi.Text("", 63, palette.Ink); clock.FontWeight = FontWeights.Light; clock.FlowDirection = FlowDirection.LeftToRight; clock.TextAlignment = TextAlignment.Center; clock.TextWrapping = TextWrapping.NoWrap; timePanel.Children.Add(new Viewbox { Child = clock, Height = 83, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly });
        date = DashboardUi.Text("", 14, palette.Muted); date.TextAlignment = TextAlignment.Center; timePanel.Children.Add(date);
        var timeCard = DashboardUi.Surface(timePanel, palette, new Thickness(24, 17, 24, 17)); timeCard.Margin = new Thickness(0, 0, 7, 0); hero.Children.Add(timeCard);
        var sessionPanel = new StackPanel(); timerLabel = DashboardUi.Text("", 12, palette.Muted); timerLabel.TextAlignment = TextAlignment.Center; sessionPanel.Children.Add(timerLabel);
        timerValue = DashboardUi.Text("", 37, palette.Accent); timerValue.FontWeight = FontWeights.SemiBold; timerValue.FlowDirection = FlowDirection.LeftToRight; timerValue.TextAlignment = TextAlignment.Center; timerValue.TextWrapping = TextWrapping.NoWrap; timerBox = new Viewbox { Child = timerValue, Height = 49, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly }; sessionPanel.Children.Add(timerBox);
        sessionName = DashboardUi.Text("", 16, palette.Ink); sessionName.TextAlignment = TextAlignment.Center; sessionPanel.Children.Add(sessionName); sessionHint = DashboardUi.Text("", 12, palette.Muted); sessionHint.TextAlignment = TextAlignment.Center; sessionPanel.Children.Add(sessionHint);
        progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 4, Margin = new Thickness(0, 12, 0, 0), Foreground = palette.Accent, Background = palette.Line, BorderThickness = new Thickness(0) }; sessionPanel.Children.Add(progress);
        var sessionCard = DashboardUi.Surface(sessionPanel, palette, new Thickness(24, 17, 24, 17)); sessionCard.Margin = new Thickness(7, 0, 0, 0); Grid.SetColumn(sessionCard, 1); hero.Children.Add(sessionCard);

        var body = new StackPanel();
        if (!string.IsNullOrEmpty(error)) body.Children.Add(DashboardUi.Surface(DashboardUi.Text(error, 14, KioskPalette.Brush("#B42D42")), palette));
        if (hasUpdate && !kiosk) { var update = Ui.Button("זמינה גרסה חדשה — לפרטים ולהורדה", commands.Updates); body.Children.Add(update); }
        var programsHeading = new DockPanel(); programsHeading.Children.Add(DashboardUi.Text("התוכנות שלך", 21, palette.Ink)); body.Children.Add(programsHeading);
        body.Children.Add(DashboardUi.Text(kiosk ? "בוחרים תוכנה ומתחילים. החזרה לכאן זמינה תמיד." : "התוכנות שיופיעו במצב הקיוסק", 12, palette.Muted));
        var apps = new WrapPanel { Margin = new Thickness(-5, 7, -5, 0) }; int index = 0;
        foreach (var app in settings.Apps)
        {
            bool available = !kiosk || FeaturePolicy.Available(app, DateTime.Now); bool exists = File.Exists(app.Path);
            var content = new StackPanel(); var icon = Ui.AppBadge(app); icon.Margin = new Thickness(0, 0, 0, 8); icon.HorizontalAlignment = HorizontalAlignment.Right;
            content.Children.Add(icon);
            content.Children.Add(DashboardUi.Text(app.Name, 16, palette.Ink)); content.Children.Add(DashboardUi.Text(!exists ? "לא נמצאה במחשב" : !available ? "מחוץ לשעות הזמינות" : kiosk ? "פתיחה  ←" : "מוכנה לשימוש", 11, palette.Muted));
            var button = DashboardUi.Tile(content, () => commands.Launch(app), palette, animate, index++); button.Height = 140; button.IsEnabled = available && exists; System.Windows.Automation.AutomationProperties.SetName(button, app.Name); apps.Children.Add(button);
        }
        body.Children.Add(apps);
        if (settings.Apps.Count == 0) body.Children.Add(DashboardUi.Text("יש להוסיף תוכנות דרך ההגדרות.", 14, palette.Muted));
        if (settings.UsbEnabled)
        {
            var usbPanel = new DockPanel(); var usbButton = Ui.Button("עיון בכוננים", commands.Usb); DockPanel.SetDock(usbButton, Dock.Left); usbPanel.Children.Add(usbButton);
            var usbText = new StackPanel(); usbText.Children.Add(DashboardUi.Text("קבצים מכונן USB", 16, palette.Ink)); usbText.Children.Add(DashboardUi.Text("המסמכים שלך, באישור סיסמה", 12, palette.Muted)); usbPanel.Children.Add(usbText);
            var usbCard = DashboardUi.Surface(usbPanel, palette, new Thickness(17, 10, 17, 10)); usbCard.Margin = new Thickness(0, 12, 0, 0); body.Children.Add(usbCard);
        }
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Update(DateTime.Now);
    }
    internal void Update(DateTime now)
    {
        clock.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture); date.Text = now.ToString("dddd, dd MMMM yyyy", Hebrew);
        var current = SessionPolicy.CurrentWindow(settings, now);
        var end = settings.SessionEndLocal ?? current?.End;
        progress.Visibility = Visibility.Collapsed;
        if (kiosk)
        {
            timerLabel.Text = settings.AutoUnlockAtScheduleEnd && end.HasValue ? "עד לפתיחת המחשב" : "עד סוף הסדר";
            sessionName.Text = current?.Name ?? (end.HasValue ? "הסדר הפעיל" : "כניסה ידנית");
            timerValue.Text = end.HasValue ? SessionPolicy.Countdown(end.Value - now) : "—";
            sessionHint.Text = !end.HasValue ? "אין שעת סיום מתוזמנת · יציאה בסיסמה" : settings.AutoUnlockAtScheduleEnd ? "בסיום החסימה תוסר אוטומטית" : "הפתיחה נשארת באמצעות סיסמה";
            if (end.HasValue && current != null && end.Value > current.Start && end.Value > now) { progress.Visibility = Visibility.Visible; progress.Value = Math.Clamp((end.Value - now).TotalSeconds / (end.Value - current.Start).TotalSeconds, 0, 1); }
        }
        else
        {
            var next = SchedulePolicy.Next(settings, now); timerLabel.Text = "עד הסדר הבא";
            timerValue.Text = next.HasValue ? SessionPolicy.Countdown(next.Value.Time - now) : "—";
            sessionName.Text = next?.Schedule.Name ?? "מוכן כשצריך";
            sessionHint.Text = next.HasValue ? "התוכנה תמתין ברקע ותופעל בזמן" : "הפעלת הקיוסק בלחיצה או לפי לוח זמנים";
        }
        timerBox.Visibility = settings.Appearance.ShowCountdown ? Visibility.Visible : Visibility.Collapsed;
        if (!settings.Appearance.ShowCountdown) progress.Visibility = Visibility.Collapsed;
    }
}
