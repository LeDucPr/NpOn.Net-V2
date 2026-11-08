# Rule 02: Dynamic IL Emit & Zero-Allocation UDAL Guidelines

## 1. Vai trò của Dynamic IL Emit (ILCode)
Trong `NpOn.Net-V2`, hiệu năng xử lý dữ liệu và mapping được đẩy lên mức native thông qua `System.Reflection.Emit`:
- **Loại bỏ chi phí Reflection**: Thay vì dùng Reflection chậm chạp tại mỗi query, hệ thống dùng `DynamicMethod` để sinh mã máy IL (Intermediate Language) runtime nhằm gán trực tiếp dữ liệu từ Database Cell sang Object Property.
- **Tương đương Native Code**: Sau khi delegate được JIT biên dịch một lần, các lần gọi tiếp theo thực thi với tốc độ tương đương code viết tay bằng C#.

---

## 2. Nguyên tắc BẮT BUỘC khi làm việc với IL Emit
1. **KHÔNG bypass hoặc vô hiệu hóa IL Emit khi gỡ lỗi hoặc tối ưu hóa**:
   - Khi cần tăng tốc độ hay gỡ lỗi, KHÔNG được chuyển ngược lại dùng Reflection chậm chạp hay Newtonsoft reflection fallback trên hot-path.
   - IL Emit là giải pháp hiệu năng cốt lõi đã được kiểm định trên hệ thống y tế thực tế, bắt buộc phải duy trì tính toàn vẹn của mã byte sinh ra.
2. **Quản lý Stack IL nghiêm ngặt**:
   - Mọi lệnh `OpCodes.Call`, `OpCodes.Callvirt`, `OpCodes.Newobj` phải tính toán stack push/pop chính xác. Stack balance phải là 0 hoặc 1 (nếu có giá trị trả về) tại điểm `OpCodes.Ret`.
   - Nếu gọi method có giá trị trả về mà không gán (vd: `StringBuilder.Append`), bắt buộc phải emit `OpCodes.Pop`.
3. **Boxing / Unboxing & Nullable Types**:
   - Value types (int, long, Guid, DateTime, Enum) khi đọc từ `INpOnCell.ValueAsObject` (kiểu `object`) phải thực hiện `OpCodes.Unbox_Any` hoặc `Convert.ChangeType` phù hợp.
   - Khi ghi ra `object`, value types bắt buộc phải `OpCodes.Box`.
4. **Delegate Caching qua WrapperCacheStore**:
   - Dynamic method chỉ được sinh MỘT LẦN duy nhất cho mỗi kiểu `Type`.
   - Lưu trữ delegate trong `WrapperCacheStore<Type, Delegate>` để tái sử dụng xuyên suốt vòng đời ứng dụng.

---

## 3. Zero-Allocation & Object Pooling
- Các Wrapper trung gian (`NpOnWrapperResult`, `ITabularResultSet`, `RowWrapper`, `CollectionWrapper`) phải được lấy từ `IObjectPoolStore` thay vì cấp phát mới trên heap bằng `new`.
- Khi kết thúc luồng dữ liệu, thực hiện gọi `.Dispose()` hoặc `.Return()` để đưa đối tượng trở lại `ConcurrentBag<T>` của pool, giữ chỉ số Garbage Collection (GC) về mức tối thiểu.

