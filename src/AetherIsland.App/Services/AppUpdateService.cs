using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AetherIsland.App.Services;

public enum UpdateCheckState { UpdateAvailable, UpToDate, NoRelease, Error }
public sealed record UpdateCheckResult(UpdateCheckState State, string? LatestVersion = null);

/// <summary>Checks official GitHub Releases on demand. No automatic downloads or execution.</summary>
public static class AppUpdateService
{
    private static readonly Uri Api = new("https://api.github.com/repos/Hakkam/AetherIsland/releases/latest");

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        using var request = new HttpRequestMessage(HttpMethod.Get, Api);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AetherIsland", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(UpdateCheckState.NoRelease);
            if (!response.IsSuccessStatusCode)
                return new(UpdateCheckState.Error);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("tag_name", out var tag))
                return new(UpdateCheckState.Error);
            var tagName = tag.GetString();
            if (string.IsNullOrWhiteSpace(tagName) ||
                !Version.TryParse(tagName.TrimStart('v', 'V'), out var latest))
                return new(UpdateCheckState.Error);
            var installed = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0);
            static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
            return Normalize(latest) > Normalize(installed)
                ? new(UpdateCheckState.UpdateAvailable, tagName)
                : new(UpdateCheckState.UpToDate, tagName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(UpdateCheckState.Error);
        }
    }
}
