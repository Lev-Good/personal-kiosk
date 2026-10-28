using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;

namespace PersonalKiosk;

internal sealed class LogoImage : Image
{
    private static readonly Lazy<List<(BitmapSource Image, int Delay)>> Frames = new(LoadFrames);
    private readonly DispatcherTimer timer = new();
    private int frame;
    internal static int FrameCount => Frames.Value.Count;
    internal LogoImage(double width, bool animate)
    {
        Width = width; Height = width * 240.0 / 426; Stretch = Stretch.Uniform; FlowDirection = FlowDirection.LeftToRight; Source = Frames.Value[0].Image;
        System.Windows.Automation.AutomationProperties.SetName(this, "לוגו לב טוב דיגיטל");
        timer.Tick += (_, _) => { if (!SystemParameters.ClientAreaAnimation) { timer.Stop(); Source = Frames.Value[0].Image; return; } frame = (frame + 1) % Frames.Value.Count; Source = Frames.Value[frame].Image; timer.Interval = TimeSpan.FromMilliseconds(Frames.Value[frame].Delay); };
        void Update() { timer.Stop(); if (animate && SystemParameters.ClientAreaAnimation && IsLoaded && IsVisible && Frames.Value.Count > 1) { timer.Interval = TimeSpan.FromMilliseconds(Frames.Value[frame].Delay); timer.Start(); } }
        Loaded += (_, _) => Update(); IsVisibleChanged += (_, _) => Update(); Unloaded += (_, _) => timer.Stop();
    }
    private static List<(BitmapSource Image, int Delay)> LoadFrames()
    {
        using var stream = typeof(LogoImage).Assembly.GetManifestResourceStream("PersonalKiosk.Assets.LevtovLogo.gif") ?? throw new IOException("קובץ הלוגו חסר.");
        using var gif = Drawing.Image.FromStream(stream);
        var frames = new List<(BitmapSource, int)>(); int count = gif.GetFrameCount(Drawing.Imaging.FrameDimension.Time);
        var delays = gif.GetPropertyItem(0x5100)?.Value ?? Array.Empty<byte>();
        for (int i = 0; i < count; i++)
        {
            gif.SelectActiveFrame(Drawing.Imaging.FrameDimension.Time, i);
            using var bitmap = new Drawing.Bitmap(160, 90, Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Drawing.Graphics.FromImage(bitmap)) { graphics.Clear(Drawing.Color.Transparent); graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; graphics.DrawImage(gif, 0, 0, 160, 90); }
            using var png = new MemoryStream(); bitmap.Save(png, Drawing.Imaging.ImageFormat.Png); png.Position = 0;
            var image = BitmapFrame.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad); image.Freeze();
            int delay = delays.Length >= (i + 1) * 4 ? BitConverter.ToInt32(delays, i * 4) * 10 : 100;
            frames.Add((image, Math.Clamp(delay, 30, 5000)));
        }
        return frames;
    }
}
