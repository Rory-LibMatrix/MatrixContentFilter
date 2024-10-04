using System.Diagnostics;
using ArcaneLibs;
using ArcaneLibs.Extensions;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.Handlers.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services;

public class MatrixContentFilterBot(
    ILogger<MatrixContentFilterBot> logger,
    AuthenticatedHomeserverGeneric hs,
    MatrixContentFilterConfiguration configuration,
    ConfigurationService filterConfigService,
    IEnumerable<IContentFilter> filters,
    AsyncMessageQueue msgQueue
) : BackgroundService {
    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        try {
            await filterConfigService.OnStartup(configuration);
            msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice").WithColoredBody("#00FF00", "Bot startup successful! Listening for events.")
                .Build());
            msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice").WithColoredBody("#00FF00", msb => {
                msb = msb.WithBody("Inserted filters implementations (internal):").WithNewline();
                foreach (var filter in filters) {
                    msb = msb.WithBody(filter.GetType().FullName).WithNewline();
                }
            }).Build());
        }
        catch (Exception e) {
            logger.LogError(e, "Error on startup");
            Environment.Exit(1); // We don't want to do a graceful shutdown if we can't start up
        }

        logger.LogInformation("Bot started!");
        await Run(cancellationToken);
    }

    private SyncHelper syncHelper;

    private async Task Run(CancellationToken cancellationToken) {
        var syncFilter = new SyncFilter() {
            Room = new() {
                NotRooms = [filterConfigService.LogRoom.RoomId],
                Timeline = new(notTypes: ["m.room.redaction"])
            }
        };
        syncHelper = new SyncHelper(hs, logger) {
            Filter = syncFilter
        };
        int i = 0;
        await foreach (var sync in syncHelper.EnumerateSyncAsync(cancellationToken).WithCancellation(cancellationToken)) {
            // if (i++ >= 100) {
            //     var sw = Stopwatch.StartNew();
            //     for (int gen = 0; gen < GC.MaxGeneration; gen++) {
            //         GC.Collect(gen, GCCollectionMode.Forced, true, true);
            //     }
            //     i = 0;
            //     msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice")
            //         .WithBody($"Garbage collection took {sw.ElapsedMilliseconds}ms")
            //         .Build());
            //     GC.
            // }

            // GC.TryStartNoGCRegion(1024 * 1024 * 1024);
            var sw = Stopwatch.StartNew();
            int actionCount = filters.Sum(x => x.ActionCount);
            try {
                await OnSyncReceived(sync);
            }
            catch (Exception e) {
                logger.LogError(e, "Error processing sync");
                msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice").WithBody($"Error processing sync: {e.Message}").Build());
            }
            finally {
                Console.WriteLine("Processed sync in {0}, executed {1} actions, {2} of memory usage", sw.Elapsed, filters.Sum(x => x.ActionCount) - actionCount, Util.BytesToString(Environment.WorkingSet));
                // GC.EndNoGCRegion();
            }
            
            // update sync filter
            if (syncFilter.Room.NotRooms[0] != filterConfigService.LogRoom.RoomId) {
                syncFilter.Room.NotRooms = [filterConfigService.LogRoom.RoomId];
                syncHelper.Filter = syncFilter;
            }
        }
    }

    private int _syncCount;

    private async Task OnSyncReceived(SyncResponse sync) {
        if (_syncCount++ == 0) return; // Skip initial sync :/

        if (sync.Rooms?.Join?.ContainsKey(filterConfigService.LogRoom.RoomId) == true) {
            sync.Rooms?.Join?.Remove(filterConfigService.LogRoom.RoomId);
        }

        if (sync.Rooms?.Join?.ContainsKey(filterConfigService.ControlRoom.RoomId) == true) {
            sync.Rooms?.Join?.Remove(filterConfigService.ControlRoom.RoomId);
        }

        // HACK: Server likes to send partial timelines during elevated activity, so we need to fetch them in order not to miss events
        var timelineFilter = new SyncFilter.RoomFilter.StateFilter(notTypes: ["m.room.redaction"], limit: 5000);
        var limitedTimelineRooms = sync.Rooms?.Join?
            .Where(x => x.Value.Timeline?.Limited ?? false)
            .Select(async x => {
                var (roomId, roomData) = x;
                var room = hs.GetRoom(roomId);
                if (roomData.Timeline?.Limited == true) {
                    msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice")
                        .WithColoredBody("FF0000", $"Room {roomId} has limited timeline, fetching! The room may be getting spammed?")
                        .Build());
                    roomData.Timeline.Events ??= [];
                    var newEvents = await room.GetMessagesAsync(roomData.Timeline.PrevBatch ?? "", 500, filter: timelineFilter.ToJson(ignoreNull: true, indent: false));
                    roomData.Timeline.Events.MergeBy(newEvents.Chunk, (x, y) => x.EventId == y.EventId, (x, y) => { });
                }
            })
            .ToList();

        if (limitedTimelineRooms?.Count > 0)
            await Task.WhenAll(limitedTimelineRooms);

        var tasks = Parallel.ForEachAsync(filters, async (filter, ct) => {
            try {
                Console.WriteLine("Processing filter {0}", filter.GetType().FullName);
                await filter.ProcessSyncAsync(sync);
            }
            catch (Exception e) {
                logger.LogError(e, "Error processing sync with filter {filter}", filter.GetType().FullName);
                msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice")
                    .WithBody($"Error processing sync with filter {filter.GetType().FullName}: {e.Message}").Build());
            }
        });

        await tasks;
    }
}