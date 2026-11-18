---
name: architecture-analysis
description: Phân tích kiến trúc phân tầng, ranh giới giữa Controller và Microservices, phát hiện vi phạm rò rỉ Driver DB sang Gateway, kiểm tra hợp đồng gRPC Code-first trong NpOn.Net-V2.
---

# Kỹ Năng Phân Tích Kiến Trúc NpOn.Net-V2 (Architecture Analysis Skill)

Khi bot hoặc developer cần phân tích, đánh giá hoặc refactor bất kỳ module nào trong hệ sinh thái NpOn.Net-V2, hãy tuân thủ quy trình kiểm định kiến trúc sau:

## 1. Ranh Giới Lớp Bắt Buộc (Strict Layer Boundaries)

- **Controllers / Gateway (`Controllers/NpOn.SSO`)**:
  - **CHỈ ĐƯỢC PHÉP**: Gọi gRPC client (`IAccountService`, `IGeneralService`), xác thực JWT, cấu hình middleware, CORS, rate limiter.
  - **TUYỆT ĐỐI CẤM**: Tham chiếu trực tiếp tới các driver cơ sở dữ liệu (`NpgsqlConnection`, `NpgsqlCommand`, `SqlConnection`, `MySqlConnection`, `StackExchange.Redis`).
  - **Lệnh kiểm tra vi phạm**:
    ```powershell
    Get-ChildItem -Path "Controllers" -Recurse -Filter "*.cs" | Select-String -Pattern "Npgsql|SqlConnection|MySql|MongoDatabase"
    ```
    *Nếu có kết quả trả về: ĐÂY LÀ VI PHẠM KIẾN TRÚC NGHIÊM TRỌNG.*

- **Microservices (`MicroServices/Account`, `MicroServices/General`)**:
  - Giao tiếp với tầng dữ liệu duy nhất thông qua **Unified UDAL** (`IDbFactoryWrapper`, `PostgresDriver`, `NpOnWrapperResult`).
  - Hợp đồng giao tiếp (Contracts) phải là **Code-First gRPC** với thư viện `protobuf-net.Grpc`.
  - Các Entity và Domain Models kế thừa từ `record` (immutable data structure).

## 2. Kiểm Tra Hợp Đồng gRPC Code-First

- Interface Service phải mang thuộc tính `[ServiceContract]`.
- Các phương thức phải mang thuộc tính `[OperationContract]` và tham số kiểu `ValueTask<T>` hoặc `Task<T>`.
- DTO truyền qua gRPC phải có thuộc tính `[DataContract]` và các trường có `[DataMember(Order = N)]`.

## 3. Kiểm Tra Central Package Management (CPM)

- Mọi file `.csproj` trong toàn bộ Solution **KHÔNG ĐƯỢC CÓ** thuộc tính `Version="..."` trong thẻ `<PackageReference>`.
- Lệnh quét vi phạm:
  ```powershell
  Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Select-String -Pattern '<PackageReference.*Version='
  ```
  *Nếu phát hiện dòng vi phạm, chuyển version đó về `Directory.Packages.props` ngay lập tức.*

