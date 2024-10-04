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

public class GetConfigCommand(IServiceProvider svcs) : ICommand {
    public string Name { get; } = "getconfig";
    public string[]? Aliases { get; } = [];
    public string Description { get; } = "Get the current configuration, optionally takes a room ID";
    public bool Unlisted { get; } = false;

    public async Task Invoke(CommandContext ctx) {
        var room = ctx.Room;
        if (ctx.Args.Length > 0) {
            try {
                room = ctx.Homeserver.GetRoom(ctx.Args[0]);
            }
            catch {
                await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice").WithBody("Invalid room ID").Build());
                return;
            }
        }

        var defaults = await ctx.Homeserver.GetAccountDataAsync<FilterConfiguration>(FilterConfiguration.EventId);
        var config = await room.GetRoomAccountDataOrNullAsync<FilterConfiguration>(FilterConfiguration.EventId);
        var msb = new MessageBuilder("m.notice").WithColoredBody("#FFCC00", "Default configuration:")
            .WithTable(tb => {
                foreach (var prop in defaults.GetType().GetProperties()) {
                    var key = prop.GetFriendlyName();
                    var val = prop.GetValue(defaults);

                    tb = tb.WithRow(rb => {
                        rb.WithCell(key);
                        rb.WithCell(val?.ToJson() ?? "null");
                    });
                }
            });

        if (config == null) {
            msb = msb.WithBody("No configuration set for this room, using defaults");
        }
        else {
            msb = msb.WithBody("Room overrides (additive):")
                .WithCodeBlock(config.ToJson(ignoreNull: true), "json");
        }

        await room.SendMessageEventAsync(msb.Build());
    }
}