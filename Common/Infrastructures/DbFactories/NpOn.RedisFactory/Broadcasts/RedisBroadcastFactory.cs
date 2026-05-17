using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public class RedisBroadcastFactory : IBaseBroadcastFactory
{
    private readonly List<BaseRedisBroadcastHandler> _handlers = [];
    public int HandlerCount { get; private set; }

    public RedisBroadcastFactory()
    {
    }
    // Danh sách lưu trữ nội bộ để quản lý các Handler đã được cộng vào

    // Factory + Handler
    public static RedisBroadcastFactory operator +(RedisBroadcastFactory? factory, BaseRedisBroadcastHandler? handler)
    {
        if (factory == null || handler == null)
            return factory!;
        factory._handlers.Add(handler);
        factory.HandlerCount++;
        return factory;
    }
}


//
// // 5. Hàm chạy chính (Main Program)
// class Program
// {
//     static async Task Main(string[] args)
//     {
//         Console.OutputEncoding = System.Text.Encoding.UTF8;
//         Console.WriteLine("=== KHỞI CHẠY HỆ THỐNG EVENT TRIGGER ===");
//
//         // Khởi tạo bộ kích hoạt hạ tầng
//         RedisPubSubTrigger trigger = new RedisPubSubTrigger();
//
//         // Khởi tạo các Service nghiệp vụ và cho tụi nó tự đăng ký vào sự kiện ngầm
//         CacheStorageService cacheService = new CacheStorageService(trigger);
//         NotificationService notificationService = new NotificationService(trigger);
//
//         // --- GIẢ LẬP SỰ KIỆN REALTME DIỄN RA ---
//         // Giả vờ có 1 tin nhắn đầu tiên bay từ Dragonfly về
//         await trigger.SimulateIncomingMessage("kenh_tin_tuc", "User 104 vừa bấm nút Đặt Hàng");
//
//         await Task.Delay(1500); // Đợi một chút
//
//         // Giả vờ có tin nhắn thứ hai bay về
//         await trigger.SimulateIncomingMessage("kenh_tin_tuc", "Lỗi Timeout kết nối tới cổng thanh toán");
//
//         Console.ReadLine();
//     }
// }