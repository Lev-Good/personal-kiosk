using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PersonalKiosk;

internal sealed class FloatingIndicator : Window
{
    private readonly Action<FloatingIndicator> moved;

    internal FloatingIndicator(Action<FloatingIndicator> moved)
    {
        this.moved = moved;
        Title = "קיוסק אישי — סמל פעיל";
        Width = 38;
        Height = 38;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        FlowDirection = FlowDirection.RightToLeft;
        Opacity = 0.56;
        SourceInitialized += (_, _) =>
        {
            var handle = Handle;
            long style = Native.GetWindowLongPtr(handle, -20).ToInt64();
            Native.SetWindowLongPtr(handle, -20, new IntPtr(style | 0x08000000L | 0x00000080L));
        };

        var icon = LoadIcon();
        Content = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromArgb(218, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 15, 118, 110)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 7, ShadowDepth = 1, Opacity = 0.16 },
            Child = new Image { Source = icon, Stretch = Stretch.Uniform, IsHitTestVisible = false },
            ToolTip = "קיוסק אישי פעיל · ניתן לגרור את הסמל"
        };

        MouseEnter += (_, _) => Opacity = 0.92;
        MouseLeave += (_, _) => Opacity = 0.56;
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            finally { moved(this); }
        };
    }

    internal IntPtr Handle => new WindowInteropHelper(this).Handle;

    private static ImageSource LoadIcon()
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (icon == null) throw new IOException();
            var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            return source;
        }
        catch
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawEllipse(Ui.Accent, null, new Point(12, 12), 11, 11);
                var text = new FormattedText("ק", System.Globalization.CultureInfo.GetCultureInfo("he-IL"), FlowDirection.RightToLeft, new Typeface("Segoe UI"), 14, Brushes.White, 1);
                context.DrawText(text, new Point(5, 3));
            }
            var bitmap = new RenderTargetBitmap(24, 24, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
    }
}
