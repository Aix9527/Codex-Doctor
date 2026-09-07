namespace CodexDoctor.Native;

public enum UiAutomationRootBindingMethod
{
    None,
    ElementFromHandle,
    NativeWindowHandleTree
}

public sealed record UiAutomationRootBinding(
    bool Success,
    object? Root,
    UiAutomationRootBindingMethod Method,
    string DiagnosticZh)
{
    public static UiAutomationRootBinding Failed(string diagnosticZh) =>
        new(false, null, UiAutomationRootBindingMethod.None, diagnosticZh);
}

public interface IUiAutomationRootProvider
{
    UiAutomationRootBinding Bind(nint handle);
}

public static class UiAutomationRootResolver
{
    public static UiAutomationRootBinding Resolve(Func<object?> elementFromHandle, Func<object?> nativeWindowHandleTree)
    {
        ArgumentNullException.ThrowIfNull(elementFromHandle);
        ArgumentNullException.ThrowIfNull(nativeWindowHandleTree);

        string? primaryError = null;
        try
        {
            var root = elementFromHandle();
            if (root is not null)
                return new(true, root, UiAutomationRootBindingMethod.ElementFromHandle, "已通过 ElementFromHandle 创建 UI Automation 根元素。");
            primaryError = "ElementFromHandle 返回 null";
        }
        catch (Exception ex)
        {
            primaryError = $"ElementFromHandle：{ex.GetType().Name}：{ex.Message}";
        }

        try
        {
            var root = nativeWindowHandleTree();
            if (root is not null)
                return new(true, root, UiAutomationRootBindingMethod.NativeWindowHandleTree,
                    $"ElementFromHandle 不可用，已通过 NativeWindowHandle 属性从 UIA 桌面树回退绑定。{primaryError}");
            return UiAutomationRootBinding.Failed($"{primaryError}；NativeWindowHandle 树搜索未找到对应元素。");
        }
        catch (Exception ex)
        {
            return UiAutomationRootBinding.Failed(
                $"{primaryError}；NativeWindowHandle 树搜索：{ex.GetType().Name}：{ex.Message}");
        }
    }
}

public sealed class WindowsUiAutomationRootProvider : IUiAutomationRootProvider
{
    private const int NativeWindowHandlePropertyId = 30020;
    private const int TreeScopeDescendants = 4;

    public UiAutomationRootBinding Bind(nint handle)
    {
        if (handle == 0) return UiAutomationRootBinding.Failed("HWND 为 0，无法创建 UI Automation 根元素。");

        dynamic automation;
        try
        {
            var type = Type.GetTypeFromProgID("UIAutomationClient.CUIAutomation")
                ?? throw new InvalidOperationException("当前 Windows 无法创建 UI Automation COM 对象。");
            automation = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("无法初始化 UI Automation。");
        }
        catch (Exception ex)
        {
            return UiAutomationRootBinding.Failed($"初始化 UI Automation 失败：{ex.GetType().Name}：{ex.Message}");
        }

        return UiAutomationRootResolver.Resolve(
            () => (object?)automation.ElementFromHandle(handle),
            () =>
            {
                dynamic desktopRoot = automation.GetRootElement();
                dynamic condition = automation.CreatePropertyCondition(
                    NativeWindowHandlePropertyId,
                    unchecked((int)handle));
                return (object?)desktopRoot.FindFirst(TreeScopeDescendants, condition);
            });
    }
}
