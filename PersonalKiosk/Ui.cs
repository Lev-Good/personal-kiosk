using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;

namespace PersonalKiosk;

internal static class Ui
{
    internal static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(26, 43, 62));
    internal static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(99, 114, 131));
    internal static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(28, 105, 97));
    internal static readonly Brush Background = new SolidColorBrush(Color.FromRgb(244, 247, 249));
    internal static void Theme(Application app)
    {
        var button = new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(20, 12, 20, 12)));
        button.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 15.0));
        button.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(213, 223, 230))));
        button.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        app.Resources.Add(typeof(Button), button);
        var textBox = new Style(typeof(TextBox));
        textBox.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9)));
        textBox.Setters.Add(new Setter(Control.FontSizeProperty, 15.0));
        app.Resources.Add(typeof(TextBox), textBox);
    }
    internal static TextBlock Text(string text, double size = 15, Brush? brush = null) => new() { Text = text, FontSize = size, Foreground = brush ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 6, 4, 6), LineHeight = size * 1.5 };
    internal static Button Button(string title, Action action, bool primary = false)
    {
        var b = new Button { Content = title };
        if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
        b.Click += (_, _) => action(); return b;
    }
    internal static Border Card(UIElement child) => new() { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(223, 230, 236)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(22), Margin = new Thickness(4, 8, 4, 8), Child = child };
    internal static Window Dialog(Window owner, string title, double width = 500, double height = 400)
    {
        return new Window { Owner = owner, Title = title, Width = width, Height = height, MinWidth = 380, MinHeight = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner, FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI"), Background = Background, Foreground = Ink, Topmost = owner.Topmost, ShowInTaskbar = false };
    }
    internal static void Message(Window owner, string title, string text)
    {
        var w = Dialog(owner, title, 580, text.Length > 300 ? 610 : 340); var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(Text(title, 24)); p.Children.Add(Text(text)); p.Children.Add(Button("סגירה", () => w.Close(), true));
        w.Content = new ScrollViewer { Content = p }; w.ShowDialog();
    }
}

internal sealed class PasswordPrompt
{
    private static DateTime nextAttempt;
    private static int failures;
    internal static bool Ask(Window owner, Settings settings, string title)
    {
        var w = Ui.Dialog(owner, title, 460, 430); w.ResizeMode = ResizeMode.NoResize; w.Topmost = true;
        var p = new StackPanel { Margin = new Thickness(28) };
        p.Children.Add(Ui.Text(title, 24)); p.Children.Add(Ui.Text("יש להזין את סיסמת הקיוסק לאישור הפעולה.", 14, Ui.Muted));
        var box = new PasswordBox { FontSize = 22, Padding = new Thickness(10), Margin = new Thickness(4), FlowDirection = FlowDirection.LeftToRight };
        p.Children.Add(box); var error = Ui.Text("", 13, Brushes.Firebrick); p.Children.Add(error);
        void Verify()
        {
            if (DateTime.UtcNow < nextAttempt) { error.Text = "יש להמתין מספר שניות לפני ניסיון נוסף."; return; }
            if (Passwords.Verify(settings, box.Password)) { failures = 0; nextAttempt = DateTime.MinValue; w.DialogResult = true; }
            else { failures++; nextAttempt = DateTime.UtcNow.AddSeconds(Math.Min(30, failures * 2)); box.Clear(); error.Text = "הסיסמה שגויה. יש להמתין לפני ניסיון נוסף."; box.Focus(); }
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Ui.Button("אישור", Verify, true)); row.Children.Add(Ui.Button("ביטול", () => w.DialogResult = false)); p.Children.Add(row);
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Verify(); e.Handled = true; } };
        w.Content = new ScrollViewer { Content = p }; w.Loaded += (_, _) => box.Focus(); return w.ShowDialog() == true;
    }
    internal static bool Set(Window owner, Settings settings, Store store)
    {
        var w = Ui.Dialog(owner, "הגדרת סיסמה", 470, 570); var p = new StackPanel { Margin = new Thickness(26) };
        p.Children.Add(Ui.Text("הגדרת סיסמה", 25)); p.Children.Add(Ui.Text("סיסמה אחת ליציאה ולאישור פעולות. אין אפשרות לשחזור. לפחות 4 תווים.", 14, Ui.Muted));
        p.Children.Add(Ui.Text("סיסמה חדשה")); var first = new PasswordBox { Padding = new Thickness(10), FontSize = 20 }; p.Children.Add(first);
        p.Children.Add(Ui.Text("הסיסמה שוב")); var second = new PasswordBox { Padding = new Thickness(10), FontSize = 20 }; p.Children.Add(second);
        var error = Ui.Text("", 13, Brushes.Firebrick); p.Children.Add(error);
        p.Children.Add(Ui.Button("שמירת סיסמה", () =>
        {
            try
            {
                if (first.Password != second.Password) throw new ArgumentException("הסיסמאות אינן תואמות.");
                var oldSalt = settings.Salt; var oldHash = settings.PasswordHash;
                Passwords.Set(settings, first.Password);
                try { store.Save(settings); } catch { settings.Salt = oldSalt; settings.PasswordHash = oldHash; throw; }
                w.DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }, true));
        w.Content = new ScrollViewer { Content = p }; w.Loaded += (_, _) => first.Focus(); return w.ShowDialog() == true;
    }
}

internal static class SettingsEditor
{
    internal static void Show(MainWindow owner, Settings original, Store store)
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(original))!;
        var w = Ui.Dialog(owner, "הגדרות הקיוסק", 800, 680);
        var outer = new DockPanel { Margin = new Thickness(24) };
        var bottom = new StackPanel { Orientation = Orientation.Horizontal };
        bottom.Children.Add(Ui.Button("שמירת הגדרות", () =>
        {
            try
            {
                if (copy.Apps.Count == 0) throw new IOException("יש להוסיף לפחות תוכנה אחת.");
                if (copy.Apps.Any(a => !File.Exists(a.Path))) throw new IOException("אחת התוכנות אינה קיימת. יש להסיר אותה או לתקן את הנתיב.");
                SchedulePolicy.Validate(copy);
                try { Startup.Apply(copy); store.Save(copy); }
                catch { try { Startup.Apply(original); } catch { } throw; }
                owner.Reload(copy); w.DialogResult = true;
            }
            catch (Exception ex) { Ui.Message(w, "לא ניתן לשמור", ex.Message); }
        }, true));
        bottom.Children.Add(Ui.Button("ביטול", () => w.Close())); DockPanel.SetDock(bottom, Dock.Bottom); outer.Children.Add(bottom);
        var p = new StackPanel(); p.Children.Add(Ui.Text("הגדרות", 30)); p.Children.Add(Ui.Text("התוכנות המופיעות כאן מותרות במצב הקיוסק. סדר הרשימה קובע את התוכנה שתפתח כל סוג קובץ.", 14, Ui.Muted));
        var list = new StackPanel();
        void Refresh()
        {
            list.Children.Clear();
            foreach (var app in copy.Apps.ToList())
            {
                var row = new StackPanel(); var bar = new DockPanel();
                var remove = Ui.Button("הסרה", () => { copy.Apps.Remove(app); Refresh(); }); DockPanel.SetDock(remove, Dock.Left); bar.Children.Add(remove);
                var edit = Ui.Button("עריכה", () => { Edit(w, app, copy, Refresh); }); DockPanel.SetDock(edit, Dock.Left); bar.Children.Add(edit);
                var up = Ui.Button("↑", () => { int i = copy.Apps.IndexOf(app); if (i > 0) { copy.Apps.RemoveAt(i); copy.Apps.Insert(i - 1, app); Refresh(); } }); DockPanel.SetDock(up, Dock.Left); bar.Children.Add(up);
                bar.Children.Add(Ui.Text(app.Name, 18)); row.Children.Add(bar);
                row.Children.Add(new TextBlock { Text = app.Path, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.LeftToRight, Foreground = Ui.Muted, FontSize = 12 });
                row.Children.Add(Ui.Text("סוגי קבצים: " + (string.IsNullOrWhiteSpace(app.Extensions) ? "הפעלה בלבד" : app.Extensions), 12, Ui.Muted));
                list.Children.Add(Ui.Card(row));
            }
        }
        Refresh(); p.Children.Add(list);
        var actions = new WrapPanel(); actions.Children.Add(Ui.Button("הוספת תוכנה", () => Edit(w, null, copy, Refresh)));
        actions.Children.Add(Ui.Button("זיהוי תוכנות מותקנות", () => { foreach (var app in Discovery.Find()) if (!copy.Apps.Any(a => Policy.SamePath(a.Path, app.Path))) copy.Apps.Add(app); Refresh(); })); p.Children.Add(actions);
        var usb = new CheckBox { Content = "הצגת כונני USB בקיוסק", IsChecked = copy.UsbEnabled, Margin = new Thickness(8), FontSize = 15 };
        usb.Checked += (_, _) => copy.UsbEnabled = true; usb.Unchecked += (_, _) => copy.UsbEnabled = false; p.Children.Add(usb);
        var gate = new CheckBox { Content = "דרישת סיסמה לקיצורי פעולות ולחלונות דו־שיח מזוהים", IsChecked = copy.GateDialogs, Margin = new Thickness(8), FontSize = 15 };
        gate.Checked += (_, _) => copy.GateDialogs = true; gate.Unchecked += (_, _) => copy.GateDialogs = false; p.Children.Add(gate);
        p.Children.Add(Ui.Text("הכיסוי אינו מלא: תפריטים פנימיים, שמירה אוטומטית, מסמכים אחרונים וקישורים בתוך תוכנה עשויים לפעול ללא אישור. אין בידוד של קבצים אישיים בחשבון הקיים.", 13, Ui.Muted));
        p.Children.Add(Ui.Button("שינוי סיסמה", () =>
        {
            if (PasswordPrompt.Ask(w, original, "אישור שינוי סיסמה") && PasswordPrompt.Set(w, original, store)) { copy.Salt = original.Salt; copy.PasswordHash = original.PasswordHash; }
        }));
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "תוכנות ואפשרויות", Padding = new Thickness(16, 10, 16, 10), Content = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        tabs.Items.Add(new TabItem { Header = "לוח זמנים", Padding = new Thickness(16, 10, 16, 10), Content = new ScrollViewer { Content = ScheduleEditor.Create(w, copy), VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        outer.Children.Add(tabs); w.Content = outer; w.ShowDialog();
    }
    private static void Edit(Window owner, AllowedApp? existing, Settings settings, Action refresh)
    {
        var w = Ui.Dialog(owner, existing == null ? "הוספת תוכנה" : "עריכת תוכנה", 600, 480);
        var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(Ui.Text("שם התוכנה")); var name = new TextBox { Text = existing?.Name ?? "" }; p.Children.Add(name);
        p.Children.Add(Ui.Text("קובץ הפעלה")); var path = new TextBox { Text = existing?.Path ?? "", FlowDirection = FlowDirection.LeftToRight }; p.Children.Add(path);
        p.Children.Add(Ui.Button("בחירת קובץ", () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "תוכנה (*.exe)|*.exe", CheckFileExists = true };
            if (picker.ShowDialog(w) == true) { path.Text = picker.FileName; if (string.IsNullOrWhiteSpace(name.Text)) name.Text = Path.GetFileNameWithoutExtension(picker.FileName); }
        }));
        p.Children.Add(Ui.Text("סיומות לפתיחת מסמכים, למשל ‎.pdf,.txt")); var extensions = new TextBox { Text = existing?.Extensions ?? "", FlowDirection = FlowDirection.LeftToRight }; p.Children.Add(extensions);
        p.Children.Add(Ui.Text("מאושרים רק מסמכי PDF, טקסט, Word, RTF ו־ODT. אין להוסיף דפדפן, סייר קבצים או כלי פקודות אם רוצים להגביל גישה.", 13, Ui.Muted));
        p.Children.Add(Ui.Button("אישור", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || !File.Exists(path.Text) || !string.Equals(Path.GetExtension(path.Text), ".exe", StringComparison.OrdinalIgnoreCase)) { Ui.Message(w, "פרטים חסרים", "יש להזין שם ולבחור קובץ EXE קיים."); return; }
            if (settings.Apps.Any(a => a != existing && Policy.SamePath(a.Path, path.Text))) { Ui.Message(w, "התוכנה כבר קיימת", "הקובץ הזה כבר נמצא ברשימה."); return; }
            var app = existing ?? new AllowedApp(); app.Name = name.Text.Trim(); app.Path = Path.GetFullPath(path.Text); app.Extensions = extensions.Text.Trim();
            if (existing == null) settings.Apps.Add(app); refresh(); w.DialogResult = true;
        }, true));
        w.Content = new ScrollViewer { Content = p }; w.ShowDialog();
    }
}

internal static class UsbBrowser
{
    internal static void Show(MainWindow owner, Settings settings)
    {
        var w = Ui.Dialog(owner, "כונני USB", 820, 620); w.Topmost = true;
        var outer = new DockPanel { Margin = new Thickness(22) }; var top = new StackPanel();
        top.Children.Add(Ui.Text("כונני USB", 28));
        var toolbar = new WrapPanel(); top.Children.Add(toolbar); var location = Ui.Text("בחירת כונן", 13, Ui.Muted); top.Children.Add(location);
        DockPanel.SetDock(top, Dock.Top); outer.Children.Add(top);
        var status = Ui.Text("פתיחת מסמך מחייבת סיסמה. מוצגים רק כוננים שזוהו כ־USB.", 13, Ui.Muted); DockPanel.SetDock(status, Dock.Bottom); outer.Children.Add(status);
        var list = new ListBox { FontSize = 17, Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        outer.Children.Add(list); string? root = null, current = null; var entries = new List<(string Path, bool Directory)>();
        void Drives()
        {
            root = current = null; location.Text = "בחירת כונן"; list.Items.Clear(); entries.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(Native.IsUsb))
            {
                try { list.Items.Add("▣   " + drive.Name + "   " + drive.VolumeLabel); entries.Add((drive.Name, true)); } catch { }
            }
            if (entries.Count == 0) status.Text = "לא נמצא כונן USB נגיש. יש לחבר כונן וללחוץ על רענון.";
        }
        void Navigate(string path)
        {
            try
            {
                root ??= Path.GetPathRoot(path)!;
                if (!Native.IsUsb(new DriveInfo(root))) throw new IOException("כונן ה־USB אינו זמין.");
                Policy.ValidateUsbPath(path, root); current = path; location.Text = path; list.Items.Clear(); entries.Clear();
                foreach (var dir in Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName))
                {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                    list.Items.Add("▸   " + Path.GetFileName(dir)); entries.Add((dir, true));
                }
                foreach (var file in Directory.EnumerateFiles(path).Where(Policy.IsDocument).OrderBy(Path.GetFileName))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                    list.Items.Add("▤   " + Path.GetFileName(file)); entries.Add((file, false));
                }
                status.Text = "לחיצה כפולה לפתיחה. סיסמה נדרשת לכל מסמך.";
            }
            catch (Exception ex) { status.Text = ex.Message; Drives(); }
        }
        void Open()
        {
            int index = list.SelectedIndex; if (index < 0 || index >= entries.Count) return;
            var item = entries[index]; if (item.Directory) { Navigate(item.Path); return; }
            try
            {
                var app = settings.Apps.FirstOrDefault(a => Policy.Accepts(a, item.Path) && File.Exists(a.Path));
                if (app == null) throw new IOException("אין תוכנה מאושרת לסוג הקובץ הזה. יש להגדיר סיומת מתאימה בהגדרות.");
                if (!PasswordPrompt.Ask(w, settings, "אישור פתיחת מסמך")) return;
                if (root == null || !Native.IsUsb(new DriveInfo(root))) throw new IOException("הכונן אינו זמין.");
                Policy.ValidateUsbPath(item.Path, root);
                owner.Launch(app, item.Path); w.Close();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        toolbar.Children.Add(Ui.Button("כל הכוננים", Drives));
        toolbar.Children.Add(Ui.Button("תיקייה קודמת", () => { if (current != null && root != null && !Policy.SamePath(current, root)) Navigate(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current))!); else Drives(); }));
        toolbar.Children.Add(Ui.Button("רענון", () => { if (current != null) Navigate(current); else Drives(); }));
        toolbar.Children.Add(Ui.Button("פתיחה", Open, true)); toolbar.Children.Add(Ui.Button("סגירה", () => w.Close()));
        list.MouseDoubleClick += (_, _) => Open(); list.KeyDown += (_, e) => { if (e.Key == Key.Enter) Open(); };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; var signature = "";
        timer.Tick += (_, _) =>
        {
            var now = string.Join(";", DriveInfo.GetDrives().Where(Native.IsUsb).Select(d => d.Name));
            if (now != signature) { signature = now; if (root == null || !now.Split(';').Contains(root)) Drives(); }
        };
        w.Closed += (_, _) => timer.Stop(); Drives(); timer.Start(); w.Content = outer; w.ShowDialog();
    }
}
