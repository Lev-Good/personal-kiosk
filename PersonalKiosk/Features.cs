using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PersonalKiosk;

public static class FeaturePolicy
{
    public static bool Available(AllowedApp app, DateTime localNow) => !app.TimeRestricted || app.Availability.Any(window => SchedulePolicy.Current(window, localNow).HasValue);
    public static void Validate(Settings settings)
    {
        if (settings.PasswordHint == null || settings.PasswordHint.Length > 200) throw new IOException("רמז הסיסמה יכול להכיל עד 200 תווים.");
        if (settings.ScheduleWarningMinutes < 1 || settings.ScheduleWarningMinutes > 60) throw new IOException("ההתראה המוקדמת חייבת להיות בין דקה ל־60 דקות.");
        foreach (var app in settings.Apps)
        {
            if (app.Availability == null || app.Availability.Any(w => w == null)) throw new IOException("טווחי הזמינות של התוכנה אינם תקינים.");
            SchedulePolicy.ValidateWindows(app.Availability);
            if (app.TimeRestricted && !app.Availability.Any(w => w.Enabled)) throw new IOException("יש להוסיף לפחות טווח זמינות פעיל לתוכנה „" + app.Name + "”.");
        }
    }
    public static void ValidateHint(Settings settings, string hint)
    {
        if (hint.Length > 200) throw new IOException("הרמז יכול להכיל עד 200 תווים.");
        if (!string.IsNullOrWhiteSpace(hint) && Passwords.Verify(settings, hint)) throw new IOException("הרמז אינו יכול להיות הסיסמה עצמה.");
    }
}

public readonly record struct ScheduleNotice(WeeklySchedule Schedule, DateTime Start, bool FinalMinute)
{
    public string Date => Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public void MarkShown()
    {
        Schedule.LastAdvanceNoticeDate = Date;
        if (FinalMinute) Schedule.LastMinuteNoticeDate = Date;
    }
}

public static class NoticePolicy
{
    public static List<ScheduleNotice> Pending(Settings settings, DateTime now)
    {
        var result = new List<ScheduleNotice>();
        if (!settings.ScheduleWarningsEnabled || !SchedulePolicy.HasSchedule(settings)) return result;
        foreach (var schedule in settings.Schedules.Where(s => s.Enabled))
        {
            if (!TimeOnly.TryParseExact(schedule.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) continue;
            // The maximum notice is one hour, so only today and tomorrow can be relevant.
            foreach (int offset in new[] { 0, 1 })
            {
                var date = DateOnly.FromDateTime(now).AddDays(offset);
                if (!schedule.Days.Contains((int)date.DayOfWeek)) continue;
                var at = date.ToDateTime(start); var remaining = at - now;
                string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (schedule.LastHandledDate == key || remaining <= TimeSpan.Zero || remaining > TimeSpan.FromMinutes(settings.ScheduleWarningMinutes)) continue;
                bool final = remaining <= TimeSpan.FromMinutes(1);
                if (final ? schedule.LastMinuteNoticeDate == key : schedule.LastAdvanceNoticeDate == key) continue;
                result.Add(new ScheduleNotice(schedule, at, final));
            }
        }
        return result.OrderBy(n => n.Start).ToList();
    }
}
