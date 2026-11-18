---
name: il-emit-engineering
description: Hướng dẫn và quy chuẩn kỹ thuật Dynamic IL Emit, IL evaluation stack balance, OpCodes, zero-allocation pooling, cấm tuyệt đối Reflection trên hot-path.
---

# Kỹ Năng Kỹ Thuật Dynamic IL Emit & Zero-Allocation (IL Emit Engineering Skill)

Module UDAL trong NpOn.Net-V2 sử dụng `System.Reflection.Emit.DynamicMethod` để sinh mã máy tại runtime. Kỹ năng này cung cấp hướng dẫn bất di bất dịch cho bot khi can thiệp vào tầng sinh mã IL:

## 1. Nguyên Tắc Cân Bằng IL Evaluation Stack

- **Nguyên tắc vàng**: Mỗi lệnh `OpCode` đẩy (push) một giá trị lên stack thì phải có lệnh tiêu thụ (consume) hoặc dỡ bỏ (`OpCodes.Pop`) giá trị đó.
- **Nếu stack bị mất cân bằng (Stack Imbalance)**: CLR sẽ ném ngoại lệ `System.InvalidProgramException: Common Language Runtime detected an invalid program`.

### Các Cặp OpCode Chuẩn

1. **Đọc trường từ DataReader**:
   ```csharp
   // Đẩy đối tượng reader lên stack
   il.Emit(OpCodes.Ldarg_0); // reader
   il.Emit(OpCodes.Ldc_I4, ordinal); // index cột
   il.Emit(OpCodes.Callvirt, getSpecificMethod); // reader.GetInt64(ordinal)
   ```
2. **Gán thuộc tính cho Đối Tượng Đích**:
   ```csharp
   // Đẩy instance đích lên stack
   il.Emit(OpCodes.Ldloc, targetInstance);
   // Đẩy giá trị vừa đọc lên stack
   // Gọi Setter
   il.Emit(OpCodes.Callvirt, propertySetter);
   ```
3. **Chuyển đổi kiểu & Boxing / Unboxing**:
   - Nếu thuộc tính là Value Type (`int`, `long`, `Guid`, `DateTime`): Sử dụng `OpCodes.Unbox_Any` nếu đọc từ `object`.
   - Nếu thuộc tính là Reference Type (`string`, `byte[]`, `class`): Sử dụng `OpCodes.Castclass`.
   - Khi truyền Value Type vào phương thức nhận `object`: Bắt buộc gọi `il.Emit(OpCodes.Box, propType)`.

## 2. Quy Tắc Bộ Nhớ Zero-Allocation & Caching

- **Tuyệt đối không compile lại Delegate**:
  - Mọi `DynamicMethod` sau khi gọi `.CreateDelegate()` phải được lưu vào bộ nhớ cache tĩnh (`ConcurrentDictionary` trong `WrapperCacheStore`).
  - Key của cache phải là tổ hợp `(Type targetType, string schemaSignature)`.
- **Tái sử dụng qua ObjectPoolStore**:
  - Mọi mảng buffer hoặc wrapper trung gian phải được mượn từ `ArrayPool<T>.Shared.Rent()` hoặc `IObjectPoolStore.Get()` và hoàn trả lại (`Return()`) trong khối `finally`.
  - Trong chu trình vòng lặp 10,000 queries, chỉ số Garbage Collection **BẮT BUỘC ĐẠT: Gen0 = 0, Gen1 = 0, Gen2 = 0**.

