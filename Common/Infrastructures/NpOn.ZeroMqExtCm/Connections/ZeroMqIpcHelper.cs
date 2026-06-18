using System.Runtime.InteropServices;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;

public static class ZeroMqIpcHelper
{
    // Renamed variable for semantic clarity: This is the Base Name, excluding the "ipc://" prefix
    public static readonly string IpcBaseName = "npon-zmq-pipe-";

    public static string CombineConnectionStringIpc(int hostPort)
    {
        // Windows ??
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"ipc://{IpcBaseName}{hostPort}";
        }
        
        // If running on Linux (Ubuntu) or macOS
        // Absolute path is required (3 slashes ipc:///)
        // Store in /tmp directory because it has Read/Write permissions for all OS users by default.
        return $"ipc:///tmp/{IpcBaseName}{hostPort}";
    }
}