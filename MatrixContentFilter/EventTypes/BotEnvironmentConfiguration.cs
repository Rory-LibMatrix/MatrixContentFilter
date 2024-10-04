using System.Text.Json.Serialization;
using LibMatrix.EventTypes;

namespace MatrixContentFilter.EventTypes;


[MatrixEvent(EventName = EventId)]
public class BotEnvironmentConfiguration : EventContent {
    public const string EventId = "gay.rory.MatrixContentFilterBot.environment";
    
    [JsonPropertyName("log_room_id")]
    public string? LogRoomId { get; set; }
    
    [JsonPropertyName("control_room_id")]
    public string? ControlRoomId { get; set; }

}