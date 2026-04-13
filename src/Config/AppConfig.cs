namespace GitHub.Accelerator.Config;

public sealed class AppConfig
{
    public string Listen { get; init; } = "127.0.0.1:8899";
    public bool GitHubOnly { get; init; } = true;
    public int ConnectTimeoutSeconds { get; init; } = 6;
    public int BadIpTtlMinutes { get; init; } = 5;
    public string[] DoHEndpoints { get; init; } =
    [
        "https://dns.google/resolve?name={0}&type=A",
        "https://cloudflare-dns.com/dns-query?name={0}&type=A",
    ];
}
