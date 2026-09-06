namespace CodexDoctor.Native;

public static class CodexDesktopSelector
{
    public static CodexDesktopInstallationInfo? SelectPreferred(IEnumerable<CodexDesktopInstallationInfo> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);
        return clients
            .Where(x => !string.IsNullOrWhiteSpace(x.ExecutablePath))
            .OrderByDescending(x => x.IsRunning)
            .ThenByDescending(x => x.ProcessIds.Count)
            .ThenByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}

public enum MigrationActionKind
{
    Migrate,
    Restore,
    Recover,
    ViewDetails
}

public sealed record MigrationActionDecision(
    MigrationActionKind Kind,
    string ActionZh,
    bool AutoExecutable,
    string ExplanationZh);

public static class MigrationActionResolver
{
    public static MigrationActionDecision Resolve(
        CodexDataDirectoryInfo dataDirectory,
        bool hasMigrationState,
        bool canRecoverInterrupted)
    {
        ArgumentNullException.ThrowIfNull(dataDirectory);

        if (dataDirectory.IsReparsePoint)
        {
            if (hasMigrationState && !string.IsNullOrWhiteSpace(dataDirectory.LinkTarget))
                return new(MigrationActionKind.Restore, "恢复 .codex", true, "检测到有效 Junction 且存在可审计迁移状态。");
            return new(MigrationActionKind.ViewDetails, "查看迁移详情", false, "检测到重解析点，但缺少可验证迁移状态或链接目标。");
        }

        if (hasMigrationState)
        {
            if (canRecoverInterrupted)
                return new(MigrationActionKind.Recover, "恢复迁移事务", true, "检测到可证明安全的中断迁移事务，可按状态文件恢复。");
            return new(MigrationActionKind.ViewDetails, "查看迁移详情", false, "存在迁移状态，但当前目录形态无法证明可安全自动处理。");
        }

        return new(MigrationActionKind.Migrate, "迁移 .codex", true, "当前为普通 .codex 目录，可在确认目标后执行安全迁移。");
    }
}

public sealed record LanguageActionDecision(
    string ActionZh,
    bool CanApply,
    bool AlreadyChinese,
    bool NeedsUserAction,
    string ExplanationZh);

public static class LanguageActionResolver
{
    public static LanguageActionDecision Resolve(CodexLanguageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Applied || state.UiLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            return new("已是中文", false, true, false, "当前可信界面语言状态已经是简体中文。");
        if (state.NeedsUserAction)
            return new("需要用户操作", false, false, true, state.MethodZh);
        return new("可自动设置", true, false, false, state.MethodZh);
    }
}
