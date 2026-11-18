---
name: code-review-excellence
description: Kỹ năng review code đỉnh cao theo chuẩn Google Engineering Practices và Microsoft .NET Clean Architecture, kiểm soát zero-allocation, cấm magic numbers, và bảo toàn ranh giới phân tầng.
---

# Kỹ Năng Review Code Đỉnh Cao (Code Review Excellence Skill)

Quy chuẩn review mã nguồn này được tổng hợp từ **Google Engineering Practices (eng-practices)** và **Microsoft .NET Architecture Best Practices**, được thiết kế riêng cho hệ sinh thái hiệu năng cao NpOn.Net-V2.

Mọi AI Bot khi đọc, đánh giá hoặc tạo mới mã nguồn phải duyệt qua danh sách kiểm tra (Review Checklist) sau:

---

## 1. Kiểm Soát Ranh Giới Kiến Trúc (Architecture & Layer Boundaries)
- [ ] **Gateway Isolation**: Các Controller trong `Controllers/` KHÔNG chứa bất kỳ tham chiếu nào tới `NpgsqlConnection`, `SqlConnection` hay DB driver nào. Mọi giao tiếp với backend phải qua gRPC Code-First (`protobuf-net.Grpc`).
- [ ] **Domain Immutability**: Các Domain Entity và Contract DTO phải được định nghĩa bằng `record` với thuộc tính `init-only` (`public string Name { get; init; }`).
- [ ] **Central Package Management**: Không có thẻ `<PackageReference>` nào chứa thuộc tính `Version="..."` trong các file `.csproj`.

---

## 2. Kiểm Soát Hiệu Năng & Zero-Allocation (Performance & Allocations)
- [ ] **Cấm Reflection trên Hot-Path**: Tuyệt đối không gọi `Type.GetProperty()`, `PropertyInfo.SetValue()`, hoặc các bộ mapper reflection chậm. Bắt buộc dùng **Dynamic IL Emit** (`DynamicMethod`).
- [ ] **IL Evaluation Stack Balance**: Mọi đoạn mã sinh IL phải bảo đảm stack cân bằng: tiêu thụ hết giá trị đã push lên stack, gọi `OpCodes.Pop` khi discard return value, và `OpCodes.Box` khi chuyển Value Type sang `object`.
- [ ] **Buffer & Wrapper Pooling**: Các đối tượng wrapper trung gian (`NpOnWrapperResult`, cell/row buffers) phải được mượn từ `ArrayPool<T>` hoặc `IObjectPoolStore` và hoàn trả trong khối `finally`.
- [ ] **Zero GC Leak**: Chạy vòng lặp 10,000 lần phải đạt: **Gen0 = 0, Gen1 = 0, Gen2 = 0**.

---

## 3. Quy Chuẩn Kiểu Dữ Liệu & Cấm Magic Numbers
- [ ] **No Magic Numbers**: Không dùng số nguyên trần (`0`, `1`, `2`, `-1`) để đại diện cho trạng thái, thao tác CRUD, hoặc phân loại. Bắt buộc sử dụng Strongly-Typed Enum (`ERepositoryAction`, `EMergeClassification`, `ESagaStatus`).
- [ ] **Type Mapping Safety**: Sử dụng `NpOnTypeConverter` để đảm bảo chuyển đổi an toàn cho Guids, Enums, Nullables, DateTime/DateOnly, Arrays, và JSONB complex types.

---

## 4. Quản Lý Đồng Thời & Saga Resiliency (Concurrency & Saga)
- [ ] **Optimistic Concurrency Control (OCC)**: Mọi thao tác cập nhật hoặc Merge trên thực thể tham gia Saga bắt buộc kiểm tra `WHERE version = @expected_version`.
- [ ] **Dual-Write Protection**: Event Outbox bắt buộc được ghi trong cùng một `NpgsqlTransaction` với câu lệnh nghiệp vụ.
- [ ] **Idempotent Inbox**: Consumer phải kiểm tra `saga_inbox` và `saga_inbox_entity_state` để loại bỏ duplicate và bảo vệ tính thứ tự nhân quả (Causality Order).

---

## 5. Vệ Sinh Mã Nguồn (Cleanliness & Diagnostics)
- [ ] **No Diagnostic Printfs**: Tuyệt đối không để lại `Console.WriteLine` trong mã nguồn sản phẩm.
- [ ] **Async/Await Hygiene**: Không bao giờ sử dụng `.Result` hoặc `.Wait()` gây thread pool starvation / deadlock. Luôn sử dụng `await` hoặc `ValueTask<T>`.

