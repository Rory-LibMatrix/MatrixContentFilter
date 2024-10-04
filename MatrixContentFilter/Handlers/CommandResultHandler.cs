using ArcaneLibs;
using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Interfaces;

namespace MatrixContentFilter.Handlers;

public static class CommandResultHandler {
    private static string binDir = FileUtils.GetBinDir();

    public static async Task HandleAsync(CommandResult res) {
        {
            if (res.Success) return;
            var room = res.Context.Room;
            var hs = res.Context.Homeserver;
            var msb = new MessageBuilder();
            if (res.Result == CommandResult.CommandResultType.Failure_Exception) {
                var angryEmojiPath = Path.Combine(binDir, "Resources", "Stickers", "JennyAngryPink.webp");
                var hash = await FileUtils.GetFileSha384Async(angryEmojiPath);
                var angryEmoji = await hs.NamedCaches.FileCache.GetOrSetValueAsync(hash, async () => {
                    await using var fs = File.OpenRead(angryEmojiPath);
                    return await hs.UploadFile("JennyAngryPink.webp", fs, "image/webp");
                });
                msb.WithCustomEmoji(angryEmoji, "JennyAngryPink")
                    .WithColoredBody("#EE4444", "An error occurred during the execution of this command")
                    .WithCodeBlock(res.Exception!.ToString(), "csharp");
            }
            // else if(res.Result == CommandResult.CommandResultType.) {
            // msb.AddMessage(new RoomMessageEventContent("m.notice", "An error occurred during the execution of this command"));
            // }
            // var msg = res.Result switch {
            //     CommandResult.CommandResultType.Failure_Exception => MessageFormatter.FormatException("An error occurred during the execution of this command", res.Exception!)
            //     CommandResult.CommandResultType.Failure_NoPermission => new RoomMessageEventContent("m.notice", "You do not have permission to run this command!"),
            //     CommandResult.CommandResultType.Failure_InvalidCommand => new RoomMessageEventContent("m.notice", $"Command \"{res.Context.CommandName}\" not found!"),
            //     _ => throw new ArgumentOutOfRangeException()
            // };

            await room.SendMessageEventAsync(msb.Build());
        }
    }
}