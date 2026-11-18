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

# Kích hoạt Git Hook pre-push bảo vệ tại máy Local (1 lần duy nhất)
.\setup-hooks.ps1
```

---

## 5. Quản Lý Gói Tập Trung (Central Package Management - CPM)

- Toàn bộ Solution kích hoạt **Central Package Management (CPM)** qua `Directory.Packages.props`.
- **Quy tắc tuyệt đối**: Không bao giờ khai báo `Version="..."` trong thẻ `<PackageReference>` của các file `.csproj`.
- Mọi cập nhật version thư viện chỉ được thực hiện tại `Directory.Packages.props`.

---

## 6. Kiến Trúc Saga Phân Tán Không Khóa & Native Merge Audit (`Common\Extensions\NpOn.Saga`)

- **Không dùng Distributed Lock**: Áp dụng Saga Local Transactions với Eventual Consistency.
- **Transactional Outbox / Inbox**: Bảo vệ Dual-Write và chống trùng lặp (Idempotent Consumer).
- **Tối ưu cực đại lệnh `MERGE`**:
  * Tận dụng PostgreSQL `RETURNING (xmax = 0)` để phân loại Insert vs Update trong 1 chu kỳ duy nhất.
  * Ghi batch audit siêu tốc qua Binary Protocol `NpgsqlBinaryImporter` (`COPY ... FROM STDIN BINARY`) đạt 350,000+ rec/s.
  * Đệm bất đồng bộ qua `System.Threading.Channels` đẩy sang Dedicated Audit DB (Postgres / ClickHouse).
- Chi tiết xem tại: [ARCHITECTURE.md](file:///d:/LeDucPr/NpOn.Net-V2/Common/Extensions/NpOn.Saga/ARCHITECTURE.md).

---

## 7. Quy Trình CI/CD Tự Động & Git Hook 2-Tier Shield

- **Tier 1 (Local)**: Git Hook `.githooks/pre-push` chặn đứng code lỗi tại máy trạm trước khi push.
- **Tier 2 (Cloud)**: GitHub Actions `.github/workflows/build-ci.yml`:
  * Job 1 `verify-and-test`: Biên dịch 0 lỗi và 29/29 Unit Tests pass.
  * Job 2 `build-and-push-images`: Kích hoạt khi merge PR hoặc commit message chứa `%buildCI%` (không phân biệt hoa thường).
  * Đóng gói Docker chiseled siêu nhẹ (137 MB – 210 MB) và đẩy lên `ghcr.io`.
  * **Retention**: Tự động dọn dẹp, chỉ giữ lại đúng 2 image (Latest + 1 bản liền kề).
- Chi tiết xem tại: [CI_CD_WORKFLOW_FLOW.md](file:///d:/LeDucPr/NpOn.Net-V2/manifests/CI_CD_WORKFLOW_FLOW.md).

---

## 8. Bản Đồ Kỹ Năng Agent (.agents/skills) & Tài Liệu Kiến Trúc

Mọi AI Bot khi truy cập hệ thống cần tham chiếu các tài liệu và kỹ năng chuyên biệt sau:
- **Kiến trúc tổng thể**: [.agents/ARCHITECTURE.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/ARCHITECTURE.md)
- **Kỹ năng Phân tích kiến trúc**: [.agents/skills/architecture-analysis/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/architecture-analysis/SKILL.md)
- **Kỹ năng Dynamic IL Emit**: [.agents/skills/il-emit-engineering/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/il-emit-engineering/SKILL.md)
- **Kỹ năng Đo lường hiệu năng**: [.agents/skills/performance-benchmarking/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/performance-benchmarking/SKILL.md)
- **Kỹ năng Saga & Native Audit**: [.agents/skills/saga-and-audit/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/saga-and-audit/SKILL.md)
- **Kỹ năng Chẩn đoán sự cố**: [.agents/skills/troubleshooting-diagnostics/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/troubleshooting-diagnostics/SKILL.md)
- **Kỹ năng Review Code Đỉnh Cao (Google/Microsoft)**: [.agents/skills/code-review-excellence/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/code-review-excellence/SKILL.md)
- **Kỹ năng Rà Soát Bảo Mật (OWASP API Top 10/CWE)**: [.agents/skills/security-audit-hardening/SKILL.md](file:///d:/LeDucPr/NpOn.Net-V2/.agents/skills/security-audit-hardening/SKILL.md)

---

## 9. Quy Chuẩn Enum Gõ Mạnh & Cấm Tuyệt Đối Magic Numbers

- Mọi trạng thái (`status`), kết quả xử lý (`result`), hoặc hành vi dữ liệu (`operation`) trong toàn bộ Entities, DTOs, Controllers, và Database queries **bắt buộc phải được định nghĩa bằng Strongly-Typed Enum** (như `EMergeClassification`, `ESagaStatus`, `ESagaInboxProcessingResult`).
- Thuộc tính `[Display(Name = "...")]` của Enum **bắt buộc phải viết bằng tiếng Anh** (ví dụ: `[Display(Name = "Pending")]`).
- Tuyệt đối cấm sử dụng số nguyên trần (Magic Numbers: `0`, `1`, `2`, `-1`,...) trong mã nguồn.

---

## 10. Quy Chuẩn Đối Xứng Tuyệt Đối Driver Database: Postgres & ScyllaDB/Cassandra (Database Driver Parity)

> [!IMPORTANT]
> **NGUYÊN TẮC ĐỐI XỨNG TUYỆT ĐỐI (DRIVER PARITY STANDARD)**:
> - Mọi tính năng, trường hợp kiểm thử (test cases), kịch bản kiểm tra hiệu năng (benchmarks), và các kiểm chứng an toàn dữ liệu đã được thiết lập cho **PostgreSQL** (`NpOn.PostgresExtCm`) **BẮT BUỘC PHẢI ĐƯỢC KIỂM THỬ VÀ ĐẠT CHUẨN TƯƠNG ĐƯƠNG TRÊN CASSANDRA / SCYLLADB** (`NpOn.CassandraExtCm`).
> - Tuyệt đối không để xảy ra tình trạng "Postgres chạy tốt nhưng Cassandra bỏ ngỏ hoặc chưa test".

### 10.1. Kiểm thử toàn bộ 22 kiểu dữ liệu Cassandra/ScyllaDB
Toàn bộ các kiểu dữ liệu CQL phải được kiểm thử đọc/ghi và ánh xạ bằng Dynamic IL Emit sang C# ReadModel:
- **Số học & Nguyên thủy**: `uuid` (Guid), `ascii` (string), `bigint` (long), `smallint` (short), `tinyint` (sbyte), `int` (int), `double` (double), `float` (float), `decimal` (decimal), `boolean` (bool).
- **Nhị phân & Định danh thời gian**: `blob` (byte[]), `timeuuid` (TimeUuid -> Guid).
- **Thời gian (Temporal)**: `date` (LocalDate -> DateTime/DateOnly), `time` (LocalTime -> TimeSpan/TimeOnly), `timestamp` (DateTimeOffset/DateTime), `duration` (TimeSpan).
- **Mạng & Số nguyên lớn**: `inet` (System.Net.IPAddress), `varint` (System.Numerics.BigInteger).
- **Tập hợp (Collections)**: `list<T>` (List<T>/T[]), `set<T>` (HashSet<T>/ISet<T>), `map<K,V>` (Dictionary<K,V>/IDictionary<K,V>).
- **Cấu trúc & Nullable**: `tuple<...>`, Strongly-Typed Enums, và Null safety (tombstones / DBNull).

### 10.2. Bảo đảm Null-Safety và Triệt Tiêu Exception
- Khi đọc giá trị từ `Cassandra.Row`, bắt buộc phải kiểm tra `row.IsNull(c)` trước khi gọi `row.GetValue(Type, c)`. Tuyệt đối không được để ném `NullReferenceException: Cannot convert null to ... because it is a value type`.
- Trường hợp giá trị null, `FlatData` lưu trữ `null` và `NpOnTypeConverter` ánh xạ an toàn về default value hoặc nullable type (`int?`, `Guid?`, `bool?`).

### 10.3. Đối xứng các kịch bản kiểm thử (Test Matrix Parity)
- **Stress Concurrency**: 50 task song song đồng thời thực hiện query/login (`Cassandra_AccountQuery_Concurrent50Tasks_ShouldAllSucceed`).
- **Loop Stability**: 100 lần lặp liên tục không suy giảm hiệu năng (`Cassandra_AccountQuery_ShouldSucceed100TimesWithoutFailure`).
- **Delegate Caching**: Delegate mapping sinh bởi Dynamic IL Emit phải được cache 100% (`GetOrCreateRowMapper`), 0 re-compilation.
- **Multi-thread Thread-Safety**: Mapping đồng thời trên 20 threads song song không bị xung đột tài nguyên (`Cassandra_MultiThreaded_ConcurrentMapping_ShouldBeThreadSafe`).
- **Static Type & DbType Caching**: `CassandraCell<T>` cache tĩnh `DbType`, không gọi Reflection trên hot-path.

### 10.4. Kiểm thử hiệu năng ánh xạ (Mapping Performance & Zero GC Leak)
- Sau khi kiểm tra chức năng, **bắt buộc phải kiểm thử hiệu năng Dynamic IL Emit Mapping** với 10,000 iterations:
  * Throughput đạt tối thiểu **> 10,000 operations/giây** (thực tế UDAL đạt 200,000+ operations/sec).
  * Kiểm soát bộ nhớ rác: **GC Gen2 = 0** tuyệt đối (`Assert.Equal(0, finalGen2)`), Gen1 <= 2.
  * Tái sử dụng bộ nhớ: 100% buffer dữ liệu phẳng được cấp phát qua `ArrayPool<object?>.Shared`.

### 10.5. Chuẩn so sánh đa Framework (3-Way Multi-Framework Benchmark)
- Đối với các Database có hỗ trợ nhiều ORM/Driver (Postgres qua `Test/NpOn.OmsDapper`):
  * **So sánh đồng thời 3 framework**: `Unified UDAL` vs `Dapper 2.1` vs `EF Core 9.0` (`AsNoTracking`).
  * **Khử định kiến cache (Rotated Execution)**: Xoay vòng thứ tự thực thi giữa UDAL, Dapper, và EF Core để kết quả đo đạc hoàn toàn khách quan.
  * **Đo đạc 4 chiều**: Throughput (rec/s), Latency (mean/min/max/P95), GC Gen0/1/2 delta, và kích thước thư viện (DLL Footprint: UDAL ~48 KB vs Dapper ~230 KB vs EF Core ~2.5 MB).





