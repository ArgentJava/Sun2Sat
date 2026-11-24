namespace Sun2Sat.Services;

/// <summary>A tiny per-user (scoped) signal. When a page changes something the side
/// menu displays, such as the pending-request count, it calls NotifyChanged() and
/// NavMenu reloads its numbers without a full page refresh.</summary>
public class NavRefresh
{
    public event Func<Task>? Changed;

    public async Task NotifyChangedAsync()
    {
        if (Changed is not null) await Changed.Invoke();
    }
}
