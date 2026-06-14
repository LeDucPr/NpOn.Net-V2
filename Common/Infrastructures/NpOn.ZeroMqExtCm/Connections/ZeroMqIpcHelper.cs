namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;

public static class ZeroMqIpcHelper
{
    public static readonly string IpcCommonConnectionString = "ipc://npon-zmq-pipe-";
    public static readonly string IpcCommonFolderName = "npon-zmq-pipe";

    public static string CombineConnectionStringIpc(int hostPort)
    {
        return $"{IpcCommonConnectionString}{hostPort}";
    }
}