using System.Diagnostics;
using ArcaneLibs;
using ArcaneLibs.Extensions;
using LibMatrix;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.Handlers.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services;

public class BotModeSanityCheckService(
    ILogger<BotModeSanityCheckService> logger,
    AuthenticatedHomeserverGeneric hs,
    ConfigurationService filterConfigService,
    IEnumerable<IContentFilter> filters,
    AsyncMessageQueue msgQueue
) : BackgroundService {
    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            await Task.Delay(10000, cancellationToken);
            var rooms = await hs.GetJoinedRooms();
            rooms.RemoveAll(x => x.RoomId == filterConfigService.LogRoom.RoomId);
            rooms.RemoveAll(x => x.RoomId == filterConfigService.ControlRoom.RoomId);

            var timelineFilter = new SyncFilter.RoomFilter.StateFilter(notTypes: ["m.room.redaction"], limit: 5000);
            var timelines = rooms.Select(async x => {
                var room = hs.GetRoom(x.RoomId);
                // var sync = await room.GetMessagesAsync(null, 1500, filter: timelineFilter.ToJson(ignoreNull: true, indent: false).UrlEncode());
                var iter = room.GetManyMessagesAsync(null, 5000, filter: timelineFilter.ToJson(ignoreNull: true, indent: false).UrlEncode(), chunkSize: 250);
                await foreach (var sync in iter) {
                    var tasks = Parallel.ForEachAsync(filters, async (filter, ct) => {
                        try {
                            Console.WriteLine("Processing filter {0} (sanity check, chunk[s={1}])", filter.GetType().FullName, sync.Chunk.Count);
                            await filter.ProcessEventListAsync(sync.Chunk);
                        }
                        catch (Exception e) {
                            logger.LogError(e, "Error processing sync with filter {filter}", filter.GetType().FullName);
                            msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice")
                                .WithBody($"Error processing sync with filter {filter.GetType().FullName}: {e.Message}").Build());
                        }
                    });

                    await tasks;
                }
            }).ToList();
            await Task.WhenAll(timelines);
        }
    }
}