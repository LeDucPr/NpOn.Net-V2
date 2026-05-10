using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceReadModel.ReadModels;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceDomain.Domains;

[TableLoader("system_log")]
public class SystemLog : BaseTrackerDomain
{
    [Field("id")] [Pk("id")] public Guid Id { get; set; }
    [Field("created_at")] public DateTime CreatedAt { get; set; }
    // [Field("event_date")] public DateTime EventDate { get; set; }
    [Field("level")] public ETrackerLogLevel Level { get; set; }
    public ETrackerLogType[] TrackerLogTypes { get; set; } = null!; // not null

    [Field("log_type")] public ETrackerLogType TrackerLogType => TrackerLogTypes.CombineFlags();
    [Field("source")] public string? Source { get; set; }
    [Field("message")] public string? Message { get; set; }
    [Field("process_uid")] public Guid? ProcessUId { get; set; }
    
    public SystemLog(SystemLogRModel rModel)
    {
        Id = rModel.Id;
        CreatedAt = rModel.CreatedAt;
        // EventDate = rModel.EventDate;
        Level = rModel.Level;
        TrackerLogTypes = rModel.TrackerLogTypes;
        Source = rModel.Source;
        Message = rModel.Message;
        ProcessUId = rModel.ProcessUId;
    }

    public SystemLog(TrackerLogAddCommand command)
    {
        Id = new Guid("284095d1-2f9c-45a9-a216-9ae4ed686820");
        CreatedAt = command.EventDate ?? DateTime.UtcNow;
        // EventDate = command.EventDate;
        Level = command.Level;
        TrackerLogTypes = command.TrackerLogTypes;
        Source = command.Source;
        Message = command.Message;
        ProcessUId = command.ProcessUId;
    }
    
}