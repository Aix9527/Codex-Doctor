# Changelog

## V9.0.0 功能重构版
- 独立重写主界面和核心服务，不引用 V8 程序集；保留历史目录。
- 将入口改为“一键修复重连”：识别实际监听端口、HTTPS 验证、创建/更新 .env、唯一备份、独立复检和回滚。
- 修复失效旧代理阻止新端口检测、带空格/export 的配置残留、取消后不回滚和以进程存在推断连接恢复的问题。
- 重建 GUI/CLI 身份区分、正常退出后携代理启动、官方安装包管理、PATH/只读修复、Git/npm 代理清理及迁移事务。
- 语言切换改用强类型 .NET Windows UI Automation，通过窗口夹具实际选中中英文并复检。
- 增加 31 项行为测试、窗口交互测试、独立发布脚本和 GitHub Actions 工作流。
- 外部安装/卸载、具体客户端语言控件及用户原始 Reconnecting 场景仍需实际验收；不承诺覆盖所有 Codex 故障。

## V8.2.0 Reconnecting 专项自愈
- 新增独立 **Reconnecting 自愈** 入口：修复前扫描 → 安全白名单计划 → 备份/执行/验证/必要时回滚 → 必要时 Desktop 重启 → fresh rescan → 确定性终态分类
- 新增 `ReconnectingRecoveryClassifier`，区分 `Recovered`、`NetworkRecovered`、`ProxyFailed`、`DnsFailed`、`TlsFailed`、`DesktopRestartFailed`、`ManualRequired`
- 新增专项计划生成器，白名单规范化为 `codex.proxy.env`、`git.proxy.clear`、`npm.proxy.clear`、`codex.desktop.restart`
- 固化代理安全边界：直连正常不写代理；未通过 HTTPS 验证的代理不写 `.codex/.env`；默认不写 Windows 用户级 HTTP/HTTPS 代理环境变量
- 专项执行继续复用既有 RepairEngine 的备份、执行后验证和失败回滚；无法安全构造的计划动作留下失败审计，不静默跳过为成功
- Desktop 重启在需要代理/配置变更时固定最后执行，取消后不再继续后续变更
- Reconnecting 自愈完成后必须执行独立 fresh rescan，不复用修复前扫描结果
- 主界面新增 V8.2.0 `Reconnecting 自愈` 按钮，运行时禁用冲突操作并记录每个动作/终态日志，完成后触发主扫描刷新
- “导出完整报告”自动附带最近一次 Reconnecting 自愈的修复前/后 ScanId、网络诊断、动作结果、验证/回滚证据和最终状态，同时继续执行用户目录标准化与敏感值脱敏
- 产品、关于页、CI artifact 与报告版本统一升级到 8.2.0
- 新增独立 `release-v8.2.0` 工作流；V8.1.2 Release 工作流转为固定 checkout `v8.1.2` tag 的手动历史复现，避免 V8.2 合并后误跑旧版本发布门

## V8.1.2 Desktop 稳定性修复
- 导出完整报告改用标准 Windows `SaveFileDialog`，允许自由选择保存目录；取消时零写入
- Desktop 卸载前重新扫描真实客户端，根据已确认的 `ChatGPT.exe` / `Codex.exe` 动态选择 `ChatGPT` / `Codex` 精确 winget 名称，不再把固定 Microsoft Store 安装 ID 当成所有已安装客户端的卸载身份
- Desktop 卸载仍只使用 winget，不直接删除应用目录；命令完成后重新扫描验证
- 中文 / English UI Automation 新增多进程窗口绑定：扫描 PID → 可信进程树 → 完全相同 EXE 路径；拒绝无关窗口和不可见窗口
- 语言后端不再只依赖 `Process.MainWindowHandle`
- 健康报告内部版本升级为 8.1.2，主窗口、关于页和产品版本统一为 V8.1.2
- V8.1.1 Release 工作流改为手动历史复现，并固定 checkout `v8.1.1`，避免 V8.1.2 合并时误触发旧版本门禁
- 新增独立 `release-v8.1.2` 工作流，继续坚持单文件、自包含、管理员 manifest、无运行时 PowerShell/外置 DLL、SHA256 和既有 Release 不覆盖

## V8.1.1 双向语言与安装管理
- 主界面将旧“一键中文”升级为 **中文 / English** 双向切换按钮，目标固定为 `zh-CN` / `en-US`
- 语言切换优先可信 Desktop UI 适配器；无可信适配器时仅对扫描确认的 Desktop EXE/PID 尝试 Windows UI Automation
- 语言操作必须重新读取/重新扫描验证；无法验证时不伪报成功，不修改未知数据库、普通 `.codex` 语言字段、MSIX/AppX 或二进制资源
- 新增 Codex Desktop / CLI 独立安装管理窗口，显示安装状态、路径与版本
- Desktop 自动安装/卸载只使用 Microsoft Store / winget 官方包 ID `9NT1R1C2HH7J`
- CLI 自动安装/卸载只使用 npm 官方包 `@openai/codex`
- winget/npm 缺失时返回 `ManualRequired`，不下载第三方安装器、不猜测来源
- 安装/卸载命令退出码不能单独判定成功，动作结束后必须重新 discovery 扫描验证
- Desktop / CLI 卸载默认保留 `.codex`、用户项目、Codex Doctor 备份与报告，并在 GUI 中二次确认
- 主界面 `安装 / 卸载` 与语言按钮均在扫描完成后启用；实际变更后回到主维修中心自动重新扫描
- 产品版本固定为 8.1.1；Windows CI / Release 继续验证管理员 manifest、PE MZ、真正单文件、无 PowerShell/外置 DLL 和 SHA256

## V8.1.0 原生维修中心
- 强制 Windows `requireAdministrator` / UAC 启动门，产品版本固定为 8.1.0
- 首页升级为“先扫描 → 问题分级 → 一键修复 → 自动复检”的维修中心
- 新增统一异步健康扫描编排，单项检查失败不会终止后续检查
- 支持 Critical / Urgent / Warning / Info / Ok 多问题并存与稳定排序
- 新增 RepairPlan、自动修复白名单、执行后验证、按动作回滚和修复后全量复检
- Git/npm 代理冲突、Codex 专用代理、语言等安全动作纳入统一修复编排
- Desktop 自动选择、真实 EXE 路径启动/重启，不依赖 `chatgpt:` URL 协议
- `.codex` 智能迁移/恢复升级为普通目录、有效 Junction、可恢复中断事务、歧义状态四类决策
- 一键中文增加“已是中文 / 可自动设置 / 需要用户操作”三态，不修改未知数据库、MSIX/AppX 或二进制资源
- 主界面提供启动、重启、一键修复、智能迁移/恢复、一键中文、导出完整报告六个主要操作
- 新增隐私安全 `HealthReportExporter`，报告记录 RepairPlan、验证/回滚、修复前后 ScanId，并将用户目录标准化为 `%USERPROFILE%`
- 健康报告对 Key/Token/Secret/Password/Cookie/Auth/Session/Bearer 等敏感内容脱敏
- GUI 公开显示软件作者 Aix / QQ / 抖音；健康报告只记录 `软件作者=Aix`
- Windows CI 强化管理员 manifest、8.1.0 版本、PE MZ、真正单文件、无 PowerShell/外置 DLL 和 SHA256 门禁

## V8 Native
- 使用 C# + .NET 8 + WinForms 原生重写，不再通过 PowerShell 启动器运行主程序
- 正式目标为 `win-x64`、self-contained、single-file 的 `CodexDoctor.exe`
- 用户无需安装 .NET，运行时不依赖 `.ps1` / `.psm1`
- GUI、弹窗、日志、诊断建议全部中文化
- DNS、直接 TLS、显式代理 HTTPS、TUN、Git/npm 代理冲突统一诊断
- 新增 `PROXY_REQUIRED` / “需要配置代理”语义：直连 TLS 失败但已验证本地代理可用且 `.codex/.env` 缺失时，不再误报 TLS 故障
- 真实 2026-09-05 诊断样本已加入回归测试
- `.codex/.env` 修复保留无关配置并自动备份旧文件
- Windows 用户环境变量写入默认关闭
- 保留 Codex 重启、`.codex` 迁移/恢复、报告导出、`codex doctor`、Git/npm 清理
- Windows CI 实际执行 `dotnet publish`，验证 PE 文件头、单文件发布、无 PowerShell 运行依赖并生成 SHA256

## V7.1.2 中文兼容版
- 保留 V7.1.2 PowerShell 实现作为历史/兼容版本
- GUI 标题、按钮、弹窗、日志、错误提示、诊断建议中文化
- 导出报告改为中文字段和中文状态说明
- 保留 UTF-8 BOM / Windows PowerShell 5.1 兼容修复
- 保留 Git/npm 单元素代理结果 `.Count` 回归修复

## V7.1
- Release Edition based on V7 Unified GUI
- Reproducible Windows EXE launcher build using pinned `ps2exe 1.0.18`
- Portable `Codex-Doctor-V7.1-Windows.zip` packaging
- SHA256 checksum generation for release verification
- Current-user installer and uninstaller scripts
- EXE launcher keeps PowerShell modules external and auditable instead of hiding runtime logic
- Windows CI validates generated EXE has a PE `MZ` header before packaging

## V7
- Unified WinForms GUI combining V5 repair/migration and V6 diagnosis
- RepairPlan decision engine separates diagnosis from mutation
- Health model with `Healthy`, `Warning`, and `Error`
- Confirmed `.codex/.env` proxy repair with backup preservation
- Optional Windows user proxy environment write (default off)
- Explicit Git/npm proxy cleanup actions
- `.codex` migration/restore with Junction safety checks
- CLI diagnosis mode with JSON output
- Windows CI runs V6 and V7 tests and validates `.ps1`/`.psm1`

## V6
- DNS resolution checks for `chatgpt.com` and `api.openai.com`
- TLS handshake diagnosis for `chatgpt.com:443`
- Explicit HTTP proxy route validation
- Clash Verge / Mihomo / sing-box process and TUN adapter detection
- Git global proxy mismatch detection
- npm proxy mismatch detection
- Deterministic failure classes: `DNS`, `TLS`, `PROXY`, `ENV_CONFLICT`, `HEALTHY`
- Read-only diagnosis mode with JSON output for automation

## V5
- Installer Edition
- GUI health indicator
- Admin privilege detection
- Restart Codex / ChatGPT Desktop
- Export diagnostics report
- Desktop and Start Menu shortcuts
- Uninstaller
- Optional PS2EXE build helper

## V4
- WinForms GUI
- One-click diagnosis and repair
- GUI migration / restore

## V3
- Clash Verge / Mihimo config discovery
- `mixed-port`, `port`, `socks-port` parsing
- Real HTTPS proxy validation

## V2
- `.codex` migration to another drive using NTFS Junction
- Migration state and rollback
- Proxy diagnostics

## V1
- Common local proxy port scan
- `.codex/.env` creation and backup
- HTTP(S)_PROXY user environment variables
