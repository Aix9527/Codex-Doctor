using System.Runtime.CompilerServices;
using CodexDoctor.Native;
using static CodexDoctor.Native.Tests.V81TestSupport;

namespace CodexDoctor.Native.Tests;

internal static class TaskV81SmartActionsContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var running = new CodexDesktopInstallationInfo("Codex Desktop", "running", @"C:\Apps\Codex-running.exe", "1", true, [9]);
        var idle = new CodexDesktopInstallationInfo("Codex Desktop", "installed", @"C:\Apps\Codex-idle.exe", "2", false, []);
        var preferred = CodexDesktopSelector.SelectPreferred([idle, running]);
        Require(preferred == running, "多个 Desktop 安装并存时必须优先选择当前运行实例。");

        var ordinary = new CodexDataDirectoryInfo(@"C:\Users\X\.codex", true, false, null, 10, 1000);
        var migrate = MigrationActionResolver.Resolve(ordinary, hasMigrationState: false, canRecoverInterrupted: false);
        Require(migrate.Kind == MigrationActionKind.Migrate, "普通 .codex 必须进入迁移状态。");
        Require(migrate.ActionZh == "迁移 .codex", "普通目录按钮文案必须为“迁移 .codex”。");

        var junction = ordinary with { IsReparsePoint = true, LinkTarget = @"D:\Codex\.codex" };
        var restore = MigrationActionResolver.Resolve(junction, hasMigrationState: true, canRecoverInterrupted: false);
        Require(restore.Kind == MigrationActionKind.Restore, "有效 Junction + 迁移状态必须进入恢复状态。");
        Require(restore.ActionZh == "恢复 .codex", "有效 Junction 按钮文案必须为“恢复 .codex”。");

        var recover = MigrationActionResolver.Resolve(ordinary, hasMigrationState: true, canRecoverInterrupted: true);
        Require(recover.Kind == MigrationActionKind.Recover, "可证明可恢复的中断事务必须进入恢复事务状态。");
        Require(recover.ActionZh == "恢复迁移事务", "中断事务按钮文案必须明确为恢复迁移事务。");

        var ambiguous = MigrationActionResolver.Resolve(ordinary, hasMigrationState: true, canRecoverInterrupted: false);
        Require(ambiguous.Kind == MigrationActionKind.ViewDetails, "状态文件存在但无法证明安全时必须转为查看详情。");
        Require(!ambiguous.AutoExecutable, "歧义迁移状态不得自动执行。");

        var zh = LanguageActionResolver.Resolve(new CodexLanguageState("zh-CN", "简体中文", "简体中文", true, false, "可信适配器"));
        Require(zh.ActionZh == "已是中文" && !zh.CanApply, "已是 zh-CN 时不得重复写入语言配置。");
        var auto = LanguageActionResolver.Resolve(new CodexLanguageState("en-US", "未知", "未知", false, false, "可信适配器"));
        Require(auto.ActionZh == "可自动设置" && auto.CanApply, "可信适配器存在时必须显示“可自动设置”。");
        var manual = LanguageActionResolver.Resolve(new CodexLanguageState("未知", "未知", "未知", false, true, "应用内设置"));
        Require(manual.ActionZh == "需要用户操作" && !manual.CanApply, "无可信适配器时必须显示“需要用户操作”。");

        var repairSource = File.ReadAllText(Path.Combine(SourceRoot().FullName, "RepairService.cs"));
        Require(repairSource.Contains("StartCodexDesktop(string executablePath)"), "RepairService 必须提供基于扫描真实路径的启动入口。");
        Require(!repairSource.Contains("chatgpt:", StringComparison.OrdinalIgnoreCase), "启动/重启不得依赖 chatgpt: URL 协议。");

        var main = File.ReadAllText(Path.Combine(SourceRoot().FullName, "MainForm.cs"));
        Require(main.Contains("CodexDesktopSelector.SelectPreferred"), "主界面必须复用统一 Desktop 选择器。");
        Require(main.Contains("_repair.StartCodexDesktop(desktop.ExecutablePath)"), "启动按钮必须通过 RepairService 使用扫描到的实际路径。");
        Require(main.Contains("MigrationActionResolver.Resolve"), "智能迁移按钮必须由迁移状态解析器驱动。");
        Require(main.Contains("LanguageActionResolver.Resolve"), "中文按钮必须由语言状态解析器驱动。");
        Require(main.Contains(@"D:\Codex"), "首次迁移无历史目标时必须保留 D:\\Codex 默认目标。");
    }
}
