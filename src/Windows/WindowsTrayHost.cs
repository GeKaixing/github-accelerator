#if WINDOWS_DESKTOP
using System.Drawing;
using System.Windows.Forms;
using GitHub.Accelerator.Core;

namespace GitHub.Accelerator.Windows;

public sealed class WindowsTrayHost : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly string _guiUrl;
    private readonly string _proxyAddr;
    private readonly Action _exitApp;
    private readonly Action<string> _openBrowser;
    private readonly Func<string, Task<(bool ok, bool blocked, int status, long ms, string message)>> _domainChecker;
    private volatile bool _disposed;
    private NotifyIcon? _notifyIcon;

    private WindowsTrayHost(
        string guiUrl,
        string proxyAddr,
        Action exitApp,
        Action<string> openBrowser,
        Func<string, Task<(bool ok, bool blocked, int status, long ms, string message)>> domainChecker)
    {
        _guiUrl = guiUrl;
        _proxyAddr = proxyAddr;
        _exitApp = exitApp;
        _openBrowser = openBrowser;
        _domainChecker = domainChecker;

        _uiThread = new Thread(UIThreadMain)
        {
            IsBackground = true,
            Name = "GitHubAcceleratorTrayThread",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait();
    }

    public static WindowsTrayHost Start(
        string guiUrl,
        string proxyAddr,
        Action exitApp,
        Action<string> openBrowser,
        Func<string, Task<(bool ok, bool blocked, int status, long ms, string message)>> domainChecker) =>
        new(guiUrl, proxyAddr, exitApp, openBrowser, domainChecker);

    private void UIThreadMain()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "GitHub Accelerator",
            ContextMenuStrip = BuildMenu(),
        };

        notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _openBrowser(_guiUrl);
        };

        _notifyIcon = notifyIcon;
        RefreshState();
        _ready.Set();
        Application.Run();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var openPanel = new ToolStripMenuItem("\u6253\u5f00\u9762\u677f");
        openPanel.Click += (_, _) => _openBrowser(_guiUrl);

        var enable = new ToolStripMenuItem("\u4e00\u952e\u52a0\u901f");
        enable.Click += (_, _) =>
        {
            var ok = GitProxyConfig.Enable(_proxyAddr);
            RefreshState();
            ShowTip(ok ? "\u5df2\u542f\u7528 Git \u4ee3\u7406" : "\u542f\u7528\u5931\u8d25");
        };

        var disable = new ToolStripMenuItem("\u5173\u95ed\u52a0\u901f");
        disable.Click += (_, _) =>
        {
            var ok = GitProxyConfig.Disable();
            RefreshState();
            ShowTip(ok ? "\u5df2\u5173\u95ed Git \u4ee3\u7406" : "\u5173\u95ed\u5931\u8d25");
        };

        var refresh = new ToolStripMenuItem("\u5237\u65b0");
        refresh.Click += (_, _) =>
        {
            RefreshState();
            ShowTip("\u72b6\u6001\u5df2\u5237\u65b0");
        };

        var testConnectivity = new ToolStripMenuItem("\u8fde\u901a\u6027\u6d4b\u8bd5");
        testConnectivity.Click += async (_, _) =>
        {
            testConnectivity.Enabled = false;
            try
            {
                var res = await _domainChecker("github.com");
                if (res.ok)
                    ShowTip($"github.com \u53ef\u8fbe {res.ms}ms");
                else if (res.blocked)
                    ShowTip("github.com \u88ab github-only \u6a21\u5f0f\u62e6\u622a");
                else
                    ShowTip($"\u6d4b\u8bd5\u5931\u8d25: {res.message}");
            }
            finally
            {
                testConnectivity.Enabled = true;
            }
        };

        var exit = new ToolStripMenuItem("\u9000\u51fa");
        exit.Click += (_, _) =>
        {
            _exitApp();
            Dispose();
        };

        menu.Items.Add(openPanel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(enable);
        menu.Items.Add(disable);
        menu.Items.Add(refresh);
        menu.Items.Add(testConnectivity);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void RefreshState()
    {
        if (_notifyIcon is null) return;
        var status = GitProxyConfig.GetStatus();
        _notifyIcon.Icon = status.Enabled ? TrayIcons.Enabled : TrayIcons.Disabled;
        _notifyIcon.Text = status.Enabled
            ? "GitHub Accelerator (\u52a0\u901f\u4e2d)"
            : "GitHub Accelerator (\u672a\u52a0\u901f)";
    }

    private void ShowTip(string message)
    {
        if (_notifyIcon is null) return;
        _notifyIcon.BalloonTipTitle = "GitHub Accelerator";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(1200);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon is not null)
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            Application.ExitThread();
        }
        catch
        {
        }
    }
}

public static class WindowsDesktopIntegration
{
    public static void EnsureDesktopShortcut(string proxyListen, string guiListen, bool githubOnly)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var thread = new Thread(() => EnsureDesktopShortcutCore(proxyListen, guiListen, githubOnly));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(3000);
        }
        catch
        {
        }
    }

    private static void EnsureDesktopShortcutCore(string proxyListen, string guiListen, bool githubOnly)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var shortcutPath = Path.Combine(desktop, "GitHub Accelerator.lnk");
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

        var iconPath = EnsureIconFile();
        var args = $"gui --listen {proxyListen} --gui-listen {guiListen} --github-only {(githubOnly ? "true" : "false")}";
        var workDir = Path.GetDirectoryName(exePath) ?? string.Empty;

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;

        var shell = Activator.CreateInstance(shellType);
        if (shell is null) return;

        var shortcut = shellType.InvokeMember(
            "CreateShortcut",
            System.Reflection.BindingFlags.InvokeMethod,
            null,
            shell,
            [shortcutPath]);
        if (shortcut is null) return;

        SetComProperty(shortcut, "TargetPath", exePath);
        SetComProperty(shortcut, "Arguments", args);
        SetComProperty(shortcut, "WorkingDirectory", workDir);
        SetComProperty(shortcut, "Description", "GitHub Accelerator");
        if (!string.IsNullOrWhiteSpace(iconPath))
            SetComProperty(shortcut, "IconLocation", iconPath);
        shortcut.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
    }

    private static void SetComProperty(object comObject, string name, object value)
    {
        comObject.GetType().InvokeMember(
            name,
            System.Reflection.BindingFlags.SetProperty,
            null,
            comObject,
            [value]);
    }

    private static string EnsureIconFile()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubAccelerator");
            Directory.CreateDirectory(dir);
            var iconPath = Path.Combine(dir, "app.ico");

            if (File.Exists(iconPath)) return iconPath;

            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                using var bg = new SolidBrush(Color.FromArgb(21, 128, 61));
                g.FillEllipse(bg, 4, 4, 56, 56);
                using var font = new Font("Segoe UI", 24, FontStyle.Bold, GraphicsUnit.Pixel);
                using var fg = new SolidBrush(Color.White);
                g.DrawString("G", font, fg, 19, 14);
            }

            var hIcon = bmp.GetHicon();
            using var icon = Icon.FromHandle(hIcon);
            using var fs = File.Create(iconPath);
            icon.Save(fs);
            NativeMethods.DestroyIcon(hIcon);
            return iconPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal static class TrayIcons
{
    public static readonly Icon Enabled = Build(Color.FromArgb(16, 185, 129));
    public static readonly Icon Disabled = Build(Color.FromArgb(107, 114, 128));

    private static Icon Build(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 4, 4, 24, 24);
            using var ring = new Pen(Color.White, 2f);
            g.DrawEllipse(ring, 4, 4, 24, 24);
        }

        var handle = bmp.GetHicon();
        using var icon = Icon.FromHandle(handle);
        var cloned = (Icon)icon.Clone();
        NativeMethods.DestroyIcon(handle);
        return cloned;
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
#endif
