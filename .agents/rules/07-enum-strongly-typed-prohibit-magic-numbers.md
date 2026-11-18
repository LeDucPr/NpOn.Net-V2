# Rule 07: Strongly-Typed Enums & Prohibition of Magic Numbers

## 1. Nguyên Tắc Cốt Lõi: Cấm Tuyệt Đối Magic Numbers
Trong toàn bộ hệ thống NpOn.Net-V2:
- **TUYỆT ĐỐI CẤM** sử dụng các số nguyên trần (Magic Numbers như `0`, `1`, `2`, `-1`,...) để biểu thị:
  - Loại thao tác dữ liệu (`Insert`, `Update`, `Merge`, `Delete`).
  - Trạng thái tiến trình nghiệp vụ hoặc vòng đời Saga (`Pending`, `Executing`, `Success`, `Failed`,...).
  - Trạng thái phân loại kết quả Merge (`NoOp`, `Inserted`, `Updated`, `Deleted`, `Conflict`).
  - Trạng thái xử lý tại Inbox/Outbox (`Applied`, `DuplicateDropped`, `Deferred`, `DeadLetter`).
  - Khóa phân loại trong Entities và Domain Models.

## 2. Quy Chuẩn Bắt Buộc
- **Mọi trạng thái bắt buộc phải được khai báo bằng Enum gõ mạnh (Strongly-Typed Enum)** tại namespace `Common.Extensions.NpOn.CommonEnums` hoặc definitions tương ứng.
- **Tại Entity & Domain Model**:
  ```csharp
  // SAI (VI PHẠM QUY CHUẨN HARNESS):
  public int Status { get; set; } = 1;
  public int OperationType { get; set; } = 2;

  // ĐÚNG CHUẨN:
  public ESagaStatus Status { get; init; } = ESagaStatus.Executing;
  public EMergeClassification OperationType { get; init; } = EMergeClassification.Updated;
  ```
- **Khi làm việc với Database Driver**:
  - Không truyền tham số số nguyên không rõ nghĩa vào query (`cmd.Parameters.AddWithValue("status", 1)`).
  - Sử dụng tên Enum hoặc ép kiểu rõ ràng: `(int)ESagaStatus.Success`.
  - Bộ chuyển đổi `NpOnTypeConverter` hỗ trợ mapping trực tiếp cả chuỗi Enum và giá trị số của Enum sang PostgreSQL.

