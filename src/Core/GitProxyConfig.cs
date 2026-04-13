using System.Diagnostics;

namespace GitHub.Accelerator.Core;

public static class GitProxyConfig
{
    public sealed class GitProxyStatus
    {
        public bool Enabled { get; init; }
        public string HttpProxy { get; init; } = string.Empty;
        public string HttpsProxy { get; init; } = string.Empty;
    }

    public static bool Enable(string proxyAddr) =>
        Run("git", $"config --global --replace-all http.proxy http://{proxyAddr}") &&
        Run("git", $"config --global --replace-all https.proxy http://{proxyAddr}");

    public static bool Disable() =>
        RunIgnoreFail("git", "config --global --unset-all http.proxy") &&
        RunIgnoreFail("git", "config --global --unset-all https.proxy");

    public static GitProxyStatus GetStatus()
    {
        var http = ReadFirst("git", "config --global --get-all http.proxy");
        var https = ReadFirst("git", "config --global --get-all https.proxy");
        return new GitProxyStatus
        {
            Enabled = !string.IsNullOrWhiteSpace(http) || !string.IsNullOrWhiteSpace(https),
            HttpProxy = http,
            HttpsProxy = https,
        };
    }

    public static string Status()
    {
        var st = GetStatus();
        return $"http.proxy={st.HttpProxy}; https.proxy={st.HttpsProxy}";
    }

    private static bool Run(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunIgnoreFail(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p!.WaitForExit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Read(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            var s = p!.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return s;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadFirst(string file, string args)
    {
        var all = Read(file, args);
        if (string.IsNullOrWhiteSpace(all)) return string.Empty;
        var first = all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return first?.Trim() ?? string.Empty;
    }
}
