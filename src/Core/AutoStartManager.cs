using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace GitHub.Accelerator.Core;

public static class AutoStartManager
{
    private const string MacLabel = "com.kaixing.github-accelerator";
    private const string WinRunKey = @"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string WinValueName = "GitHubAccelerator";

    public static bool Enable(string[] appArgs)
    {
        var launchArgs = BuildLaunchArgs(appArgs);
        if (launchArgs.Count == 0) return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return EnableMac(launchArgs);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return EnableWindows(launchArgs);
        return false;
    }

    public static bool Disable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DisableMac();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DisableWindows();
        return false;
    }

    public static string Status()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = MacPlistPath();
            return File.Exists(path) ? $"enabled ({path})" : "disabled";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var key = Registry.CurrentUser.OpenSubKey(WinRunKey, false);
            var value = key?.GetValue(WinValueName)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? "disabled" : "enabled";
        }

        return "unsupported";
    }

    private static List<string> BuildLaunchArgs(string[] appArgs)
    {
        var processPath = Environment.ProcessPath;
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var p = processPath!;
            var file = Path.GetFileName(p).ToLowerInvariant();
            if ((file == "dotnet" || file == "dotnet.exe") && File.Exists(assemblyPath))
            {
                args.Add(p);
                args.Add(assemblyPath);
            }
            else
            {
                args.Add(p);
            }
        }
        else if (File.Exists(assemblyPath) && assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dotnet = ResolveDotNetPath();
            if (!string.IsNullOrWhiteSpace(dotnet))
            {
                args.Add(dotnet);
                args.Add(assemblyPath);
            }
        }

        if (args.Count == 0) return args;

        args.Add("gui");
        args.AddRange(appArgs);
        return args;
    }

    private static string ResolveDotNetPath()
    {
        var candidates = new[]
        {
            "/usr/local/share/dotnet/dotnet",
            "/usr/local/bin/dotnet",
            "/opt/homebrew/bin/dotnet",
            "dotnet",
        };
        return candidates.FirstOrDefault(c => File.Exists(c) || c == "dotnet") ?? string.Empty;
    }

    private static bool EnableMac(List<string> args)
    {
        try
        {
            var path = MacPlistPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BuildMacPlist(args), Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool DisableMac()
    {
        try
        {
            var path = MacPlistPath();
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MacPlistPath()
    {
        var overrideDir = Environment.GetEnvironmentVariable("GITHUB_ACCELERATOR_AUTOSTART_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return Path.Combine(overrideDir, $"{MacLabel}.plist");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", $"{MacLabel}.plist");
    }

    private static string BuildMacPlist(List<string> args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\"><dict>");
        sb.AppendLine($"  <key>Label</key><string>{MacLabel}</string>");
        sb.AppendLine("  <key>ProgramArguments</key><array>");
        foreach (var arg in args)
            sb.AppendLine($"    <string>{XmlEscape(arg)}</string>");
        sb.AppendLine("  </array>");
        sb.AppendLine("  <key>RunAtLoad</key><true/>");
        sb.AppendLine("  <key>KeepAlive</key><true/>");
        sb.AppendLine("  <key>StandardOutPath</key><string>/tmp/github-accelerator.log</string>");
        sb.AppendLine("  <key>StandardErrorPath</key><string>/tmp/github-accelerator.log</string>");
        sb.AppendLine("</dict></plist>");
        return sb.ToString();
    }

    [SupportedOSPlatform("windows")]
    private static bool EnableWindows(List<string> args)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(WinRunKey, true);
            if (key == null) return false;
            var value = string.Join(" ", args.Select(QuoteForCmd));
            key.SetValue(WinValueName, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool DisableWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WinRunKey, true);
            if (key == null) return true;
            key.DeleteValue(WinValueName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteForCmd(string s) =>
        s.Contains(' ') || s.Contains('"') ? $"\"{s.Replace("\"", "\\\"")}\"" : s;

    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
}
