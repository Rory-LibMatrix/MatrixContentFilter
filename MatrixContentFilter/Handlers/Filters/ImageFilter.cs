using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.EventTypes;
using MatrixContentFilter.Services;
using MatrixContentFilter.Services.AsyncActionQueues;

namespace MatrixContentFilter.Handlers.Filters;

public class ImageFilter(
    ConfigurationService cfgService,
    AuthenticatedHomeserverGeneric hs,
    AsyncMessageQueue msgQueue,
    InfoCacheService infoCache,
    AbstractAsyncActionQueue actionQueue)
    : IContentFilter {
    public override async Task ProcessSyncAsync(SyncResponse syncResponse) {
        Console.WriteLine("Processing image filter");
        if (syncResponse.Rooms?.Join is null) return;
        var tasks = syncResponse.Rooms.Join.Select(ProcessRoomAsync);
        await Task.WhenAll(tasks);
    }

    // private SemaphoreSlim semaphore = new(8, 8);

    private async Task ProcessRoomAsync(KeyValuePair<string, SyncResponse.RoomsDataStructure.JoinedRoomDataStructure> syncRoom) {
        var (roomId, roomData) = syncRoom;
        if (roomId == cfgService.LogRoom.RoomId || roomId == cfgService.ControlRoom.RoomId) return;
        if (roomData.Timeline?.Events is null) return;
        var config = cfgService.RoomConfigurationOverrides.GetValueOrDefault(roomId)?.ImageFilter;

        var room = hs.GetRoom(roomId);

        var tasks = roomData.Timeline.Events.Select(msg => ProcessEventAsync(room, msg, config));
        await Task.WhenAll(tasks);
    }

    public override async Task ProcessEventListAsync(List<StateEventResponse> events) {
        var tasks = events.GroupBy(x => x.RoomId).Select(async x => {
            var room = hs.GetRoom(x.Key);
            var config = cfgService.RoomConfigurationOverrides.GetValueOrDefault(x.Key)?.ImageFilter;
            var tasks = x.Select(msg => ProcessEventAsync(room, msg, config));
            await Task.WhenAll(tasks);
        });
        
        await Task.WhenAll(tasks);
    }

    private async Task ProcessEventAsync(GenericRoom room, StateEventResponse msg, FilterConfiguration.BasicFilterConfiguration roomConfiguration) {
        if (msg.Type != "m.room.message") return;
        var content = msg.TypedContent as RoomMessageEventContent;
        if (content?.MessageType != "m.image") return;

        // await semaphore.WaitAsync();

        await actionQueue.EqueueActionAsync(msg.EventId, async () => {
            while (true) {
                try {
                    Console.WriteLine("Redacting image message: {0}", msg.EventId);
                    await room.RedactEventAsync(msg.EventId ?? throw new ArgumentException("Event ID is null?"), "Not allowed to send images in this room!");
                    break;
                }
                catch (Exception e) {
                    msgQueue.EnqueueMessageAsync(cfgService.LogRoom, new MessageBuilder("m.notice")
                        .WithBody($"Error redacting image message in {room.RoomId}!")
                        .WithCollapsibleSection("Error data", msb => msb.WithCodeBlock(e.ToString(), "csharp"))
                        .Build());
                }
            }

            var displayName = await infoCache.GetDisplayNameAsync(room.RoomId, msg.Sender);
            var roomName = await infoCache.GetRoomNameAsync(room.RoomId);

            msgQueue.EnqueueMessageAsync(cfgService.LogRoom, new MessageBuilder("m.notice")
                .WithBody($"Image sent by ").WithMention(msg.Sender, displayName).WithBody(" in ").WithMention(room.RoomId, roomName).WithBody(" was removed!").WithNewline()
                .WithCollapsibleSection("Message data", msb => msb.WithCodeBlock(content.ToJson(ignoreNull: true), "json"))
                .Build());
        });
        ActionCount++;

        // semaphore.Release();
    }
}