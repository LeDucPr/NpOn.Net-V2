# NpOn.Net-V2 Agent Harness Framework

Khung Harness dành cho AI Agents và Developers trong hệ thống backend AIoT hiệu năng cao **NpOn.Net-V2**.

---

## 📁 Cấu trúc thư mục Khung Harness

```
NpOn.Net-V2/
├── AGENTS.md                          # Bản đặc tả quy tắc và chỉ dẫn vận hành cho Agents
├── run-harness.ps1                    # Shortcut chạy toàn bộ 5 giai đoạn Harness
├── .agents/
│   ├── README.md                      # Tài liệu tổng quan về Harness Framework
│   ├── rules/
│   │   ├── 01-architecture-guidelines.md  # Quy chuẩn phân tách lớp, Microservices & gRPC
│   │   ├── 02-ilemit-zeroallocation.md    # Nguyên tắc Dynamic IL Emit & Zero-Allocation UDAL
│   │   ├── 03-testing-benchmarks.md       # Tiêu chuẩn Unit Test, Contract & Stress Test
│   │   └── 04-debugging-diagnostics.md    # Hướng dẫn debug IL Emit, Pool & Connection Leak
│   ├── harness/
│   │   ├── harness.ps1                # Orchestrator thực thi 5 giai đoạn kiểm định
│   │   └── harness.bat                # Launcher trên Windows Command Prompt
│   └── reports/                       # Thư mục lưu trữ báo cáo tự động sau mỗi lần chạy
```

---

## ⚡ 5 Giai đoạn Bắt buộc trong Mỗi Lần Chạy Harness

Mỗi lần thực thi lệnh `.\run-harness.ps1`, harness bắt buộc phải duyệt qua đủ 5 giai đoạn sau:

1. **Phân tích kiến trúc (Architecture Analysis)**:
   - Kiểm tra đồ thị phụ thuộc giữa Controllers -> MicroServices -> Common -> Infrastructures.
   - Kiểm tra tính toàn vẹn của gRPC Code-first (`protobuf-net.Grpc`) và DDD Domain boundary.
   - Biên dịch và phân tích cảnh báo của Roslyn compiler.
2. **Test thử nghiệm (Verification & Unit Testing)**:
   - Chạy toàn bộ các test cases tự động (`NpOn.Core.Tests`).
   - Kiểm chứng mã IL Emit sinh động tại runtime, khả năng gán trường, xử lý null và chuyển kiểu enum.
3. **Hiệu năng & Tối ưu (Performance & Zero-Allocation)**:
   - Đo lường thời gian thực thi (latency microsecond/millisecond) của bộ IL Emit mapper so với Reflection.
   - Đo lường phát sinh rác GC (Gen0, Gen1, Gen2) và memory delta trên hot-path.
4. **Dễ đọc cho người (Human Readability)**:
   - Giao diện console trực quan với nhãn màu `[PASS]`, `[WARN]`, `[FAIL]`.
   - Xuất file báo cáo Markdown chuẩn định dạng tại `.agents/reports/harness_latest.md`.
5. **Khả năng gỡ lỗi & Chẩn đoán (Debugging & Diagnostics)**:
   - Kiểm tra mã byte IL động, xác định các trường hợp stack imbalance hoặc lỗi kiểu dữ liệu.
   - Bắt log chi tiết về môi trường máy chủ, luồng CPU, bộ nhớ và các tham số connection pool.

