using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GitHub.Accelerator.Config;

namespace GitHub.Accelerator.Resolver;

public sealed class AdaptiveResolver
{
    private readonly AppConfig _config;
    private readonly HttpClient _http = new();
    private readonly Dictionary<string, DateTimeOffset> _badIpUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public AdaptiveResolver(AppConfig config) => _config = config;

    public async Task<IReadOnlyList<IPAddress>> ResolveCandidatesAsync(string host, CancellationToken ct)
    {
        var set = new HashSet<IPAddress>();

        try
        {
            var sys = await Dns.GetHostAddressesAsync(host, ct);
            foreach (var ip in sys.Where(i => i.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                set.Add(ip);
        }
        catch
        {
        }

        foreach (var endpointPattern in _config.DoHEndpoints)
        {
            try
            {
                var uri = string.Format(endpointPattern, Uri.EscapeDataString(host));
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.TryAddWithoutValidation("accept", "application/dns-json");
                var rsp = await _http.SendAsync(req, ct);
                if (!rsp.IsSuccessStatusCode) continue;
                var body = await rsp.Content.ReadFromJsonAsync<DohResponse>(cancellationToken: ct);
                if (body?.Answer == null) continue;
                foreach (var ans in body.Answer)
                {
                    if (ans.Type != 1) continue;
                    if (IPAddress.TryParse(ans.Data, out var ip)) set.Add(ip);
                }
            }
            catch
            {
            }
        }

        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            return set.Where(ip =>
            {
                var key = $"{host}|{ip}";
                if (_badIpUntil.TryGetValue(key, out var until) && until > now) return false;
                return true;
            }).ToArray();
        }
    }

    public void MarkBad(string host, IPAddress ip)
    {
        var key = $"{host}|{ip}";
        lock (_lock)
        {
            _badIpUntil[key] = DateTimeOffset.UtcNow.AddMinutes(_config.BadIpTtlMinutes);
        }
    }

    private sealed class DohResponse
    {
        [JsonPropertyName("Status")] public int Status { get; set; }
        [JsonPropertyName("Answer")] public DohAnswer[]? Answer { get; set; }
    }

    private sealed class DohAnswer
    {
        [JsonPropertyName("type")] public int Type { get; set; }
        [JsonPropertyName("data")] public string Data { get; set; } = string.Empty;
    }
}
