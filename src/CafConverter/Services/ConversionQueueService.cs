namespace CafConverter.Services;

public sealed class ConversionQueueService
{
    public async Task RunAsync(IEnumerable<Func<CancellationToken, Task>> jobs, int concurrency, CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(Math.Clamp(concurrency, 1, 4));
        var tasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try { await job(cancellationToken); }
            finally { semaphore.Release(); }
        }).ToArray();
        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { await Task.WhenAll(tasks.Select(IgnoreCancellationAsync)); }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try { await task; } catch (OperationCanceledException) { }
    }
}
