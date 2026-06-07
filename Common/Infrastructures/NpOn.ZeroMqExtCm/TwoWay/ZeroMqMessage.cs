using Common.Extensions.NpOn.CommonMode;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public class ZeroMqMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Channel { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public bool IsReply { get; set; }
    public string? ErrorMessage { get; set; }

    public static ZeroMqMessage FromJson(string json)
    {
        return JsonModeWithCache.FromJson<ZeroMqMessage>(json) ?? new ZeroMqMessage();
    }

    public string ToJson()
    {
        return JsonModeWithCache.ToJson(this) ?? string.Empty;
    }
}