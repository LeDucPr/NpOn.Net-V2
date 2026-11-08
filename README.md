# NpOn.Net-V2: Hệ thống Backend AIoT Y tế Hiệu năng cao

Chào mừng bạn đến với **NpOn.Net-V2**, một nền tảng backend microservices hiện đại được xây dựng trên **.NET 10**, tập trung vào khả năng mở rộng phân tán, tính sẵn sàng cao, bảo mật zero-trust liên dịch vụ và tối ưu hóa tài nguyên phần cứng qua kiến trúc **Unified UDAL** & **Dynamic IL Emit**.

---

## 🏗️ Bản Đồ Kiến Trúc Hệ Thống (Architecture Map)

Hệ thống được thiết kế theo mô hình Microservices phân tán với ranh giới Domain rõ ràng (Domain-Driven Design).

```
                                [ Client / Web / Mobile App ]
                                              │
                       ┌──────────────────────┴──────────────────────┐
                       │ HTTP/1.1 JSON (Port 14023)                  │ Public gRPC (Port 15023)
                       ▼                                             ▼
        ┌────────────────────────────────────────────────────────────────────────┐
        │                        Controllers/NpOn.SSO                            │
        │           (Public Gateway / Reverse Proxy / Token Validator)           │
        └───────────────────┬─────────────────────────────────┬──────────────────┘
                            │                                 │
     (mTLS / gRPC HTTP/2    │              (mTLS / gRPC HTTP/2│
      Port 40004)           │               Port 40000)       │
                            ▼                                 ▼
             ┌─────────────────────────────┐   ┌─────────────────────────────┐
             │     NpOn.AccountService     │   │     NpOn.GeneralService     │
             │ (Identity / Auth / Session) │   │ (Dynamic Master Query / DSL)│
             └──────────────┬──────────────┘   └──────────────┬──────────────┘
                            │                                 │
                            │ (ZeroMQ 2-Way RPC)              │
                            ▼                                 │
             ┌─────────────────────────────┐                  │
             │     NpOn.TrackerService     │                  │
             │(Realtime Tracking/Broadcast)│                  │
             └──────────────┬──────────────┘                  │
                            │                                 │
     ┌──────────────────────┴─────────────────────────────────┴──────────────────────┐
     │                       Unified UDAL Data Access Layer                          │
     │  - SemaphoreSlim Connection Throttling (Bảo vệ Database Connection Pool)     │
     │  - Zero-Allocation ObjectPoolStore (Tái sử dụng Table / Row / Cell Wrappers)  │
     │  - Dynamic IL Emit (Sinh mã máy IL runtime - Native C# Execution Speed)       │
     └──────────────────────┬─────────────────────────────────┬──────────────────────┘
                            │                                 │
             ┌──────────────┴──────────────┐   ┌──────────────┴──────────────┐
             ▼                             ▼   ▼                             ▼
       [ PostgreSQL ]                [ Dragonfly/Redis ]              [ RabbitMQ ]
   (account & general db)           (Token Cache & State)         (Event-Driven Pub/Sub)
```

---

## 🧩 Các Thành Phần & Dịch Vụ Cốt Lõi

### 1. Phân Tầng Dịch Vụ (Microservices Layer)
*   **SSO Controller (`Controllers/NpOn.SSO`)**:
    *   **Port 14023**: Cổng vào RESTful API (HTTP/1.1 JSON) phục vụ Web và Mobile Client.
    *   **Port 15023**: Cổng gRPC công khai (Public gRPC) cho phép tích hợp trực tiếp client hiệu năng cao.
    *   Nhiệm vụ: Tiếp nhận request, phân giải route, kiểm tra token, mã hóa mật khẩu và forward sang dịch vụ nội bộ qua gRPC.
*   **AccountService (`MicroServices/Account/Service/NpOn.AccountService` - Port 40004)**:
    *   Quản lý danh tính, phân quyền, bảo mật, sinh mã JWT và kiểm soát phiên làm việc (Session).
*   **GeneralService (`MicroServices/General/Service/NpOn.GeneralService` - Port 40000)**:
    *   Trái tim của hệ thống truy vấn động: Lưu trữ catalog câu lệnh SQL trong bảng `tblmaster`, công thức tính toán (`generic_formula`), và dữ liệu đa ngôn ngữ (`mlg_srv_locale`).
*   **TrackerService (`MicroServices/Tracker/Service/NpOn.TrackerService` - Port 40003)**:
    *   Dịch vụ tracking thời gian thực, giao tiếp song hướng qua ZeroMQ 2-way RPC và phát sóng sự kiện qua Redis Broadcast.
*   **MigrationService (`MicroServices/Migration`)**:
    *   Dịch vụ chuyên biệt quản lý và áp dụng cấu trúc dữ liệu, di chuyển schema cơ sở dữ liệu.

### 2. Giao Thức Bảo Mật Liên Dịch Vụ (Inter-Service Security & mTLS)
*   **Code-First gRPC**: Sử dụng `protobuf-net.Grpc` trên nền HTTP/2 Binary giúp loại bỏ hoàn toàn chi phí serialize chuỗi JSON giữa các microservices nội bộ.
*   **mTLS (Mutual TLS)**: Xác thực song phương bằng chứng chỉ số (`sso-client.pfx`, `account-server.pfx`, `ca.crt`) kết hợp cơ chế tự động xoay vòng chứng chỉ (Certificate Rotation).
*   **Fallback JWT**: Tích hợp cơ chế xác thực JWT nội bộ (`InterServiceSecretKey`) bảo đảm tính sẵn sàng cao khi chuyển đổi hạ tầng.

### 3. Hạ Tầng Truy Cập Dữ Liệu Hợp Nhất (Unified UDAL & Dynamic IL Emit)
*   **Dynamic IL Emit (Mã máy IL runtime)**:
    *   Trong `PostgresMappingExtensions.cs`, `JsonModeWithCache.cs`, và `NpOnWrapperResultToObjectExtensions.cs`, hệ thống sử dụng `System.Reflection.Emit` và `DynamicMethod`.
    *   Sinh mã byte gán dữ liệu trực tiếp vào bộ nhớ đối tượng tại runtime, đạt tốc độ ngang mã viết tay (Native C# Speed), loại bỏ 100% chi phí Reflection.
*   **Zero-Allocation & Object Pooling (`IObjectPoolStore`)**:
    *   Các wrapper trung gian (`NpOnWrapperResult`, `ITabularResultSet`, row/cell wrappers) được lấy từ `ConcurrentBag` trong `ObjectPoolStore` và tái sử dụng, giữ lượng rác sinh cho GC ở mức tiệm cận 0.
*   **Kiểm Soát Tải Bằng Semaphore (`SemaphoreSlim`)**:
    *   Bảo vệ pool kết nối Database, triệt tiêu nguy cơ Connection Escalation khi lưu lượng truy cập tăng vọt.

---

## 🗄️ Cấu Trúc Cơ Sở Dữ Liệu PostgreSQL

Hệ thống lưu trữ trên PostgreSQL (Port `5432`), phân tách làm 2 database độc lập:

### 1. Account Database (`PgSQLTable/account.sql`)
*   `acc_srv_account`: Lưu thông tin tài khoản người dùng, email, số điện thoại, mật khẩu mã hóa PBKDF2.
*   `acc_srv_account_login`: Lưu vết phiên đăng nhập, refresh token, trạng thái session.
*   `acc_srv_account_permission_controller`: Bảng phân quyền chi tiết theo từng controller/action.
*   `acc_srv_account_group`: Quản lý nhóm người dùng và vai trò.

### 2. General Database (`PgSQLTable/general.sql`)
*   `tblmaster`: Chứa các mẫu truy vấn SQL động (vd: `account_get_by_username_and_password`).
*   `generic_formula`: Công thức tính toán nghiệp vụ động.
*   `mlg_srv_locale`: Bảng tài nguyên ngôn ngữ đa quốc gia.

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Local

### 1. Chuẩn Bị Cơ Sở Dữ Liệu & Hạ Tầng
Đảm bảo bạn đã cài đặt PostgreSQL, Redis/Dragonfly, và RabbitMQ (hoặc chạy qua Docker).

*   **Tạo Database**:
    1. Tạo database: `account` -> Chạy script [PgSQLTable/account.sql](PgSQLTable/account.sql).
    2. Tạo database: `general` -> Chạy script [PgSQLTable/general.sql](PgSQLTable/general.sql).
*   **Redis/Dragonfly**: Port `6379`.
*   **RabbitMQ**: Port `5672` (Management UI: `15672`).

### 2. Khởi Chạy Dịch Vụ
Khởi động các dịch vụ theo thứ tự qua terminal hoặc qua script:

```powershell
# Cách 1: Khởi chạy và đóng gói đồng thời toàn bộ microservices bằng Docker
.\run.ps1 all

# Hoặc khởi chạy độc lập từng project để debug qua .NET CLI:
# 1. GeneralService (Port 40000)
dotnet run --project MicroServices/General/Service/NpOn.GeneralService

# 2. AccountService (Port 40004)
dotnet run --project MicroServices/Account/Service/NpOn.AccountService

# 3. SSO Controller (Port 14023)
dotnet run --project Controllers/NpOn.SSO
```

---

## 🔐 Cơ Chế Đăng Nhập & Kiểm Thử Tài Khoản `KhaBanh`

Hệ thống cung cấp sẵn tài khoản quản trị thử nghiệm **`KhaBanh`** (Ngô Bá Khá) trong script `account.sql`.

### 1. Cơ Chế Mã Hóa Mật Khẩu
Khi client gửi yêu cầu đăng nhập, mật khẩu được xử lý qua 2 bước bảo mật tại SSO Controller:
1. Tính toán mã băm: `MD5(raw_password).ToLower()`.
2. Tạo chuỗi Salt chuẩn từ `ContextServiceCode.DefaultSaltPassword` và áp dụng thuật toán **PBKDF2** (369 vòng lặp, SHA1, 160-bit key) -> Base64.
*   Mật khẩu gốc: `Banhditusapve@1234`
*   Chuỗi Hash tương ứng trong DB: `jyGimGTAj2niJMgxijU7x7iR1RA=`

### 2. Lệnh Kiểm Thử Đăng Nhập (cURL)

> [!NOTE]
> Endpoint `/api/Account/Login` mở công khai (`[AllowAnonymous]`), do đó **KHÔNG** cần truyền Header `Authorization` khi thực hiện yêu cầu đăng nhập.

```bash
curl --location 'http://localhost:14023/api/Account/Login' \
--header 'Content-Type: application/json' \
--data-raw '{
    "UserName": "KhaBanh",
    "Password": "Banhditusapve@1234",
    "AuthType": 2
}'
```

*   `AuthType = 2`: Tương ứng với `EAuthentication.WebApp` (Web Application).
*   Kết quả trả về bao gồm: `Token` (JWT Access Token), `RefreshToken`, thông tin tài khoản và danh sách quyền hạn.

---

## 🤖 Bộ Khung Kiểm Định Tự Động (Agent Harness)

Dự án tích hợp bộ khung kiểm định tự động đa tầng theo chuẩn AI Agent tại `.agents/` và `AGENTS.md`.

```powershell
# Chạy đầy đủ 5 giai đoạn: Kiến trúc -> Test -> Hiệu năng -> Báo cáo -> Gỡ lỗi
.\run-harness.ps1

# Chạy nhanh (bỏ qua build nếu đã build trước đó)
.\run-harness.ps1 -SkipBuild

# Chạy với chế độ chẩn đoán chi tiết (Full callstack & OpCode dump)
.\run-harness.ps1 -DetailedDebug
```

Mỗi lần chạy sẽ tự động xuất báo cáo chi tiết tại:
*   `.agents/reports/harness_latest.md`
*   `.agents/reports/harness_<timestamp>.md`

---

## 📊 Báo Cáo Hiệu Năng So Sánh (Dapper vs Unified UDAL)

So sánh thực tế khi xử lý hơn **20,000 bản ghi** tài khoản đồng thời:

| Chỉ số | Dapper | Unified UDAL | Kết luận |
| :--- | :--- | :--- | :--- |
| **Độ trễ trung bình** | 384ms - 2000ms+ (Jitter cao) | **520ms - 581ms (Cực kỳ ổn định)** | UDAL triệt tiêu hiện tượng lệch độ trễ (latency jitter) |
| **GC Gen0 Pressure** | Cao (do mapping cấp phát heap liên tục) | **Tiệm cận 0 (Nhờ ObjectPoolStore)** | Bảo vệ Garbage Collector khi có tải đột biến |
| **Tải cao (100 Tasks đồng thời)** | Dễ nghẽn Connection Pool | **Nhanh hơn 18-20%** | Nhờ Dynamic IL Emit & Throttling Semaphore |

---

Dự án được thực hiện để tri ân và tưởng nhớ anh trai **Lương Hoàng Long** — người đã truyền nguồn động lực to lớn để tôi hoàn thành hệ thống này. Theo tâm nguyện của anh, dự án này chính thức được công bố là mã nguồn mở (**Open Source**) với mong muốn đóng góp giá trị cho cộng đồng công nghệ.
