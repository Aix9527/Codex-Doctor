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
