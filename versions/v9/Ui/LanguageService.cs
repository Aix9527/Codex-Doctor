using System.Windows.Automation;

namespace CodexDoctor.V9;

public static class LanguageService
{
    public static Task SetAsync(DesktopInstallation desktop, string language, CancellationToken ct)
    {
        if (language is not "zh-CN" and not "en-US") throw new ArgumentException("不支持的目标语言。");
        // UI Automation must not execute on the thread that owns a window it may inspect.
        return Task.Run(async () =>
        {
            var ids = DesktopService.Running(desktop.Path).ToHashSet();
            if (ids.Count == 0) throw new IOException("客户端没有运行，请先启动客户端再切换语言。");
            string[] names = language == "zh-CN" ? ["简体中文", "中文（简体）", "中文(简体)", "Chinese (Simplified)", "Simplified Chinese", "简体中文 (Chinese, Simplified)"] : ["English", "英语", "英文", "English (United States)"];
            AutomationElement[] Roots()
            {
                var roots = new List<AutomationElement>();
                foreach (AutomationElement root in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition))
                {
                    try { if (ids.Contains(root.Current.ProcessId) && !root.Current.IsOffscreen) roots.Add(root); } catch (ElementNotAvailableException) { }
                }
                return roots.ToArray();
            }
            AutomationElement? Find(string[] labels)
            {
                foreach (var root in Roots()) foreach (var label in labels)
                {
                    var found = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, label));
                    if (found is not null) return found;
                }
                return null;
            }
            ct.ThrowIfCancellationRequested();
            var control = Find(["Language", "语言"]);
            if (control is null)
            {
                var settings = Find(["Settings", "设置"]);
                if (settings is null)
                {
                    var account = Find(["Profile", "Account", "个人资料", "账户", "个人中心"]);
                    if (account is not null) { Activate(account); await Task.Delay(300, ct); settings = Find(["Settings", "设置"]); }
                }
                if (settings is null || !Activate(settings)) throw new IOException("没有找到可操作的 Settings/设置控件，请在客户端手动打开设置后重试。");
                await Task.Delay(350, ct);
                var general = Find(["General", "通用", "常规"]); if (general is not null) Activate(general);
                await Task.Delay(200, ct); control = Find(["Language", "语言"]);
            }
            if (control is null) throw new IOException("未找到 Language/语言设置。此客户端界面不在已支持的控件范围内。");
            if (!IsChoiceControl(control))
            {
                var parent = TreeWalker.ControlViewWalker.GetParent(control);
                var choices = parent?.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));
                if (choices?.Count == 1) control = choices[0];
            }
            if (control.Current.IsKeyboardFocusable) control.SetFocus();
            if (!Activate(control)) throw new IOException("找到语言标签，但没有可操作的语言选择控件。");
            AutomationElement? option = null;
            for (var attempt = 0; attempt < 20 && option is null; attempt++)
            {
                await Task.Delay(150, ct);
                foreach (var name in names)
                    option ??= control.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
                option ??= Find(names);
            }
            if (option is null) throw new IOException("当前语言列表没有找到目标语言。");
            if (option.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection)) ((SelectionItemPattern)selection).Select();
            else if (!Activate(option)) throw new IOException("目标语言控件不可选择。");
            await Task.Delay(300, ct);
            ct.ThrowIfCancellationRequested();
            var verified = false;
            try
            {
                if (control.TryGetCurrentPattern(SelectionPattern.Pattern, out var selected)) verified = ((SelectionPattern)selected).Current.GetSelection().Any(x => names.Contains(x.Current.Name));
                if (!verified && control.TryGetCurrentPattern(ValuePattern.Pattern, out var value)) verified = names.Contains(((ValuePattern)value).Current.Value);
                if (!verified && option.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectedOption)) verified = ((SelectionItemPattern)selectedOption).Current.IsSelected;
            }
            catch (ElementNotAvailableException) { }
            if (!verified) throw new IOException("已选择目标语言，但尚未确认设置值。请在客户端复核；不报告切换成功。");
            if (control.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expanded)) ((ExpandCollapsePattern)expanded).Collapse();
        }, ct);
    }

    private static bool IsChoiceControl(AutomationElement element) => element.Current.ControlType == ControlType.ComboBox || element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out _) || element.TryGetCurrentPattern(InvokePattern.Pattern, out _);
    private static bool Activate(AutomationElement element)
    {
        if (!element.Current.IsEnabled) return false;
        if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) { ((ExpandCollapsePattern)expand).Expand(); return true; }
        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var select)) { ((SelectionItemPattern)select).Select(); return true; }
        return false;
    }
}
