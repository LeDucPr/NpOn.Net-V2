# Rule 08: Quy Chuẩn Đối Xứng Tuyệt Đối Driver Database (PostgreSQL & ScyllaDB/Cassandra)

> **Mã quy tắc**: `RULE-08-DATABASE-DRIVER-PARITY`  
> **Phạm vi áp dụng**: `Common/Infrastructures/NpOn.PostgresExtCm`, `Common/Infrastructures/NpOn.CassandraExtCm`, `Common/Extensions/NpOn.ICommonDb`, `Test/NpOn.Core.Tests`.  
> **Mức độ bắt buộc**: `CRITICAL` (Bắt buộc 100%, vi phạm sẽ bị Git Hook và CI chặn push/merge).

---

## 1. Nguyên Tắc Cốt Lõi (Core Principle)

Mọi tính năng, trường hợp kiểm thử (test cases), kịch bản kiểm tra hiệu năng (benchmarks), và các kiểm chứng an toàn dữ liệu đã được thiết lập cho **PostgreSQL** (`NpOn.PostgresExtCm`) **BẮT BUỘC PHẢI ĐƯỢC THIẾT LẬP VÀ ĐẠT CHUẨN TƯƠNG ĐƯƠNG TRÊN CASSANDRA / SCYLLADB** (`NpOn.CassandraExtCm`).

Tuyệt đối cấm tình trạng bất đối xứng giữa các driver: "Postgres chạy tốt và có test đầy đủ nhưng Cassandra bị bỏ ngỏ hoặc thiếu kiểm thử".

---

## 2. Danh Mục 22 Kiểu Dữ Liệu CQL Bắt Buộc Phải Hỗ Trợ Đầy Đủ

Mọi driver ScyllaDB / Cassandra phải hỗ trợ đọc, ghi và ánh xạ Dynamic IL Emit không dùng Reflection đối với toàn bộ 22 kiểu dữ liệu sau:

| STT | CQL Type | Kiểu .NET Tương Ứng | BCL Normalization | Ghi Chú Kỹ Thuật |
| :---: | :--- | :--- | :--- | :--- |
| 1 | `uuid` | `Guid` | Native | Khóa chính ngẫu nhiên UUID v4 |
| 2 | `timeuuid` | `TimeUuid` | `(Guid)tu` | Chuyển đổi về `System.Guid` chuẩn |
| 3 | `ascii` | `string` / `char` | Native | Bảng mã ASCII chuẩn |
| 4 | `text` / `varchar` | `string` | Native | Chuỗi UTF-8 không giới hạn |
| 5 | `bigint` | `long` | Native | Số nguyên có dấu 64-bit |
| 6 | `int` | `int` | Native | Số nguyên có dấu 32-bit |
| 7 | `smallint` | `short` | Native | Số nguyên có dấu 16-bit |
| 8 | `tinyint` | `sbyte` | Native | Số nguyên có dấu 8-bit |
| 9 | `double` | `double` | Native | Số thực dấu phẩy động 64-bit |
| 10 | `float` | `float` | Native | Số thực dấu phẩy động 32-bit |
| 11 | `decimal` | `decimal` | Native | Số thập phân độ chính xác cao |
| 12 | `boolean` | `bool` | Native | Giá trị logic True/False |
| 13 | `blob` | `byte[]` | Native | Dữ liệu nhị phân thô |
| 14 | `date` | `LocalDate` | `DateTime` / `DateOnly` | Chuyển đổi qua BCL DateTime |
| 15 | `time` | `LocalTime` | `TimeSpan` / `TimeOnly` | Chuyển đổi qua BCL TimeSpan |
| 16 | `timestamp` | `DateTimeOffset` / `DateTime` | Native UTC | Mốc thời gian kèm múi giờ |
| 17 | `duration` | `Duration` | `TimeSpan` | Khoảng thời gian |
| 18 | `inet` | `IPAddress` | `IPAddress` / `string` | Địa chỉ mạng IPv4 / IPv6 |
| 19 | `varint` | `BigInteger` | `BigInteger` / `string` | Số nguyên vô hạn |
| 20 | `list<T>` | `List<T>` / `T[]` | `IEnumerable` Loop | Danh sách phần tử có thứ tự |
| 21 | `set<T>` | `HashSet<T>` / `ISet<T>` | `ISet` Loop | Tập hợp phần tử không trùng lặp |
| 22 | `map<K,V>` | `Dictionary<K,V>` | `IDictionary` Loop | Ánh xạ cặp Key-Value |

---

## 3. Quy Tắc Bắt Buộc Về Null-Safety & Exception Prevention

Khi đọc dữ liệu từ `Cassandra.Row` trong `CassandraResultSetWrapper`:
1. **Kiểm tra `row.IsNull(c)` trước khi đọc giá trị**:
   ```csharp
   _rentedFlatBuffer[flatIdx++] = row.IsNull(c) ? null : row.GetValue(cached.OrderedSchemas[c].DataType, c).NormalizeCassandraValue();
   ```
2. **Cấm tuyệt đối gọi trực tiếp `row.GetValue(Type, c)` trên cột null**: Việc này sẽ ném `NullReferenceException: Cannot convert null to ... because it is a value type` trong DataStax CassandraCSharpDriver.
3. **Ánh xạ an toàn về Nullable Models**:
   Các C# ReadModel hỗ trợ đầy đủ các trường nullable (`long?`, `DateTime?`, `Guid?`, `int?`, `bool?`, `string?`). Khi gặp tombstone trong Cassandra, giá trị phải gán là `null` mà không gây bất kỳ ngoại lệ nào.

---

## 4. Ma Trận Kiểm Thử Đối Xứng (Test Parity Matrix)

Toàn bộ các test suite sau bắt buộc phải tồn tại song song và vượt qua 100%:

| Nhóm Kiểm Thử | Postgres Test Suite | Cassandra Test Suite |
| :--- | :--- | :--- |
| **Static Cell Caching** | `PostgresCell_StaticTypeCaching_ShouldProduceAccurateDbType` | `CassandraCell_StaticTypeCaching_ShouldProduceAccurateDbType` |
| **Type Normalization** | `PostgresUtils_NormalizeForNpgsql_ShouldReturnAccurateNpgsqlDbType` | `CassandraUtils_NormalizeForCassandra_ShouldReturnAccurateCassandraDbType` |
| **IL Delegate Cache** | `PostgresMappingExtensions_CreateRowMapper_ShouldCacheCompiledDelegate` | `CassandraMappingExtensions_CreateRowMapper_ShouldCacheCompiledDelegate` |
| **Multi-Thread Safety** | `Postgres_MultiThreaded_ConcurrentMapping_ShouldBeThreadSafe` | `Cassandra_MultiThreaded_ConcurrentMapping_ShouldBeThreadSafe` |
| **Stress Concurrency** | `Postgres_AccountQuery_Concurrent50Tasks_ShouldAllSucceed` | `Cassandra_AccountQuery_Concurrent50Tasks_ShouldAllSucceed` |
| **Loop Stability** | `Postgres_AccountQuery_ShouldSucceed100TimesWithoutFailure` | `Cassandra_AccountQuery_ShouldSucceed100TimesWithoutFailure` |
| **Full Types Verification**| `DataTypeMappingTests.NpOnTypeConverter_*` | `CassandraDriver_AllDataTypes_NonNullValues_*` & `CassandraDriver_AllDataTypes_NullValues_*` |
| **Dynamic IL Mapping Benchmark** | `DynamicILMappingBenchmarkTests` / `ILEmitZeroAllocationTests` | `Cassandra_DynamicIL_RowMapper_Benchmark_*` & `Cassandra_DynamicIL_ObjectMapping_Benchmark_*` |

---

## 5. Quy Chuẩn Kiểm Thử Hiệu Năng Ánh Xạ (Mapping Performance & Zero GC Leak Standard)

Sau khi hoàn tất kiểm thử chức năng của bất kỳ database driver nào, **bắt buộc phải thực hiện đo đạc và kiểm thử hiệu năng ánh xạ (Dynamic IL Emit Mapping)** trên hot-path tối thiểu **10,000 iterations**:

1. **Thông lượng tối thiểu (Throughput Standard)**:
   - Tốc độ sinh và ánh xạ delegate IL Emit (`GetOrCreateRowMapper` và `FlatObjectMapper`) phải đạt tối thiểu **> 10,000 operations/giây** (thực tế kiến trúc UDAL đạt 200,000+ operations/giây).
2. **Kiểm soát tuyệt đối rác bộ nhớ (Zero GC Leak Standard)**:
   - Vòng lặp 10,000 lần mapping **tuyệt đối không được kích hoạt GC Gen2** (`Assert.Equal(0, finalGen2)`).
   - GC Gen1 chỉ được phép ở mức tối thiểu (`finalGen1 <= 2`) do hạ tầng runner/background thread.
   - Toàn bộ mảng trung gian và buffer dữ liệu phẳng bắt buộc phải thuê từ `ArrayPool<object?>.Shared` và trả lại (`Return`) khi hoàn tất chu trình đọc.
3. **Cấm tái biên dịch delegate (Compiled Delegate Caching)**:
   - Mọi delegate IL Emit sau khi sinh phải được lưu trữ vĩnh viễn trong `ConcurrentDictionary` cache bằng schema key chuẩn hóa. Tỷ lệ cache hit trên hot-path phải đạt 100%.

---

## 6. Quy Chuẩn So Sánh Đa Framework (Multi-Framework Benchmarking: UDAL vs Dapper vs EF Core)

Đối với các hệ quản trị cơ sở dữ liệu quan hệ được hỗ trợ rộng rãi (như PostgreSQL qua `Test/NpOn.OmsDapper`):
1. **Kiến trúc so sánh 3-Way đồng cấp**:
   - So sánh đồng thời 3 framework: **Unified UDAL** (Vectorized Pages + Dynamic IL Emit) vs **Dapper 2.1** (Reflection Emit) vs **EF Core 9.0** (`AsNoTracking` + Compiled Queries).
2. **Kỹ thuật xoay tua khử định kiến cache (Rotated Execution)**:
   - Các lượt đo liên tục (100 runs) phải xoay vòng thứ tự thực thi (`RunTriadSequentialBenchmarkAsync` & `RunTriadConcurrentBenchmarkAsync`) giữa Dapper, EF Core và UDAL để triệt tiêu hoàn toàn hiệu ứng cache hệ điều hành và DB connection warmup bias.
3. **Đo đạc toàn diện 4 chiều (4-Dimensional Evaluation)**:
   - **Throughput (rec/s)**: Khả năng tiêu thụ và trả về bản ghi mỗi giây.
   - **Latency (ms)**: Độ trễ trung bình, Min, Max, và phân vị P95.
   - **GC & Footprint**: Số lần kích hoạt GC Gen0/1/2 và dung lượng RAM delta.
   - **Binary Footprint (DLL Size)**: So sánh kích thước assembly (UDAL ~48 KB vs Dapper ~230 KB vs EF Core ~2.5 MB).


