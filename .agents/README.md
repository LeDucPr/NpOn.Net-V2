# NpOn.Net-V2 Agent Harness Framework & Master Architecture Guide

Chào mừng tất cả các AI Agents (**Antigravity, Claude, Codex, Cursor**) và Developers đến với hệ sinh thái **NpOn.Net-V2** - Hệ thống Backend AIoT Y tế Hiệu năng cao trên nền tảng **.NET 10**.

---

## 📁 Bản Đồ Tài Liệu & Chỉ Dẫn Dành Cho Bot (Agent Navigation Map)

```
.agents/
├── README.md                          # Tài liệu chỉ mục tổng quan này
├── ARCHITECTURE.md                    # Bản đặc tả kỹ thuật kiến trúc toàn diện của dự án
├── harness/
│   ├── harness.ps1                    # Bộ khung chạy 5 giai đoạn kiểm định tự động
│   └── harness.bat                    # Launcher tương thích Windows CMD
├── reports/
│   └── harness_latest.md              # Báo cáo kiểm định 5 giai đoạn hợp lệ mới nhất
├── benchmarks/
│   ├── benchmark_100runs_summary.md   # Báo cáo thực nghiệm 100 lần lặp Throughput/Latency/RAM
│   └── dapper_vs_udal_latest.md       # Báo cáo đối chuẩn UDAL vs Dapper 2.1
├── rules/                             # Các quy tắc kỹ thuật bắt buộc phải tuân thủ
│   ├── 01-architecture-guidelines.md  # Quy chuẩn phân tách lớp, Microservices & gRPC
│   ├── 02-ilemit-zeroallocation.md    # Nguyên tắc Dynamic IL Emit & Zero-Allocation UDAL
│   ├── 03-testing-benchmarks.md       # Tiêu chuẩn Unit Test, Contract & Stress Test
│   ├── 04-debugging-diagnostics.md    # Hướng dẫn debug IL Emit, Pool & Connection Leak
│   ├── 05-saga-and-outbox-rules.md    # Quy tắc Saga không khóa, Outbox, Inbox & Native Merge Audit
│   ├── 06-cpm-package-management.md   # Quy tắc Central Package Management (Directory.Packages.props)
│   ├── 07-enum-strongly-typed-prohibit-magic-numbers.md # Quy chuẩn Enum gõ mạnh, cấm Magic Numbers
│   └── 08-database-driver-parity-and-exhaustive-testing.md # Quy chuẩn đối xứng driver: Postgres & Cassandra
└── skills/                            # Các kỹ năng phân tích chuyên sâu dành cho AI Agent
    ├── architecture-analysis/         # Phân tích ranh giới lớp, vi phạm Driver DB sang Gateway
    ├── il-emit-engineering/           # Kỹ thuật sinh mã IL runtime, cân bằng Evaluation Stack
    ├── performance-benchmarking/      # Kỹ thuật đo Throughput, Latency, Zero GC Leak profiling
    ├── saga-and-audit/                # Kỹ thuật Saga, Outbox/Inbox và PostgreSQL Binary COPY
    ├── troubleshooting-diagnostics/   # Kỹ thuật chẩn đoán Connection Starvation, Kafka IP, RAM
    ├── code-review-excellence/        # Kỹ năng review code chuẩn Google & Microsoft Clean Architecture
    └── security-audit-hardening/      # Kỹ năng kiểm tra bảo mật OWASP API Top 10 & CWE Hardening
```

---

## ⚡ 5 Giai Đoạn Bắt Buộc Trong Mỗi Lần Chạy Harness (`run-harness.ps1`)

**QUY TẮC BẮT BUỘC**: Trước khi bàn giao bất kỳ tác vụ nào, hệ thống **bắt buộc phải chạy và vượt qua 100% cả 5 giai đoạn**:

```powershell
.\run-harness.ps1
```

1. **Giai đoạn 1: Phân tích kiến trúc (Architecture Analysis)**:
   - Kiểm tra ranh giới giữa Gateway (Controllers) và Microservices. Tuyệt đối cấm Controller gọi trực tiếp DB driver.
   - Biên dịch toàn bộ Solution `NpOnV2.sln`, đảm bảo 0 lỗi biên dịch.
2. **Giai đoạn 2: Test thử nghiệm (Automated Verification & Unit Tests)**:
   - Chạy toàn bộ test suites trong `Test/NpOn.Core.Tests`.
   - Kiểm chứng Dynamic IL Emit, `NpOnTypeConverter` (JSONB, Arrays, Guids, Enums).
3. **Giai đoạn 3: Hiệu năng & Tối ưu hóa (Performance Benchmarking)**:
   - Đo Throughput hot-path ($\ge 3,000 \text{ ops/s}$).
   - Đo chỉ số Garbage Collection: **Gen0 = 0, Gen1 = 0, Gen2 = 0** (Zero GC Leak).
4. **Giai đoạn 4: Dễ đọc cho người (Reporting & Dashboard)**:
   - Xuất file báo cáo Markdown tại `.agents/reports/harness_latest.md`.
5. **Giai đoạn 5: Khả năng gỡ lỗi & Chẩn đoán (Diagnostics)**:
   - Giám sát Working Set RAM, Semaphore pool và kiểm tra tính hợp lệ của IL OpCode stacks.

---

## 🔒 3 Điều Cấm Tuyệt Đối Dành Cho Mọi AI Bot

1. **TUYỆT ĐỐI KHÔNG BYPASS HOẶC THAY THẾ DYNAMIC IL EMIT**:
   - Không được thay thế mã sinh IL Emit bằng Reflection (`Type.GetProperty().SetValue()`) trên hot-path.
2. **TUYỆT ĐỐI KHÔNG ĐƯA CÁC LỆNH IN DEBUG VÀO MÃ NGUỒN CHÍNH**:
   - Không để lại `Console.WriteLine` trong các Service hoặc Core Libraries.
3. **TUYỆT ĐỐI KHÔNG ĐẶT `Version="..."` TRONG FILE `.csproj`**:
   - Mọi phiên bản thư viện phải được quản lý tập trung tại `Directory.Packages.props`.
