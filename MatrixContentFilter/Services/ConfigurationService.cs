using ArcaneLibs.Extensions;
using LibMatrix;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using LibMatrix.Utilities;
using MatrixContentFilter.EventTypes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MatrixContentFilter.Services;

public class ConfigurationService(ILogger<ConfigurationService> logger, AuthenticatedHomeserverGeneric hs, AsyncMessageQueue msgQueue) : BackgroundService {
    public BotEnvironmentConfiguration EnvironmentConfiguration { get; private set; }
    public FilterConfiguration DefaultConfiguration { get; private set; }
    public Dictionary<string, FilterConfiguration> RoomConfigurationOverrides { get; } = new();
    public Dictionary<string, FilterConfiguration> FinalRoomConfigurations { get; } = new();

    public GenericRoom LogRoom { get; private set; } = null!;
    public GenericRoom ControlRoom { get; private set; } = null!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var syncHelper = new SyncHelper(hs, logger) {
            NamedFilterName = CommonSyncFilters.GetAccountDataWithRooms
        };

        await foreach (var sync in syncHelper.EnumerateSyncAsync(stoppingToken).WithCancellation(stoppingToken)) {
            if (sync is { AccountData: null, Rooms: null }) continue;
            logger.LogInformation("Received configuration update: {syncData}", sync.ToJson(ignoreNull: true));
            await OnSyncReceived(sync);
        }
    }

    public async Task OnSyncReceived(SyncResponse sync) {
        if (sync.AccountData?.Events?.FirstOrDefault(x => x.Type == BotEnvironmentConfiguration.EventId) is { } envEvent) {
            EnvironmentConfiguration = envEvent.TypedContent as BotEnvironmentConfiguration;
            msgQueue.EnqueueMessageAsync(LogRoom, new MessageBuilder("m.notice")
                .WithColoredBody("#FF0088", "Environment configuration updated from sync.").WithNewline()
                .WithCollapsibleSection("JSON data:", msb => msb.WithCodeBlock(EnvironmentConfiguration.ToJson(), "json"))
                // .WithCollapsibleSection("Full event JSON", _msb => _msb.WithCodeBlock(envEvent.ToJson(), "json"))
                .Build());
            LogRoom = hs.GetRoom(EnvironmentConfiguration.LogRoomId!);
            ControlRoom = hs.GetRoom(EnvironmentConfiguration.ControlRoomId!);
        }

        if (sync.AccountData?.Events?.FirstOrDefault(x => x.Type == FilterConfiguration.EventId) is { } filterEvent) {
            DefaultConfiguration = filterEvent.TypedContent as FilterConfiguration;
            msgQueue.EnqueueMessageAsync(LogRoom, new MessageBuilder("m.notice")
                .WithColoredBody("#00FF88", "Default filter configuration updated from sync.").WithNewline()
                .WithCollapsibleSection("JSON data:", msb => msb.WithCodeBlock(DefaultConfiguration.ToJson(), "json"))
                // .WithCollapsibleSection("Full event JSON", _msb => _msb.WithCodeBlock(filterEvent.ToJson(), "json"))
                .Build());
        }

        await Parallel.ForEachAsync(sync.Rooms?.Join ?? [], async (syncRoom, ct) => {
            var (roomId, roomData) = syncRoom;
            if (roomId == LogRoom!.RoomId || roomId == ControlRoom!.RoomId) return;
            var room = hs.GetRoom(roomId);

            if (roomData.AccountData?.Events?.FirstOrDefault(x => x.Type == FilterConfiguration.EventId) is { } roomFilterEvent) {
                RoomConfigurationOverrides[roomId] = roomFilterEvent.TypedContent as FilterConfiguration;
                var roomName = await room.GetNameOrFallbackAsync();
                msgQueue.EnqueueMessageAsync(LogRoom, new MessageBuilder("m.notice")
                    .WithColoredBody("#00FF88", msb => msb.WithBody($"Filter configuration updated for ").WithMention(roomId, roomName).WithBody(" from sync.")).WithNewline()
                    .WithCollapsibleSection("JSON data:", msb => msb.WithCodeBlock(RoomConfigurationOverrides[roomId].ToJson(), "json"))
                    .Build());
            }
        });
    }

    public async Task OnStartup(MatrixContentFilterConfiguration configuration) {
        BotEnvironmentConfiguration _environmentConfiguration;
        try {
            _environmentConfiguration = await hs.GetAccountDataAsync<BotEnvironmentConfiguration>(BotEnvironmentConfiguration.EventId);
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: MatrixException.ErrorCodes.M_NOT_FOUND }) throw;
            logger.LogWarning("No environment configuration found, creating one");
            _environmentConfiguration = new BotEnvironmentConfiguration();
        }

        if (string.IsNullOrWhiteSpace(_environmentConfiguration.ControlRoomId)) {
            LogRoom = await hs.CreateRoom(new() {
                Name = "MatrixContentFilter logs",
                Invite = configuration.Admins,
                Visibility = "private"
            });
            var powerlevels = await LogRoom.GetPowerLevelsAsync();
            powerlevels.EventsDefault = 20;
            foreach (var admin in configuration.Admins) {
                powerlevels.Users[admin] = 100;
            }

            await LogRoom.SendStateEventAsync(RoomPowerLevelEventContent.EventId, powerlevels);

            _environmentConfiguration.LogRoomId = LogRoom.RoomId;
            await hs.SetAccountDataAsync(BotEnvironmentConfiguration.EventId, _environmentConfiguration);
        }
        else {
            LogRoom = hs.GetRoom(_environmentConfiguration.LogRoomId!);
        }

        if (string.IsNullOrWhiteSpace(_environmentConfiguration.ControlRoomId)) {
            ControlRoom = await hs.CreateRoom(new() {
                Name = "MatrixContentFilter control room",
                Invite = configuration.Admins,
                Visibility = "private"
            });
            var powerlevels = await ControlRoom.GetPowerLevelsAsync();
            powerlevels.EventsDefault = 20;
            foreach (var admin in configuration.Admins) {
                powerlevels.Users[admin] = 100;
            }

            await ControlRoom.SendStateEventAsync(RoomPowerLevelEventContent.EventId, powerlevels);

            _environmentConfiguration.ControlRoomId = ControlRoom.RoomId;
            await hs.SetAccountDataAsync(BotEnvironmentConfiguration.EventId, _environmentConfiguration);
        }
        else {
            ControlRoom = hs.GetRoom(_environmentConfiguration.ControlRoomId!);
        }

        FilterConfiguration _filterConfiguration;
        try {
            _filterConfiguration = await hs.GetAccountDataAsync<FilterConfiguration>(FilterConfiguration.EventId);
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: MatrixException.ErrorCodes.M_NOT_FOUND }) throw;
            logger.LogWarning("No filter configuration found, creating one");
            msgQueue.EnqueueMessageAsync(LogRoom, new MessageBuilder("m.notice").WithColoredBody("#FF0000", "No filter configuration found, creating one").Build());
            _filterConfiguration = new FilterConfiguration();
        }

        Dictionary<string, object> changes = new();

        T Log<T>(string key, T value) {
            changes[key] = value;
            return value;
        }

        _filterConfiguration.IgnoredUsers ??= Log("ignored_users", (List<string>) [
            hs.WhoAmI.UserId,
            .. configuration.Admins
        ]);

        _filterConfiguration.FileFilter ??= new();
        _filterConfiguration.FileFilter.IgnoredUsers ??= Log("file_filter->ignored_users", (List<string>) []);
        _filterConfiguration.FileFilter.Allowed ??= Log("file_filter->allowed", false);

        _filterConfiguration.ImageFilter ??= new();
        _filterConfiguration.ImageFilter.IgnoredUsers ??= Log("image_filter->ignored_users", (List<string>) []);
        _filterConfiguration.ImageFilter.Allowed ??= Log("image_filter->allowed", false);

        _filterConfiguration.VideoFilter ??= new();
        _filterConfiguration.VideoFilter.IgnoredUsers ??= Log("video_filter->ignored_users", (List<string>) []);
        _filterConfiguration.VideoFilter.Allowed ??= Log("video_filter->allowed", false);

        _filterConfiguration.AudioFilter ??= new();
        _filterConfiguration.AudioFilter.IgnoredUsers ??= Log("audio_filter->ignored_users", (List<string>) []);
        _filterConfiguration.AudioFilter.Allowed ??= Log("audio_filter->allowed", false);

        _filterConfiguration.UrlFilter ??= new();
        _filterConfiguration.UrlFilter.IgnoredUsers ??= Log("url_filter->ignored_users", (List<string>) []);
        _filterConfiguration.UrlFilter.Allowed ??= Log("url_filter->allowed", false);

        if (changes.Count > 0) {
            await hs.SetAccountDataAsync(FilterConfiguration.EventId, _filterConfiguration);
            msgQueue.EnqueueMessageAsync(LogRoom, new MessageBuilder("m.notice").WithColoredBody("#FF0000", "Default filter configuration updated").WithNewline()
                .WithTable(msb => {
                    msb = msb.WithTitle("Default configuration changes", 2);

                    foreach (var (key, value) in changes) {
                        var formattedValue = value switch {
                            List<string> list => string.Join(", ", list),
                            _ => value.ToString()
                        };
                        msb = msb.WithRow(rb => { rb.WithCell(key).WithCell(formattedValue ?? "formattedValue was null!"); });
                    }
                }).Build());
        }
    }

    private async Task RebuildRoomConfigurations(FilterConfiguration? defaultConfig, Dictionary<string, FilterConfiguration?>? roomConfigurations) {
        defaultConfig ??= await hs.GetAccountDataAsync<FilterConfiguration>(FilterConfiguration.EventId);
    }

    public async Task<FilterConfiguration> GetFinalRoomConfiguration(string roomId) {
        if (FinalRoomConfigurations.TryGetValue(roomId, out var config)) return config;
        var roomConfig = RoomConfigurationOverrides.GetValueOrDefault(roomId);
        var defaultConfig = DefaultConfiguration;

        FinalRoomConfigurations[roomId] = config;
        return config;
    }
}