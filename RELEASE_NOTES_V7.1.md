# Codex Doctor V7.1 Release Notes

V7.1 是 V7 Unified GUI 的正式发布封装版本，重点不是改变诊断算法，而是把已经验证的 V7 功能变成更容易下载、安装和双击启动的 Windows 发行包。

## 新增

- `CodexDoctorV7.exe`：由 Windows CI 使用固定版本 `ps2exe 1.0.18` 编译的双击启动器。
- `Codex-Doctor-V7.1-Windows.zip`：包含 EXE 启动器、V7 主程序、`lib/` 模块、测试、安装/卸载脚本和文档。
- SHA256 校验文件：用于验证下载包完整性。
- `安装_Codex_Doctor_V7.1.bat`：安装到 `%LOCALAPPDATA%\Programs\CodexDoctorV7` 并创建桌面快捷方式。
- `卸载_Codex_Doctor_V7.1.bat`：删除程序和快捷方式，保留诊断日志与报告。
- `Build-EXE.ps1`：本地可复现 EXE 构建脚本。

## 安全设计

EXE 只负责启动同目录的 `Codex-Doctor-V7.ps1`。诊断和修复逻辑仍保存在可审计的 PowerShell 模块中，避免将网络/代理修改逻辑隐藏进不可直接阅读的二进制文件。

V7.1 延续 V7 的安全策略：

- 默认只修改 Codex 自己的 `.codex/.env`。
- Windows 用户级代理环境变量默认不写入。
- Git/npm 全局代理清理需要明确确认。
- DNS、TLS、TUN 问题只诊断，不自动修改系统安全配置。
- `.codex` 迁移保留备份和 Junction 恢复路径。

## 推荐使用流程

1. 解压完整 ZIP，不要单独复制 EXE。
2. 双击 `CodexDoctorV7.exe`。
3. 执行“一键诊断”。
4. 查看 DNS / TLS / PROXY / ENV_CONFLICT / HEALTHY 分类。
5. 仅在有明确修复计划时执行“修复建议项”。
6. 重启 Codex / ChatGPT Desktop 后再次诊断。

## 兼容性

- Windows 10/11
- Windows PowerShell 5.1+
- WinForms
- V7 CLI JSON 模式继续可用
