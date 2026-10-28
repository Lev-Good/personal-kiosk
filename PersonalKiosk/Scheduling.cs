using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PersonalKiosk;

public sealed class WeeklySchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "סדר חדש";
    public bool Enabled { get; set; } = true;
    public string Start { get; set; } = "09:00";
    public string End { get; set; } = "13:00";
    public List<int> Days { get; set; } = new() { 0, 1, 2, 3, 4 };
    public string LastHandledDate { get; set; } = "";
    public string LastAdvanceNoticeDate { get; set; } = "";
    public string LastMinuteNoticeDate { get; set; } = "";
}

public readonly record struct ScheduleOccurrence(WeeklySchedule Schedule, string StartDate);

public static class SchedulePolicy
{
    public static bool HasSchedule(Settings settings) => settings.ScheduleEnabled && settings.Schedules.Any(s => s.Enabled);
    public static void Validate(Settings settings)
    {
        if (settings.Schedules == null || settings.Schedules.Any(s => s == null)) throw new IOException("רשימת הזמנים אינה תקינה.");
        ValidateWindows(settings.Schedules);
        if (HasSchedule(settings) && string.IsNullOrEmpty(settings.PasswordHash)) throw new IOException("יש להגדיר סיסמה לפני הפעלת לוח הזמנים.");
        if (settings.ScheduleEnabled && !settings.Schedules.Any(s => s.Enabled)) throw new IOException("יש להוסיף לפחות סדר פעיל או לכבות את לוח הזמנים.");
    }
    public static void ValidateWindows(IEnumerable<WeeklySchedule> windows)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schedule in windows)
        {
            if (string.IsNullOrWhiteSpace(schedule.Id) || !ids.Add(schedule.Id) || string.IsNullOrWhiteSpace(schedule.Name)) throw new IOException("לכל סדר נדרשים שם ומזהה נפרדים.");
            if (!TimeOnly.TryParseExact(schedule.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(schedule.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || start == end) throw new IOException("יש להזין שעות שונות בפורמט 24 שעות, למשל 09:00 עד 13:00.");
            if (schedule.Days == null || schedule.Days.Count == 0 || schedule.Days.Any(d => d < 0 || d > 6)) throw new IOException("יש לבחור לפחות יום אחד לכל סדר.");
            if (!string.IsNullOrEmpty(schedule.LastHandledDate) && !DateOnly.TryParseExact(schedule.LastHandledDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new IOException("נתוני ההפעלה האחרונה אינם תקינים.");
        }
    }
    // Pure wall-clock calculation: the caller supplies local time. An overnight entry
    // belongs to its START day, and the end is exclusive. SessionPolicy separately
    // handles the owner's optional automatic opening at the end of the merged session.
    public static ScheduleOccurrence? Current(WeeklySchedule schedule, DateTime localNow)
    {
        if (!schedule.Enabled || !TimeOnly.TryParseExact(schedule.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !TimeOnly.TryParseExact(schedule.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || start == end || schedule.Days == null) return null;
        var time = TimeOnly.FromDateTime(localNow);
        var date = DateOnly.FromDateTime(localNow);
        if (start < end) { if (time < start || time >= end) return null; }
        else if (time < end) date = date.AddDays(-1);
        else if (time < start) return null;
        if (!schedule.Days.Contains((int)date.DayOfWeek)) return null;
        return new ScheduleOccurrence(schedule, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
    public static List<ScheduleOccurrence> Due(Settings settings, DateTime localNow) => !HasSchedule(settings) ? new() : settings.Schedules.Select(s => Current(s, localNow)).Where(o => o.HasValue && o.Value.Schedule.LastHandledDate != o.Value.StartDate).Select(o => o!.Value).ToList();
    public static void MarkCurrentHandled(Settings settings, DateTime localNow)
    {
        foreach (var occurrence in Due(settings, localNow)) occurrence.Schedule.LastHandledDate = occurrence.StartDate;
    }
    public static (WeeklySchedule Schedule, DateTime Time)? Next(Settings settings, DateTime localNow)
    {
        if (!HasSchedule(settings)) return null;
        (WeeklySchedule Schedule, DateTime Time)? next = null;
        foreach (var schedule in settings.Schedules.Where(s => s.Enabled))
        {
            var current = Current(schedule, localNow);
            if (current.HasValue && schedule.LastHandledDate != current.Value.StartDate) return (schedule, localNow);
            if (!TimeOnly.TryParseExact(schedule.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) continue;
            for (int day = 0; day <= 7; day++)
            {
                var date = DateOnly.FromDateTime(localNow).AddDays(day);
                var time = date.ToDateTime(start);
                if (!schedule.Days.Contains((int)date.DayOfWeek) || time <= localNow || schedule.LastHandledDate == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) continue;
                if (next == null || time < next.Value.Time) next = (schedule, time);
                break;
            }
        }
        return next;
    }
}
