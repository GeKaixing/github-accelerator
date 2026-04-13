using System.Net;
using System.Net.Sockets;
using System.Text;
using GitHub.Accelerator.Config;
using GitHub.Accelerator.Core;
using GitHub.Accelerator.Resolver;

namespace GitHub.Accelerator.Proxy;

public sealed class HttpProxyServer
{
    private readonly AppConfig _config;
    private readonly AdaptiveResolver _resolver;
    private readonly Action<string> _log;

    public HttpProxyServer(AppConfig config, AdaptiveResolver resolver, Action<string>? logger = null)
    {
        _config = config;
        _resolver = resolver;
        _log = logger ?? Console.WriteLine;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var (ip, port) = ParseListen(_config.Listen);
        var listener = new TcpListener(ip, port);
        listener.Start();
        _log($"[proxy] listening on {_config.Listen}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(requestLine)) return;

        var headers = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || line.Length == 0) break;
            headers.Add(line);
        }

        var parts = requestLine.Split(' ');
        if (parts.Length < 2) return;
        var method = parts[0].ToUpperInvariant();
        var target = parts[1];

        if (method == "CONNECT")
        {
            await HandleConnectAsync(stream, target, ct);
            return;
        }

        await WriteSimpleResponseAsync(stream, 501, "Not Implemented", "Only CONNECT is supported in min build.");
    }

    private async Task HandleConnectAsync(NetworkStream clientStream, string target, CancellationToken ct)
    {
        if (!TryParseHostPort(target, 443, out var host, out var port))
        {
            await WriteSimpleResponseAsync(clientStream, 400, "Bad Request", "Invalid CONNECT target");
            return;
        }

        if (_config.GitHubOnly && !GitHubDomainRules.IsGitHubHost(host))
        {
            await WriteSimpleResponseAsync(clientStream, 403, "Forbidden", "github-only mode");
            return;
        }

        var remote = await ConnectWithFailoverAsync(host, port, ct);
        if (remote is null)
        {
            await WriteSimpleResponseAsync(clientStream, 502, "Bad Gateway", "connect upstream failed");
            return;
        }

        using (remote)
        {
            var ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
            await clientStream.WriteAsync(ok, ct);

            var up = remote.GetStream();
            var t1 = clientStream.CopyToAsync(up, ct);
            var t2 = up.CopyToAsync(clientStream, ct);
            await Task.WhenAny(t1, t2);
        }
    }

    private async Task<TcpClient?> ConnectWithFailoverAsync(string host, int port, CancellationToken ct)
    {
        var candidates = await _resolver.ResolveCandidatesAsync(host, ct);
        foreach (var ip in candidates)
        {
            try
            {
                var c = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_config.ConnectTimeoutSeconds));
                await c.ConnectAsync(ip, port, cts.Token);
                return c;
            }
            catch
            {
                _resolver.MarkBad(host, ip);
            }
        }

        // fallback: system host connect once
        try
        {
            var c = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.ConnectTimeoutSeconds));
            await c.ConnectAsync(host, port, cts.Token);
            return c;
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteSimpleResponseAsync(NetworkStream stream, int status, string reason, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(payload);
    }

    private static (IPAddress, int) ParseListen(string listen)
    {
        if (!TryParseHostPort(listen, 8899, out var host, out var port))
            return (IPAddress.Loopback, 8899);
        if (!IPAddress.TryParse(host, out var ip)) ip = IPAddress.Loopback;
        return (ip, port);
    }

    private static bool TryParseHostPort(string value, int defaultPort, out string host, out int port)
    {
        host = "";
        port = defaultPort;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (!value.Contains(':'))
        {
            host = value.Trim();
            return true;
        }

        var idx = value.LastIndexOf(':');
        if (idx <= 0) return false;
        host = value[..idx].Trim();
        return int.TryParse(value[(idx + 1)..], out port);
    }
}
