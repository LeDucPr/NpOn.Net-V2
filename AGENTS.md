# NpOn.Net-V2: AI Agent & Developer Operating Manifest (AGENTS.md)

Chào mừng AI Agent và Developer đến với hệ sinh thái **NpOn.Net-V2** - Hệ thống Backend AIoT Y tế Hiệu năng cao dựa trên nền tảng .NET 10.

---

## 1. Cơ Chế Chạy và Vận Hành Toàn Diện (System Run Mechanics)

Hệ thống `NpOn.Net-V2` được thiết kế theo mô hình Microservices phân tán với hạ tầng Unified UDAL tối ưu hóa triệt để tài nguyên phần cứng.

```
                          [ Client / Web / Mobile App ]
                                       │
                                       ▼ (HTTP/1.1 JSON - Port 14023)
                        ┌───────────────────────────────┐
                        │      Controllers/NpOn.SSO     │
                        │    (Gateway / Public Entry)   │
                        └───────┬───────────────┬───────┘
                                │               │
       (gRPC HTTP/2 - Port 40004)               │ (gRPC HTTP/2 - Port 40000)
                                ▼               ▼
           ┌────────────────────────┐       ┌────────────────────────┐
           │   NpOn.AccountService  │       │   NpOn.GeneralService  │
           │  (Identity/Auth/Tokens)│       │  (Dynamic Query Master)│
           └────────────┬───────────┘       └────────────┬───────────┘
                        │                                │
                        ▼                                ▼
       ┌────────────────────────────────────────────────────────┐
       │                Unified UDAL Data Access                │
       │  - SemaphoreSlim Connection Throttling (Factory Pool)  │
       │  - Zero-Allocation ObjectPoolStore (Wrappers Recycling)│
       │  - Dynamic IL Emit (Runtime Machine Code Generation)   │
       └────────────────────────┬───────────────────────────────┘
                                │
                 ┌──────────────┴──────────────┐
                 ▼                             ▼
       [ PostgreSQL (account) ]      [ PostgreSQL (general) ]
```

### 1.1 Khởi động và Build song song (`run.ps1`)
- **Tự động nhận diện IP Host (`Get-HostIP`)**: Tự động lọc card mạng vật lý để gán biến môi trường `$env:HOST_IP` cho Kafka/RedPanda và Docker kết nối về host database.
- **Biên dịch Đa luồng Multi-Core (`dotnet publish -t:PublishContainer`)**: Khởi chạy đồng thời các tiến trình `Start-Job` để build song song tất cả các Microservices (`AccountService`, `GeneralService`, `SSO`), rút ngắn thời gian đóng gói Docker image tới 3-5 lần.
- **Khởi chạy Docker Orchestration**:
  * `SSO`: Port `14023` (Cổng API Gateway chính tiếp nhận request).
  * `AccountService`: Port `40004` (Dịch vụ tài khoản & bảo mật).
  * `GeneralService`: Port `40000` (Dịch vụ câu lệnh & metadata động).

### 1.2 Đường ống Kép Dual-Pipeline gRPC (`protobuf-net.Grpc`)
- **Nội bộ S2S**: Giao tiếp giữa SSO Controller và các dịch vụ nền tảng đi qua HTTP/2 Binary độc quyền (`CommonGrpcCall`), loại bỏ hoàn toàn chi phí serialize/deserialize chuỗi JSON lặp đi lặp lại.
- **Cổng ngoài Public**: Mở đồng thời HTTP/1.1 REST chuẩn cho Frontend và cổng gRPC Plaintext (`h2c`) cho developer kiểm thử trực tiếp bằng Postman / gRPC CLI.

### 1.3 Kiến trúc Unified UDAL & Dynamic IL Emit
- **Kiểm soát tải bằng Semaphore (`SemaphoreSlim`)**: Mọi kết nối Database được bảo vệ qua Semaphore đếm số lượng kết nối tối đa, ngăn ngừa Connection Spike làm sập Postgres.
- **Dynamic IL Emit (Mã máy IL runtime)**:
  * Trong `PostgresMappingExtensions.cs`, `JsonModeWithCache.cs`, và `NpOnWrapperResultToObjectExtensions.cs`, hệ thống sử dụng `System.Reflection.Emit` và `DynamicMethod`.
  * Thay vì duyệt Reflection mỗi khi đọc một bản ghi từ DataReader/RowWrapper, IL Emit sinh mã mapping gán giá trị trực tiếp vào thuộc tính của Object với tốc độ tương đương mã viết tay native (Native C# Speed).
- **Bộ nhớ Zero-Allocation & Object Pooling (`IObjectPoolStore`)**:
  * Các đối tượng wrapper trung gian (`NpOnWrapperResult`, `ITabularResultSet`, row/cell containers) được lấy ra từ `ConcurrentBag` trong `ObjectPoolStore` và tái sử dụng, giữ lượng rác phát sinh cho GC ở mức tiệm cận 0.

---

## 2. Yêu Cầu Bắt Buộc Đối Với Bộ Khung Harness (`run-harness.ps1`)

**QUY TẮC BẮT BUỘC**: Mỗi lần chạy kiểm tra hoặc xác nhận thay đổi (build/test/commit), hệ thống **BẮT BUỘC PHẢI THỰC HIỆN TOÀN BỘ 5 GIAI ĐOẠN** sau:

### Giai đoạn 1: Phân tích kiến trúc (Architecture & Code Analysis)
- Kiểm tra toàn bộ ranh giới giữa Gateway (Controllers) và Microservices. Tuyệt đối không cho phép Controller tham chiếu trực tiếp thư viện DB Driver (`NpgsqlConnection`, `SqlConnection`).
- Biên dịch toàn bộ Solution `NpOnV2.sln`, đảm bảo 0 lỗi biên dịch và phân loại các cảnh báo (warnings) của trình biên dịch Roslyn.

### Giai đoạn 2: Test thử nghiệm (Automated Verification & Unit Tests)
- Chạy toàn bộ test suites trong `Test/NpOn.Core.Tests`.
- Kiểm chứng tính toàn vẹn của mã byte sinh bởi IL Emit:
  * `DynamicObjectMapper` chuyển đổi dữ liệu từ Cell sang Object chính xác.
  * Hỗ trợ null safety, DBNull, và các kiểu Flag Enum.
  * Kiểm tra chu trình Get và Return của `ObjectPoolStore`.

### Giai đoạn 3: Hiệu năng & Tối ưu hóa (Performance Benchmarking)
- Đo lường thông lượng (Throughput ops/sec) và độ trễ trung bình (Latency).
- Đo đạc chỉ số Garbage Collector (`Gen0`, `Gen1`, `Gen2`) và mức tiêu hao RAM delta trước và sau quá trình mapping vòng lặp (10,000 iterations).

### Giai đoạn 4: Dễ đọc cho người (Human Readability & Rich Reporting)
- Giao diện CLI rõ ràng, phân định bằng các thẻ trạng thái `[PASS]`, `[WARN]`, `[FAIL]`.
- Xuất file báo cáo Markdown chuẩn định dạng lưu trữ tại `.agents/reports/harness_latest.md` và `.agents/reports/harness_<timestamp>.md` để lưu vết lịch sử kiểm thử.

### Giai đoạn 5: Khả năng gỡ lỗi & Chẩn đoán (Debugging & Diagnostics)
- Giám sát tài nguyên runtime (Working Set RAM, ThreadPool worker status, WrapperCacheStore hit rate).
- Đưa ra hướng dẫn cụ thể về Callstack và nguyên nhân khi có lỗi phát sinh mà **KHÔNG ĐƯỢC TẮT HOẶC THAY THẾ IL EMIT**.

---

## 3. Quy Định Tuyệt Đối Về Dynamic IL Emit (ILCode)

> [!IMPORTANT]
> **NGUYÊN TẮC BẤT DI BẤT DỊCH VỀ IL EMIT**:
> - Khi thực hiện tối ưu hóa, kiểm thử hay gỡ lỗi, **TUYỆT ĐỐI KHÔNG ĐƯỢC VÔ HIỆU HÓA HOẶC BYPASS IL EMIT**.
> - Không thay thế IL Emit bằng Reflection (`Type.GetProperty().SetValue()`) hay các thư viện serialize reflection chậm chạp trên hot-path.
> - Mọi thao tác tối ưu hoặc mở rộng phải tôn trọng sự cân bằng của IL Evaluation Stack (`OpCodes.Pop` khi discard return value, `OpCodes.Box` cho Value Types, `OpCodes.Castclass` / `OpCodes.Unbox_Any`).
> - Delegate sau khi sinh phải được cache lại trong `WrapperCacheStore` để tránh recompilation.

---

## 4. Hướng Dẫn Lệnh Cho Agent & Developer

```powershell
# Chạy đầy đủ 5 giai đoạn Harness (Kiến trúc + Test + Hiệu năng + Báo cáo + Chẩn đoán)
.\run-harness.ps1

# Chạy Harness nhanh (bỏ qua bước build nếu solution đã build trước đó)
.\run-harness.ps1 -SkipBuild

# Chạy Harness với chế độ Debug chi tiết (in stacktrace và opcode error dump)
.\run-harness.ps1 -DetailedDebug

# Khởi chạy hệ thống Microservices cục bộ
.\run.ps1 all

# Chạy Benchmark so sánh Dapper vs Unified UDAL
dotnet run --project Test/NpOn.OmsDapper/NpOn.OmsDapper.csproj
```

