using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services.AsyncActionQueues;

public abstract class AbstractAsyncActionQueue : BackgroundService {
    private readonly ConcurrentStack<string> _recentIds = new();
    private readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions() {
        SingleReader = true
    });
    private static CancellationTokenSource _cts = new();

    /// <summary>
    ///     Enqueue an action to be executed asynchronously
    /// </summary>
    /// <param name="id">Reproducible ID</param>
    /// <param name="action">Action to execute</param>
    /// <returns>`true` if action was appended, `false` if action was not added, eg. due to duplicate ID</returns>
    public virtual async Task<bool> EqueueActionAsync(string id, Func<Task> action) {
        throw new NotImplementedException();
    }

    private async Task ProcessQueue() {
        throw new NotImplementedException();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            await ProcessQueue();
            Console.WriteLine("AbstractAsyncActionQueue waiting for new actions, this should never happen!");
        }

        //clear backlog and exit
        await ProcessQueue();
    }
}