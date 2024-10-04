using ArcaneLibs.Collections;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.Homeservers;

namespace MatrixContentFilter.Services;

public class InfoCacheService(AuthenticatedHomeserverGeneric hs) {
    private static readonly ExpiringSemaphoreCache<string> DisplayNameCache = new();
    public static readonly ExpiringSemaphoreCache<string> RoomNameCache = new();

    public async Task<string> GetDisplayNameAsync(string roomId, string userId) =>
        await DisplayNameCache.GetOrAdd($"{roomId}\t{userId}", async () => {
            var room = hs.GetRoom(roomId);
            var userState = await room.GetStateAsync<RoomMemberEventContent>(RoomMemberEventContent.EventId, userId);
            if (!string.IsNullOrWhiteSpace(userState?.DisplayName)) return userState.DisplayName;

            var user = await hs.GetProfileAsync(userId);
            if (!string.IsNullOrWhiteSpace(user?.DisplayName)) return user.DisplayName;

            return userId;
        }, TimeSpan.FromMinutes(5));

    public async Task<string> GetRoomNameAsync(string roomId) =>
        await RoomNameCache.GetOrAdd(roomId, async () => {
            var room = hs.GetRoom(roomId);
            var name = await room.GetNameOrFallbackAsync();
            return name;
        }, TimeSpan.FromMinutes(30));
}