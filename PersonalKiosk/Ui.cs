using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PersonalKiosk;

internal static class Ui
{
    internal static readonly Brush Ink = Brush("#0F172A");
    internal static readonly Brush Muted = Brush("#64748B");
    internal static readonly Brush Accent = Brush("#0F766E");
    internal static readonly Brush AccentSoft = Brush("#F0FDFA");
    internal static readonly Brush AccentPale = Brush("#CCFBF1");
    internal static readonly Brush Background = Brush("#F8FAFC");
    internal static readonly Brush Surface = Brush("#FFFFFF");
    internal static readonly Brush Line = Brush("#E2E8F0");
    private static SolidColorBrush Brush(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
    internal static void Theme(Application app)
    {
        var button = new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 8, 16, 8)));
        button.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 15.0));
        button.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        button.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 38.0));
        button.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        button.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        button.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        button.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        var buttonBorder = new FrameworkElementFactory(typeof(Border));
        buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        buttonBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        buttonBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        buttonBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        buttonBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var buttonContent = new FrameworkElementFactory(typeof(ContentPresenter));
        buttonContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        buttonContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        buttonBorder.AppendChild(buttonContent);
        button.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Button)) { VisualTree = buttonBorder }));
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#5EEAD4"))); over.Setters.Add(new Setter(Control.BackgroundProperty, AccentSoft)); button.Triggers.Add(over);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .48)); button.Triggers.Add(disabled);
        app.Resources.Add(typeof(Button), button);
        var textBox = new Style(typeof(TextBox));
        textBox.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 9, 12, 9)));
        textBox.Setters.Add(new Setter(Control.FontSizeProperty, 15.0));
        textBox.Setters.Add(new Setter(Control.MinHeightProperty, 40.0));
        textBox.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
        textBox.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
        textBox.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        app.Resources.Add(typeof(TextBox), textBox);
        var password = new Style(typeof(PasswordBox));
        password.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 9, 12, 9))); password.Setters.Add(new Setter(Control.MinHeightProperty, 40.0)); password.Setters.Add(new Setter(Control.BackgroundProperty, Surface)); password.Setters.Add(new Setter(Control.BorderBrushProperty, Line)); app.Resources.Add(typeof(PasswordBox), password);
        var combo = new Style(typeof(ComboBox)); combo.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7))); combo.Setters.Add(new Setter(Control.MinHeightProperty, 40.0)); combo.Setters.Add(new Setter(Control.BackgroundProperty, Surface)); combo.Setters.Add(new Setter(Control.BorderBrushProperty, Line)); app.Resources.Add(typeof(ComboBox), combo);
        app.Resources.Add(typeof(CheckBox), ToggleStyle());
        app.Resources.Add(typeof(ScrollBar), ScrollBarStyle());
    }
    internal static TextBlock Text(string text, double size = 15, Brush? brush = null) => new() { Text = text, FontSize = size, Foreground = brush ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 6, 4, 6), LineHeight = size * 1.5 };
    internal static Button Button(string title, Action action, bool primary = false)
    {
        var b = new Button { Content = title };
        if (primary) { b.Background = Accent; b.Foreground = Brushes.White; b.BorderBrush = Accent; }
        b.Click += (_, _) => action(); return b;
    }
    internal static Border Card(UIElement child) => new() { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(4, 8, 4, 8), Child = child };
    internal static Border SettingRow(string title, string description, UIElement action)
    {
        var grid = new Grid { MinHeight = 64 }; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) }; copy.Children.Add(Text(title, 16)); copy.Children.Add(Text(description, 13, Muted)); grid.Children.Add(copy);
        if (action is FrameworkElement element) { element.VerticalAlignment = VerticalAlignment.Center; element.HorizontalAlignment = HorizontalAlignment.Left; } Grid.SetColumn(action, 1); grid.Children.Add(action); return Card(grid);
    }
    internal static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 0, 8) };
    internal static CheckBox Toggle(bool value, Action<bool> changed)
    {
        var toggle = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
        toggle.Checked += (_, _) => changed(true); toggle.Unchecked += (_, _) => changed(false); return toggle;
    }
    internal static CheckBox ChoicePill(string text, bool value)
    {
        var check = new CheckBox { Content = text, IsChecked = value, Width = 48, Height = 38, Margin = new Thickness(4), Cursor = Cursors.Hand, Foreground = Ink, FontWeight = FontWeights.SemiBold, HorizontalContentAlignment = HorizontalAlignment.Center };
        var border = new FrameworkElementFactory(typeof(Border)); border.Name = "Pill"; border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetValue(Border.BackgroundProperty, Surface); border.SetValue(Border.BorderBrushProperty, Line); border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = border }; var selected = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true }; selected.Setters.Add(new Setter(Border.BackgroundProperty, AccentSoft, "Pill")); selected.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "Pill")); template.Triggers.Add(selected); check.Template = template; return check;
    }
    internal static Border AppBadge(AllowedApp app, double size = 44)
    {
        UIElement content;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(app.Path);
            if (icon == null) throw new IOException();
            var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight((int)size - 12, (int)size - 12)); source.Freeze();
            content = new Image { Source = source, Width = size - 12, Height = size - 12, Stretch = Stretch.Uniform };
        }
        catch
        {
            string letters = app.Name == "Word" ? "W" : app.Extensions.Contains(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : app.Name == "פנקס רשימות" ? "Aa" : app.Name.Trim().Length > 0 ? app.Name.Trim()[0].ToString() : "•";
            content = new TextBlock { Text = letters, FontSize = letters.Length > 2 ? 12 : 18, FontWeight = FontWeights.SemiBold, Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(10), Background = AccentPale, Child = content, Margin = new Thickness(0, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    }
    internal static Window Dialog(Window owner, string title, double width = 500, double height = 400)
    {
        return new Window { Owner = owner, Title = title, Width = width, Height = height, MinWidth = 380, MinHeight = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner, FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), Background = Background, Foreground = Ink, Topmost = owner.Topmost, ShowInTaskbar = false };
    }

    private static Style ToggleStyle()
    {
        var style = new Style(typeof(CheckBox)); style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand)); style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 46.0)); style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 26.0)); style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));
        var outer = new FrameworkElementFactory(typeof(Grid));
        var track = new FrameworkElementFactory(typeof(Border)); track.Name = "Track"; track.SetValue(Border.WidthProperty, 46.0); track.SetValue(Border.HeightProperty, 26.0); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(13)); track.SetValue(Border.BackgroundProperty, Brush("#CBD5E1")); track.SetValue(Border.BorderBrushProperty, Brush("#94A3B8")); track.SetValue(Border.BorderThicknessProperty, new Thickness(1)); outer.AppendChild(track);
        var dot = new FrameworkElementFactory(typeof(Border)); dot.Name = "Dot"; dot.SetValue(Border.WidthProperty, 20.0); dot.SetValue(Border.HeightProperty, 20.0); dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); dot.SetValue(Border.BackgroundProperty, Brushes.White); dot.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 3, 23, 3)); dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); dot.SetValue(FrameworkElement.EffectProperty, new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = .22 }); outer.AppendChild(dot);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = outer };
        var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true }; on.Setters.Add(new Setter(Border.BackgroundProperty, Accent, "Track")); on.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "Track")); on.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(23, 3, 3, 3), "Dot")); template.Triggers.Add(on);
        var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; off.Setters.Add(new Setter(UIElement.OpacityProperty, .45)); template.Triggers.Add(off); style.Setters.Add(new Setter(Control.TemplateProperty, template)); return style;
    }
    private static Style ScrollBarStyle()
    {
        const string xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type ScrollBar}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Grid Width='6' Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='#94A3B8' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>";
        var style = new Style(typeof(ScrollBar)); style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 6.0)); style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(xaml))); return style;
    }
    internal static void Message(Window owner, string title, string text)
    {
        var w = Dialog(owner, title, 580, text.Length > 300 ? 610 : 340); var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(Text(title, 24)); p.Children.Add(Text(text)); p.Children.Add(Button("סגירה", () => w.Close(), true));
        w.Content = new ScrollViewer { Content = p }; w.ShowDialog();
    }
    internal static UIElement BrandFooter(Window owner, bool allowLink = true, bool animateLogo = true)
    {
        var outer = new DockPanel { Margin = new Thickness(4, 8, 4, 4) };
        var logo = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(2), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = new LogoImage(96, animateLogo) };
        DockPanel.SetDock(logo, Dock.Right); outer.Children.Add(logo);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Text(Brand.Credit + " · " + Brand.Rights, 12, Muted));
        panel.Children.Add(Button("לתוכנות חינמיות נוספות — אתר לב טוב דיגיטל", () =>
        {
            if (!allowLink) { Message(owner, "פתיחת אתר הבית", "ניתן לפתוח את האתר לאחר יציאה מהקיוסק בסיסמה."); return; }
            try { Brand.OpenLink(Brand.HomeUrl); } catch (Exception ex) { Message(owner, "לא ניתן לפתוח את האתר", ex.Message); }
        })); outer.Children.Add(panel); return outer;
    }
}

internal sealed class PasswordPrompt
{
    private static DateTime nextAttempt;
    private static int failures;
    internal static bool Ask(Window owner, Settings settings, string title)
    {
        var w = Ui.Dialog(owner, title, 480, 520); w.ResizeMode = ResizeMode.NoResize; w.Topmost = true;
        var p = new StackPanel { Margin = new Thickness(28) };
        p.Children.Add(Ui.Text(title, 24)); p.Children.Add(Ui.Text("יש להזין את סיסמת הקיוסק לאישור הפעולה.", 14, Ui.Muted));
        var box = new PasswordBox { FontSize = 22, Padding = new Thickness(10), Margin = new Thickness(4), FlowDirection = FlowDirection.LeftToRight };
        p.Children.Add(box); var error = Ui.Text("", 13, Brushes.Firebrick); p.Children.Add(error);
        var hint = Ui.Text("", 14, Ui.Muted); hint.Visibility = Visibility.Collapsed;
        p.Children.Add(Ui.Button("הצגת רמז", () => { hint.Text = string.IsNullOrWhiteSpace(settings.PasswordHint) ? "לא הוגדר רמז לסיסמה." : settings.PasswordHint; hint.Visibility = Visibility.Visible; })); p.Children.Add(hint);
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
        var w = Ui.Dialog(owner, "הגדרת סיסמה", 500, 680); var p = new StackPanel { Margin = new Thickness(26) };
        p.Children.Add(Ui.Text("הגדרת סיסמה", 25)); p.Children.Add(Ui.Text("סיסמה אחת ליציאה ולאישור פעולות. אין אפשרות לשחזור. לפחות 4 תווים.", 14, Ui.Muted));
        p.Children.Add(Ui.Text("סיסמה חדשה")); var first = new PasswordBox { Padding = new Thickness(10), FontSize = 20 }; p.Children.Add(first);
        p.Children.Add(Ui.Text("הסיסמה שוב")); var second = new PasswordBox { Padding = new Thickness(10), FontSize = 20 }; p.Children.Add(second);
        p.Children.Add(Ui.Text("רמז לסיסמה · לא חובה")); var hint = new TextBox { Text = settings.PasswordHint, MaxLength = 200 }; p.Children.Add(hint);
        p.Children.Add(Ui.Text("הרמז יוצג לכל מי שיבקש לראותו במסך הסיסמה. אין להזין בו את הסיסמה עצמה.", 13, Ui.Muted));
        var error = Ui.Text("", 13, Brushes.Firebrick); p.Children.Add(error);
        p.Children.Add(Ui.Button("שמירת סיסמה", () =>
        {
            try
            {
                if (first.Password != second.Password) throw new ArgumentException("הסיסמאות אינן תואמות.");
                if (!string.IsNullOrWhiteSpace(hint.Text) && hint.Text.Trim() == first.Password) throw new ArgumentException("הרמז אינו יכול להיות הסיסמה עצמה.");
                var oldSalt = settings.Salt; var oldHash = settings.PasswordHash; var oldHint = settings.PasswordHint;
                Passwords.Set(settings, first.Password);
                settings.PasswordHint = hint.Text.Trim();
                try { store.Save(settings); } catch { settings.Salt = oldSalt; settings.PasswordHash = oldHash; settings.PasswordHint = oldHint; throw; }
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
        bool manualUpdateChecked = false;
        var w = Ui.Dialog(owner, "הגדרות הקיוסק", Math.Min(1040, SystemParameters.WorkArea.Width - 40), Math.Min(780, SystemParameters.WorkArea.Height - 40));
        var outer = new DockPanel { Margin = new Thickness(24) };
        var bottom = new StackPanel { Orientation = Orientation.Horizontal };
        bottom.Children.Add(Ui.Button("שמירת הגדרות", () =>
        {
            try
            {
                if (copy.Apps.Count == 0) throw new IOException("יש להוסיף לפחות תוכנה אחת.");
                if (copy.Apps.Any(a => !File.Exists(a.Path))) throw new IOException("אחת התוכנות אינה קיימת. יש להסיר אותה או לתקן את הנתיב.");
                SchedulePolicy.Validate(copy);
                FeaturePolicy.Validate(copy); FeaturePolicy.ValidateHint(copy, copy.PasswordHint);
                // A background check may complete while this settings copy is being edited.
                if (!manualUpdateChecked)
                {
                    if (copy.IncludePrereleaseUpdates == original.IncludePrereleaseUpdates) { copy.LastUpdateCheckUtc = original.LastUpdateCheckUtc; copy.LatestUpdateTag = original.LatestUpdateTag; copy.LatestUpdateUrl = original.LatestUpdateUrl; }
                    else { copy.LastUpdateCheckUtc = null; copy.LatestUpdateTag = ""; copy.LatestUpdateUrl = ""; }
                }
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
                int position = copy.Apps.IndexOf(app);
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var identity = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                identity.Children.Add(Ui.AppBadge(app));
                var details = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                details.Children.Add(Ui.Text(app.Name, 18)); details.Children.Add(new TextBlock { Text = app.Path, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.LeftToRight, Foreground = Ui.Muted, FontSize = 12, MaxWidth = 430 });
                details.Children.Add(Ui.Text("סוגי קבצים: " + (string.IsNullOrWhiteSpace(app.Extensions) ? "הפעלה בלבד" : app.Extensions) + (app.TimeRestricted ? " · זמינות מוגבלת" : ""), 12, app.TimeRestricted ? Ui.Accent : Ui.Muted)); identity.Children.Add(details); row.Children.Add(identity);
                var controls = new WrapPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
                controls.Children.Add(Ui.Button("עריכה", () => Edit(w, app, copy, Refresh)));
                controls.Children.Add(Ui.Button("הסרה", () => { copy.Apps.Remove(app); Refresh(); }));
                var up = Ui.Button("למעלה ↑", () => { int i = copy.Apps.IndexOf(app); if (i > 0) { copy.Apps.RemoveAt(i); copy.Apps.Insert(i - 1, app); Refresh(); } }); up.IsEnabled = position > 0; controls.Children.Add(up);
                var down = Ui.Button("למטה ↓", () => { int i = copy.Apps.IndexOf(app); if (i >= 0 && i < copy.Apps.Count - 1) { copy.Apps.RemoveAt(i); copy.Apps.Insert(i + 1, app); Refresh(); } }); down.IsEnabled = position < copy.Apps.Count - 1; controls.Children.Add(down);
                Grid.SetColumn(controls, 1); row.Children.Add(controls); list.Children.Add(Ui.Card(row));
            }
        }
        Refresh(); p.Children.Add(list);
        var actions = new WrapPanel(); actions.Children.Add(Ui.Button("הוספת תוכנה", () => Edit(w, null, copy, Refresh)));
        actions.Children.Add(Ui.Button("זיהוי תוכנות מותקנות", () => { foreach (var app in Discovery.Find()) if (!copy.Apps.Any(a => Policy.SamePath(a.Path, app.Path))) copy.Apps.Add(app); Refresh(); })); p.Children.Add(actions);
        p.Children.Add(Ui.SettingRow("כונני USB", "הצגת כוננים נשלפים ודפדפן קבצים פשוט במסך הקיוסק.", Ui.Toggle(copy.UsbEnabled, value => copy.UsbEnabled = value)));
        p.Children.Add(Ui.SettingRow("אישור פעולות רגישות", "דרישת סיסמה לקיצורי פעולות ולחלונות דו־שיח מזוהים.", Ui.Toggle(copy.GateDialogs, value => copy.GateDialogs = value)));
        p.Children.Add(Ui.Card(Ui.Text("הכיסוי אינו מלא: תפריטים פנימיים, שמירה אוטומטית, מסמכים אחרונים וקישורים בתוך תוכנה עשויים לפעול ללא אישור. אין בידוד של קבצים אישיים בחשבון הקיים.", 13, Ui.Muted)));
        TextBox? hintEditor = null;
        p.Children.Add(Ui.Button("שינוי סיסמה", () =>
        {
            if (PasswordPrompt.Ask(w, original, "אישור שינוי סיסמה") && PasswordPrompt.Set(w, original, store)) { copy.Salt = original.Salt; copy.PasswordHash = original.PasswordHash; copy.PasswordHint = original.PasswordHint; if (hintEditor != null) hintEditor.Text = copy.PasswordHint; }
        }));
        var extras = new StackPanel(); extras.Children.Add(Ui.Text("סיסמה ועדכונים", 25));
        var hint = new TextBox { Text = copy.PasswordHint, MaxLength = 200, Width = 270 }; hintEditor = hint; hint.TextChanged += (_, _) => copy.PasswordHint = hint.Text.Trim();
        extras.Children.Add(Ui.SettingRow("רמז לסיסמה", "אופציונלי, נשמר כטקסט גלוי. אין להזין בו את הסיסמה עצמה.", hint));
        extras.Children.Add(Ui.SettingRow("בדיקת עדכונים אוטומטית", "בדיקה בפתיחה ואחת ליום בזמן שהתוכנה פועלת.", Ui.Toggle(copy.AutoCheckUpdates, value => copy.AutoCheckUpdates = value)));
        extras.Children.Add(Ui.SettingRow("גרסאות ניסיון", "הצגת עדכונים מערוץ גרסאות הניסיון בנוסף לגרסאות יציבות.", Ui.Toggle(copy.IncludePrereleaseUpdates, value => copy.IncludePrereleaseUpdates = value)));
        var updateStatus = Ui.Text(copy.LastUpdateCheckUtc.HasValue ? "בדיקה אחרונה: " + copy.LastUpdateCheckUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "עדיין לא בוצעה בדיקה ידנית.", 13, Ui.Muted);
        var downloadUpdate = Ui.Button("פתיחת דף ההורדה", () => { try { if (UpdatePolicy.TrustedReleaseUrl(copy.LatestUpdateUrl)) Brand.OpenLink(copy.LatestUpdateUrl); } catch (Exception ex) { Ui.Message(w, "לא ניתן לפתוח", ex.Message); } }, true); downloadUpdate.Visibility = UpdatePolicy.Newer(copy.LatestUpdateTag, BuildInfo.ReleaseVersion) ? Visibility.Visible : Visibility.Collapsed;
        var checkUpdate = Ui.Button("בדיקה כעת", () => { });
        checkUpdate.Click += async (_, _) =>
        {
            checkUpdate.IsEnabled = false; updateStatus.Text = "בודק עדכונים…";
            try
            {
                var result = await UpdatePolicy.FetchAsync(copy.IncludePrereleaseUpdates);
                manualUpdateChecked = true; copy.LastUpdateCheckUtc = DateTimeOffset.UtcNow; copy.LatestUpdateTag = result?.Tag ?? ""; copy.LatestUpdateUrl = result?.Url ?? "";
                bool newer = result != null && UpdatePolicy.Newer(result.Tag, BuildInfo.ReleaseVersion);
                updateStatus.Text = newer ? "זמינה גרסה חדשה: " + result!.Tag : result == null ? "לא נמצאה מהדורה מתאימה בערוץ שנבחר." : "מותקנת הגרסה העדכנית ביותר."; downloadUpdate.Visibility = newer ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception) { updateStatus.Text = "בדיקת העדכונים לא הושלמה. יש לבדוק את החיבור ולנסות שוב."; }
            finally { checkUpdate.IsEnabled = true; }
        };
        var updateActions = new WrapPanel(); updateActions.Children.Add(checkUpdate); updateActions.Children.Add(downloadUpdate);
        var updatePanel = new StackPanel(); updatePanel.Children.Add(updateStatus); updatePanel.Children.Add(updateActions); extras.Children.Add(Ui.SettingRow("בדיקת עדכונים ידנית", "הבדיקה משתמשת בערוץ שנבחר לעיל ונשמרת בעת שמירת ההגדרות.", updatePanel));
        extras.Children.Add(Ui.Card(Ui.Text("הבדיקה פונה ל־GitHub כדי לקרוא פרטי מהדורות. לא נשלחים הגדרות, סיסמה או רמז, ולא מתבצעת התקנה אוטומטית.", 14, Ui.Muted)));
        extras.Children.Add(Ui.BrandFooter(w, true, copy.Appearance.AnimationsEnabled));
        var pages = new UIElement[] { Ui.Scroll(p), Ui.Scroll(ScheduleEditor.Create(w, copy)), Ui.Scroll(AppearanceEditor.Create(w, copy)), Ui.Scroll(extras) };
        var shell = new Grid { Margin = new Thickness(0, 0, 0, 12) }; shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); shell.ColumnDefinitions.Add(new ColumnDefinition());
        var navCard = new Border { Background = Ui.AccentSoft, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(8), Margin = new Thickness(0, 0, 16, 0) };
        var nav = new StackPanel(); nav.Children.Add(Ui.Text("הגדרות", 26)); nav.Children.Add(Ui.Text("כל האפשרויות במקום אחד", 12, Ui.Muted)); navCard.Child = nav; shell.Children.Add(navCard);
        var host = new ContentControl(); Grid.SetColumn(host, 1); shell.Children.Add(host);
        var labels = new[] { "תוכנות ואפשרויות", "לוח זמנים", "עיצוב המסך", "סיסמה ועדכונים" }; var navButtons = new List<Button>();
        void SelectPage(int index)
        {
            host.Content = pages[index];
            for (int i = 0; i < navButtons.Count; i++) { navButtons[i].Background = i == index ? Ui.Surface : Brushes.Transparent; navButtons[i].BorderBrush = i == index ? Ui.Line : Brushes.Transparent; navButtons[i].Foreground = i == index ? Ui.Accent : Ui.Ink; }
        }
        for (int i = 0; i < labels.Length; i++) { int index = i; var button = Ui.Button(labels[i], () => SelectPage(index)); button.HorizontalContentAlignment = HorizontalAlignment.Right; button.HorizontalAlignment = HorizontalAlignment.Stretch; navButtons.Add(button); nav.Children.Add(button); }
        SelectPage(0); outer.Children.Add(shell); w.Content = outer; w.ShowDialog();
    }
    private static void Edit(Window owner, AllowedApp? existing, Settings settings, Action refresh)
    {
        var w = Ui.Dialog(owner, existing == null ? "הוספת תוכנה" : "עריכת תוכנה", 680, 700);
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
        var timing = new Settings { ScheduleEnabled = existing?.TimeRestricted ?? false, Schedules = existing == null ? new() : System.Text.Json.JsonSerializer.Deserialize<List<WeeklySchedule>>(System.Text.Json.JsonSerializer.Serialize(existing.Availability))! };
        p.Children.Add(ScheduleEditor.Create(w, timing, true));
        p.Children.Add(Ui.Button("אישור", () =>
        {
            try { SchedulePolicy.ValidateWindows(timing.Schedules); if (timing.ScheduleEnabled && !timing.Schedules.Any(s => s.Enabled)) throw new IOException("יש להוסיף לפחות טווח זמינות פעיל."); }
            catch (Exception ex) { Ui.Message(w, "טווחי הזמינות אינם תקינים", ex.Message); return; }
            if (string.IsNullOrWhiteSpace(name.Text) || !File.Exists(path.Text) || !string.Equals(Path.GetExtension(path.Text), ".exe", StringComparison.OrdinalIgnoreCase)) { Ui.Message(w, "פרטים חסרים", "יש להזין שם ולבחור קובץ EXE קיים."); return; }
            if (settings.Apps.Any(a => a != existing && Policy.SamePath(a.Path, path.Text))) { Ui.Message(w, "התוכנה כבר קיימת", "הקובץ הזה כבר נמצא ברשימה."); return; }
            var app = existing ?? new AllowedApp(); app.Name = name.Text.Trim(); app.Path = Path.GetFullPath(path.Text); app.Extensions = extensions.Text.Trim();
            app.TimeRestricted = timing.ScheduleEnabled; app.Availability = timing.Schedules;
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
                var app = settings.Apps.FirstOrDefault(a => Policy.Accepts(a, item.Path) && File.Exists(a.Path) && FeaturePolicy.Available(a, DateTime.Now));
                if (app == null) throw new IOException("אין תוכנה מאושרת וזמינה בשעה זו לסוג הקובץ הזה. יש לבדוק את הסיומות ושעות הזמינות בהגדרות.");
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
