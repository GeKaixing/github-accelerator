using System.Net;
using System.Diagnostics;
using System.Text.Json;
using GitHub.Accelerator.Config;
using GitHub.Accelerator.Core;
using GitHub.Accelerator.Proxy;
using GitHub.Accelerator.Resolver;

var argsList = args.ToList();
var cmd = argsList.Count > 0 ? argsList[0].ToLowerInvariant() : "serve";

switch (cmd)
{
    case "serve":
        await ServeAsync(argsList.Skip(1).ToArray());
        break;
    case "gui":
        await GuiAsync(argsList.Skip(1).ToArray());
        break;
    case "check":
        await CheckAsync(argsList.Skip(1).ToArray());
        break;
    case "enable":
        Enable(argsList.Skip(1).ToArray());
        break;
    case "disable":
        Disable();
        break;
    case "status":
        Console.WriteLine(GitProxyConfig.Status());
        break;
    case "autostart":
        AutoStart(argsList.Skip(1).ToArray());
        break;
    default:
        PrintHelp();
        break;
}

static AppConfig BuildConfig(string[] args) => new()
{
    Listen = ReadArg(args, "--listen", "127.0.0.1:8899"),
    GitHubOnly = ReadArg(args, "--github-only", "true").Equals("true", StringComparison.OrdinalIgnoreCase),
};

static async Task ServeAsync(string[] args)
{
    var cfg = BuildConfig(args);
    var resolver = new AdaptiveResolver(cfg);
    var proxy = new HttpProxyServer(cfg, resolver, s => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {s}"));

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    await proxy.RunAsync(cts.Token);
}

static async Task GuiAsync(string[] args)
{
    var cfg = BuildConfig(args);
    var guiListen = ReadArg(args, "--gui-listen", "127.0.0.1:19000");
    var autoOpenBrowser = ReadArg(args, "--open-browser", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
    var guiUrl = $"http://{guiListen}";

    using var instance = SingleInstanceGuard.TryAcquire("github-accelerator-gui-single-instance");
    if (!instance.Acquired)
    {
        OpenBrowser(guiUrl);
        Console.WriteLine($"gui already running, opened {guiUrl}");
        return;
    }

    var resolver = new AdaptiveResolver(cfg);
    var proxy = new HttpProxyServer(cfg, resolver, s => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {s}"));

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    _ = Task.Run(() => proxy.RunAsync(cts.Token), cts.Token);

    var builder = WebApplication.CreateBuilder(Array.Empty<string>());
    var app = builder.Build();
    var startedAt = DateTimeOffset.Now;

    var htmlPath = Path.Combine(AppContext.BaseDirectory, "index.html");
    if (!File.Exists(htmlPath))
        htmlPath = Path.Combine(AppContext.BaseDirectory, "src", "Ui", "index.html");
    if (!File.Exists(htmlPath))
        htmlPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Ui", "index.html");

    app.MapGet("/", async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        if (!File.Exists(htmlPath))
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"index.html not found: {htmlPath}", context.RequestAborted);
            return;
        }
        var html = await File.ReadAllTextAsync(htmlPath, context.RequestAborted);
        await context.Response.WriteAsync(html, context.RequestAborted);
    });

    app.MapGet("/api/status", () =>
    {
        var gs = GitProxyConfig.GetStatus();
        var resp = new StatusResponse
        {
            proxy_addr = cfg.Listen,
            gui_addr = guiListen,
            github_only = cfg.GitHubOnly,
            started_at = startedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            git_proxy = new GitProxyStatusResponse
            {
                enabled = gs.Enabled,
                http_proxy = gs.HttpProxy,
                https_proxy = gs.HttpsProxy,
            }
        };
        return ApiJson.Result(resp, ApiJsonContext.Default.StatusResponse);
    });

    app.MapGet("/api/test/domain", async (string domain) =>
    {
        var res = await ConnectivityChecker.CheckDomainViaProxyAsync(cfg.Listen, domain, 8);
        var resp = new TestDomainResponse
        {
            ok = res.ok,
            blocked = res.blocked,
            status = res.status,
            ms = res.ms,
            message = res.message,
            error = res.ok ? "" : res.message,
        };
        return ApiJson.Result(resp, ApiJsonContext.Default.TestDomainResponse);
    });

    app.MapPost("/api/git/enable", () =>
    {
        var ok = GitProxyConfig.Enable(cfg.Listen);
        var st = GitProxyConfig.GetStatus();
        var resp = new GitActionResponse
        {
            ok = ok,
            status = new GitProxyStatusResponse
            {
                enabled = st.Enabled,
                http_proxy = st.HttpProxy,
                https_proxy = st.HttpsProxy,
            }
        };
        return ApiJson.Result(resp, ApiJsonContext.Default.GitActionResponse);
    });

    app.MapPost("/api/git/disable", () =>
    {
        var ok = GitProxyConfig.Disable();
        var st = GitProxyConfig.GetStatus();
        var resp = new GitActionResponse
        {
            ok = ok,
            status = new GitProxyStatusResponse
            {
                enabled = st.Enabled,
                http_proxy = st.HttpProxy,
                https_proxy = st.HttpsProxy,
            }
        };
        return ApiJson.Result(resp, ApiJsonContext.Default.GitActionResponse);
    });

    app.Urls.Clear();
    app.Urls.Add(guiUrl);
    Console.WriteLine($"[gui] listening on {guiUrl}");

    if (autoOpenBrowser)
        OpenBrowser(guiUrl);

    await app.RunAsync(cts.Token);
}

static async Task CheckAsync(string[] args)
{
    var proxyAddr = ReadArg(args, "--proxy", "127.0.0.1:8899");
    var domain = ReadArg(args, "--domain", "github.com");

    var http = new HttpClient(new HttpClientHandler
    {
        Proxy = new WebProxy($"http://{proxyAddr}"),
        UseProxy = true,
    })
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    try
    {
        var rsp = await http.GetAsync($"https://{domain}/");
        Console.WriteLine($"check ok: {domain} status={(int)rsp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"check failed: {domain} {ex.Message}");
    }
}

static void Enable(string[] args)
{
    var proxyAddr = ReadArg(args, "--proxy", "127.0.0.1:8899");
    var ok = GitProxyConfig.Enable(proxyAddr);
    Console.WriteLine(ok ? $"git proxy enabled: {proxyAddr}" : "git proxy enable failed");
}

static void Disable()
{
    var ok = GitProxyConfig.Disable();
    Console.WriteLine(ok ? "git proxy disabled" : "git proxy disable failed");
}

static void AutoStart(string[] args)
{
    var action = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

    if (action == "status")
    {
        Console.WriteLine($"autostart: {AutoStartManager.Status()}");
        return;
    }

    if (action == "enable")
    {
        var cfg = BuildConfig(args);
        var guiListen = ReadArg(args, "--gui-listen", "127.0.0.1:19000");
        var ok = AutoStartManager.Enable(
        [
            "--listen", cfg.Listen,
            "--gui-listen", guiListen,
            "--github-only", cfg.GitHubOnly ? "true" : "false",
        ]);
        Console.WriteLine(ok ? "autostart enabled" : "autostart enable failed");
        return;
    }

    if (action == "disable")
    {
        var ok = AutoStartManager.Disable();
        Console.WriteLine(ok ? "autostart disabled" : "autostart disable failed");
        return;
    }

    Console.WriteLine("autostart usage: autostart [enable|disable|status]");
}

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
    catch
    {
    }
}

static string ReadArg(string[] args, string key, string fallback)
{
    for (var i = 0; i < args.Length; i++)
        if (args[i] == key && i + 1 < args.Length) return args[i + 1];
    return fallback;
}

static void PrintHelp()
{
    Console.WriteLine(@"GitHub.Accelerator

Usage:
  github-accelerator serve [--listen 127.0.0.1:8899] [--github-only true|false]
  github-accelerator gui [--listen 127.0.0.1:8899] [--gui-listen 127.0.0.1:19000] [--github-only true|false] [--open-browser true|false]
  github-accelerator check [--proxy 127.0.0.1:8899] [--domain github.com]
  github-accelerator enable [--proxy 127.0.0.1:8899]
  github-accelerator disable
  github-accelerator status
  github-accelerator autostart [enable|disable|status] [--listen 127.0.0.1:8899] [--gui-listen 127.0.0.1:19000] [--github-only true|false]
");
}
