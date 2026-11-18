---
name: troubleshooting-diagnostics
description: Chẩn đoán sự cố: Connection Pool Starvation, SemaphoreSlim limits, Redis/Dragonfly handshake, Kafka IP resolution, Docker container memory profiling.
---

# Kỹ Năng Chẩn Đoán & Khắc Phục Sự Cố (Troubleshooting & Diagnostics Skill)

Kỹ năng này cung cấp các kịch bản kiểm tra và chẩn đoán sự cố runtime dành cho bot khi hệ thống gặp lỗi:

## 1. Sự Cố Treo Kết Nối Database (Connection Pool Starvation)

- **Triệu chứng**: Các request gRPC hoặc HTTP bị timeout, `NpgsqlException: The connection pool has been exhausted`.
- **Nguyên nhân**:
  - Quên `await using` hoặc `using` khi mở `NpgsqlDataReader` / `NpgsqlConnection`.
  - Không giải phóng Semaphore đếm kết nối trong khối `finally`.
- **Cách khắc phục**:
  - Kiểm tra `DbFactoryWrapper.cs`: Luôn bao bọc `await semaphore.WaitAsync()` với `try ... finally { semaphore.Release(); }`.
  - Giám sát số lượng connection active trên Postgres:
    ```sql
    SELECT count(*), state FROM pg_stat_activity GROUP BY state;
    ```

## 2. Sự Cố Sai Lệch Địa Chỉ IP Kafka / RedPanda Trong Docker

- **Triệu chứng**: Service log báo `Broker: Broker transport failure` hoặc `Connect to host.docker.internal failed`.
- **Nguyên nhân**: Biến môi trường `$env:HOST_IP` không nhận diện đúng card mạng vật lý đang kết nối router/LAN.
- **Cách khắc phục**:
  - Chạy lại hàm nhận diện IP:
    ```powershell
    (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'vEthernet|WSL|Docker|Loopback' -and $_.IPAddress -notmatch '^169.254' } | Select-Object -First 1).IPAddress
    ```
  - Khởi động lại broker bằng `.\run.ps1 all`.

## 3. Sự Cố Rò Rỉ Bộ Nhớ Docker (Container Memory Spill)

- **Lệnh giám sát trực tiếp không stream**:
  ```powershell
  docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}"
  ```
- **Ngưỡng chuẩn**:
  - `sso`: $\le 80 \text{ MiB}$
  - `generalservice`: $\le 80 \text{ MiB}$
  - `accountservice`: $\le 120 \text{ MiB}$
  - *Nếu một service tăng vọt trên 300 MiB: Kiểm tra ngay các static cache hoặc collection không có cơ chế giới hạn dung lượng.*

