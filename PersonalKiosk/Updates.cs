using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalKiosk;

public sealed class ReleaseNumber : IComparable<ReleaseNumber>
{
    private readonly int major, minor, patch;
    private readonly string[] prerelease;
    public bool IsPrerelease => prerelease.Length != 0;
    private ReleaseNumber(int major, int minor, int patch, string[] prerelease) { this.major = major; this.minor = minor; this.patch = patch; this.prerelease = prerelease; }
    public static bool TryParse(string? value, out ReleaseNumber number)
    {
        number = null!;
        if (value == null || value.Length > 150) return false;
        var match = Regex.Match(value, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int a) || !int.TryParse(match.Groups[2].Value, out int b) || !int.TryParse(match.Groups[3].Value, out int c)) return false;
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : Array.Empty<string>();
        if (pre.Any(s => s.All(char.IsAsciiDigit) && s.Length > 1 && s[0] == '0')) return false;
        number = new ReleaseNumber(a, b, c, pre); return true;
    }
    public int CompareTo(ReleaseNumber? other)
    {
        if (other == null) return 1;
        foreach (int difference in new[] { major.CompareTo(other.major), minor.CompareTo(other.minor), patch.CompareTo(other.patch) }) if (difference != 0) return difference;
        if (!IsPrerelease || !other.IsPrerelease) return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            string a = prerelease[i], b = other.prerelease[i]; bool an = a.All(char.IsAsciiDigit), bn = b.All(char.IsAsciiDigit);
            int compare = an && bn ? a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b) : an != bn ? an ? -1 : 1 : string.CompareOrdinal(a, b);
            if (compare != 0) return compare;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
}

public sealed record UpdateRelease(string Tag, string Url);

public static class UpdatePolicy
{
    public const string ApiUrl = "https://api.github.com/repos/Lev-Good/personal-kiosk/releases?per_page=100";
    private const string TagPath = "/Lev-Good/personal-kiosk/releases/tag/";
    public static bool TrustedReleaseUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath.StartsWith(TagPath, StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Length > TagPath.Length;
    public static bool Newer(string? candidate, string current)
        => ReleaseNumber.TryParse(candidate, out var a) && ReleaseNumber.TryParse(current, out var b) && a.CompareTo(b) > 0;
    public static bool CheckDue(DateTimeOffset? last, DateTimeOffset now) => !last.HasValue || now - last.Value >= TimeSpan.FromDays(1) || last.Value > now.AddMinutes(5);
    public static UpdateRelease? SelectLatest(string json, bool includePrerelease)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("תגובת שרת העדכונים אינה תקינה.");
        UpdateRelease? best = null; ReleaseNumber? bestVersion = null;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False) continue;
            if (!item.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String || !ReleaseNumber.TryParse(tag.GetString(), out var version)) continue;
            bool test = version.IsPrerelease || (item.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True);
            if (test && !includePrerelease) continue;
            if (!item.TryGetProperty("html_url", out var link) || link.ValueKind != JsonValueKind.String || !TrustedReleaseUrl(link.GetString())) continue;
            if (!string.Equals(Uri.UnescapeDataString(new Uri(link.GetString()!).AbsolutePath[TagPath.Length..]), tag.GetString(), StringComparison.Ordinal)) continue;
            if (!item.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array || !assets.EnumerateArray().Any(asset => asset.ValueKind == JsonValueKind.Object && asset.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == "PersonalKioskSetup.exe" && asset.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String && state.GetString() == "uploaded")) continue;
            if (bestVersion == null || version.CompareTo(bestVersion) > 0) { bestVersion = version; best = new UpdateRelease(tag.GetString()!, link.GetString()!); }
        }
        return best;
    }
    public static async Task<UpdateRelease?> FetchAsync(bool includePrerelease, HttpMessageHandler? handler = null, CancellationToken token = default)
    {
        using var client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalKiosk/" + BuildInfo.Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var json = await client.GetStringAsync(ApiUrl, token).ConfigureAwait(false);
        return SelectLatest(json, includePrerelease);
    }
}
