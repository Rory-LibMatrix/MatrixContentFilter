using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Utilities.Bot.Interfaces;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.Services;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Commands;

public class CheckHistoryCommand(
    ConfigurationService filterConfigService,
    IEnumerable<IContentFilter> filters,
    AsyncMessageQueue msgQueue,
    InfoCacheService infoCache
) : ICommand {
    public string Name { get; } = "checkhistory";
    public string[]? Aliases { get; } = ["check"];
    public string Description { get; } = "Re-apply filters to last x messages (default: 100)";
    public bool Unlisted { get; } = false;

    public async Task Invoke(CommandContext ctx) {
        var count = 100;
        if (ctx.Args.Length > 0) {
            if (!int.TryParse(ctx.Args[0], out count)) {
                await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice").WithBody($"'{count}' is not a valid number!").Build());
                return;
            }
        }

        msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom,
            new MessageBuilder("m.notice").WithBody($"Re-applying filters to last {count} messages in ")
                .WithMention(ctx.Room.RoomId, await infoCache.GetRoomNameAsync(ctx.Room.RoomId)).Build());

        await foreach (var resp in ctx.Room.GetManyMessagesAsync(limit: count, chunkSize: Math.Min(count, 250))) {
            foreach (var filter in filters) {
                await filter.ProcessEventListAsync(resp.Chunk);
            }
        }
    }

    // /// <summary>Triggered when the application host is ready to start the service.</summary>
    // /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    // protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
    //     while (!cancellationToken.IsCancellationRequested) {
    //         await Task.Delay(10000, cancellationToken);
    //         var rooms = await hs.GetJoinedRooms();
    //         rooms.RemoveAll(x => x.RoomId == filterConfigService.LogRoom.RoomId);
    //         rooms.RemoveAll(x => x.RoomId == filterConfigService.ControlRoom.RoomId);
    //
    //         var timelineFilter = new SyncFilter.RoomFilter.StateFilter(notTypes: ["m.room.redaction"], limit: 5000);
    //         var timelines = rooms.Select(async x => {
    //             var room = hs.GetRoom(x.RoomId);
    //             // var sync = await room.GetMessagesAsync(null, 1500, filter: timelineFilter.ToJson(ignoreNull: true, indent: false).UrlEncode());
    //             var iter = room.GetManyMessagesAsync(null, 5000, filter: timelineFilter.ToJson(ignoreNull: true, indent: false).UrlEncode(), chunkSize: 250);
    //             await foreach (var sync in iter) {
    //                 var tasks = Parallel.ForEachAsync(filters, async (filter, ct) => {
    //                     try {
    //                         Console.WriteLine("Processing filter {0} (sanity check, chunk[s={1}])", filter.GetType().FullName, sync.Chunk.Count);
    //                         await filter.ProcessEventListAsync(sync.Chunk);
    //                     }
    //                     catch (Exception e) {
    //                         logger.LogError(e, "Error processing sync with filter {filter}", filter.GetType().FullName);
    //                         msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom, new MessageBuilder("m.notice")
    //                             .WithBody($"Error processing sync with filter {filter.GetType().FullName}: {e.Message}").Build());
    //                     }
    //                 });
    //
    //                 await tasks;
    //             }
    //         }).ToList();
    //         await Task.WhenAll(timelines);
    //     }
    // }
}