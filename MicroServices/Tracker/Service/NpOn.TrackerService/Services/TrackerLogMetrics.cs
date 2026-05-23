using System.Text;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public static class TrackerLogMetrics
{
    private static long _pushLogsRequests;
    private static long _pushLogsSuccess;
    private static long _pushLogsFailed;
    private static long _pushLogsEmpty;
    private static long _logsReceived;
    private static long _logsStored;
    private static long _logsDropped;

    public static void TrackPushLogsRequest(int commandCount)
    {
        Interlocked.Increment(ref _pushLogsRequests);
        if (commandCount > 0)
        {
            Interlocked.Add(ref _logsReceived, commandCount);
        }
        else
        {
            Interlocked.Increment(ref _pushLogsEmpty);
        }
    }

    public static void TrackPushLogsSuccess(int entryCount)
    {
        Interlocked.Increment(ref _pushLogsSuccess);
        Interlocked.Add(ref _logsStored, entryCount);
    }

    public static void TrackPushLogsFailed(int entryCount)
    {
        Interlocked.Increment(ref _pushLogsFailed);
        Interlocked.Add(ref _logsDropped, entryCount);
    }

    public static string Collect()
    {
        var builder = new StringBuilder();

        builder.AppendLine("# HELP npon_tracker_push_logs_requests_total Total push log requests received by TrackerLogService.");
        builder.AppendLine("# TYPE npon_tracker_push_logs_requests_total counter");
        builder.AppendLine($"npon_tracker_push_logs_requests_total {_pushLogsRequests}");

        builder.AppendLine("# HELP npon_tracker_push_logs_empty_total Push log requests with no commands.");
        builder.AppendLine("# TYPE npon_tracker_push_logs_empty_total counter");
        builder.AppendLine($"npon_tracker_push_logs_empty_total {_pushLogsEmpty}");

        builder.AppendLine("# HELP npon_tracker_push_logs_success_total Successfully persisted push log requests.");
        builder.AppendLine("# TYPE npon_tracker_push_logs_success_total counter");
        builder.AppendLine($"npon_tracker_push_logs_success_total {_pushLogsSuccess}");

        builder.AppendLine("# HELP npon_tracker_push_logs_failed_total Failed push log requests.");
        builder.AppendLine("# TYPE npon_tracker_push_logs_failed_total counter");
        builder.AppendLine($"npon_tracker_push_logs_failed_total {_pushLogsFailed}");

        builder.AppendLine("# HELP npon_tracker_logs_received_total Total log entries received by TrackerLogService.");
        builder.AppendLine("# TYPE npon_tracker_logs_received_total counter");
        builder.AppendLine($"npon_tracker_logs_received_total {_logsReceived}");

        builder.AppendLine("# HELP npon_tracker_logs_stored_total Total log entries successfully stored to ClickHouse.");
        builder.AppendLine("# TYPE npon_tracker_logs_stored_total counter");
        builder.AppendLine($"npon_tracker_logs_stored_total {_logsStored}");

        builder.AppendLine("# HELP npon_tracker_logs_dropped_total Total log entries dropped because storage failed.");
        builder.AppendLine("# TYPE npon_tracker_logs_dropped_total counter");
        builder.AppendLine($"npon_tracker_logs_dropped_total {_logsDropped}");

        return builder.ToString();
    }
}
