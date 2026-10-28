using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PersonalKiosk;

internal static class AppearanceEditor
{
    internal static UIElement Create(Window owner, Settings settings)
    {
        var panel = new StackPanel(); panel.Children.Add(Ui.Text("מסך הקיוסק", 25));
        panel.Children.Add(Ui.Text("שעון ותאריך חיים, רקע לבחירה ותנועה עדינה. השינויים נכנסים לתוקף לאחר שמירת ההגדרות.", 14, Ui.Muted));
        panel.Children.Add(Ui.Text("סגנון הרקע"));
        var themes = new ComboBox { FontSize = 16, Margin = new Thickness(6), ItemsSource = new[] { "אור רך · טורקיז", "לילה · כחול כהה", "חול · צבעים חמים", "תמונה אישית" }, SelectedIndex = Array.IndexOf(AppearancePolicy.Themes, settings.Appearance.Theme) };
        themes.SelectionChanged += (_, _) => { if (themes.SelectedIndex >= 0) settings.Appearance.Theme = AppearancePolicy.Themes[themes.SelectedIndex]; }; panel.Children.Add(themes);
        var path = Ui.Text(string.IsNullOrWhiteSpace(settings.Appearance.BackgroundImagePath) ? "לא נבחרה תמונה אישית." : "תמונה אישית שמורה: " + Path.GetFileName(settings.Appearance.BackgroundImagePath), 13, Ui.Muted); panel.Children.Add(path);
        panel.Children.Add(Ui.Button("בחירת תמונת רקע", () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "תמונות רקע|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
            if (picker.ShowDialog(owner) != true) return;
            try { settings.Appearance.BackgroundImagePath = AppearancePolicy.ImportWallpaper(picker.FileName); settings.Appearance.Theme = "photo"; themes.SelectedIndex = 3; path.Text = "התמונה נשמרה במחשב ותוצג לאחר שמירת ההגדרות."; }
            catch (Exception ex) { Ui.Message(owner, "לא ניתן לבחור תמונה", ex.Message); }
        }));
        panel.Children.Add(Ui.Text("נשמר עותק מקומי של התמונה, כך שהרקע יישאר גם אם קובץ המקור יועבר. התמונות אינן נשלחות לאינטרנט. אם הרקע חסר, יוצג רקע מובנה.", 13, Ui.Muted));
        var animations = new CheckBox { Content = "אנימציות עדינות ולוגו מונפש", IsChecked = settings.Appearance.AnimationsEnabled, Margin = new Thickness(8), FontSize = 15 };
        animations.Checked += (_, _) => settings.Appearance.AnimationsEnabled = true; animations.Unchecked += (_, _) => settings.Appearance.AnimationsEnabled = false; panel.Children.Add(animations);
        var countdown = new CheckBox { Content = "הצגת ספירה לאחור לסיום הסדר או לתחילת הסדר הבא", IsChecked = settings.Appearance.ShowCountdown, Margin = new Thickness(8), FontSize = 15 };
        countdown.Checked += (_, _) => settings.Appearance.ShowCountdown = true; countdown.Unchecked += (_, _) => settings.Appearance.ShowCountdown = false; panel.Children.Add(countdown);
        panel.Children.Add(Ui.Text("האנימציות מכבדות גם את הגדרת צמצום התנועה של Windows. ביטול האנימציות מציג את הלוגו כתמונה קבועה.", 13, Ui.Muted));
        panel.Children.Add(Ui.Text("פתיחת המחשב בסוף הסדר", 22));
        var unlock = new CheckBox { Content = "סיום החסימה אוטומטית בסוף רצף הסדרים", IsChecked = settings.AutoUnlockAtScheduleEnd, Margin = new Thickness(8), FontSize = 15 };
        unlock.Checked += (_, _) => settings.AutoUnlockAtScheduleEnd = true; unlock.Unchecked += (_, _) => settings.AutoUnlockAtScheduleEnd = false; panel.Children.Add(unlock);
        panel.Children.Add(Ui.Text("מופעל כברירת מחדל. בסוף הסדר החסימה מוסרת והמסך נסגר; התוכנה נשארת ברקע לסדר הבא. סדרים חופפים או צמודים נחשבים לרצף אחד. בכניסה ידנית מחוץ לסדר אין שעת פתיחה אוטומטית. אפשרות זו זמינה רק בהגדרות, כשהקיוסק כבוי.", 14, Ui.Muted));
        panel.Children.Add(Ui.BrandFooter(owner, true, settings.Appearance.AnimationsEnabled)); return panel;
    }
}
