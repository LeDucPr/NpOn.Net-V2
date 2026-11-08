# Rule 01: Architecture & Microservices Guidelines

## 1. Phân chia ranh giới dịch vụ (Bounded Contexts)
Hệ thống `NpOn.Net-V2` áp dụng kiến trúc Domain-Driven Design (DDD) và Microservices:
- **Public Gateway (`Controllers/NpOn.SSO`, CMS)**: Đóng vai trò Reverse Proxy và Public API endpoint. Chỉ tiếp nhận request từ client, xác thực ban đầu, chuyển tiếp lệnh/query qua gRPC tới các Microservices bên trong mạng nội bộ. KHÔNG được phép truy vấn Database trực tiếp từ Controller.
- **Microservices Domain**:
  - `NpOn.AccountService`: Quản lý danh tính, phân quyền, đăng nhập, token sessions.
  - `NpOn.GeneralService`: Quản lý catalog dữ liệu động (`tblmaster`, `generic_formula`, locales), dynamic SQL templates.
  - `NpOn.TrackerService`: Quản lý luồng xử lý tracking, 2-way ZeroMQ messaging, Redis broadcast.
- **Common & Infrastructure Layer**:
  - `Common/Extensions/NpOn.ICommonDb`: Cung cấp giao diện trừu tượng Unified UDAL (`INpOnCell`, `INpOnRowWrapper`, `INpOnWrapperResult`).
  - `Common/Infrastructures/DbFactories`: Quản lý kết nối đa RDBMS (PostgreSQL, Cassandra, Redis, RabbitMQ, Kafka, ClickHouse, MySQL, MSSQL) qua `DbFactoryWrapper`.

---

## 2. Đường ống Dual-Pipeline gRPC
- **Giao tiếp Service-to-Service (S2S)**: Bắt buộc sử dụng gRPC Code-first (`protobuf-net.Grpc`) trên HTTP/2 binary. Không dùng HTTP/1.1 REST giữa các Microservice nội bộ để giảm thiểu latency và chi phí serialization.
- **Public Interfaces**: Controller mở HTTP/1.1 JSON cho Web/Mobile App và đồng thời mở cổng gRPC `h2c` cho kiểm thử và integration client tốc độ cao.

---

## 3. Quản lý tài nguyên Database & Connection Throttling
- Mọi kết nối Database qua Unified UDAL phải được quản lý bởi **SemaphoreSlim** bên trong `Factory.GetConnectionAsync()`.
- Tuyệt đối không tự ý mở `new NpgsqlConnection` hay `SqlConnection` tự do trong business code mà phải lấy qua `DbFactoryWrapper`.
- Giới hạn tải kết nối (Connection Throttling) là cốt lõi để bảo vệ database không bị cạn kiệt pool khi có tải đột biến (Spike load).

