using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Interfaces;

namespace MatrixContentFilter.Commands.ConfigureSubCommands;

public class ControlRoomConfigureSubCommand : ICommand<ConfigureCommand> {
    public string Name { get; } = "controlroom";
    public string[]? Aliases { get; }
    public string Description { get; } = "Configure the control room";
    public bool Unlisted { get; }

    public async Task Invoke(CommandContext ctx) {
        if (ctx.Args.Length == 0) {
            await ctx.Room.SendMessageEventAsync(new MessageBuilder("m.notice").WithBody("meow").Build());
        }
        
    }
}