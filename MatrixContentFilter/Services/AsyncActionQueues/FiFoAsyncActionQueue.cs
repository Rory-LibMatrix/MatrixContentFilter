using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services.AsyncActionQueues;

public class FiFoAsyncActionQueue(ILogger<FiFoAsyncActionQueue> logger, MatrixContentFilterConfiguration cfg) : AbstractAsyncActionQueue {
    // private readonly ConcurrentQueue<(string Id, Func<Task> Action)> _queue = new();
    private readonly HashSet<string> _recentIds = new();
    private readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions() {
        SingleReader = true
    });
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
        await _queue.Writer.WriteAsync(action);
        _recentIds.Add(id);

        if (_queue.Reader.Count > 100) {
            logger.LogWarning("Action Queue is getting full, consider increasing the rate limit or exempting the bot!");
        }

        return true;
    }

    private async Task ProcessQueue() {
        await foreach (var task in _queue.Reader.ReadAllAsync()) {
            await _semaphore.WaitAsync();
            _ = Task.Run(async () => {
                try {
                    await task.Invoke();
                }
                finally {
                    _semaphore.Release();
                }
            });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            await ProcessQueue();
            logger.LogWarning("Waiting for new actions, ProcessQueue returned early!");
        }

        //clear backlog and exit
        await ProcessQueue();
    }
}