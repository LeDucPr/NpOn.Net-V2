using System.ComponentModel.DataAnnotations;

namespace MicroServices.Tracker.Definitions.NpOn.TrackerEnum;

[Flags]
public enum ETrackerLogType : byte
{
    [Display(Name="ErrorLog")] ErrorLog = 1 << 0, 
    [Display(Name="EventLog")] EventLog = 1 << 1, 
    [Display(Name="AuditLog")] AuditLog = 1 << 2,
    [Display(Name="TraceLog")] TraceLog = 1 << 3
}

[Flags]
public enum ETrackerLogLevel : byte
{
    [Display(Name="Information")] Information = 1 << 0,
    [Display(Name="Warning")] Warning = 1 << 1,
    [Display(Name="Error")] Error = 1 << 2,
    [Display(Name="Critical")] Critical = 1 << 3,
    [Display(Name="Debug")] Debug = 1 << 4
}