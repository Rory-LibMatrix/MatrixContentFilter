using System.Text.Json.Serialization;
using ArcaneLibs.Attributes;
using LibMatrix.EventTypes;

namespace MatrixContentFilter.EventTypes;

[MatrixEvent(EventName = EventId)]
public class FilterConfiguration : EventContent {
    public const string EventId = "gay.rory.MatrixContentFilterBot.filter_configuration";
    
    [JsonPropertyName("image_filter")]
    [FriendlyName(Name = "Images")]
    public BasicFilterConfiguration? ImageFilter { get; set; }
    
    [JsonPropertyName("video_filter")]
    [FriendlyName(Name = "Videos")]
    public BasicFilterConfiguration? VideoFilter { get; set; }
    
    [JsonPropertyName("audio_filter")]
    [FriendlyName(Name = "Audio")]
    public BasicFilterConfiguration? AudioFilter { get; set; }
    
    [JsonPropertyName("file_filter")]
    [FriendlyName(Name = "Files")]
    public BasicFilterConfiguration? FileFilter { get; set; }
    
    [JsonPropertyName("url_filter")]
    [FriendlyName(Name = "Links")]
    public BasicFilterConfiguration? UrlFilter { get; set; }
    
    [JsonPropertyName("ignored_users")]
    [FriendlyName(Name = "Ignored Users")]
    public List<string>? IgnoredUsers { get; set; }
    
    
    public class BasicFilterConfiguration {
        [JsonPropertyName("allowed")]
        public bool? Allowed { get; set; }
        
        [JsonPropertyName("ignored_users")]
        public List<string>? IgnoredUsers { get; set; }
    }
}