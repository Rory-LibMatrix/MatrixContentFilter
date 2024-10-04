using System.Collections.Concurrent;
using LibMatrix.EventTypes.Spec;
using LibMatrix.RoomTypes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services;

public class AsyncMessageQueue(ILogger<AsyncMessageQueue> logger, MatrixContentFilterConfiguration cfg) : BackgroundService {
    private readonly ConcurrentQueue<(GenericRoom Room, RoomMessageEventContent Content)> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(cfg.ConcurrencyLimits.LogMessages, cfg.ConcurrencyLimits.LogMessages);
    public void EnqueueMessageAsync(GenericRoom room, RoomMessageEventContent content) {
        _queue.Enqueue((room, content));
        
        if (_queue.Count > 100) {
            logger.LogWarning($"Message Queue is getting full (c={_queue.Count}), consider increasing the rate limit or exempting the bot!");
        }
    }
    
    private async Task ProcessQueue() {
        while (_queue.TryDequeue(out var message)) {
            await _semaphore.WaitAsync();
            _ = Task.Run(async () => {
                try {
                    await message.Room.SendMessageEventAsync(message.Content);
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
            await Task.Delay(1000, stoppingToken);
        }
        
        //clear backlog and exit
        await ProcessQueue();
    }
}