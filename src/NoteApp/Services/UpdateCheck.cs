using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public sealed record LatestRelease(Version Version, string Tag, Uri Page);

// "Is there a newer NoteApp?" — the latest GitHub Release of the repository (published by
// .github/workflows/release.yml from a v1.2.3 tag) against this build's version. At most
// once a day; any failure (offline, rate limit, no release yet) just means no answer.
public static class UpdateCheck
{
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/VitakSharper/NoteAppWpf/releases/latest");
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    // "v1.2.3" and "1.2" read as 1.2.3 and 1.2.0; anything else is no version.
    public static Version? VersionOf(string? tag)
    {
        var text = tag?.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version) ? Normalize(version) : null;
    }

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    public static bool IsDue(DateTime? lastCheckUtc, DateTime nowUtc) => lastCheckUtc is not { } last || nowUtc - last >= Interval;

    // GitHub's "latest release" JSON; drafts and pre-releases never count.
    public static Option<LatestRelease> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()
                || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())
                return Option<LatestRelease>.Empty();

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var page = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            return VersionOf(tag) is { } version && Uri.TryCreate(page, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? new Option<LatestRelease>.Some(new LatestRelease(version, tag!, uri))
                : Option<LatestRelease>.Empty();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Option<LatestRelease>.Empty();
        }
    }

    public static async Task<Option<LatestRelease>> FetchAsync(HttpClient http, CancellationToken cancellation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NoteApp", "update-check"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, cancellation);
            return response.IsSuccessStatusCode
                ? Parse(await response.Content.ReadAsStringAsync(cancellation))
                : Option<LatestRelease>.Empty();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return Option<LatestRelease>.Empty();
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
