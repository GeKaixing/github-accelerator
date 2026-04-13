# GitHub Accelerator

这个项目是从 `SteamTools` 的 GitHub 加速思路裁剪出的最小 C# 可运行版本（Web UI + 本地代理）。

- 本地代理（HTTP CONNECT）
- GitHub 域名规则（可选 github-only）
- 系统 DNS + DoH 候选解析
- 失败 IP 临时隔离（bad IP quarantine）
- 一键写入/清理 Git 全局代理
- 双击启动 + 开机启动 + 单实例控制

## 目录

- `Program.cs`：命令入口
- `src/Proxy/HttpProxyServer.cs`：最小代理服务
- `src/Resolver/AdaptiveResolver.cs`：候选 IP 解析与隔离
- `src/Core/GitHubDomainRules.cs`：GitHub 域名匹配
- `src/Core/GitProxyConfig.cs`：一键 Git 代理开关
- `src/Core/AutoStartManager.cs`：开机启动管理（macOS/Windows）
- `src/Core/SingleInstanceGuard.cs`：GUI 单实例控制

## 运行

```bash
dotnet run --project github-accelerator.csproj -- gui --listen 127.0.0.1:8899 --gui-listen 127.0.0.1:19010 --github-only true
```

## 一键加速（无需手工 git config）

```bash
dotnet run --project github-accelerator.csproj -- enable --proxy 127.0.0.1:8899
```

关闭：

```bash
dotnet run --project github-accelerator.csproj -- disable
```

查看：

```bash
dotnet run --project github-accelerator.csproj -- status
```

## 开机启动

启用：

```bash
dotnet run --project github-accelerator.csproj -- autostart enable --listen 127.0.0.1:8899 --gui-listen 127.0.0.1:19010 --github-only true
```

关闭：

```bash
dotnet run --project github-accelerator.csproj -- autostart disable
```

状态：

```bash
dotnet run --project github-accelerator.csproj -- autostart status
```

说明：
- macOS 写入 `~/Library/LaunchAgents/com.kaixing.github-accelerator.plist`
- Windows 写入 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`

## 单实例

`gui` 模式默认单实例：
- 第一次启动：拉起代理 + Web UI
- 再次启动：不重复启动，只唤醒已有面板
