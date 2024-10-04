using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services.AsyncActionQueues;

public class LiFoAsyncActionQueue(ILogger<LiFoAsyncActionQueue> logger, MatrixContentFilterConfiguration cfg) : AbstractAsyncActionQueue {
    // private readonly ConcurrentQueue<(string Id, Func<Task> Action)> _queue = new();
    private readonly HashSet<string> _recentIds = new();
    private readonly ConcurrentStack<(string Id, Func<Task> Action)> _queue = new();
    private static CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _semaphore = new(cfg.ConcurrencyLimits.Redactions, cfg.ConcurrencyLimits.Redactions);

    /// <summary>
    ///     Enqueue an action to be executed asynchronously
    /// </summary>
    /// <param name="id">Reproducible ID</param>
    /// <param name="action">Action to execute</param>
    /// <returns>`true` if action was appended, `false` if action was not added, eg. due to duplicate ID</returns>
    public override async Task<bool> EqueueActionAsync(string id, Func<Task> action) {
        if (_recentIds.Contains(id)) {
            logger.LogWarning("Duplicate action ID detected, ignoring action");
            return false;
        }

        _queue.Push((id, action));
        _recentIds.Add(id);
        _cts.Cancel(false);

        return true;
    }

    private async Task ProcessQueue() {
        // await foreach (var task in _queue2.Reader.ReadAllAsync()) {
        while (_queue.TryPop(out var task)) {
            await _semaphore.WaitAsync();
            _ = Task.Run(async () => {
                try {
                    await task.Action.Invoke();
                    _recentIds.Remove(task.Id);
                }
                finally {
                    _semaphore.Release();
                }
            });
        }

        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            await ProcessQueue();
            Console.WriteLine(GetType().Name + " waiting for new actions");
            try {
                await Task.Delay(10000, _cts.Token);
            }
            catch (TaskCanceledException) {
                Console.WriteLine(GetType().Name + " _cts cancelled");
                // ignore
            }
        }

        //clear backlog and exit
        await ProcessQueue();
    }
}