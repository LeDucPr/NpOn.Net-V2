# Rule 05: Distributed Saga, Concurrency Locks & Multi-Engine Audit

## 1. Nguyên Tắc Saga Không Khóa (Non-Blocking Transactions)
- **Tuyệt đối cấm sử dụng Distributed Locks kéo dài**: Không dùng `SELECT FOR UPDATE` xuyên suốt các cuộc gọi mạng hoặc Two-Phase Commit (2PC) giữa các microservices.
- **Tính Nhất Quán Sau Cùng (Eventual Consistency)**: Chia nhỏ luồng nghiệp vụ thành chuỗi các Local Transactions. Mỗi service cam kết và giải phóng connection ngay lập tức.
- **Compensating Actions**: Mọi bước trong Saga bắt buộc phải có bước bù trừ (revert logic) tương ứng khi có sự cố.

## 2. Quy Chuẩn "Khóa Xung Đột" (Race Condition Concurrency Locks)
- **Domain Entity Versioning**: Mọi thực thể tham gia Saga bắt buộc phải có trường `version: long`. Khi cập nhật bắt buộc kiểm tra `WHERE version = @expected_version` (CAS Token). Nếu `affected_rows == 0`, ném ngoại lệ xung đột ngay lập tức và từ chối ghi đè dữ liệu.
- **Causality Token Trong Outbox**: Mọi Outbox event phải đóng dấu `(entity_key, pre_version, post_version, sequence_number)`.
- **Phân Vùng Đơn Điệu Trên Broker**: Bắt buộc dùng `Partition Key = entity_key` khi đẩy event lên Kafka/RabbitMQ để đảm bảo tính tuần tự tuyệt đối (Strict FIFO per Entity).
- **Inbox Causality Guard**: Inbox chỉ chấp nhận event khi `event.pre_version == last_applied_version`. Tuyệt đối không cho phép event đến trễ hoặc out-of-order làm hỏng chuỗi Audit Trail.

## 3. Quy Chuẩn Đa Database Engine Cho Lệnh `MERGE` & Audit Siêu Tốc
- **PostgreSQL / YugabyteDB**: Bắt buộc dùng `RETURNING (xmax = 0) AS is_insert` và `NpgsqlBinaryImporter` (`COPY ... FROM STDIN BINARY`).
- **Microsoft SQL Server (MSSQL)**: Bắt buộc dùng `MERGE ... OUTPUT $action AS Action, DELETED.*, INSERTED.*` và `SqlBulkCopy`.
- **MySQL (InnoDB 8.0+)**: Bắt buộc dùng `affected_rows` (1: Insert, 2: Update) kết hợp staging table và `LOAD DATA LOCAL INFILE`.
- **ClickHouse**: Bắt buộc dùng `ReplacingMergeTree(version)` và `ClickHouseBulkCopy` (RowBinary Format TCP Native Protocol).
- **ScyllaDB / Cassandra**: Bắt buộc dùng Paxos LWT `IF version = :expected_ver` và Token-Aware batched prepared statements.
- **Kênh Đệm Bất Đồng Bộ Lock-Free**: Quá trình ghi audit không được chặn request client. Bắt buộc dùng `System.Threading.Channels` gom micro-batch và flush sang Database chuyên biệt.

## 4. Kiến Trúc Kép 2-Phase & Khóa Phức Hợp 10-Byte
- **Bóc Tách ADN Tại Bộ Nhớ (Phase 1)**: Khi gọi `CommandBuilder` với `BaseDomain`, dùng Dynamic IL Emit trích xuất khóa và payload vào `ArrayPool<T>` buffer tạm thời. Lệnh SQL chỉ `RETURNING` các cột định danh siêu nhẹ (`id`, `is_insert`, `version`, `delta`), không kéo toàn bộ bảng qua socket mạng.
- **Khóa Phức Hợp Zero-Allocation (10 Bytes Index)**: Bảng có nhiều cột `[Pk]` bắt buộc dùng `stackalloc char[128]` và FarmHash64 để sinh `key_hash: BIGINT`. Khóa chính Inbox/State là `(entity_type_id: INT2, key_hash: BIGINT)` chỉ 10 bytes, cấm đánh index trên chuỗi `VARCHAR(128)` ghép nối.

## 5. Quy Chuẩn Ghi Audit Delta-Only (Chỉ Ghi Trường Thực Sự Thay Đổi)
- **Cấm Ghi Full Snapshot Khi Update**: Khi thực thi `Merge` hoặc `Update`, cấm ghi toàn bộ các trường của Domain vào audit log. Bắt buộc chỉ ghi các trường thực sự có giá trị thay đổi giữa DB và Domain input (`{"field": {"old": v1, "new": v2}}`).
- **Xử Lý No-Op Tiết Kiệm 100%**: Nếu dữ liệu truyền vào giống hệt dữ liệu hiện tại trong DB, gán `EMergeClassification.NoOp` và bỏ qua không ghi audit log, ngăn chặn hoàn toàn Write Amplification.

