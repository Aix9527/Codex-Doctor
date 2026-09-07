# Codex Doctor V9 功能重构版

V9 重写主界面、代理发现、重连修复、配置事务、进程执行、客户端发现、安装管理、迁移恢复和语言设置自动化。V8 保留为历史版本。V9 已独立，不引用 V8 程序集，不加载旧主界面，也不通过反射添加按钮。

## 一键修复重连

1. 启动本地代理软件，启用 HTTP 或 mixed 混合端口。
2. 双击 `CodexDoctorV9.exe`，核对数据目录（默认 `%USERPROFILE%\.codex`）。
3. 点击 **一键修复重连**。无需事先扫描，不依赖固定端口列表。
4. 程序按当前 Windows 代理、进程代理环境、旧配置和实际监听端口发现候选。无效旧代理不会阻止继续搜索；本机 DNS 或直连失败不会阻止代理 HTTPS 验证。
5. 找到有效代理后，备份并创建/更新 `.env` 中的 `HTTP_PROXY`、`HTTPS_PROXY` 及其小写形式。其他配置（包括自定义 `NO_PROXY`）保留。
6. 独立重读配置并再次检查代理。失败或取消时撤销本次写入；若用户已再次修改文件，则拒绝覆盖并提示手动恢复。
7. 保存任务后完全退出并重新打开 Codex；也可先选择客户端，再点击 **重启并应用代理**。Doctor 会把代理加入新进程环境，不修改 Windows 全局代理。

重连按钮负责用户明确要求的“发现代理并修复 .env”。它不自动强杀正在运行的客户端。HTTP 403 可能证明 HTTPS 隧道存在，但不能证明账号、地区、服务状态或会话可用；界面不会因此显示“Reconnecting 已恢复”。

目前自动写入仅支持无凭据的本机 HTTP/混合代理（含 IPv6 loopback）。仅 SOCKS、认证代理或 PAC-only 配置需先在代理软件启用 HTTP/混合监听端口。更改 `.env` 是否由特定 Desktop 版本自动读取仍需实测；“重启并应用代理”另行注入子进程环境。

## 其他操作

| 入口 | 实际行为 |
| --- | --- |
| 一键扫描 | Desktop/CLI/工具入口、数据目录、配置元信息、磁盘、DNS、直连 TLS、TUN 线索、实际代理 HTTPS |
| 启动 / 重启 | 核对真实 GUI 可执行文件、重新核对进程身份、正常退出、启动后查验进程 |
| 安装 / 卸载 | 调用官方 winget / npm，并验证包 ID 或 CLI 版本；失败不报成功 |
| 修复 CLI PATH | 备份并补齐用户 PATH，保留其他项，重读验证 |
| 修复配置只读 | 仅移除 .env/config.toml 的 ReadOnly 属性，保留内容；不放宽 ACL |
| 清理 Git/npm 代理 | 备份默认用户配置，只删除代理项，独立读取验证，失败恢复 |
| 迁移 / 恢复 | 退出 Codex 后复制校验、保留原目录、创建并核对 Junction；恢复最新目标数据 |
| 撤销上次代理修复 | 校验配置哈希和目标路径后恢复备份；支持程序重启后读取修复记录 |
| 切换中文 / 英文 | 操作客户端自身设置控件；控件不兼容时明确未完成，不伪造语言配置 |
| 导出报告 | 导出检查结果、执行记录、代理证据和备份位置，不导出配置原文或凭据 |

普通用户即可运行。安装工具可能自行请求权限；迁移路径不允许互相嵌套或含嵌套链接。迁移副本和历史备份不自动删除。

## 验证范围

- 31 项行为测试使用临时目录和本地服务夹具，包含真实 Git 配置操作、Junction 迁移恢复、进程超时、代理配置回滚；另有真实 WinForms 窗口的 UI Automation 流程测试，验证中文和英文选项确实被选中。
- 真实本地代理集成测试只在临时目录写 `.env`，不触碰使用中的 Codex 配置。
- 安装/卸载外部软件、UI 语言切换以及用户原先的 Reconnecting 故障，需要对应客户端场景验收。不得用单元测试结果替代这些验收。
- 登录失效、额度不足、服务故障不会通过改本地配置自动修复。配置语法/MCP 语义自动修复尚未包含；不会随意重置用户模型和 MCP 配置。

## 构建

```powershell
dotnet run --project versions/v9/tests/Tests.csproj -c Release
./versions/v9/Build.ps1 -OutputDirectory ./work/publish-v9
```

输出目录必须为空。脚本运行行为测试、发布、自包含 EXE 自检，生成 SHA256。真实代理集成检查：

```powershell
dotnet run --project versions/v9/tests/Tests.csproj -c Release -- --live-proxy
```

## 官方依据

- [OpenAI Windows 文档](https://learn.chatgpt.com/docs/windows/windows-app)：Store 包 `9PLM9XGG6VKS`、Windows 数据目录和客户端设置（2026-09-08 核对）。
- [Codex CLI 官方源码](https://github.com/openai/codex)：npm 包 `@openai/codex`。
- [winget 官方退出码](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md)：仅明确的 `0x8A150014` 表示未找到安装包，不能把其他查询错误当成卸载成功。
