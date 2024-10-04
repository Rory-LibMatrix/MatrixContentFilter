using LibMatrix.EventTypes.Spec;
using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Services;

namespace MatrixContentFilter.Handlers;

public static class InviteHandler {
    public static async Task HandleAsync(InviteHandlerHostedService.InviteEventArgs invite) {
        var room = invite.Homeserver.GetRoom(invite.RoomId);
        if (!invite.MemberEvent.Sender!.EndsWith("rory.gay")) {
            await room.LeaveAsync($"{invite.MemberEvent.Sender} is not allowed to invite this bot!");
            return;
        }

        try {
            await room.JoinAsync(reason: $"I was invited by {invite.MemberEvent.Sender}");
            await room.SendMessageEventAsync(new RoomMessageEventContent("m.notice", "Hello! I've arrived!"));
        } catch (Exception e) {
            var newroom = await invite.Homeserver.CreateRoom(new() {
                Name = $"Join error report",
                Invite = [invite.MemberEvent.Sender]
            });
            var msb = new MessageBuilder();
            msb.WithColoredBody("#EE4444", $"An error occurred during accepting the invite to {invite.RoomId}")
                .WithCodeBlock(e.ToString(), "csharp");
            await newroom.SendMessageEventAsync(msb.Build());
        }
    }
}