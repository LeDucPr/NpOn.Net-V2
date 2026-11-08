# Rule 03: Testing, Verification & Benchmark Standards

## 1. Yêu cầu Kiểm thử Tự động (Automated Testing)
1. **Toàn bộ thay đổi phải có Test**:
   - Mọi thay đổi liên quan đến cấu trúc ReadModel, mapper hay serializer đều phải được kiểm tra qua `NpOn.Core.Tests`.
   - Chạy lệnh `dotnet test Test/NpOn.Core.Tests/NpOn.Core.Tests.csproj` và đảm bảo tỷ lệ Pass là 100%.
2. **Kiểm tra biên (Boundary & Null Safety)**:
   - Dynamic IL Emit mapper phải được kiểm thử với các tình huống:
     * Cột có giá trị `null` hoặc `DBNull.Value`.
     * Kiểu dữ liệu không đồng nhất (cần ép kiểu `Convert.ChangeType`).
     * Giá trị enum chưa được định nghĩa trong contract.

---

## 2. Tiêu chuẩn Đánh giá Hiệu năng (Benchmark Protocols)
1. **Đo đạc Throughput & Latency**:
   - Hot path mapper phải đạt tốc độ xử lý hàng trăm nghìn records trong phạm vi milliseconds mà không gây áp lực GC.
   - So sánh định kỳ giữa Unified UDAL và Dapper để đảm bảo UDAL luôn nhanh hơn hoặc ngang bằng Dapper với mức tiêu tốn RAM thấp hơn.
2. **Giám sát Garbage Collector (GC Metrics)**:
   - Theo dõi số lượng phát sinh GC Gen0, Gen1, Gen2 trong các lần chạy tải lớn.
   - Nếu Gen0 tăng vọt trong quá trình mapping vòng lặp, cần kiểm tra ngay việc quên dùng `ObjectPoolStore` hoặc unbox sai cách trong IL Emit.

