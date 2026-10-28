using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PersonalKiosk;

internal sealed class NoticeWindow : Window
{
    internal NoticeWindow(IReadOnlyList<ScheduleNotice> notices)
    {
        Title = "התראה לפני תחילת סדר"; Width = 430; Height = 285; ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        FlowDirection = FlowDirection.RightToLeft; FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); Background = Ui.Background;
        var p = new StackPanel { Margin = new Thickness(22) };
        p.Children.Add(Ui.Text(notices.All(n => n.FinalMinute) ? "הסדר מתחיל בעוד פחות מדקה" : "הסדר מתחיל בקרוב", 23));
        p.Children.Add(Ui.Text(string.Join(" · ", notices.Select(n => n.Schedule.Name + " " + n.Start.ToString("HH:mm"))) + "\nהקיוסק יופעל אוטומטית. זה הזמן לשמור את העבודה הפתוחה.", 15));
        p.Children.Add(Ui.Button("הבנתי", Close, true)); Content = new ScrollViewer { Content = p };
        Left = SystemParameters.WorkArea.Right - Width - 20; Top = SystemParameters.WorkArea.Bottom - Height - 20;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; var expires = DateTime.UtcNow.AddSeconds(30);
        timer.Tick += (_, _) => { if (DateTime.UtcNow >= expires || DateTime.Now >= notices.Min(n => n.Start)) Close(); };
        Closed += (_, _) => timer.Stop(); timer.Start();
    }
}
