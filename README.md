# Codex Proxy Switcher for Windows

project: [hloolx/codex-proxy-switcher-win](https://github.com/hloolx/codex-proxy-switcher-win)

学 AI 上 L 站：[linux.do](https://linux.do/)

Codex Proxy Switcher for Windows 是一个轻量级 Windows 启动器，用来在启动 OpenAI Codex 桌面端前选择网络模式：

- 原生启动：不注入代理环境变量，按 Codex 默认网络环境启动。
- VPN 启动：激活 Codex 前临时设置 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 和 `NO_PROXY`，启动后立即恢复。

它不会修改 Windows 全局代理，也不会写入系统级环境变量。为兼容 Windows 商店应用激活，程序会在启动窗口内短暂调整当前用户环境变量，激活 Codex 后恢复原值。

## 功能

- 首次运行输入本地代理端口，例如 `7897`、`7890`、`9000`。
- 端口保存到 `%APPDATA%\CodexProxySwitcher\settings.json`，以后自动读取。
- 自动查询 Windows 商店版 Codex 的安装路径。
- 支持“原生启动”和“VPN 启动”两种模式。
- 可随时在界面中修改代理端口。
- 首次配置完成后可选择创建桌面快捷方式，只提示一次。
- 保留一键启动用的 `.bat` / `.ps1` 脚本。

## 直接下载使用

最方便的方式是直接下载已经打包好的 exe：

- 仓库内置版：[release/Codex-Proxy-Switcher.exe](release/Codex-Proxy-Switcher.exe)
- 直接下载链接：[Codex-Proxy-Switcher.exe](https://github.com/hloolx/codex-proxy-switcher-win/raw/main/release/Codex-Proxy-Switcher.exe)
- GitHub Release 版：[CodexProxySwitcher-win-x64-v0.1.6.zip](https://github.com/hloolx/codex-proxy-switcher-win/releases/latest)

下载后双击 `Codex-Proxy-Switcher.exe` 即可。这个 exe 是自包含 win-x64 版本，普通用户不需要自己安装 .NET Runtime，也不需要自己编译。

普通用户只需要这个单独的 exe 文件就可以完成主要功能：第一次运行时填写本地代理端口，之后它会自动保存配置、自动查找 Codex 安装位置，并让你选择“原生启动”或“VPN 启动”。

如果 Windows 弹出 SmartScreen 提示，这是因为个人开源 exe 没有代码签名。确认来源是本仓库后，可以选择“更多信息”然后继续运行。

## 它主要解决什么问题

这个工具主要解决 Windows 桌面版 Codex 在没有官方代理入口时的网络启动问题。

典型场景：

- Codex 桌面端一直显示 `Reconnecting`。
- Codex 可以打开，但请求模型时反复断线或连接失败。
- 电脑上的代理软件已经运行，但 Codex 没有自动走代理。
- 新安装电脑直接启动 WindowsApps 里的 `Codex.exe` 时提示“拒绝访问”。
- 不想修改 Windows 全局代理，只想让 Codex 这一个进程走代理。
- 不想每次手写 `.ps1` / `.bat`，希望第一次填端口后长期复用。

它的做法是在启动 Codex 前，为 Codex 激活过程临时设置：

```txt
HTTP_PROXY=http://127.0.0.1:<port>
HTTPS_PROXY=http://127.0.0.1:<port>
ALL_PROXY=http://127.0.0.1:<port>
NO_PROXY=localhost,127.0.0.1,::1
```

这样可以让支持这些环境变量的网络请求走本地代理。它不会改系统代理，用户级环境变量也会在激活 Codex 后恢复。

启动器会优先使用 Codex 的 AppUserModelID 进行系统应用激活，避免直接运行 `C:\Program Files\WindowsApps\...\app\Codex.exe` 时触发 Electron 路径/参数解析问题。

## 不能保证解决的情况

如果 `Reconnecting` 是下面这些原因造成的，这个工具不一定能解决：

- OpenAI / Codex 服务端故障。
- 账号、订阅、额度、组织权限或风控问题。
- 代理软件没有运行，端口填错，或代理规则不允许访问 OpenAI。
- 登录流程打开的是外部浏览器，而外部浏览器没有走代理。
- 公司网络使用 TLS 中间代理但没有配置可信 CA。
- Codex 官方后续改了包名、安装目录或不再读取标准代理环境变量。

## ChatGPT 登录和 API Key 登录

根据 OpenAI Codex 认证文档，Codex 支持两种 OpenAI 模型登录方式：ChatGPT 登录和 API key 登录；Codex app、CLI、IDE Extension 都可以使用 API key 登录。官方文档也说明，Codex 使用 ChatGPT 登录时通常会打开浏览器完成登录流程。

本工具不负责创建、保存或切换 API key，也不修改 Codex 的认证方式。它只做一件事：以原生模式或代理模式启动 Windows 桌面版 Codex。

实际影响可以这样理解：

- 如果你已经登录成功，但 Codex 使用时经常 `Reconnecting`，VPN 启动模式通常最值得尝试。
- 如果你使用 API key 登录/调用，而失败原因是 Codex 进程无法连到 OpenAI，VPN 启动模式可能有帮助。
- 如果失败发生在外部浏览器登录页面打不开，可能还需要让系统浏览器或系统代理也能访问 OpenAI。
- 如果是 API key 无效、额度不足、组织限制、模型权限问题，这个工具不会解决。

参考：OpenAI Codex Authentication 文档  
https://developers.openai.com/codex/auth

## 为什么这样做

Windows 桌面版 Codex 当前没有一个稳定、显式的“只给 Codex 配代理”的官方入口。直接改 Windows 全局代理会影响浏览器、下载器、Git、包管理器和其他应用，排查起来很麻烦。

这个启动器选择“临时环境变量 + 系统应用激活”的方式，有几个好处：

- 范围小：只在 Codex 激活窗口内调整代理变量，并在启动后恢复。
- 可回退：点“原生启动”就不注入代理变量。
- 可迁移：第一次输入端口后保存在当前 Windows 用户目录。
- 更安全：不写系统环境变量，不要求长期管理员权限。
- 更直观：启动前明确选择“原生启动”或“VPN 启动”。

## 最佳使用方式

推荐流程：

1. 先启动你的代理软件，确认本地 HTTP 代理端口，例如 `7897`、`7890` 或 `9000`。
2. 第一次运行 `Codex-Proxy-Switcher.exe`，只输入端口号，不需要输入完整 URL。
3. 平时优先点击“VPN 启动”打开 Codex。
4. 如果你在国内外网络之间切换，或代理软件关闭了，点击“原生启动”。
5. 如果端口变化，点“修改端口”更新一次即可。
6. 不要同时从原始 Codex 图标和本工具启动 Codex；建议只固定本工具到任务栏。

## 使用

如果你拿到的是编译好的 exe：

1. 双击 `Codex-Proxy-Switcher.exe`。
2. 第一次输入你的本地代理端口。
3. 选择“原生启动”或“VPN 启动”。

如果你使用脚本版本：

1. 修改 `proxy-url.txt`，例如：

   ```txt
   http://127.0.0.1:7897
   ```

2. 双击 `Start-Codex-VPN.bat`。

## 开发者构建

需要 .NET 9 SDK。

如果当前机器只有 .NET Runtime，没有 SDK，可以把 .NET 9 SDK 安装到仓库本地目录，不需要写入系统目录：

```powershell
$InstallDir = Join-Path (Get-Location) ".dotnet"
$Script = Join-Path $env:TEMP "dotnet-install.ps1"
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $Script
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Script -Channel 9.0 -Architecture x64 -InstallDir $InstallDir
```

安装完成后，使用仓库内 SDK 构建。这里把 .NET 首次运行目录和 NuGet 缓存也放到仓库内，避免写入用户目录时遇到权限问题：

```powershell
$root = Get-Location
$env:PATH = (Join-Path $root ".dotnet") + ";" + $env:PATH
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $root ".nuget\packages"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

如果 NuGet restore 在受限环境中失败，需要允许本次构建访问 `https://api.nuget.org/v3/index.json`。

```powershell
.\build.ps1
```

构建产物会输出到：

```txt
dist\Codex-Proxy-Switcher.exe
```

如果需要自包含版本：

```powershell
.\build.ps1 -SelfContained
```

## Codex 查找逻辑

启动器会按顺序尝试：

1. 通过 PowerShell 的 `Get-AppxPackage -Name OpenAI.Codex` 查询安装目录和 Package Family Name。
2. 读取 `AppxManifest.xml` 获取 Application Id，生成类似 `OpenAI.Codex_2p2nqsd0c76g0!App` 的 AppUserModelID。
3. 优先通过 AppUserModelID 激活 Codex，避免绕过 Windows 商店应用启动链路。
4. VPN 模式会在激活前短暂设置当前用户级代理环境变量，激活后立即恢复原值。
5. 如果 Appx 查询失败，再在 `C:\Program Files\WindowsApps` 中查找 `OpenAI.Codex_*_x64__2p2nqsd0c76g0`。

如果 Codex 官方后续更改包名或目录结构，需要更新匹配规则。

## 类似项目调研

这个项目和现有项目的区别在于：它只做一件事，让 Windows 版 Codex 桌面端启动时可选择是否注入本地代理环境变量。

- [openai/codex](https://github.com/openai/codex)：OpenAI 官方 Codex 项目，包含 CLI、文档和核心实现。
- [Finesssee/ProxyPilot](https://github.com/Finesssee/ProxyPilot)：面向 AI 编程工具的 Windows-native CLIProxyAPI 分支，范围更大，包含 TUI、系统托盘和多提供商 OAuth。
- [siddhantparadox/codexmanager](https://github.com/siddhantparadox/codexmanager)：Codex 配置和资产管理器，重点是编辑 Codex 本地配置、技能和会话。
- [MisakiMei-hub/fmclient-codex-launchers](https://github.com/MisakiMei-hub/fmclient-codex-launchers)：包含固定端口的 Codex 代理启动修复；本项目进一步提供首次端口配置、持久化和原生/VPN 模式选择。

## 许可证

MIT License
