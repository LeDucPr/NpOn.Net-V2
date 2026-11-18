---
name: security-audit-hardening
description: Kỹ năng rà soát và kiểm tra bảo mật đỉnh cao theo chuẩn OWASP Top 10 API Security, CWE Top 25, chống SQL Injection, rò rỉ JWT Secret, phòng thủ Timing Attack và bảo vệ hạ tầng phân tán.
---

# Kỹ Năng Rà Soát Bảo Mật & Tăng Cường Phòng Thủ (Security Audit & Hardening Skill)

Quy chuẩn này được xây dựng dựa trên tiêu chuẩn **OWASP API Security Top 10 (2023/2024)** và **CWE/SANS Top 25 Most Dangerous Software Weaknesses**, áp dụng cho toàn bộ kiến trúc phân tán NpOn.Net-V2:

---

## 1. Phòng Chống Injection (CWE-89: SQL / NoSQL / Command Injection)
- **Parameterized Queries**:
  - Mọi câu lệnh SQL trong `PostgresDriver.cs`, `NpOnDbCommandParam`, và các Adapter bắt buộc phải dùng Parameterized Query (`cmd.Parameters.Add(...)` hoặc `@param`).
  - **TUYỆT ĐỐI CẤM** nối chuỗi raw SQL: `$"SELECT * FROM {tableName} WHERE id = '{userInput}'"`.
- **Dynamic Table & Column Identifier Whitelisting**:
  - Tên bảng và tên cột khi sinh câu lệnh động phải được validate qua whitelist regex `^[a-zA-Z_][a-zA-Z0-9_]*$` trước khi đưa vào template câu lệnh.

---

## 2. Xác Thực & Ủy Quyền JWT Vault (Broken Object Level Authorization - BOLA / BFLA)
- **Kiểm Tra Chữ Ký Token Toàn Diện**:
  - Xác thực đầy đủ `IssuerSigningKey`, `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, và `ClockSkew` $\le 5$ giây.
  - Sử dụng thuật toán bất đối xứng `RSA256` hoặc khóa bí mật `HS256` với độ dài tối thiểu 256 bits (32 bytes).
- **Phòng Thủ Timing Attack (CWE-208)**:
  - Khi so sánh hash mật khẩu hoặc token bí mật, bắt buộc dùng `CryptographicOperations.FixedTimeEquals` thay vì toán tử `==` để loại bỏ rủi ro rò rỉ thời gian.
- **Token Revocation List**:
  - Kiểm tra Dragonfly/Redis blacklist key khi logout hoặc vô hiệu hóa tài khoản trước khi cấp phép vào Microservice.

---

## 3. Bảo Vệ Dữ Liệu Lưu Trữ & Truyền Tải (Cryptographic Failures)
- **DataProtection Keys**:
  - Khóa DataProtection (`/home/app/.aspnet/DataProtection-Keys`) được mount qua volume riêng biệt, phân quyền chỉ container có quyền đọc (`chmod 600`).
- **Secret & Credential Masking**:
  - Chuỗi kết nối DB (`Host=...;Password=...`) phải được truyền qua biến môi trường hoặc Docker Secrets.
  - Trong log hoặc báo cáo harness, bắt buộc mask mật khẩu: `Password=***`.

---

## 4. Chống Tấn Công Từ Chối Dịch Vụ (Denial of Service - DoS) & Connection Starvation
- **Connection Throttling**:
  - Mọi thao tác Database phải đi qua `SemaphoreSlim` để hạn chế số lượng kết nối tối đa mở đồng thời.
- **Circuit Breaker & Rate Limiting**:
  - Tầng Gateway (`NpOn.SSO`) áp dụng Rate Limiter theo IP và Token.
  - Tầng gRPC client cấu hình timeout và retry có giới hạn (tránh Retry Storm làm sập dịch vụ nội bộ).

---

## 5. Phòng Thủ Hàng Đợi Chết & Poison Message (CWE-400 / Uncontrolled Resource Consumption)
- Không cho phép message lỗi lặp lại vô tận trong Message Broker (Kafka/RabbitMQ).
- Sau tối đa 3 lần retry thất bại, bắt buộc chuyển vùng cách ly sang **Dead Letter Queue (DLQ)** và kích hoạt cảnh báo.

