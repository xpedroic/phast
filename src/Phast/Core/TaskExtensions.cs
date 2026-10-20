namespace Phast.Core;

public static class TaskExtensions
{
    /// <summary>Fire-and-forget a task, logging (instead of losing) any exception.</summary>
    public static async void Forget(this Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Background task failed: {ex}"); }
    }
}
