---
name: saga-and-audit
description: Thiết kế và kiểm thử luồng Saga: Transactional Outbox, Transactional Inbox (Idempotency), Non-blocking Lock-Free execution, Native MERGE auditing với PostgreSQL RETURNING và Binary Protocol COPY.
---

# Kỹ Năng Thiết Kế Saga Đa Engine & Khóa Xung Đột (Saga, Multi-Engine & Concurrency Control)

Kỹ năng này hướng dẫn bot triển khai các giao dịch phân tán không khóa (Non-blocking Saga), xử lý xung đột đồng thời (Race Condition Concurrency Locks) và thực thi lệnh `MERGE` tốc độ cao trên đa database engine:

---

## 1. Cơ Chế "Khóa Xung Đột" (Race Condition Concurrency Lock)

### Vấn Đề
Khi 2 tiến trình cùng đọc một bản ghi và ghi đè nối tiếp nhau, sẽ xảy ra hiện tượng **Lost Update** và làm gãy hỏng dữ liệu Audit Trail trong Inbox:
- Thread A đọc Version 1 $\to$ ghi Version 2 (Audit: $V_1 \to V_2$).
- Thread B đọc Version 1 $\to$ ghi Version 2' (Audit: $V_1 \to V_{2'}$).
- Khi tới Inbox, trạng thái thực tế đã là $V_2$ nhưng Event B lại ghi nhận `pre_state` là $V_1$, gây sai lệch audit và không thể rollback bù trừ logic (Compensate).

### Quy Chuẩn Triển Khai Khóa Xung Đột (3 Lớp Bảo Vệ)
1. **Lớp 1: Domain Entity Versioning (CAS Token)**:
   - Mọi bản ghi tham gia Saga bắt buộc có cột `version: long`.
   - Lệnh `UPDATE` hoặc `MERGE` bắt buộc có điều kiện `WHERE version = @expected_version`.
   - Nếu `affected_rows == 0`: Ném `SagaConcurrencyConflictException` ngay lập tức, hủy transaction và không ghi event rác vào Outbox.
2. **Lớp 2: Outbox Causality Token & Partition Ordering**:
   - Outbox event bắt buộc mang `(entity_key, pre_version, post_version, sequence_number)`.
   - Khi publish lên Kafka / RedPanda / RabbitMQ: Bắt buộc dùng **`Partition Key = entity_key`** để đảm bảo 100% event của 1 thực thể được gửi tuần tự (Strict FIFO).
3. **Lớp 3: Inbox Causality Guard**:
   - Bảng `saga_inbox_entity_state (entity_type, entity_key, last_applied_version, last_message_id)`.
   - Chỉ áp dụng event khi `event.pre_version == last_applied_version`.
   - Nếu `event.post_version <= last_applied_version`: Bỏ qua (Duplicate / Stale).
   - Nếu `event.pre_version > last_applied_version`: Tạm hoãn / NACK chờ event trước hoàn tất.

---

## 2. Ma Trận Đa Database Engine Cho Lệnh `MERGE` & Audit Tốc Độ Cao

| Database Engine | Cú pháp Native Merge / Upsert | Cơ chế Phân loại Insert vs Update (1 Pass) | Giao thức nạp Audit Siêu Tốc (Bulk Ingestion) | Thông lượng Audit Ước tính |
|---|---|---|---|:---:|
| **PostgreSQL / YugabyteDB** | `INSERT ... ON CONFLICT (...) DO UPDATE ...` | `RETURNING (xmax = 0) AS is_insert` *(xmax = 0 là INSERT, xmax <> 0 là UPDATE)* | `NpgsqlBinaryImporter` (`COPY ... FROM STDIN BINARY`) | **350,000 – 500,000 rec/s** |
| **Microsoft SQL Server (MSSQL)** | `MERGE INTO Target USING Source ON ... WHEN MATCHED ... WHEN NOT MATCHED ...` | Mệnh đề `OUTPUT $action AS Action, DELETED.*, INSERTED.*` *($action trả về 'INSERT' hoặc 'UPDATE' trực tiếp!)* | `SqlBulkCopy` (TDS Protocol Streaming) | **250,000 – 400,000 rec/s** |
| **MySQL (InnoDB 8.0+)** | `INSERT INTO ... ON DUPLICATE KEY UPDATE col = VALUES(col), ver = ver + 1` | `affected_rows` trong MySQL Wire Protocol: `1` = Insert, `2` = Update, `0` = No-op | `MySqlBulkLoader` (`LOAD DATA LOCAL INFILE` Binary CSV Stream) | **200,000 – 350,000 rec/s** |
| **ClickHouse** | Append-Only Insert với `ReplacingMergeTree(version)` | Không in-place update. Mọi write là Insert mang `_version` mới, engine tự động merge background | `ClickHouseBulkCopy` (RowBinary Format TCP Native Protocol) | **1,000,000+ rec/s** |
| **Cassandra / ScyllaDB** | `INSERT ... USING TIMESTAMP` (Mặc định là Upsert) | Paxos LWT: `UPDATE ... IF version = :expected_ver` trả về `[applied] = true/false` | Asynchronous Token-Aware Batched Prepared Statements | **300,000 – 600,000 rec/s** |
| **MongoDB** | `bulkWrite` với `updateOne(..., { $set: ... }, { upsert: true })` | `BulkWriteResult.UpsertedIds` mapping trực tiếp danh sách các document vừa được tạo mới | BSON Wire Protocol Batched Chunks | **150,000 – 250,000 rec/s** |

---

## 3. Kỹ Thuật Đệm Bất Đồng Bộ Lock-Free (`System.Threading.Channels`)

- Quá trình ghi audit không bao giờ được nằm trên luồng đồng bộ (hot-path) làm tăng độ trễ của API response.
- Tầng UDAL đẩy batch audit vào `Channel<SagaAuditBatch>` không khóa trong RAM trong **0.002 ms**.
- Background Worker gom nhóm (Micro-batching) mỗi 10ms hoặc 5,000 bản ghi để flush 1 lần xuống Database chuyên biệt bằng giao thức Binary Stream của từng engine tương ứng.

---

## 4. Kiến Trúc Kép 2-Phase Chia Sẻ ADN (Dual-Phase Shared-ADN Pipeline)

- **Phase 1 (Pre-Execution tại `CommandBuilder<T> where T : BaseDomain`)**:
  - Tận dụng đối tượng `BaseDomain` đang nằm trong RAM: trích xuất `EntityKey`, `ExpectedVersion`, và nén payload bằng FastGetter IL Emit (`ArrayPool<T>` buffer).
  - Tự động thêm mệnh đề trả về siêu nhẹ vào SQL: `RETURNING id, (xmax = 0) AS is_insert, version`.
- **Phase 2 (Post-Execution tại Data Stream)**:
  - Database chỉ trả về vài bytes: `(id, is_insert, version)`. Không nạp ngược lại toàn bộ cột qua socket mạng (tiết kiệm 95% bandwidth).
  - Fusion Engine ghép `is_insert` với ADN đã chuẩn bị ở Phase 1.
  - Phân loại chuẩn xác 100% qua `EMergeClassification.Inserted` hoặc `Updated`.
  - Nếu xảy ra xung đột `affected_rows == 0`: Hủy bỏ ADN buffer, ném `SagaConcurrencyConflictException`. Zero audit rác!

---

## 5. Xử Lý Bảng Nhiều Khóa (Composite & Alternate Keys) Với Chi Phí Ghi Nhỏ Nhất

- **Trích xuất Khóa Phức Hợp Không Cấp Phát (Zero-Allocation via `Span<char>`)**:
  - Khi bảng có nhiều trường `[Pk]`, sử dụng `stackalloc char[128]` trên Callstack để nối chuỗi khóa với `ISpanFormattable`.
  - Tính mã băm 64-bit `key_hash = FarmHash64(span)` trong 8 nanoseconds. Không cấp phát chuỗi String lên Managed Heap!
- **Khóa Chính 10 Bytes Cho Bảng Inbox/State**:
  - Đánh Index trên cặp `PRIMARY KEY (entity_type: INT2, key_hash: BIGINT)`.
  - Kích thước Index giảm 92% so với chuỗi `VARCHAR(128)`, cho phép cache hàng chục triệu bản ghi trong RAM với chi phí ghi nhỏ nhất.
- **Merge Đa Khóa**:
  - PostgreSQL: `ON CONFLICT (col1, col2, ...)` hoặc `ON CONFLICT ON CONSTRAINT uq_name`.
  - MSSQL: `MERGE ... ON T.key1 = S.key1 AND T.key2 = S.key2`.
  - MySQL: `ON DUPLICATE KEY UPDATE` tự động bắt toàn bộ Unique Keys.

---

## 6. Quy Chuẩn Ghi Audit Delta-Only (Chỉ Ghi Trường Thực Sự Thay Đổi)

- **Cấm Tuyệt Đối Ghi Full Snapshot Khi Update**:
  - Khi thực hiện `Merge<T>` hoặc `Update<T>` với `BaseDomain` (chứa hàng chục trường), tuyệt đối không serialize toàn bộ đối tượng vào audit payload.
  - Phải trích xuất và chỉ lưu các trường **thực sự thay đổi** (`delta_changes`: `{"field": {"old": val1, "new": val2}}`).
- **Phán Quyết Delta Tại Single Atomic Round-Trip**:
  - **PostgreSQL**: Dùng CTE kết hợp `IS DISTINCT FROM` và `jsonb_strip_nulls(jsonb_build_object(...))`. Cột nào không đổi sẽ bị loại bỏ ngay tại lõi engine DB.
  - **MSSQL**: Tận dụng `OUTPUT DELETED.*, INSERTED.*` lọc cột có giá trị khác nhau.
- **Tiêu Chuẩn Tiết Kiệm No-Op**:
  - Nếu bản ghi truyền vào giống hệt dữ liệu hiện có trong DB (không có trường nào bị đổi): phân loại `EMergeClassification.NoOp` và **bỏ qua hoàn toàn không ghi Audit**, tiết kiệm 100% dung lượng I/O đĩa.
- **Bảng Audit Lưu Trữ Gọn Nhẹ (`saga_entity_audit`)**:
  - Primary Key: `audit_id (BIGSERIAL)`.
  - Index tra cứu: `(entity_type_id: INT2, key_hash: BIGINT, to_version: BIGINT)` chỉ 18 bytes.
  - Cột `changed_fields JSONB`: chỉ chứa 40 - 90 bytes delta payload thay vì 1.5 KB full snapshot.


---

## 5. Xử Lý Bảng Nhiều Khóa (Composite & Alternate Keys) Với Chi Phí Ghi Nhỏ Nhất

- **Trích xuất Khóa Phức Hợp Không Cấp Phát (Zero-Allocation via `Span<char>`)**:
  - Khi bảng có nhiều trường `[Pk]`, sử dụng `stackalloc char[128]` trên Callstack để nối chuỗi khóa với `ISpanFormattable`.
  - Tính mã băm 64-bit `key_hash = FarmHash64(span)` trong 8 nanoseconds. Không cấp phát chuỗi String lên Managed Heap!
- **Khóa Chính 10 Bytes Cho Bảng Inbox/State**:
  - Đánh Index trên cặp `PRIMARY KEY (entity_type: INT2, key_hash: BIGINT)`.
  - Kích thước Index giảm 92% so với chuỗi `VARCHAR(128)`, cho phép cache hàng chục triệu bản ghi trong RAM với chi phí ghi nhỏ nhất.
- **Merge Đa Khóa**:
  - PostgreSQL: `ON CONFLICT (col1, col2, ...)` hoặc `ON CONFLICT ON CONSTRAINT uq_name`.
  - MSSQL: `MERGE ... ON T.key1 = S.key1 AND T.key2 = S.key2`.
  - MySQL: `ON DUPLICATE KEY UPDATE` tự động bắt toàn bộ Unique Keys.

---

## 6. Quy Chuẩn Ghi Audit Delta-Only (Chỉ Ghi Trường Thực Sự Thay Đổi)

- **Cấm Tuyệt Đối Ghi Full Snapshot Khi Update**:
  - Khi thực hiện `Merge<T>` hoặc `Update<T>` với `BaseDomain` (chứa hàng chục trường), tuyệt đối không serialize toàn bộ đối tượng vào audit payload.
  - Phải trích xuất và chỉ lưu các trường **thực sự thay đổi** (`delta_changes`: `{"field": {"old": val1, "new": val2}}`).
- **Phán Quyết Delta Tại Single Atomic Round-Trip**:
  - **PostgreSQL**: Dùng CTE kết hợp `IS DISTINCT FROM` và `jsonb_strip_nulls(jsonb_build_object(...))`. Cột nào không đổi sẽ bị loại bỏ ngay tại lõi engine DB.
  - **MSSQL**: Tận dụng `OUTPUT DELETED.*, INSERTED.*` lọc cột có giá trị khác nhau.
- **Tiêu Chuẩn Tiết Kiệm No-Op**:
  - Nếu bản ghi truyền vào giống hệt dữ liệu hiện có trong DB (không có trường nào bị đổi): phân loại `EMergeClassification.NoOp` và **bỏ qua hoàn toàn không ghi Audit**, tiết kiệm 100% dung lượng I/O đĩa.
- **Bảng Audit Lưu Trữ Gọn Nhẹ (`saga_entity_audit`)**:
  - Primary Key: `audit_id (BIGSERIAL)`.
  - Index tra cứu: `(entity_type_id: INT2, key_hash: BIGINT, to_version: BIGINT)` chỉ 18 bytes.
  - Cột `changed_fields JSONB`: chỉ chứa 40 - 90 bytes delta payload thay vì 1.5 KB full snapshot.

