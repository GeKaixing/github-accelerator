# AGENT.md

## 项目定位
`github-accelerator` 是一个面向 GitHub 访问优化的桌面/CLI 工具，当前以 **本地代理 + Web 面板 + macOS 状态栏宿主** 为主。

核心目标：
- 一键启用/关闭 Git 代理
- GitHub 相关域名连通性测试
- macOS 状态栏常驻控制（含延迟测试面板）
- 保持包体尽量小（优先自包含瘦身发布）

## 技术栈
- 后端/CLI: `.NET 8` (`github-accelerator.csproj`)
- 代理实现: C# TCP/HTTP CONNECT
- Web UI: `src/Ui/index.html`（内置静态页面）
- macOS 菜单栏宿主: Swift (`packaging/mac/MenuBarHost.swift`)

## 目录说明
- `Program.cs`: CLI + HTTP API 入口
- `src/Config/*`: 配置模型
- `src/Core/*`: 规则、状态、JSON、单实例、开机启动
- `src/Proxy/*`: 代理服务实现
- `src/Resolver/*`: DNS/DoH 解析与失败隔离
- `src/Ui/index.html`: Web 控制面板
- `packaging/mac/*`: macOS `.app` 打包脚本与菜单栏宿主
- `packaging/windows/*`: Windows 安装脚本
- `packaging/release/*`: 发布辅助脚本

## 常用命令
### 本地运行
```bash
dotnet run --project github-accelerator.csproj -- gui --listen 127.0.0.1:8999 --gui-listen 127.0.0.1:19010 --github-only true
```

### CLI 功能
```bash
dotnet run --project github-accelerator.csproj -- enable --proxy 127.0.0.1:8999
dotnet run --project github-accelerator.csproj -- disable
dotnet run --project github-accelerator.csproj -- status

dotnet run --project github-accelerator.csproj -- autostart enable --listen 127.0.0.1:8999 --gui-listen 127.0.0.1:19010 --github-only true
dotnet run --project github-accelerator.csproj -- autostart disable
dotnet run --project github-accelerator.csproj -- autostart status
```

### macOS 打包
```bash
# 先发布 runtime（二进制）
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:UseAppHost=true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:EnableCompressionInSingleFile=true -p:DebuggerSupport=false \
  -p:InvariantGlobalization=true -p:StripSymbols=true \
  -o dist/osx-arm64

# 再打 .app + zip
./packaging/mac/build-mac-app.sh
```

### Windows 打包
```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:UseAppHost=true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:EnableCompressionInSingleFile=true -p:DebuggerSupport=false \
  -p:InvariantGlobalization=true -p:StripSymbols=true \
  -o dist/win-x64

cd dist
zip -qr GitHubAccelerator-win-x64.zip win-x64
```

## 代码修改约定
1. 尽量不引入新运行时依赖，优先保持包体。
2. 菜单栏交互改动优先在 `MenuBarHost.swift` 完成，避免改动后端协议。
3. Web 面板测试项与菜单栏延迟测试目标需保持一致。
4. 变更打包逻辑时必须本地重打包并验证产物存在。
5. 不提交 `bin/`、`obj/`、`dist/` 构建产物。

## 关键交互约束（已确认）
- 启动应用不自动弹出两个网页标签。
- Dock 点击可重新打开面板。
- 状态栏支持：一键加速、关闭加速、刷新、延迟测试面板。
- 延迟测试面板支持：测试全部、逐项测试、重置、关闭。

## 故障排查
- 日志文件: `/tmp/github-accelerator-gui.log`
- 若 Git 推送失败并提示本地代理不可达，使用：
```bash
git -c http.proxy= -c https.proxy= push -u origin main
```

## 提交流程建议
```bash
git add .
git commit -m "feat: <简要描述>"
git -c http.proxy= -c https.proxy= push -u origin main
```

## 发布 Checklist
发版前逐项确认：

1. `dotnet build -c Release github-accelerator.csproj` 通过。
2. Web 面板可打开，`/api/status` 返回正常 JSON。
3. 一键加速/关闭加速在 GUI 与状态栏菜单都可用。
4. 延迟测试面板可打开、`测试全部` 与单项测试可用、`关闭` 可用。
5. 启动后不双开标签页；Dock 点击可重新打开面板。
6. mac 图标正常显示（Dock + 菜单栏应用图标）。
7. mac 包重新打包并存在：`dist/GitHubAccelerator-macOS-arm64.zip`。
8. win 包重新打包并存在：`dist/GitHubAccelerator-win-x64.zip`。
9. 清理无关构建产物，确认 `.gitignore` 生效（不提交 `bin/obj/dist`）。
10. 提交信息清晰，推送前确认远程地址正确。
