using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class TrackerServiceTestRequest
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class TrackerServiceTestResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class TrackerServiceTestTrigger : BaseZeroMqTwoWayTrigger<TrackerServiceTestRequest, TrackerServiceTestResponse>
{
    private readonly ILogger<TrackerServiceTestTrigger> _logger;

    public TrackerServiceTestTrigger(ILogger<TrackerServiceTestTrigger> logger) : base("TrackerServiceChannel")
    {
        _logger = logger;
    }

    protected override Task<TrackerServiceTestResponse> ProcessLogicAsync(TrackerServiceTestRequest request)
    {
        _logger.LogInformation($"[ZeroMQ TwoWay] Đã nhận và xử lý yêu cầu: {request.Action} lúc {request.Timestamp}");
        return Task.FromResult(new TrackerServiceTestResponse
        {
            Message = $"Phản hồi từ TrackerService cho hành động '{request.Action}'",
            ProcessedAt = DateTime.UtcNow
        });
    }
}

public class
    TrackerServiceTwoWayHandler : BaseZeroMqTwoWayHandler<TrackerServiceTestRequest, TrackerServiceTestResponse>
{
    public TrackerServiceTwoWayHandler(TrackerServiceTestTrigger trigger) : base(trigger)
    {
    }
}