using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceCollectionExtensions
{
    private static readonly string IpcCommonConnectionString = "ipc://npon-zmq-pipe-";


    private static bool CombineConnectionStringIpc(out string? connectionString)
    {
        connectionString = null;
        int hostPort = EApplicationConfiguration.HostPort.GetAppSettingConfig().AsDefaultInt();
        if (hostPort == 0)
        {
#if DEBUG
            return false;
#endif
            throw new ArgumentException("HostPort is required to configuration IPC identifier");
        }

        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();

        // Cấu hình đường dẫn file IPC tùy theo Hệ điều hành
        if (OperatingSystem.IsWindows())
        {
            // Trên Windows, IPC của ZeroMQ sử dụng cơ chế Named Pipes
            // Định dạng bắt buộc: ipc:///chuo_ten_pipe
            connectionString = $"{IpcCommonConnectionString}{hostPort}";
        }
        else
        {
            // Trên Linux / WSL2 / Docker, IPC sử dụng Unix Domain Socket (là một file vật lý)
            // Thường lưu trong thư mục tạm /tmp hoặc /home/app để đảm bảo quyền ghi
            string baseDir = "/tmp";
            if (!Directory.Exists(baseDir))
            {
                baseDir = Path.GetTempPath();
            }

            string ipcFolder = Path.Combine(baseDir, appName);

            try
            {
                if (!Directory.Exists(ipcFolder))
                    Directory.CreateDirectory(ipcFolder);
            }
            catch
            {
                // Nếu lỗi quyền ghi, fallback về thẳng thư mục temp cơ bản
                ipcFolder = Path.GetTempPath();
            }

            // Đường dẫn file kết quả dạng: ipc:///tmp/YourAppName/zmq-40004.ipc
            string fullPath = Path.Combine(ipcFolder, $"zmq-{hostPort}.ipc");
            connectionString = $"ipc://{fullPath.Replace("\\", "/")}";
        }

        return true;
    }

    public static IServiceCollection AddZeroMqTwoWay(this IServiceCollection services,
        string? connectionString = null,
        params Type[]? handlerTypes)
    {
        // Gọi hàm sinh chuỗi kết nối IPC thay vì InProc
        if (!CombineConnectionStringIpc(out connectionString))
            return services;

        if (handlerTypes != null)
        {
            foreach (var type in handlerTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(BaseZeroMqTwoWayHandler), provider => provider.GetRequiredService(type));
            }
        }

        services.AddSingleton<IZeroMqTwoWayProvider, ZeroMqTwoWayProvider>(provider =>
        {
            var connectOption = new ZeroMqConnectOption();
            connectOption.SetConnectionString(connectionString!);

            ZeroMqTwoWayProvider? factoryWrapper = new ZeroMqTwoWayProvider(connectOption);

            var handlers = provider.GetServices<BaseZeroMqTwoWayHandler>().ToArray();
            if (!handlers.Any())
                return factoryWrapper;

            foreach (var handler in handlers)
                factoryWrapper += handler;

            if (!factoryWrapper!.BuildFactory(out string? errorString))
                Console.WriteLine(errorString);

            return factoryWrapper;
        });

        return services;
    }
}