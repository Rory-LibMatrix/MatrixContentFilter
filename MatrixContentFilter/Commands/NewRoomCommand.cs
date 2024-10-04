using System.Text;
using ArcaneLibs.Attributes;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Commands;
using LibMatrix.Utilities.Bot.Interfaces;
using MatrixContentFilter.EventTypes;
using Microsoft.Extensions.DependencyInjection;

namespace MatrixContentFilter.Commands;

public class NewRoomCommand(IServiceProvider svcs) : ICommand {
    public string Name { get; } = "newroom";
    public string[]? Aliases { get; } = ["nr"];
    public string Description { get; } = "Create a new room";
    public bool Unlisted { get; } = false;

    public async Task Invoke(CommandContext ctx) {
        await ctx.Homeserver.CreateRoom(new() {
            Invite = [ctx.MessageEvent.Sender!]
        });
    }
}