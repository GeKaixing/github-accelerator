import Cocoa

final class ConnectivityPopoverController: NSViewController {
    private let targets: [(name: String, domain: String)]
    private var statusLabels: [String: NSTextField] = [:]
    private var buttonByDomain: [String: NSButton] = [:]

    var testDomainHandler: ((String, @escaping (Bool, Int, Int) -> Void) -> Void)?
    var closeHandler: (() -> Void)?

    init(targets: [(name: String, domain: String)]) {
        self.targets = targets
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) {
        return nil
    }

    override func loadView() {
        let root = NSView(frame: NSRect(x: 0, y: 0, width: 460, height: 520))

        let stack = NSStackView()
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 8
        stack.translatesAutoresizingMaskIntoConstraints = false

        let title = NSTextField(labelWithString: "延迟测试")
        title.font = NSFont.boldSystemFont(ofSize: 15)
        stack.addArrangedSubview(title)

        let topRow = NSStackView()
        topRow.orientation = .horizontal
        topRow.spacing = 8

        let testAllBtn = NSButton(title: "测试全部", target: self, action: #selector(runAllTests(_:)))
        testAllBtn.bezelStyle = .rounded
        topRow.addArrangedSubview(testAllBtn)

        let resetBtn = NSButton(title: "重置结果", target: self, action: #selector(resetResults(_:)))
        resetBtn.bezelStyle = .rounded
        topRow.addArrangedSubview(resetBtn)

        let closeBtn = NSButton(title: "关闭", target: self, action: #selector(closePanel(_:)))
        closeBtn.bezelStyle = .rounded
        topRow.addArrangedSubview(closeBtn)

        stack.addArrangedSubview(topRow)
        stack.addArrangedSubview(separator())

        for t in targets {
            let row = NSStackView()
            row.orientation = .horizontal
            row.alignment = .centerY
            row.spacing = 8

            let name = NSTextField(labelWithString: t.name)
            name.frame = NSRect(x: 0, y: 0, width: 220, height: 22)
            name.translatesAutoresizingMaskIntoConstraints = false
            name.widthAnchor.constraint(equalToConstant: 220).isActive = true

            let status = NSTextField(labelWithString: "未测试")
            status.textColor = .secondaryLabelColor
            status.translatesAutoresizingMaskIntoConstraints = false
            status.widthAnchor.constraint(equalToConstant: 130).isActive = true

            let btn = NSButton(title: "测试", target: self, action: #selector(runSingleTest(_:)))
            btn.bezelStyle = .rounded
            btn.identifier = NSUserInterfaceItemIdentifier(t.domain)

            row.addArrangedSubview(name)
            row.addArrangedSubview(status)
            row.addArrangedSubview(btn)
            stack.addArrangedSubview(row)

            statusLabels[t.domain] = status
            buttonByDomain[t.domain] = btn
        }

        root.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 14),
            stack.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -14),
            stack.topAnchor.constraint(equalTo: root.topAnchor, constant: 12),
            stack.bottomAnchor.constraint(lessThanOrEqualTo: root.bottomAnchor, constant: -12)
        ])

        self.view = root
    }

    @objc private func runAllTests(_ sender: Any?) {
        runAllSerial(index: 0, okCount: 0)
    }

    @objc private func resetResults(_ sender: Any?) {
        for (_, label) in statusLabels {
            label.stringValue = "未测试"
            label.textColor = .secondaryLabelColor
        }
    }

    @objc private func closePanel(_ sender: Any?) {
        closeHandler?()
    }

    @objc private func runSingleTest(_ sender: NSButton) {
        guard let id = sender.identifier?.rawValue else { return }
        runSingle(id)
    }

    private func runSingle(_ domain: String, completion: (() -> Void)? = nil) {
        guard let label = statusLabels[domain] else {
            completion?()
            return
        }
        label.stringValue = "测试中..."
        label.textColor = .systemOrange

        testDomainHandler?(domain) { ok, ms, statusCode in
            DispatchQueue.main.async {
                if ok {
                    label.stringValue = "\(ms) ms"
                    label.textColor = .systemGreen
                } else {
                    label.stringValue = "失败"
                    label.textColor = .systemRed
                }
                completion?()
            }
        }
    }

    private func runAllSerial(index: Int, okCount: Int) {
        if index >= targets.count {
            return
        }
        let d = targets[index].domain
        runSingle(d) { [weak self] in
            guard let self else { return }
            let ok = self.statusLabels[d]?.textColor == .systemGreen
            self.runAllSerial(index: index + 1, okCount: ok ? (okCount + 1) : okCount)
        }
    }

    private func separator() -> NSView {
        let line = NSBox()
        line.boxType = .separator
        line.translatesAutoresizingMaskIntoConstraints = false
        line.heightAnchor.constraint(equalToConstant: 1).isActive = true
        return line
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!
    private var serviceProcess: Process?
    private var statusLineItem: NSMenuItem?
    private var connectivityPopover: NSPopover?

    private let testTargets: [(name: String, domain: String)] = [
        ("Github Dev", "github.dev"),
        ("Github Api", "api.github.com"),
        ("Github Assets", "github.githubassets.com"),
        ("Github Education", "education.github.com"),
        ("Github Resources", "resources.github.com"),
        ("Github Uploads", "uploads.github.com"),
        ("Github Archiveprogram", "archiveprogram.github.com"),
        ("Github UserContent", "githubusercontent.com"),
        ("Github 网站 (Git Push)", "github.com"),
        ("Github App", "githubapp.com"),
        ("Github.io", "github.io"),
    ]

    private let proxyListen = "127.0.0.1:8999"
    private let guiListen = "127.0.0.1:19010"

    func applicationDidFinishLaunching(_ notification: Notification) {
        setupStatusItem()
        startService()
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) {
            self.refreshStatus(nil)
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        stopService()
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        openPanel(nil)
        return false
    }

    private func setupStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = statusItem.button {
            button.title = "GA"
            button.toolTip = "GitHub Accelerator"
        }

        let menu = NSMenu()
        let statusLine = NSMenuItem(title: "状态: 读取中...", action: nil, keyEquivalent: "")
        statusLine.isEnabled = false
        statusLineItem = statusLine

        menu.addItem(statusLine)
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "一键加速", action: #selector(enableProxy(_:)), keyEquivalent: "e"))
        menu.addItem(NSMenuItem(title: "关闭加速", action: #selector(disableProxy(_:)), keyEquivalent: "d"))
        menu.addItem(NSMenuItem(title: "刷新", action: #selector(refreshStatus(_:)), keyEquivalent: "f"))
        menu.addItem(NSMenuItem(title: "延迟测试", action: #selector(showConnectivityPanel(_:)), keyEquivalent: "t"))

        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "Open Panel", action: #selector(openPanel(_:)), keyEquivalent: "o"))
        menu.addItem(NSMenuItem(title: "Restart Service", action: #selector(restartService(_:)), keyEquivalent: "r"))
        menu.addItem(NSMenuItem.separator())
        menu.addItem(NSMenuItem(title: "Quit", action: #selector(quitApp(_:)), keyEquivalent: "q"))

        statusItem.menu = menu
    }

    @objc private func openPanel(_ sender: Any?) {
        guard let url = URL(string: "http://\(guiListen)") else { return }
        NSWorkspace.shared.open(url)
    }

    @objc private func showConnectivityPanel(_ sender: Any?) {
        guard let button = statusItem.button else { return }

        if connectivityPopover == nil {
            let vc = ConnectivityPopoverController(targets: testTargets)
            vc.testDomainHandler = { [weak self] domain, completion in
                self?.testDomain(domain, completion: completion)
            }
            vc.closeHandler = { [weak self] in
                self?.connectivityPopover?.performClose(nil)
            }
            let pop = NSPopover()
            pop.behavior = .applicationDefined
            pop.contentViewController = vc
            connectivityPopover = pop
        }

        guard let pop = connectivityPopover else { return }
        if pop.isShown {
            pop.performClose(nil)
        } else {
            pop.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }

    @objc private func enableProxy(_ sender: Any?) {
        callPost("/api/git/enable")
    }

    @objc private func disableProxy(_ sender: Any?) {
        callPost("/api/git/disable")
    }

    @objc private func refreshStatus(_ sender: Any?) {
        callGetStatus()
    }

    @objc private func restartService(_ sender: Any?) {
        stopService()
        startService()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) {
            self.refreshStatus(nil)
            self.openPanel(nil)
        }
    }

    @objc private func quitApp(_ sender: Any?) {
        NSApp.terminate(nil)
    }

    private func startService() {
        guard serviceProcess == nil else { return }
        guard let bin = runtimeBinaryPath() else { return }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: bin)
        process.arguments = [
            "gui",
            "--listen", proxyListen,
            "--gui-listen", guiListen,
            "--open-browser", "false",
            "--github-only", "true"
        ]

        let logPath = "/tmp/github-accelerator-gui.log"
        FileManager.default.createFile(atPath: logPath, contents: nil)
        if let handle = FileHandle(forWritingAtPath: logPath) {
            handle.seekToEndOfFile()
            process.standardOutput = handle
            process.standardError = handle
        }

        do {
            try process.run()
            serviceProcess = process
        } catch {
            NSLog("[GitHubAccelerator] failed to start service: \(error)")
        }
    }

    private func stopService() {
        guard let process = serviceProcess else { return }
        if process.isRunning {
            process.terminate()
            process.waitUntilExit()
        }
        serviceProcess = nil
    }

    private func runtimeBinaryPath() -> String? {
        guard let resourceURL = Bundle.main.resourceURL else { return nil }
        let bin = resourceURL.appendingPathComponent("runtime/github-accelerator").path
        return FileManager.default.isExecutableFile(atPath: bin) ? bin : nil
    }

    private func endpoint(_ path: String) -> URL? {
        URL(string: "http://\(guiListen)\(path)")
    }

    private func callPost(_ path: String) {
        guard let url = endpoint(path) else { return }
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        URLSession.shared.dataTask(with: req) { _, _, _ in
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
                self.refreshStatus(nil)
            }
        }.resume()
    }

    private func callGetStatus() {
        guard let url = endpoint("/api/status") else { return }
        URLSession.shared.dataTask(with: url) { data, _, _ in
            var enabled = false
            if let data,
               let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let gitProxy = obj["git_proxy"] as? [String: Any],
               let e = gitProxy["enabled"] as? Bool {
                enabled = e
            }

            DispatchQueue.main.async {
                self.statusLineItem?.title = enabled ? "状态: 已加速" : "状态: 未加速"
                self.statusItem.button?.title = enabled ? "GA+" : "GA"
            }
        }.resume()
    }

    private func testDomain(_ domain: String, completion: @escaping (Bool, Int, Int) -> Void) {
        guard let escaped = domain.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = endpoint("/api/test/domain?domain=\(escaped)") else {
            completion(false, 0, 0)
            return
        }

        URLSession.shared.dataTask(with: url) { data, _, _ in
            guard let data,
                  let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                completion(false, 0, 0)
                return
            }
            let ok = (obj["ok"] as? Bool) ?? false
            let ms = (obj["ms"] as? Int) ?? Int((obj["ms"] as? Double) ?? 0)
            let statusCode = (obj["status"] as? Int) ?? 0
            completion(ok, ms, statusCode)
        }.resume()
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
