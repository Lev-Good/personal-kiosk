using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PersonalKiosk;

internal static class AppearanceEditor
{
    internal static UIElement Create(Window owner, Settings settings)
    {
        var panel = new StackPanel(); panel.Children.Add(Ui.Text("עיצוב מסך הקיוסק", 25));
        panel.Children.Add(Ui.Text("שעון ותאריך חיים, רקע לבחירה ותנועה עדינה. השינויים נכנסים לתוקף לאחר שמירת ההגדרות.", 14, Ui.Muted));
        var themes = new ComboBox { FontSize = 16, Width = 250, ItemsSource = new[] { "אור רך · טורקיז", "לילה · כחול כהה", "חול · צבעים חמים", "נוף · אגם טרופי", "נוף · מדבר", "נוף · אגם והר מושלג", "נוף · מפל ביער", "נוף · הרים מושלגים", "נוף · זוהר צפוני", "נוף · חוף בשקיעה", "נוף · מטע תה", "נוף · אגמי הרים", "נוף · אגם מוריין", "תמונה אישית" }, SelectedIndex = Array.IndexOf(AppearancePolicy.Themes, settings.Appearance.Theme) };
        themes.SelectionChanged += (_, _) => { if (themes.SelectedIndex >= 0) settings.Appearance.Theme = AppearancePolicy.Themes[themes.SelectedIndex]; }; panel.Children.Add(Ui.SettingRow("סגנון הרקע", "בחירת פלטת הצבעים הראשית של מסך הקיוסק.", themes));
        var landscapes = new WrapPanel();
        foreach (var choice in new[]
        {
            (Theme: "lake", Title: "אגם טרופי", Index: 3), (Theme: "desert", Title: "מדבר", Index: 4),
            (Theme: "mountainlake", Title: "אגם והר", Index: 5), (Theme: "forest", Title: "מפל ביער", Index: 6),
            (Theme: "snow", Title: "הרים מושלגים", Index: 7), (Theme: "northern", Title: "זוהר צפוני", Index: 8),
            (Theme: "ocean", Title: "חוף בשקיעה", Index: 9), (Theme: "tea", Title: "מטע תה", Index: 10),
            (Theme: "mirrorlake", Title: "אגמי הרים", Index: 11), (Theme: "moraine", Title: "אגם מוריין", Index: 12)
        })
        {
            var preview = new StackPanel(); var source = AppearancePolicy.LoadBuiltInWallpaper(choice.Theme);
            preview.Children.Add(new Border { Width = 132, Height = 68, CornerRadius = new CornerRadius(7), ClipToBounds = true, Child = new Image { Source = source, Stretch = System.Windows.Media.Stretch.UniformToFill } });
            var label = Ui.Text(choice.Title, 13); label.TextAlignment = TextAlignment.Center; preview.Children.Add(label);
            var button = Ui.Button("", () => themes.SelectedIndex = choice.Index); button.Content = preview; button.Padding = new Thickness(5); button.Margin = new Thickness(5); landscapes.Children.Add(button);
        }
        panel.Children.Add(Ui.Card(landscapes));
        var path = Ui.Text(string.IsNullOrWhiteSpace(settings.Appearance.BackgroundImagePath) ? "לא נבחרה תמונה אישית." : "תמונה שמורה: " + Path.GetFileName(settings.Appearance.BackgroundImagePath), 12, Ui.Muted);
        var choose = Ui.Button("בחירת תמונה", () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "תמונות רקע|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
            if (picker.ShowDialog(owner) != true) return;
            try { settings.Appearance.BackgroundImagePath = AppearancePolicy.ImportWallpaper(picker.FileName); settings.Appearance.Theme = "photo"; themes.SelectedIndex = 13; path.Text = "התמונה נשמרה במחשב ותוצג לאחר שמירת ההגדרות."; }
            catch (Exception ex) { Ui.Message(owner, "לא ניתן לבחור תמונה", ex.Message); }
        });
        var imageAction = new StackPanel { Orientation = Orientation.Horizontal }; imageAction.Children.Add(choose); imageAction.Children.Add(path);
        panel.Children.Add(Ui.SettingRow("תמונת רקע אישית", "נשמר עותק מקומי בלבד; התמונה אינה נשלחת לאינטרנט.", imageAction));
        panel.Children.Add(Ui.Card(Ui.Text("עשר תמונות הנוף המובנות אינן כוללות אנשים, נשמרות בתוך התוכנה וזמינות גם ללא אינטרנט. פרטי המקור והרישיון הציבורי מצורפים לקובץ LandscapeLicenses.txt.", 13, Ui.Muted)));
        panel.Children.Add(Ui.SettingRow("אנימציות עדינות", "תנועה רגועה ולוגו מונפש; מכבד את הגדרת צמצום התנועה של Windows.", Ui.Toggle(settings.Appearance.AnimationsEnabled, value => settings.Appearance.AnimationsEnabled = value)));
        panel.Children.Add(Ui.SettingRow("ספירה לאחור", "הצגת הזמן שנותר לסיום הסדר או לתחילת הסדר הבא.", Ui.Toggle(settings.Appearance.ShowCountdown, value => settings.Appearance.ShowCountdown = value)));
        panel.Children.Add(Ui.Text("פתיחת המחשב בסוף הסדר", 22));
        panel.Children.Add(Ui.SettingRow("סיום חסימה אוטומטי", "בסוף רצף סדרים החסימה מוסרת והיישום ממתין ברקע לסדר הבא.", Ui.Toggle(settings.AutoUnlockAtScheduleEnd, value => settings.AutoUnlockAtScheduleEnd = value)));
        panel.Children.Add(Ui.Card(Ui.Text("מופעל כברירת מחדל. סדרים חופפים או צמודים נחשבים לרצף אחד. בכניסה ידנית מחוץ לסדר אין שעת פתיחה אוטומטית. אפשרות זו זמינה רק בהגדרות, כשהקיוסק כבוי.", 14, Ui.Muted)));
        panel.Children.Add(Ui.BrandFooter(owner, true, settings.Appearance.AnimationsEnabled)); return panel;
    }
}
