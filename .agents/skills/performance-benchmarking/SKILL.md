---
name: performance-benchmarking
description: Đo lường Throughput, Latency P95/P99, GC Gen0/1/2 collections, và kiểm tra Zero GC Leak trên hot-path 10,000 iterations.
---

# Kỹ Năng Đo Lường & Đánh Giá Hiệu Năng (Performance Benchmarking Skill)

Kỹ năng này hướng dẫn bot thực hiện đo đạc, benchmark đối chuẩn và giám sát tài nguyên bộ nhớ theo đúng tiêu chuẩn NpOn.Net-V2:

## 1. Phương Pháp Đo Đạc Chuẩn

Khi đo lường một thuật toán hoặc phương thức UDAL, luôn tuân thủ quy trình 4 bước:

1. **Giai đoạn Làm Nóng (Warm-up Phase)**:
   - Chạy trước 500 – 1,000 vòng lặp không đo đạc để JIT Compiler hoàn tất biên dịch mã máy native và JIT Tier 1 / Dynamic PGO.
2. **Giai đoạn Thu Gom Bộ Nhớ Ban Đầu (Baseline Memory GC)**:
   ```csharp
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   long baselineMemory = GC.GetTotalMemory(true);
   int g0Before = GC.CollectionCount(0);
   int g1Before = GC.CollectionCount(1);
   int g2Before = GC.CollectionCount(2);
   ```
3. **Giai đoạn Đo Đạc (Measurement Loop)**:
   - Sử dụng `System.Diagnostics.Stopwatch.StartNew()` với độ phân giải cao (`Stopwatch.IsHighResolution == true`).
   - Chạy tối thiểu 10,000 iterations trên hot-path.
4. **Giai đoạn Thu Thập Delta & Chỉ Số GC**:
   ```csharp
   sw.Stop();
   long finalMemory = GC.GetTotalMemory(false);
   int g0Delta = GC.CollectionCount(0) - g0Before;
   int g1Delta = GC.CollectionCount(1) - g1Before;
   int g2Delta = GC.CollectionCount(2) - g2Before;
   double elapsedMs = sw.Elapsed.TotalMilliseconds;
   double throughput = (iterations / elapsedMs) * 1000.0;
   ```

## 2. Tiêu Chuẩn Đạt Chuẩn (Acceptance Criteria)

- **Throughput Hot-path**: $\ge 3,000 \text{ operations/giây}$ (thực tế UDAL mapping đạt $200,000+ \text{ operations/giây}$).
- **Throughput Streaming Bulk**: $\ge 800,000 \text{ records/giây}$.
- **Gen0 / Gen1 / Gen2 Delta**: **Bắt buộc Gen2 = 0** trong chu trình mapping vòng lặp.
- **RAM Delta**: $\le 3.0 \text{ MB}$ cho 10,000 iterations.

## 3. Quy Chuẩn Kiểm Thử Hiệu Năng Ánh Xạ Driver (Mapping Benchmark Parity)

Mọi database driver (PostgreSQL, Cassandra/ScyllaDB, SQL Server) sau khi kiểm tra chức năng **bắt buộc phải có test suite đo lường hiệu năng ánh xạ Dynamic IL Emit** (10,000 iterations):
1. **RowMapper Benchmark**: Đo thời gian và thông lượng chuyển đổi raw data array sang RowWrapper dictionary.
2. **Object Mapper Benchmark**: Đo thời gian và thông lượng ánh xạ dữ liệu phẳng sang Strongly-Typed ReadModel.
3. **Tiêu chuẩn**: Throughput $> 10,000 \text{ ops/sec}$, GC Gen2 = 0 (Zero GC Leak).

## 4. Chuẩn Đo Đạc Đa Framework 3-Way (UDAL vs Dapper vs EF Core 9)

Tại project `Test/NpOn.OmsDapper`:
1. **Kiến trúc so sánh**: So sánh đồng cấp Unified UDAL (Vectorized Pages + Dynamic IL Emit) vs Dapper 2.1 (Reflection Emit) vs EF Core 9.0 (`AsNoTracking`).
2. **Kỹ thuật xoay tua (Rotated Execution)**: Xoay vòng thứ tự thực thi (`RunTriadSequentialBenchmarkAsync` & `RunTriadConcurrentBenchmarkAsync`) giữa Dapper, EF Core, và UDAL qua $N$ lần lặp để triệt tiêu bias.
3. **Chỉ số 4 chiều**: Thông lượng (rec/s), Độ trễ (Mean/Min/Max/P95 ms), GC Collections (Gen0/1/2), và Kích thước assembly (DLL Size).


