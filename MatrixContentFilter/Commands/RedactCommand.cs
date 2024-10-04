using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Utilities.Bot.Interfaces;
using MatrixContentFilter.Abstractions;
using MatrixContentFilter.Services;
using MatrixContentFilter.Services.AsyncActionQueues;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Commands;

public class RedactCommand(
    ConfigurationService filterConfigService,
    AsyncMessageQueue msgQueue,
    InfoCacheService infoCache,
    ConfigurationService cfgService,
    AbstractAsyncActionQueue actionQueue
) : ICommand {
    public string Name { get; } = "redact";
    public string[]? Aliases { get; } = [];
    public string Description { get; } = "Redact last x messages from user (default: 500)";
    public bool Unlisted { get; } = false;

    public async Task Invoke(CommandContext ctx) {
        var count = 500;

        if (ctx.Args.Length == 0) {
            await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice")
                .WithBody("Please provide a user ID to redact messages from. (Make sure that it isn't formatted! Do not autocomplete!)").Build());
            return;
        }

        var mxid = ctx.Args[0];
        if (ctx.Args.Length > 1) {
            if (!int.TryParse(ctx.Args[1], out count)) {
                await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice").WithBody($"'{count}' is not a valid number!").Build());
                return;
            }
        }

        var displayName = await infoCache.GetDisplayNameAsync(ctx.Room.RoomId, ctx.MessageEvent.Sender);
        var roomName = await infoCache.GetRoomNameAsync(ctx.Room.RoomId);

        msgQueue.EnqueueMessageAsync(filterConfigService.LogRoom,
            new MessageBuilder("m.notice").WithBody($"Removing last {count} messages from ").WithMention(mxid)
                .WithBody(" in ").WithMention(ctx.Room.RoomId, await infoCache.GetRoomNameAsync(ctx.Room.RoomId)).Build());
        var hourglassReaction = await ctx.Room.SendTimelineEventAsync("m.reaction", new RoomMessageReactionEventContent() {
            RelatesTo = new() {
                EventId = ctx.MessageEvent.EventId,
                RelationType = "m.annotation",
                Key = "\u23f3" //hour glass emoji
            }
        });
        
        await foreach (var resp in ctx.Room.GetManyMessagesAsync(limit: count, chunkSize: Math.Min(count, 250)
                           ,filter: new SyncFilter.RoomFilter.StateFilter(types: [RoomMemberEventContent.EventId, RoomMessageEventContent.EventId], senders: [mxid])
                               .ToJson(indent: false, ignoreNull: true).UrlEncode())
                           ) {
            foreach (var msg in resp.Chunk) {
                if (msg.Sender != mxid) continue;
                if (msg is not { Type: RoomMemberEventContent.EventId or RoomMessageEventContent.EventId }) continue;
                if (msg.RawContent is not { Count: > 0 }) continue;

                await actionQueue.EqueueActionAsync(msg.EventId, async () => {
                    while (true) {
                        try {
                            await ctx.Room.RedactEventAsync(msg.EventId ?? throw new ArgumentException("Event ID is null?"), "Message removed by moderator.");
                            break;
                        }
                        catch (Exception e) {
                            msgQueue.EnqueueMessageAsync(cfgService.LogRoom, new MessageBuilder("m.notice")
                                .WithBody($"Error redacting message in {ctx.Room.RoomId}!")
                                .WithCollapsibleSection("Error data", msb => msb.WithCodeBlock(e.ToString(), "csharp"))
                                .Build());
                        }
                    }

                    msgQueue.EnqueueMessageAsync(cfgService.LogRoom, new MessageBuilder("m.notice")
                        .WithBody($"Message sent by ").WithMention(msg.Sender, displayName).WithBody(" in ").WithMention(ctx.Room.RoomId, roomName)
                        .WithBody(" was removed in request by ").WithMention(ctx.Room.RoomId, roomName).WithBody("!").WithNewline()
                        .WithCollapsibleSection("Message data", msb => msb.WithCodeBlock(msg.RawContent.ToJson(ignoreNull: true), "json"))
                        .Build());
                });
            }
            
            await ctx.Room.RedactEventAsync(hourglassReaction.EventId);
            await ctx.Room.SendTimelineEventAsync("m.reaction", new RoomMessageReactionEventContent() {
                RelatesTo = new() {
                    EventId = ctx.MessageEvent.EventId,
                    RelationType = "m.annotation",
                    Key = "\u2714\ufe0f" //check mark emoji
                }
            });
        }
    }
}