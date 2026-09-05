# Codex Doctor V7.1.2 Hotfix Release Notes

V7.1.2 修复 V7.1.1 在部分 Windows / PowerShell 环境中点击“一键诊断”时出现的运行时错误：

> 在此对象上找不到属性“Count”。请确认该属性存在。

## 修复

- 修复 `Diagnosis.psm1` 在 `Set-StrictMode -Version Latest` 下处理 Git/npm 代理配置时的标量退化问题。
- Git 与 npm 代理值现在始终强制归一化为数组，再读取 `.Count`。
- 覆盖 0 个、1 个、2 个代理配置值的稳定行为，避免 PowerShell 5.1 单元素管道结果退化为字符串。
- 新增 `DiagnosisScalar.Tests.ps1` 回归测试，先复现 RED，再验证修复后的 GREEN。
- EXE 文件版本更新为 `7.1.2.0`。

## 包含此前热修复

V7.1.2 同时包含 V7.1.1 的启动修复：

- ps2exe Launcher 不再依赖可能为空的 `$MyInvocation.MyCommand.Path`。
- 主程序保持 UTF-8 BOM，兼容 Windows PowerShell 5.1 中文 UI。

## 安装建议

请下载完整 `Codex-Doctor-V7.1.2-Windows.zip` 并完整解压。不要只复制 EXE，因为 EXE 启动器仍需要同目录的 `Codex-Doctor-V7.ps1` 与 `lib/` 模块。

如果已经安装 V7.1/V7.1.1，可直接使用 V7.1.2 完整包重新安装/覆盖。
