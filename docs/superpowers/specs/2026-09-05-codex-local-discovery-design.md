# Codex Doctor V8.0.1：本机 Codex 自动发现与配置审计设计

日期：2026-09-05

## 目标

为 Codex Doctor V8 Native 增加一套只读的“本机 Codex 自动发现与配置审计”子系统，解决当前程序仅通过固定路径或 PATH 判断 Codex 的局限。

核心目标：

1. 自动发现本机 Codex / ChatGPT Desktop 的实际安装位置、安装类型、版本与运行进程。
2. 自动发现 Codex CLI 的真实路径与 PATH 可调用状态。
3. 自动发现 `.codex` 数据目录、Junction 状态、常见配置文件与网络/代理相关配置。
4. 扫描结果默认只读，不修改注册表、PATH、配置文件或系统设置。
5. 敏感配置只报告“已配置/未配置”，不显示 token、API key、cookie、登录凭证原文。
6. 让“重启 Codex”和“运行 codex doctor”优先复用扫描结果，不再依赖硬编码路径或直接调用 `codex`。

## 当前问题

V8.0 的 `RestartCodexDesktop()` 目前仅检查少数固定路径，随后回退到 `chatgpt:` 协议；当用户安装位置不同或协议未注册时会失败。

V8.0 的 `RunCodexDoctor()` 直接执行 `codex doctor`，当 CLI 已安装但未加入当前进程 PATH 时会误判为“未安装”。

## 方案

新增 `CodexDiscoveryService.cs` 作为独立发现层，避免把扫描逻辑继续堆进 `RepairService` 或 GUI。

### 扫描范围

#### 1. Desktop 安装发现

按可靠性从高到低收集候选项：

- 当前运行中的 `Codex` / `ChatGPT` 进程及其 `MainModule.FileName`。
- `App Paths` 注册信息（只读）。
- 已知用户安装目录，如 `%LOCALAPPDATA%\Programs\Codex`、`%LOCALAPPDATA%\Programs\ChatGPT`。
- `Program Files` / `Program Files (x86)` 常见安装目录。
- MSIX / Microsoft Store 包注册信息（只读）。
- Start Menu / Desktop 快捷方式目标（如可安全解析）。

每个候选项记录：

- 产品类型：Codex Desktop / ChatGPT Desktop / 未知兼容客户端。
- 安装来源：MSIX/Store、传统安装、进程发现、快捷方式等。
- 可执行文件路径。
- 文件版本 / 产品版本。
- 是否正在运行。
- 进程 ID 列表。

不主动递归扫描整个磁盘，避免性能问题。若常规发现为空，可增加“深度扫描”选项作为后续增强，不纳入 V8.0.1 默认流程。

#### 2. Codex CLI 发现

检查：

- `PATH` 中 `codex.exe` / `codex.cmd` / `codex.bat`。
- `where.exe codex` 结果。
- npm 全局前缀/常见用户 npm bin 目录。
- `%APPDATA%\npm`。
- 常见用户级安装目录。

结果记录：

- 是否发现 CLI。
- 实际可执行/命令入口路径。
- 是否能从当前 PATH 直接调用。
- `codex --version` 的安全版本输出（超时保护）。
- 是否支持 `codex doctor`（仅通过安全探测，不执行修复）。

#### 3. `.codex` 数据目录与配置

检查默认 `%USERPROFILE%\.codex`，并识别：

- 目录是否存在。
- 是否为 Junction / Reparse Point。
- Junction 目标路径（若可读取）。
- 目录大小与文件数量（设置合理上限和异常保护）。
- `.env` 是否存在。
- `config.toml` 等已知文本配置是否存在。
- 迁移状态文件是否存在。

配置扫描仅提取允许字段：

- `HTTP_PROXY` / `HTTPS_PROXY` / `ALL_PROXY` / `NO_PROXY`。
- 与运行模式相关的非敏感布尔/枚举配置。
- 对未知字段只记录文件存在，不输出值。

敏感键名匹配 `TOKEN`、`KEY`、`SECRET`、`PASSWORD`、`COOKIE`、`AUTH`、`SESSION` 等时，仅显示“已配置”，绝不输出值。

#### 4. 外部网络相关配置

复用现有诊断数据并附加到扫描结果：

- Windows 用户级代理环境变量。
- Git `http.proxy` / `https.proxy`。
- npm `proxy` / `https-proxy`。
- Clash Verge / Mihomo / sing-box 进程状态。
- TUN 网卡状态。

## 数据模型

新增：

- `CodexDiscoveryResult`
- `CodexDesktopInstallationInfo`
- `CodexCliInfo`
- `CodexDataDirectoryInfo`
- `CodexConfigFileInfo`
- `CodexConfigEntryInfo`
- `CodexProcessInfo`

所有模型提供中文 JSON 属性名，保持 V8 当前报告可直接阅读的中文风格。

## GUI

主界面新增按钮：`扫描本机 Codex`。

点击后打开 `CodexDiscoveryForm`，分区展示：

1. Codex / ChatGPT Desktop
2. Codex CLI
3. `.codex` 数据目录
4. 配置文件
5. 网络与代理相关配置

提供只读操作：

- 重新扫描
- 打开安装目录
- 打开 `.codex` 目录
- 复制扫描摘要
- 导出扫描报告

不存在的功能：

- 自动删除配置
- 自动改 PATH
- 自动改注册表
- 自动清理用户数据

## 与现有功能集成

### 重启 Codex

`RepairService.RestartCodexDesktop()` 调整为接收发现结果或可执行路径：

1. 优先重启当前正在运行且被发现的 Desktop 实例。
2. 若没有正在运行实例，使用发现到的最高可信安装路径启动。
3. 不再把 `chatgpt:` URL 协议作为主路径。
4. 若完全未找到 Desktop，显示纯中文提示并引导先执行“扫描本机 Codex”。

### 运行 codex doctor

1. 先使用发现到的 CLI 真实路径。
2. PATH 可调用时可直接执行。
3. CLI 不存在时显示：“已检测到 Codex Desktop，但未检测到 Codex CLI；此项可跳过，不影响网络诊断与代理修复。”
4. 不再把底层 .NET `Process.Start` 英文异常直接展示给用户。

## 安全与隐私

- 默认扫描只读。
- 不读取浏览器 cookie、Windows Credential Manager、登录 token 数据库或任何未明确允许的认证存储。
- 不输出 API key、access token、refresh token、cookie、密码等敏感值。
- 报告中的敏感配置仅输出 `已配置=true/false`。
- 所有外部命令使用超时、无窗口模式并限制参数。

## TDD / 验收条件

新增回归测试必须先 RED，再实现 GREEN：

1. 从临时目录候选中找到 Desktop EXE 并返回版本/路径。
2. 从模拟 PATH 找到 Codex CLI，并区分 PATH 可调用与仅路径可发现。
3. `.codex` 目录识别普通目录与 Junction/Reparse Point。
4. `.env` 中代理值可显示，敏感键必须脱敏。
5. 扫描报告字段为中文，敏感值不出现在 JSON。
6. `RunCodexDoctor` 使用显式 CLI 路径，而不是硬编码 `codex`。
7. `RestartCodexDesktop` 使用发现到的 Desktop 路径，不依赖 `chatgpt:` 协议。
8. Windows GitHub Actions 上完整 V8 Native 测试、发布、单文件门禁继续通过。

## 版本与发布

版本：V8.0.1

建议产物继续保持：

- `CodexDoctor.exe`
- `CodexDoctor.exe.sha256`

使用现有 .NET 8 `win-x64`、self-contained、single-file 发布规则，并创建独立 `v8.0.1` Release。