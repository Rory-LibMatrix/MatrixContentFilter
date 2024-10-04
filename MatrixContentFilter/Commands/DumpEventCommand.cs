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

public class DumpEventCommand(
    ConfigurationService filterConfigService,
    AsyncMessageQueue msgQueue,
    InfoCacheService infoCache,
    ConfigurationService cfgService,
    AbstractAsyncActionQueue actionQueue
) : ICommand {
    public string Name { get; } = "dump";
    public string[]? Aliases { get; } = [];
    public string Description { get; } = "Dump event by ID";
    public bool Unlisted { get; } = false;

    public async Task Invoke(CommandContext ctx) {
        var evt = await ctx.Room.GetEventAsync(ctx.Args[0]);
        await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice").WithBody(evt.ToJson(ignoreNull: true)).Build());
    }
}