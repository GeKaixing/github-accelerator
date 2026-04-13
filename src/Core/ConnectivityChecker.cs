using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace GitHub.Accelerator.Core;

public static class ConnectivityChecker
{
    public static async Task<(bool ok, bool blocked, int status, long ms, string message)> CheckDomainViaProxyAsync(string proxyAddr, string domain, int timeoutSeconds = 8)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var (proxyHost, proxyPort) = ParseHostPort(proxyAddr, 8899);
            await tcp.ConnectAsync(proxyHost, proxyPort, cts.Token);

            await using var stream = tcp.GetStream();
            stream.ReadTimeout = timeoutSeconds * 1000;
            stream.WriteTimeout = timeoutSeconds * 1000;

            var connectHost = domain + ":443";
            var req = $"CONNECT {connectHost} HTTP/1.1\r\nHost: {connectHost}\r\nProxy-Connection: Keep-Alive\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(req);
            await stream.WriteAsync(bytes, cts.Token);

            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var statusLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(statusLine))
                return (false, false, 0, sw.ElapsedMilliseconds, "empty proxy response");

            var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var status = parts.Length >= 2 && int.TryParse(parts[1], out var s) ? s : 0;

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null || line.Length == 0) break;
            }

            if (status == 403)
                return (false, true, status, sw.ElapsedMilliseconds, "blocked by github-only mode");
            if (status != 200)
                return (false, false, status, sw.ElapsedMilliseconds, "connect tunnel failed");

            using var ssl = new SslStream(stream, leaveInnerStreamOpen: true, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(domain);
            return (true, false, 200, sw.ElapsedMilliseconds, "CONNECT + TLS OK");
        }
        catch (Exception ex)
        {
            return (false, false, 0, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private static (string host, int port) ParseHostPort(string addr, int defaultPort)
    {
        if (string.IsNullOrWhiteSpace(addr)) return ("127.0.0.1", defaultPort);
        var idx = addr.LastIndexOf(':');
        if (idx <= 0) return (addr.Trim(), defaultPort);
        var host = addr[..idx].Trim();
        if (!int.TryParse(addr[(idx + 1)..], out var port)) port = defaultPort;
        return (host, port);
    }
}
