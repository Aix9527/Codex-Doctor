namespace CodexDoctor.Native;

public sealed class RepairActionCatalog
{
    private readonly IReadOnlyDictionary<string, IRepairAction> _actions;

    public RepairActionCatalog(IEnumerable<IRepairAction>? actions = null)
    {
        var map = new Dictionary<string, IRepairAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions ?? [])
        {
            if (string.IsNullOrWhiteSpace(action.ActionId))
                throw new ArgumentException("RepairAction ActionId 不能为空。", nameof(actions));
            if (action.ActionId.Equals("windows.user.proxy.write", StringComparison.OrdinalIgnoreCase))
                continue;
            map[action.ActionId] = action;

            // 健康扫描器使用稳定的问题->动作合同 ID；底层动作保留自身实现 ID。
            if (action.ActionId.Equals("git.proxy.clear", StringComparison.OrdinalIgnoreCase))
                map["git.proxy.cleanup"] = action;
            else if (action.ActionId.Equals("npm.proxy.clear", StringComparison.OrdinalIgnoreCase))
                map["npm.proxy.cleanup"] = action;
        }
        _actions = map;
    }

    public bool TryGet(string actionId, out IRepairAction? action)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            action = null;
            return false;
        }
        return _actions.TryGetValue(actionId, out action);
    }
}
