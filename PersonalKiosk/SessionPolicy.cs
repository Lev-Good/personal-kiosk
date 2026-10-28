using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersonalKiosk;

public sealed record StudyWindow(DateTime Start, DateTime? End, string Name);

public static class SessionPolicy
{
    // Weekly windows repeat. At least a full week on either side of now ensures
    // that midnight, touching windows and weekend/week-boundary chains merge.
    // A chain spanning a full week has no finite opening time under this schedule.
    public static StudyWindow? CurrentWindow(Settings settings, DateTime localNow)
    {
        if (!SchedulePolicy.HasSchedule(settings)) return null;
        var intervals = new List<(DateTime Start, DateTime End, string Name)>();
        foreach (var row in settings.Schedules.Where(s => s.Enabled))
        {
            if (!TimeOnly.TryParseExact(row.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) || !TimeOnly.TryParseExact(row.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || start == end) continue;
            for (int offset = -8; offset <= 8; offset++)
            {
                var date = DateOnly.FromDateTime(localNow).AddDays(offset);
                if (!row.Days.Contains((int)date.DayOfWeek)) continue;
                intervals.Add((date.ToDateTime(start), date.AddDays(end < start ? 1 : 0).ToDateTime(end), row.Name));
            }
        }
        var sorted = intervals.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
        for (int i = 0; i < sorted.Count;)
        {
            var first = sorted[i++]; var end = first.End; var names = new HashSet<string> { first.Name };
            while (i < sorted.Count && sorted[i].Start <= end) { end = end > sorted[i].End ? end : sorted[i].End; names.Add(sorted[i++].Name); }
            if (localNow >= first.Start && localNow < end) return new StudyWindow(first.Start, end - first.Start >= TimeSpan.FromDays(7) ? null : end, names.Count == 1 ? first.Name : "רצף סדרים");
        }
        return null;
    }
    public static bool ShouldUnlock(Settings settings, DateTime localNow) => settings.KioskActive && settings.AutoUnlockAtScheduleEnd && settings.SessionEndLocal.HasValue && localNow >= settings.SessionEndLocal.Value;
    public static void Begin(Settings settings, DateTime now, bool resume)
    {
        if (!resume || !settings.KioskActive || !settings.SessionEndLocal.HasValue) settings.SessionEndLocal = CurrentWindow(settings, now)?.End;
        settings.KioskActive = true; SchedulePolicy.MarkCurrentHandled(settings, now);
    }
    public static void End(Settings settings, DateTime now, bool automatic)
    {
        settings.KioskActive = false; settings.SessionEndLocal = null;
        if (!automatic) SchedulePolicy.MarkCurrentHandled(settings, now);
    }
    public static string Countdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        return ((int)remaining.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + remaining.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + remaining.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }
}
