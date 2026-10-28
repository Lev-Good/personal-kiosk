using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace PersonalKiosk;

public sealed class KioskVisualSettings
{
    public string Theme { get; set; } = "aurora";
    public string BackgroundImagePath { get; set; } = "";
    public bool AnimationsEnabled { get; set; } = true;
    public bool ShowCountdown { get; set; } = true;
}

public static class AppearancePolicy
{
    public static readonly string[] Themes = { "aurora", "midnight", "sand", "photo" };
    public static bool Motion(KioskVisualSettings settings) => settings.AnimationsEnabled && SystemParameters.ClientAreaAnimation;
    public static void Validate(KioskVisualSettings? settings)
    {
        if (settings == null || Array.IndexOf(Themes, settings.Theme) < 0 || settings.BackgroundImagePath == null || settings.BackgroundImagePath.Length > 2048) throw new IOException("הגדרות העיצוב אינן תקינות.");
        if (settings.Theme == "photo" && string.IsNullOrWhiteSpace(settings.BackgroundImagePath)) throw new IOException("יש לבחור תמונה לרקע האישי.");
    }
    public static BitmapSource? LoadWallpaper(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 20 * 1024 * 1024) return null;
            int width;
            using (var header = File.OpenRead(path))
            {
                var decoder = BitmapDecoder.Create(header, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                width = decoder.Frames[0].PixelWidth; int height = decoder.Frames[0].PixelHeight;
                if (width < 1 || height < 1 || width > 16000 || height > 16000) return null;
            }
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = Math.Min(width, 2048); bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException or System.IO.FileFormatException) { return null; }
    }
    public static string ImportWallpaper(string source, string? destinationDirectory = null)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (Array.IndexOf(new[] { ".png", ".jpg", ".jpeg", ".bmp" }, extension) < 0 || LoadWallpaper(source) == null) throw new IOException("יש לבחור תמונת PNG, JPG או BMP תקינה, בגודל של עד 20 MB.");
        var directory = destinationDirectory ?? Path.Combine(Store.DirectoryPath, "Wallpapers"); Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension); File.Copy(source, target); return target;
    }
}

internal sealed class KioskPalette
{
    internal Brush Ink { get; }
    internal Brush Muted { get; }
    internal Brush Accent { get; }
    internal Brush Surface { get; }
    internal Brush Line { get; }
    internal bool Dark { get; }
    internal KioskPalette(KioskVisualSettings settings)
    {
        Dark = settings.Theme == "midnight";
        Ink = Brush(Dark ? "#F0F5FF" : "#172D3B"); Muted = Brush(Dark ? "#A0B2CC" : "#657C87"); Accent = Brush(Dark ? "#7EE2CD" : "#087F78");
        Surface = Brush(Dark ? "#EE18283E" : "#F5FFFFFF"); Line = Brush(Dark ? "#34475D" : "#DBE7EA");
    }
    internal static SolidColorBrush Brush(string value) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); b.Freeze(); return b; }
}

internal static class DashboardUi
{
    internal static Grid Background(KioskVisualSettings settings)
    {
        var grid = new Grid { ClipToBounds = true };
        string first = settings.Theme == "midnight" ? "#0B1426" : settings.Theme == "sand" ? "#FAF5E9" : "#ECF7F6";
        string last = settings.Theme == "midnight" ? "#1A3147" : settings.Theme == "sand" ? "#EEEDE2" : "#E9EEF9";
        grid.Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(first), (Color)ColorConverter.ConvertFromString(last), new Point(1, 0), new Point(0, 1));
        var wallpaper = settings.Theme == "photo" ? AppearancePolicy.LoadWallpaper(settings.BackgroundImagePath) : null;
        if (wallpaper != null)
        {
            grid.Children.Add(new Image { Source = wallpaper, Stretch = Stretch.UniformToFill, IsHitTestVisible = false });
            grid.Children.Add(new Border { Background = KioskPalette.Brush("#B8EAF1F5"), IsHitTestVisible = false });
        }
        else
        {
            var blob = new System.Windows.Shapes.Ellipse { Width = 480, Height = 480, Fill = new RadialGradientBrush(Color.FromArgb(42, 39, 190, 168), Colors.Transparent), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -220, -160, 0), IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
            grid.Children.Add(blob);
            if (AppearancePolicy.Motion(settings)) blob.Loaded += (_, _) => ((TranslateTransform)blob.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-12, 12, TimeSpan.FromSeconds(14)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            blob.Unloaded += (_, _) => ((TranslateTransform)blob.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
        }
        return grid;
    }
    internal static TextBlock Text(string value, double size, Brush brush) => new() { Text = value, FontSize = size, Foreground = brush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
    internal static Border Surface(UIElement child, KioskPalette palette, Thickness? padding = null) => new() { Background = palette.Surface, BorderBrush = palette.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22), Padding = padding ?? new Thickness(24), Child = child };
    internal static Button Tile(UIElement child, Action action, KioskPalette palette, bool animate, int index)
    {
        var button = new Button { Content = child, Width = 204, Height = 152, Padding = new Thickness(21), Margin = new Thickness(5), Background = palette.Surface, Foreground = palette.Ink, BorderBrush = palette.Line, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, RenderTransform = new TranslateTransform(), Cursor = System.Windows.Input.Cursors.Hand };
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(19));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty)); border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty)); border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty)); border.AppendChild(presenter);
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        button.Click += (_, _) => action();
        void Hover(bool over)
        {
            button.BorderBrush = over ? palette.Accent : palette.Line;
            if (animate) ((TranslateTransform)button.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(over ? -3 : 0, TimeSpan.FromMilliseconds(170)) { EasingFunction = new QuadraticEase() });
        }
        button.MouseEnter += (_, _) => { if (button.IsEnabled) Hover(true); }; button.MouseLeave += (_, _) => Hover(false);
        button.IsEnabledChanged += (_, _) => button.Opacity = button.IsEnabled ? 1 : .48;
        if (animate) button.Loaded += (_, _) => button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, button.IsEnabled ? 1 : .48, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(index * 35), FillBehavior = FillBehavior.Stop });
        return button;
    }
}
