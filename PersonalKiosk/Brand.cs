using System;
using System.Diagnostics;

namespace PersonalKiosk;

public static class Brand
{
    public const string Owner = "לב טוב דיגיטל";
    public const string HomeUrl = "https://digital.levtov.uk/index.html";
    public const string Credit = "התוכנה נוצרה על ידי לב טוב דיגיטל";
    public const string Rights = "© 2026 לב טוב דיגיטל. כל הזכויות שמורות.";
    public static void OpenLink(string url)
    {
        if (url != HomeUrl && !UpdatePolicy.TrustedReleaseUrl(url)) throw new InvalidOperationException("הקישור אינו מאושר.");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
