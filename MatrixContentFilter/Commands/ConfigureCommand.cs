using System.Text;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Utilities.Bot.Commands;
using LibMatrix.Utilities.Bot.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MatrixContentFilter.Commands;

public class ConfigureCommand(IServiceProvider svcs) : ICommandGroup {
    public string Name { get; } = "configure";
    public string[]? Aliases { get; } = ["config", "cfg"];
    public string Description { get; }
    public bool Unlisted { get; } = true;

    public async Task Invoke(CommandContext ctx) {
        var commands = svcs.GetServices<ICommand>().Where(x => x.GetType().IsAssignableTo(typeof(ICommand<>).MakeGenericType(GetType()))).ToList();

        if (ctx.Args.Length == 0) {
            await ctx.Room.SendMessageEventAsync(HelpCommand.GenerateCommandList(commands).Build());
        }
        else {
            var subcommand = ctx.Args[0];
            var command = commands.FirstOrDefault(x => x.Name == subcommand || x.Aliases?.Contains(subcommand) == true);
            if (command == null) {
                await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent("m.notice", "Unknown subcommand"));
                return;
            }

            await command.Invoke(new CommandContext {
                Room = ctx.Room,
                MessageEvent = ctx.MessageEvent,
                CommandName = ctx.CommandName,
                Args = ctx.Args.Skip(1).ToArray(),
                Homeserver = ctx.Homeserver
            });
        }
    }
}