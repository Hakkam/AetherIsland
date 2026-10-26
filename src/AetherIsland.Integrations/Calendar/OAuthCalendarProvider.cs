using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AetherIsland.Integrations.Calendar;

/// <summary>
/// Optional delegated calendar reader for Google Calendar and Microsoft Graph.
/// Uses native public-client authorization code + PKCE, system browser, a local
/// loopback redirect and encrypted token persistence supplied by the host app.
/// No account passwords, browser cookies, or embedded client secrets are used.
/// </summary>
public sealed class OAuthCalendarProvider
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Func<string, string?> _read;
    private readonly Action<string, string> _write;
    private readonly Action<string> _remove;

    public OAuthCalendarProvider(Func<string, string?> read, Action<string, string> write, Action<string> remove)
    {
        _read = read;
        _write = write;
        _remove = remove;
    }

    private static string StoreKey(string provider) => "calendar-oauth-" + provider;
    public bool IsConnected(string provider) => Read(provider)?.RefreshToken is { Length: > 0 };
    public void Disconnect(string provider) => _remove(StoreKey(provider));

    private sealed class Token
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private Token? Read(string provider)
    {
        var secret = _read(StoreKey(provider));
        if (string.IsNullOrWhiteSpace(secret)) return null;
        try { return JsonSerializer.Deserialize<Token>(secret); }
        catch (JsonException) { return null; }
    }

    private void Save(string provider, Token token) =>
        _write(StoreKey(provider), JsonSerializer.Serialize(token));

    public async Task ConnectAsync(string provider, string clientId, CancellationToken cancellationToken = default)
    {
        if (provider is not (Google or Microsoft)) throw new ArgumentException("Unsupported provider", nameof(provider));
        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Configure the OAuth desktop application client ID first.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var ct = timeout.Token;

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirect = provider == Google ? $"http://127.0.0.1:{port}/" : $"http://localhost:{port}/";
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var authorization = provider == Google
            ? "https://accounts.google.com/o/oauth2/v2/auth"
            : "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        var scope = provider == Google
            ? "https://www.googleapis.com/auth/calendar.readonly"
            : "offline_access Calendars.Read";
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirect,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        if (provider == Google)
        {
            query["access_type"] = "offline";
            query["prompt"] = "consent";
        }

        Process.Start(new ProcessStartInfo(authorization + "?" + QueryString(query))
        {
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("Could not open system browser");

        // The socket is already bound before opening the browser; state and PKCE
        // prevent redirect injection or code substitution.
        string? code = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using var browser = await listener.AcceptTcpClientAsync(ct);
            await using var stream = browser.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var firstLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(firstLine)) continue;
            while (true)
            {
                var header = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(header)) break;
            }

            var parts = firstLine.Split(' ');
            if (parts.Length < 2 || parts[0] != "GET") continue;
            var callback = new Uri("http://localhost" + parts[1]);
            var values = ParseQuery(callback.Query);
            var authorized = values.TryGetValue("state", out var returnedState) &&
                CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(returnedState), Encoding.UTF8.GetBytes(state));
            var error = values.TryGetValue("error", out var oauthError) ? oauthError : null;
            if (!authorized && (values.ContainsKey("code") || error is not null))
                throw new InvalidOperationException("Invalid OAuth response state.");
            code = values.TryGetValue("code", out var received) ? received : null;
            var html = error is null && code is not null
                ? "<html><body style='font-family:system-ui'>Calendar connected. You can close this tab.</body></html>"
                : "<html><body style='font-family:system-ui'>Authorization was not completed.</body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
            await stream.WriteAsync(response, ct);
            await stream.WriteAsync(bytes, ct);
            await stream.FlushAsync(ct);
            if (error is not null) throw new InvalidOperationException("Calendar authorization was declined.");
            if (code is not null) break;
        }
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("No OAuth authorization code received.");

        using var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["grant_type"] = "authorization_code"
        });
        using var responseToken = await Http.PostAsync(TokenEndpoint(provider), request, ct);
        if (!responseToken.IsSuccessStatusCode)
            throw new InvalidOperationException("Calendar token exchange failed. Verify the OAuth client registration.");
        using var doc = JsonDocument.Parse(await responseToken.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var access = GetString(root, "access_token") ?? "";
        var refresh = GetString(root, "refresh_token") ?? Read(provider)?.RefreshToken ?? "";
        if (access.Length == 0 || refresh.Length == 0)
            throw new InvalidOperationException("Calendar provider did not return a reusable authorization token.");
        Save(provider, new Token
        {
            AccessToken = access, RefreshToken = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, GetInt(root, "expires_in") - 90))
        });
    }

    public async Task<List<CalendarEvent>> LoadEventsAsync(string provider, string clientId, CancellationToken ct = default)
    {
        var token = Read(provider);
        if (token is null) return new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Calendar OAuth client ID missing.");
        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = token.RefreshToken,
                ["scope"] = provider == Google ? "https://www.googleapis.com/auth/calendar.readonly" : "offline_access Calendars.Read"
            });
            using var refreshed = await Http.PostAsync(TokenEndpoint(provider), form, ct);
            if (!refreshed.IsSuccessStatusCode)
                throw new InvalidOperationException("Calendar authorization expired. Disconnect and reconnect the account.");
            using var auth = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync(ct));
            var root = auth.RootElement;
            token.AccessToken = GetString(root, "access_token") ?? "";
            token.RefreshToken = GetString(root, "refresh_token") ?? token.RefreshToken;
            token.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, GetInt(root, "expires_in") - 90));
            if (token.AccessToken.Length == 0) throw new InvalidOperationException("Calendar token refresh returned no access token.");
            Save(provider, token);
        }

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(30);
        string url;
        if (provider == Google)
        {
            url = "https://www.googleapis.com/calendar/v3/calendars/primary/events?" + QueryString(new()
            {
                ["timeMin"] = start.ToString("O"),
                ["timeMax"] = end.ToString("O"),
                ["singleEvents"] = "true",
                ["orderBy"] = "startTime",
                ["maxResults"] = "100"
            });
        }
        else if (provider == Microsoft)
        {
            url = "https://graph.microsoft.com/v1.0/me/calendarView?" + QueryString(new()
            {
                ["startDateTime"] = start.ToString("O"),
                ["endDateTime"] = end.ToString("O"),
                ["$top"] = "100"
            });
        }
        else throw new ArgumentException("Unsupported provider", nameof(provider));

        using var msg = new HttpRequestMessage(HttpMethod.Get, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        if (provider == Microsoft) msg.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");
        using var response = await Http.SendAsync(msg, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Calendar request failed. Check account permissions and reconnect if needed.");
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var events = new List<CalendarEvent>();
        if (provider == Google)
        {
            if (!data.RootElement.TryGetProperty("items", out var items)) return events;
            foreach (var ev in items.EnumerateArray())
            {
                var startObj = GetObject(ev, "start");
                var endObj = GetObject(ev, "end");
                var date = GetString(startObj, "dateTime") ?? GetString(startObj, "date");
                var endDate = GetString(endObj, "dateTime") ?? GetString(endObj, "date");
                if (date is null) continue;
                var isAllDay = GetString(startObj, "date") is not null;
                events.Add(new CalendarEvent
                {
                    Title = GetString(ev, "summary") ?? "(Untitled)",
                    Start = ParseTime(date, isAllDay),
                    End = endDate is null ? ParseTime(date, isAllDay).AddHours(1) : ParseTime(endDate, isAllDay),
                    AllDay = isAllDay,
                    Location = GetString(ev, "location"),
                    Url = GetString(ev, "hangoutLink") ?? GetString(ev, "htmlLink"),
                    Source = "Google Calendar"
                });
            }
        }
        else
        {
            if (!data.RootElement.TryGetProperty("value", out var values)) return events;
            foreach (var ev in values.EnumerateArray())
            {
                var startObj = GetObject(ev, "start");
                var endObj = GetObject(ev, "end");
                var begin = GetString(startObj, "dateTime");
                if (begin is null) continue;
                var isAllDay = ev.TryGetProperty("isAllDay", out var day) && day.ValueKind == JsonValueKind.True;
                var location = GetObject(ev, "location");
                var meeting = GetObject(ev, "onlineMeeting");
                events.Add(new CalendarEvent
                {
                    Title = GetString(ev, "subject") ?? "(Untitled)",
                    Start = ParseTime(begin, isAllDay),
                    End = ParseTime(GetString(endObj, "dateTime") ?? begin, isAllDay),
                    AllDay = isAllDay,
                    Location = GetString(location, "displayName"),
                    Url = GetString(meeting, "joinUrl") ?? GetString(ev, "webLink"),
                    Source = "Microsoft Calendar"
                });
            }
        }
        return events.OrderBy(x => x.Start).ToList();
    }

    private static string TokenEndpoint(string provider) => provider == Google
        ? "https://oauth2.googleapis.com/token"
        : "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    private static DateTime ParseTime(string raw, bool allDay) =>
        allDay ? DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None).Date
               : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).LocalDateTime;

    private static JsonElement GetObject(JsonElement root, string key) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var result) ? result : default;

    private static string? GetString(JsonElement root, string key) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var result) &&
        result.ValueKind == JsonValueKind.String ? result.GetString() : null;

    private static int GetInt(JsonElement root, string key) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value) &&
        value.TryGetInt32(out var parsed) ? parsed : 3600;

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string QueryString(Dictionary<string, string> pairs) =>
        string.Join("&", pairs.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx < 0) continue;
            values[WebUtility.UrlDecode(part[..idx])] = WebUtility.UrlDecode(part[(idx + 1)..]);
        }
        return values;
    }
}
