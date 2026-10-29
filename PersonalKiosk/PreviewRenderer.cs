using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PersonalKiosk;

// Off-screen layout verification only: no window is shown, no input hooks,
// no startup registration, no network checks and no settings are written.
internal static class PreviewRenderer
{
    internal static void Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var home = new MainWindow(new Store(), new Settings { Apps = Discovery.Find() }, true);
        var homeContent = (UIElement)home.Content; home.Content = null;
        Save(homeContent, Path.Combine(folder, "home.png"), 1060, 800);
        var owner = new Window();
        var schedule = new Settings { Schedules = new() { new WeeklySchedule { Name = "סדר א׳" }, new WeeklySchedule { Name = "סדר ב׳", Start = "15:30", End = "19:00" }, new WeeklySchedule { Name = "סדר ג׳", Start = "20:30", End = "22:30" } } };
        Save(new ScrollViewer { Content = ScheduleEditor.Create(owner, schedule) }, Path.Combine(folder, "schedules.png"), 780, 900);
        Save(new ScrollViewer { Content = ScheduleEditor.Create(owner, new Settings { ScheduleEnabled = true, Schedules = new() { new WeeklySchedule { Name = "זמן כתיבה", Start = "13:00", End = "15:30" } } }, true) }, Path.Combine(folder, "availability.png"), 680, 650);
        var now = DateTime.Now.Date.AddHours(11).AddMinutes(24).AddSeconds(16);
        foreach (var theme in new[] { "aurora", "midnight", "sand", "lake", "desert", "alps" })
        {
            var sample = new Settings { Apps = Discovery.Find(), KioskActive = true, ScheduleEnabled = true, SessionEndLocal = now.Date.AddHours(13), Schedules = new() { new WeeklySchedule { Name = "סדר א׳", Start = "09:00", End = "13:00", Days = new() { (int)now.DayOfWeek } } }, Appearance = new KioskVisualSettings { Theme = theme, AnimationsEnabled = false } };
            var kiosk = new MainWindow(new Store(), sample, true); kiosk.RenderAt(now); var content = (UIElement)kiosk.Content; kiosk.Content = null;
            Save(content, Path.Combine(folder, "kiosk-" + theme + ".png"), 1200, 880); kiosk.Close();
        }
        Save(new ScrollViewer { Content = AppearanceEditor.Create(owner, new Settings()) }, Path.Combine(folder, "appearance-settings.png"), 800, 900);
        home.Close(); owner.Close();
    }
    private static void Save(UIElement content, string path, int width, int height)
    {
        var root = new Border { Background = Ui.Background, Padding = new Thickness(18), FlowDirection = FlowDirection.RightToLeft, Child = content };
        TextElementFont(root);
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void TextElementFont(DependencyObject root) => System.Windows.Documents.TextElement.SetFontFamily(root, new FontFamily("Segoe UI"));
}
