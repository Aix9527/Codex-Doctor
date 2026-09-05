# Codex Doctor V8.0.1：本机 Codex 自动发现、配置审计与中文设置设计

日期：2026-09-05

## 目标

为 Codex Doctor V8 Native 增加一套“本机 Codex 自动发现 + 配置审计 + 中文设置”子系统，解决当前程序仅通过固定路径或 PATH 判断 Codex 的局限，并提供安全的一键简体中文能力。

核心目标：

1. 自动发现本机 Codex / ChatGPT Desktop 的实际安装位置、安装类型、版本与运行进程。
2. 自动发现 Codex CLI 的真实路径与 PATH 可调用状态。
3. 自动发现 `.codex` 数据目录、Junction 状态、常见配置文件与网络/代理相关配置。
4. 自动识别当前客户端类型、可用语言设置能力，并提供“一键设置为简体中文”。
5. 明确区分“界面语言”“回答语言偏好”“终端/CLI 输出偏好”，不把其中一种误报为全部汉化成功。
6. 扫描结果默认只读，不修改注册表、PATH、配置文件或系统设置。
7. 敏感配置只报告“已配置/未配置”，不显示 token、API key、cookie、登录凭证原文。
8. 让“重启 Codex”和“运行 codex doctor”优先复用扫描结果，不再依赖硬编码路径或直接调用 `codex`。

## 当前问题

V8.0 的 `RestartCodexDesktop()` 目前仅检查少数固定路径，随后回退到 `chatgpt:` 协议；当用户安装位置不同或协议未注册时会失败。

V8.0 的 `RunCodexDoctor()` 直接执行 `codex doctor`，当 CLI 已安装但未加入当前进程 PATH 时会误判为“未安装”。

V8.0 也没有“客户端语言状态”模型，不能判断当前 Desktop/CLI 是否支持安全自动切换中文，更不能区分“UI 中文”和“模型默认中文回复”。

## 总体方案

新增三个独立服务：

- `CodexDiscoveryService.cs`：只读发现 Desktop、CLI、`.codex`、配置与网络环境。
- `CodexLanguageService.cs`：检测客户端语言能力、执行可逆的中文设置、恢复原语言。
- `CodexDiscoveryForm.cs`：展示扫描结果、语言状态、只读配置与操作入口。

`RepairService` 只保留修复动作，并接收发现服务返回的显式路径，不再自行猜测 Codex 安装位置。

## 扫描范围

### 1. Desktop 安装发现

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

不主动递归扫描整个磁盘。若常规发现为空，可增加“深度扫描”作为后续增强，不纳入 V8.0.1 默认流程。

### 2. Codex CLI 发现

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
- 是否支持 `codex doctor`（仅安全探测，不执行修复）。

### 3. `.codex` 数据目录与配置

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
- 与运行模式、语言偏好相关的明确非敏感布尔/枚举配置。
- 对未知字段只记录文件存在，不输出值。

敏感键名匹配 `TOKEN`、`KEY`、`SECRET`、`PASSWORD`、`COOKIE`、`AUTH`、`SESSION` 等时，仅显示“已配置”，绝不输出值。

### 4. 外部网络相关配置

复用现有诊断数据并附加到扫描结果：

- Windows 用户级代理环境变量。
- Git `http.proxy` / `https.proxy`。
- npm `proxy` / `https-proxy`。
- Clash Verge / Mihomo / sing-box 进程状态。
- TUN 网卡状态。

## 一键设置 Codex 为简体中文

### 语言状态模型

新增三个独立状态：

1. `界面语言`：Desktop UI 实际语言，如 `zh-CN`、`en-US`、未知。
2. `回答语言偏好`：Codex/ChatGPT 默认回复语言偏好。
3. `CLI输出偏好`：Codex CLI 的默认解释/总结语言偏好。

程序不得因为“回答语言偏好=中文”就声称“界面已中文化”。

### 自动设置规则

按钮名称：`一键设置 Codex 为简体中文`。

执行流程：

1. 先运行本机 Codex 扫描，识别实际客户端。
2. 调用 `CodexLanguageService.Detect()` 返回当前语言状态与可用设置方式。
3. 如果检测到官方/稳定的本地语言配置入口，先备份原始值，再写入 `zh-CN`。
4. 如果只支持应用内设置而没有稳定可写配置，程序不修改安装文件或内部数据库；显示纯中文引导，并打开安全可达的设置入口或目标应用。
5. 对 CLI/回答语言偏好，仅在识别到明确文本配置文件且可以安全、可逆地修改时写入中文偏好；否则只显示建议，不伪装为成功。
6. 修改完成后使用扫描得到的真实 Desktop EXE 路径重启对应客户端。
7. 重启后再次检测；只有检测结果确认 `界面语言=zh-CN` 时，才显示“Codex 界面已设置为简体中文”。

### 恢复原语言

提供 `恢复原语言`：

- 保存最后一次语言修改前的原始状态到 `%LOCALAPPDATA%\CodexDoctorV8\language-backup.json`。
- 备份文件不包含 token、cookie 或认证数据。
- 恢复时仅还原由 Codex Doctor 修改过的语言相关字段。

### 禁止行为

V8.0.1 不允许：

- 修改 Codex/ChatGPT 安装包资源文件实现“破解式汉化”。
- 修改签名后的 MSIX/AppX 包内容。
- 猜测未知数据库字段并直接写入。
- 删除应用数据或登录状态。
- 把“打开设置页面”误报为“已设置成功”。

## 数据模型

新增：

- `CodexDiscoveryResult`
- `CodexDesktopInstallationInfo`
- `CodexCliInfo`
- `CodexDataDirectoryInfo`
- `CodexConfigFileInfo`
- `CodexConfigEntryInfo`
- `CodexProcessInfo`
- `CodexLanguageState`
- `CodexLanguageCapability`
- `CodexLanguageBackup`

所有模型提供中文 JSON 属性名，保持 V8 当前报告可直接阅读的中文风格。

## GUI

主界面新增按钮：

- `扫描本机 Codex`
- `一键设置 Codex 为简体中文`

点击“扫描本机 Codex”后打开 `CodexDiscoveryForm`，分区展示：

1. Codex / ChatGPT Desktop
2. Codex CLI
3. `.codex` 数据目录
4. 配置文件
5. 网络与代理相关配置
6. 语言状态

语言区域显示：

- 当前客户端
- 当前界面语言
- 回答语言偏好
- CLI 输出偏好
- 是否支持自动设置
- 设置方式

提供操作：

- 重新扫描
- 一键设置为简体中文
- 恢复原语言
- 打开安装目录
- 打开 `.codex` 目录
- 复制扫描摘要
- 导出扫描报告

不存在的功能：

- 自动删除配置
- 自动改 PATH
- 自动改注册表中的无关项
- 自动清理用户数据

## 与现有功能集成

### 重启 Codex

`RepairService.RestartCodexDesktop(string executablePath, IReadOnlyCollection<int> processIds)`：

1. 优先关闭扫描确认的 Codex/ChatGPT 进程。
2. 使用发现到的真实 Desktop EXE 路径启动。
3. 不再把 `chatgpt:` URL 协议作为主路径。
4. 若完全未找到 Desktop，显示纯中文提示并引导先执行“扫描本机 Codex”。

### 运行 codex doctor

`RepairService.RunCodexDoctor(string cliPath)`：

1. 使用发现到的 CLI 真实路径。
2. PATH 可调用时扫描结果也会记录，但执行仍优先使用绝对路径。
3. CLI 不存在时显示：“已检测到 Codex Desktop，但未检测到 Codex CLI；此项可跳过，不影响网络诊断与代理修复。”
4. 不再把底层 .NET `Process.Start` 英文异常直接展示给用户。

## 安全与隐私

- 默认扫描只读。
- 语言修改属于显式用户动作，必须由按钮触发。
- 不读取浏览器 cookie、Windows Credential Manager、登录 token 数据库或任何未明确允许的认证存储。
- 不输出 API key、access token、refresh token、cookie、密码等敏感值。
- 报告中的敏感配置仅输出 `已配置=true/false`。
- 所有外部命令使用超时、无窗口模式并限制参数。
- 所有语言修改必须可回滚；不具备稳定可逆路径时只能引导，不能写入。

## TDD / 验收条件

新增回归测试必须先 RED，再实现 GREEN：

1. 从临时目录候选中找到 Desktop EXE 并返回版本/路径。
2. 从模拟 PATH 找到 Codex CLI，并区分 PATH 可调用与仅路径可发现。
3. `.codex` 目录识别普通目录与 Junction/Reparse Point。
4. `.env` 中代理值可显示，敏感键必须脱敏。
5. 扫描报告字段为中文，敏感值不出现在 JSON。
6. `RunCodexDoctor` 使用显式 CLI 路径，而不是硬编码 `codex`。
7. `RestartCodexDesktop` 使用发现到的 Desktop 路径，不依赖 `chatgpt:` 协议。
8. 语言检测必须区分 UI 语言、回答语言偏好、CLI 输出偏好。
9. 当存在可写语言配置时，设置 `zh-CN` 前必须生成语言备份，设置后能恢复原值。
10. 当不存在安全可写配置时，服务必须返回 `NeedsUserAction=true`，且不得报告 `Applied=true`。
11. 扫描/语言报告不得出现敏感值。
12. Windows GitHub Actions 上完整 V8 Native 测试、发布、单文件门禁继续通过。

## 版本与发布

版本：V8.0.1

产物继续保持：

- `CodexDoctor.exe`
- `CodexDoctor.exe.sha256`

使用现有 .NET 8 `win-x64`、self-contained、single-file 发布规则，并创建独立 `v8.0.1` Release。