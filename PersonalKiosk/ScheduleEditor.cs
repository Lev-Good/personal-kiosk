using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalKiosk;

internal static class ScheduleEditor
{
    internal static UIElement Create(Window owner, Settings settings, bool availability = false)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(availability ? "זמינות התוכנה לפי שעות" : "הפעלה לפי שעות הסדרים", 24));
        var enabled = new CheckBox { Content = availability ? "הגבלת התוכנה לימים ולשעות שלהלן" : "הפעלת קיוסק אוטומטית לפי לוח זמנים שבועי", IsChecked = settings.ScheduleEnabled, FontSize = 15, Margin = new Thickness(8) };
        enabled.Checked += (_, _) => settings.ScheduleEnabled = true; enabled.Unchecked += (_, _) => settings.ScheduleEnabled = false; panel.Children.Add(enabled);
        panel.Children.Add(Ui.Text(availability ? "במצב קיוסק התוכנה זמינה רק בתוך טווח פעיל. מחוץ לשעות אלה החלונות שלה ימוזערו בלי לסגור מסמכים. כשההגבלה כבויה, התוכנה זמינה תמיד." : "לכל סדר בוחרים ימים וטווח שעות. הקיוסק מופעל בתחילת הטווח, או כשחוזרים למחשב בתוך הטווח. סוף הטווח אינו מוציא מהקיוסק — היציאה תמיד בסיסמה.", 14, Ui.Muted));
        if (!availability)
        {
            panel.Children.Add(Ui.Text("התזמון פועל לפי שעון המחשב כשהחשבון מחובר. התוכנה נשארת ברקע ועולה בכניסה ל־Windows כל עוד התזמון מופעל. היא אינה מדליקה מחשב כבוי או מעירה אותו משינה.", 13, Ui.Muted));
            var warnings = new CheckBox { Content = "התראה מוקדמת והתראה נוספת בדקה האחרונה", IsChecked = settings.ScheduleWarningsEnabled, Margin = new Thickness(8), FontSize = 15 };
            warnings.Checked += (_, _) => settings.ScheduleWarningsEnabled = true; warnings.Unchecked += (_, _) => settings.ScheduleWarningsEnabled = false; panel.Children.Add(warnings);
            var leadRow = new WrapPanel(); leadRow.Children.Add(Ui.Text("התראה מוקדמת, דקות:", 15));
            var lead = new ComboBox { ItemsSource = Enumerable.Range(1, 60).ToList(), SelectedItem = settings.ScheduleWarningMinutes, FontSize = 16, Margin = new Thickness(8), Width = 100 };
            lead.SelectionChanged += (_, _) => { if (lead.SelectedItem is int minutes) settings.ScheduleWarningMinutes = minutes; }; leadRow.Children.Add(lead); panel.Children.Add(leadRow);
        }
        var list = new StackPanel(); panel.Children.Add(list);
        void Refresh()
        {
            list.Children.Clear();
            foreach (var schedule in settings.Schedules.ToList())
            {
                var p = new StackPanel(); var bar = new DockPanel();
                var remove = Ui.Button("הסרה", () => { settings.Schedules.Remove(schedule); Refresh(); }); DockPanel.SetDock(remove, Dock.Left); bar.Children.Add(remove);
                var edit = Ui.Button("עריכה", () => Edit(owner, settings, schedule, Refresh, availability)); DockPanel.SetDock(edit, Dock.Left); bar.Children.Add(edit);
                bar.Children.Add(Ui.Text(schedule.Name + (schedule.Enabled ? "" : " · כבוי"), 19)); p.Children.Add(bar);
                var details = new WrapPanel();
                details.Children.Add(Ui.Text(string.Join(" · ", schedule.Days.OrderBy(d => d).Select(DayName)) + "   |   ", 14, Ui.Muted));
                var timeText = Ui.Text(schedule.Start + "–" + schedule.End, 14, Ui.Muted); timeText.FlowDirection = FlowDirection.LeftToRight; details.Children.Add(timeText);
                if (string.CompareOrdinal(schedule.End, schedule.Start) < 0) details.Children.Add(Ui.Text("(למחרת)", 14, Ui.Muted));
                p.Children.Add(details); list.Children.Add(Ui.Card(p));
            }
        }
        var buttons = new WrapPanel();
        buttons.Children.Add(Ui.Button(availability ? "הוספת טווח זמינות" : "הוספת סדר", () => Edit(owner, settings, null, Refresh, availability)));
        buttons.Children.Add(Ui.Button("דוגמה: סדר א׳, ב׳ וג׳", () =>
        {
            foreach (var entry in new[] { ("סדר א׳", "09:00", "13:00"), ("סדר ב׳", "15:30", "19:00"), ("סדר ג׳", "20:30", "22:30") })
                if (!settings.Schedules.Any(s => s.Name == entry.Item1)) settings.Schedules.Add(new WeeklySchedule { Name = entry.Item1, Start = entry.Item2, End = entry.Item3 });
            Refresh();
        })); panel.Children.Add(buttons);
        panel.Children.Add(Ui.Text(availability ? "הטווחים פועלים בנפרד מתזמון הפעלת הקיוסק. אפשר להגדיר כמה טווחים ביום, כולל חציית חצות. ההגבלה חלה רק בזמן שהקיוסק פעיל." : "שעות הדוגמה הן הצעה לעריכה ואינן מפעילות את התזמון עד שמסמנים את האפשרות ושומרים. יציאה בסיסמה מבטלת הפעלה חוזרת של הסדר הנוכחי בלבד; הסדר הבא עדיין יופעל.", 13, Ui.Muted));
        Refresh(); return panel;
    }
    private static string DayName(int day) => new[] { "א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "שבת" }[day];
    private static void Edit(Window owner, Settings settings, WeeklySchedule? existing, Action refresh, bool availability)
    {
        var w = Ui.Dialog(owner, availability ? "טווח זמינות" : "הגדרת סדר", 600, 560); var panel = new StackPanel { Margin = new Thickness(24) };
        var source = existing ?? new WeeklySchedule();
        panel.Children.Add(Ui.Text(availability ? "שם הטווח" : "שם הסדר")); var name = new TextBox { Text = source.Name }; panel.Children.Add(name);
        var enabled = new CheckBox { Content = "סדר פעיל", IsChecked = source.Enabled, Margin = new Thickness(8) }; panel.Children.Add(enabled);
        panel.Children.Add(Ui.Text("ימי התחלת הסדר")); var days = new WrapPanel(); var checks = Enumerable.Range(0, 7).Select(day => new CheckBox { Content = DayName(day), IsChecked = source.Days.Contains(day), Margin = new Thickness(8), FontSize = 16 }).ToArray(); foreach (var check in checks) days.Children.Add(check); panel.Children.Add(days);
        panel.Children.Add(Ui.Text("שעת התחלה · 24 שעות")); var start = new TextBox { Text = source.Start, FlowDirection = FlowDirection.LeftToRight }; panel.Children.Add(start);
        panel.Children.Add(Ui.Text(availability ? "שעת סיום הזמינות" : "סוף טווח ההפעלה · אינו מוציא מהקיוסק")); var end = new TextBox { Text = source.End, FlowDirection = FlowDirection.LeftToRight }; panel.Children.Add(end);
        panel.Children.Add(Ui.Text("לדוגמה 09:00 עד 13:00. שעת סיום מוקדמת מההתחלה מגדירה סדר שחוצה חצות.", 13, Ui.Muted));
        var error = Ui.Text("", 13, System.Windows.Media.Brushes.Firebrick); panel.Children.Add(error);
        panel.Children.Add(Ui.Button("אישור", () =>
        {
            try
            {
                string Normalize(string text) => TimeOnly.TryParseExact(text.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time.ToString("HH:mm", CultureInfo.InvariantCulture) : text.Trim();
                var edited = new WeeklySchedule { Id = source.Id, Name = name.Text.Trim(), Enabled = enabled.IsChecked == true, Days = checks.Select((c, i) => (c, i)).Where(p => p.c.IsChecked == true).Select(p => p.i).ToList(), Start = Normalize(start.Text), End = Normalize(end.Text) };
                SchedulePolicy.Validate(new Settings { Schedules = new() { edited } });
                // Keep occurrence history when an unchanged row is saved; changing its
                // timing intentionally creates a new schedule that can fire immediately.
                if (existing != null && existing.Start == edited.Start && existing.End == edited.End && existing.Enabled == edited.Enabled && existing.Days.OrderBy(d => d).SequenceEqual(edited.Days.OrderBy(d => d))) { edited.LastHandledDate = existing.LastHandledDate; edited.LastAdvanceNoticeDate = existing.LastAdvanceNoticeDate; edited.LastMinuteNoticeDate = existing.LastMinuteNoticeDate; }
                if (existing != null) settings.Schedules[settings.Schedules.IndexOf(existing)] = edited;
                else settings.Schedules.Add(edited);
                refresh(); w.DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }, true)); w.Content = new ScrollViewer { Content = panel }; w.ShowDialog();
    }
}
