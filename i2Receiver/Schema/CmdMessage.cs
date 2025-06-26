using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace i2Receiver.Schema
{
    public class CmdMessage
    {
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string? Data { get; set; }

        [JsonProperty("cmd")]
        public string Command { get; set; }
    }

    public class CueCommand
    {
        [JsonProperty("cue_id")] public string CueId { get; set; } = string.Empty;
        [JsonProperty("cues")] public List<CueListObject>? Cues { get; set; }
        [JsonProperty("start_time")] public string? StartTime { get; set; }
    }

    public class CueListObject
    {
        [JsonProperty("star")] public string StarUuid { get; set; } = string.Empty;
        [JsonProperty("duration")] public int Duration { get; set; } = 3600;
        [JsonProperty("flavor")] public string Flavor { get; set; } = string.Empty;
    }

    public class RadarFrame
    {
        [JsonProperty("data")] public string FrameData { get; set; } = string.Empty;
        [JsonProperty("timestamp")] public int Timestamp { get; set; } = 0;
        [JsonProperty("filename")] public string Filename { get; set; } = string.Empty;
    }
}