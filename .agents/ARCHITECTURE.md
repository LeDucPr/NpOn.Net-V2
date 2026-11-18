# NpOn.Net-V2: Toàn Bộ Kiến Trúc Kỹ Thuật Hệ Thống (Master Architecture Blueprint)

> **Dành cho AI Agents (Antigravity, Claude, Codex, Cursor) & Developers**:  
> Đây là bản đặc tả kiến trúc toàn diện và duy nhất của hệ sinh thái **NpOn.Net-V2** - Hệ thống Backend AIoT Y tế Hiệu năng cao xây dựng trên nền tảng **.NET 10**.

---

## 1. Bản Đồ Topology Mạng & Microservices (Network & Service Topology)

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
       │  - Universal NpOnTypeConverter (Array/Jsonb/Guid/Enums)│
       └────────────────────────┬───────────────────────────────┘
                                │
                 ┌──────────────┴──────────────┐
                 ▼                             ▼
       [ PostgreSQL (account) ]      [ PostgreSQL (general) ]
```

### 1.1 Bảng Phân Bổ Cổng Dịch Vụ (Port Allocation Table)

| Thành phần | Port | Giao thức | Vai trò & Trách nhiệm |
|---|:---:|:---:|---|
| **`Controllers/NpOn.SSO`** | **14023** | HTTP/1.1 REST & h2c | API Gateway đón nhận traffic từ Client, xác thực JWT Bearer, Rate Limiting, Reverse Proxy |
| **`MicroServices/Account`** | **40004** | gRPC (HTTP/2) | Quản lý định danh (Identity), Account, Profile, Token Vault, Multi-Broker Event Bus |
| **`MicroServices/General`** | **40000** | gRPC (HTTP/2) | Truy vấn Metadata động, Dynamic Query Master, TblMaster & FldQuery |
| **`PostgreSQL`** | **5432** | TCP PostgreSQL | Cơ sở dữ liệu quan hệ chính (`account`, `general`) |
| **`Dragonfly / Redis`** | **6379** | RESP Protocol | Bộ nhớ đệm phân tán L1/L2 tốc độ cao, Token Revocation List |
| **`RabbitMQ`** | **5672 / 15672** | AMQP 0-9-1 | Hàng đợi tin nhắn nghiệp vụ bất đồng bộ |
| **`Kafka / RedPanda`** | **9092 / 19092** | Kafka Protocol | Streaming Event Bus cho thiết bị AIoT y tế thời gian thực |

---

## 2. Đường Ống Kép Dual-Pipeline gRPC (`protobuf-net.Grpc`)

1. **Nội bộ Server-to-Server (S2S Hot-Path)**:
   - Giao tiếp giữa SSO Controller và các dịch vụ nền tảng đi qua kênh HTTP/2 Binary Code-First gRPC độc quyền (`CommonGrpcCall`).
   - Loại bỏ hoàn toàn chi phí serialize/deserialize chuỗi JSON lặp đi lặp lại giữa các microservices.
2. **Cổng ngoài Public (Client Facing)**:
   - Cung cấp đồng thời RESTful HTTP/1.1 chuẩn JSON cho các ứng dụng Frontend/Mobile.
   - Hỗ trợ cổng gRPC Plaintext (`h2c`) cho phép debug/test trực tiếp qua Postman hoặc gRPC CLI.

---

## 3. Kiến Trúc Unified UDAL & Dynamic IL Emit

### 3.1 Kiểm Soát Tải Bằng Semaphore (`SemaphoreSlim`)
- Mọi kết nối Database được kiểm soát nghiêm ngặt qua Semaphore đếm số lượng kết nối tối đa (`PostgresConnectionNumber`).
- Ngăn ngừa tình trạng Connection Spike làm cạn kiệt tài nguyên Postgres khi có tải đột biến (Connection Pool Starvation).

### 3.2 Dynamic IL Emit (Sinh Mã Máy IL Runtime - Native C# Speed)
- Nằm tại: `PostgresDriver.cs`, `JsonModeWithCache.cs`, `NpOnWrapperResultToObjectExtensions.cs`.
- **Nguyên lý hoạt động**:
  - Không sử dụng Reflection (`Type.GetProperty().SetValue()`) trên đường ống xử lý dữ liệu.
  - Sử dụng `System.Reflection.Emit.DynamicMethod` để sinh mã byte IL trực tiếp tại runtime đọc các phương thức gõ mạnh cụ thể của `NpgsqlDataReader` (`GetInt64`, `GetString`, `GetGuid`, `GetDateTime`, `GetBoolean`,...).
  - Tốc độ đọc dữ liệu đạt tới **862,000+ records/giây**, nhanh hơn Dapper từ **14.9% đến 32.1%**.
- **Quy tắc bộ nhớ IL Evaluation Stack**:
  - Đảm bảo stack cân bằng tuyệt đối: `OpCodes.Box` cho Value Types khi cần, `OpCodes.Castclass` / `OpCodes.Unbox_Any` cho kiểu đích, và `OpCodes.Pop` khi discard giá trị trả về.
  - Sau khi biên dịch, Delegate được cache trong `WrapperCacheStore` để tái sử dụng vĩnh viễn (zero recompilation).

### 3.3 Bộ Nhớ Zero-Allocation & Object Pooling (`IObjectPoolStore`)
- Các đối tượng wrapper trung gian (`NpOnWrapperResult`, `ITabularResultSet`, row/cell containers) được lấy ra từ `ConcurrentBag` trong `ObjectPoolStore` và tái sử dụng qua `ArrayPool<T>`.
- Đạt chỉ số **Gen0 = 0, Gen1 = 0, Gen2 = 0** trong vòng lặp 10,000 lần truy vấn (Zero GC Leak).

### 3.4 Bộ Chuyển Kiểu Toàn Năng (`NpOnTypeConverter`)
- Nằm tại: `Common/Extensions/NpOn.ICommonDb/DbResults/Extensions/NpOnTypeConverter.cs`.
- Hỗ trợ tự động chuyển đổi 100% kiểu dữ liệu .NET sang PostgreSQL và ngược lại:
  - Kiểu nguyên thủy: `long`, `int`, `short`, `byte`, `double`, `float`, `decimal`, `bool`, `string`.
  - Kiểu thời gian: `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`.
  - Kiểu định danh: `Guid`, `Enum` (chuỗi hoặc số nguyên).
  - Mảng và Tập hợp: `T[]`, `List<T>`, `IList<T>`.
  - Cấu trúc phức tạp: `object`, `record`, `class` tự động map sang **JSONB native** của PostgreSQL.

---

## 4. Quản Lý Gói Tập Trung (Central Package Management - CPM)

- Toàn bộ 40+ project `.csproj` trong Solution được quản lý tập trung qua file gốc:
  `Directory.Packages.props`
- **Quy chuẩn**:
  - Tuyệt đối không khai báo thuộc tính `Version="..."` trong thẻ `<PackageReference>` của các file `.csproj` con.
  - Mọi nâng cấp phiên bản thư viện (như `Npgsql`, `protobuf-net.Grpc`, `Serilog`,...) chỉ được thực hiện tại `Directory.Packages.props`.

---

## 5. Kiến Trúc Saga Phân Tán Không Khóa, Đa Database Engine & Khóa Xung Đột (`Common\Extensions\NpOn.Saga`)

- **Mục tiêu**: Chống deadlock và race condition khi xử lý giao dịch đồng thời qua nhiều microservices.
- **Trụ cột cốt lõi**:
  1. **Non-blocking Local Transactions**: Mỗi service commit dữ liệu cục bộ và giải phóng connection ngay trong vài ms.
  2. **Khóa Xung Đột (Optimistic Concurrency Token / Entity Versioning)**:
     - Mọi thực thể mang trường `version: long`. Khi cập nhật bắt buộc kiểm tra `WHERE version = @expected_version`.
     - Ngăn chặn hoàn toàn Lost Update và hiện tượng đọc dữ liệu cũ (Stale Read) ghi đè làm sai lệch Audit Trail.
  3. **Transactional Outbox & Inbox Đơn Điệu**:
     - Outbox mang bộ tứ `(entity_key, pre_version, post_version, sequence_number)` và partition theo `entity_key` để đảm bảo thứ tự tuần tự tuyệt đối (Strict FIFO).
     - Inbox đóng vai trò Causality Guard: Chỉ áp dụng event khi `event.pre_version == last_applied_version`.
  4. **Ma Trận Đa Database Engine Cho Lệnh `MERGE` & Audit Siêu Tốc**:
     - **PostgreSQL / YugabyteDB**: `RETURNING (xmax = 0)` và `NpgsqlBinaryImporter` (`COPY ... FROM STDIN BINARY`) đạt 350,000+ rec/s.
     - **Microsoft SQL Server (MSSQL)**: Native `MERGE ... OUTPUT $action AS Action, DELETED.*, INSERTED.*` và `SqlBulkCopy` đạt 250,000+ rec/s.
     - **MySQL (InnoDB 8.0+)**: `affected_rows` (1: Insert, 2: Update) kết hợp `LOAD DATA LOCAL INFILE` đạt 200,000+ rec/s.
     - **ClickHouse**: Vectorized Append-Only Insert với `ReplacingMergeTree(version)` và `ClickHouseBulkCopy` TCP Native đạt 1,000,000+ rec/s.
     - **Cassandra / ScyllaDB**: Paxos LWT `UPDATE ... IF version = :expected_ver` và Token-Aware Batching.
     - **Kênh Đệm Bất Đồng Bộ Lock-Free**: `System.Threading.Channels` gom micro-batch và flush sang Dedicated Audit Database trong background.
- Chi tiết xem tại: [ARCHITECTURE.md](file:///d:/LeDucPr\NpOn.Net-V2\Common\Extensions\NpOn.Saga\ARCHITECTURE.md).

---

## 6. Cơ Chế Bảo Vệ 2 Lớp (CI/CD & Git Hook 2-Tier Shield)

1. **Lớp 1: Chốt Chặn Local (Pre-Push Hook)**:
   - File: `.githooks/pre-push` (Kích hoạt bằng `.\setup-hooks.ps1`).
   - Tự động chạy `dotnet build NpOnV2.sln -c Release` và 29 Unit Tests trước khi cho phép `git push`.
   - Nếu có bất kỳ lỗi nào, lệnh `git push` bị chặn đứng ngay lập tức.
2. **Lớp 2: Chốt Chặn Cloud CI Gate**:
   - File: `.github/workflows/build-ci.yml`.
   - Job `verify-and-test` kiểm tra môi trường sạch độc lập.
   - Job `build-and-push-images` chỉ chạy khi:
     - Toàn bộ test pass 100%.
     - Pull Request được MERGE hoặc Commit message chứa từ khóa `%buildCI%` (không phân biệt hoa thường).
   - Đóng gói Docker container trên nền .NET 10 Chiseled Ubuntu siêu nhẹ (137 MB – 210 MB) và đẩy lên `ghcr.io`.
   - **Chính sách lưu giữ**: Tự động dọn dẹp, chỉ giữ lại đúng 2 image (Bản `latest` và đúng 1 bản liền kề trước đó).

---

## 7. Quy Tắc Bắt Buộc 5 Giai Đoạn Harness (`run-harness.ps1`)

**QUY TẮC BẤT DI BẤT DỊCH**: Bất kỳ thay đổi code nào trước khi bàn giao bắt buộc phải thực thi và vượt qua 100% cả 5 giai đoạn:
1. **Giai đoạn 1 (Architecture & Compilation)**: Kiểm tra ranh giới lớp, không cho phép Controller gọi trực tiếp DB driver. 0 lỗi biên dịch.
2. **Giai đoạn 2 (Testing & Dynamic IL Verification)**: Chạy toàn bộ test suites trong `NpOn.Core.Tests`.
3. **Giai đoạn 3 (Performance & Zero-Allocation)**: Đo Throughput (đạt > 3,000 ops/s) và GC collections (Gen0/1/2 = 0).
4. **Giai đoạn 4 (Reporting)**: Xuất file báo cáo chuẩn Markdown tại `.agents/reports/harness_latest.md`.
5. **Giai đoạn 5 (Diagnostics)**: Kiểm tra tính cân bằng của IL Evaluation Stack và Semaphore connection pool.

---

## 8. Quy Chuẩn Đối Xứng Driver Database: Postgres & ScyllaDB/Cassandra (Driver Parity)

- **Nguyên tắc**: Mọi trường hợp test case, stress concurrency, delegate caching, và zero-allocation mapping có trên PostgreSQL (`NpOn.PostgresExtCm`) **đều phải có tương ứng 1:1 trên Cassandra / ScyllaDB (`NpOn.CassandraExtCm`)**.
- **22 Kiểu Dữ Liệu CQL Hỗ Trợ Đầy Đủ**:
  * Số học: `uuid`, `ascii`, `bigint`, `smallint`, `tinyint`, `int`, `double`, `float`, `decimal`, `boolean`.
  * Nhị phân & Định danh thời gian: `blob`, `timeuuid` (TimeUuid -> Guid).
  * Thời gian: `date` (LocalDate -> DateTime), `time` (LocalTime -> TimeSpan), `timestamp` (DateTimeOffset/DateTime), `duration`.
  * Mạng & Số nguyên lớn: `inet` (IPAddress), `varint` (BigInteger).
  * Tập hợp & Cấu trúc: `list<T>`, `set<T>`, `map<K,V>`, `tuple<...>`.
- **Null Safety Tuyệt Đối**: Kiểm tra `row.IsNull(c)` trước khi gọi `row.GetValue(Type, c)` để triệt tiêu lỗi `NullReferenceException` khi gặp tombstone.
- **Cache Khóa Chính Vĩnh Viễn**: `PrimaryKeyCache` (`ConcurrentDictionary<string, HashSet<string>>`) loại bỏ hoàn toàn các lượt gọi truy vấn metadata đồng bộ trên hot-path.


