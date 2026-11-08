# Rule 04: Debugging & Diagnostics Guidelines

## 1. Gỡ lỗi Dynamic IL Emit
Khi làm việc với `DynamicMethod`, các lỗi thường gặp không hiển thị stack trace cụ thể mà ném ra:
- `InvalidProgramException`: Do stack IL bị lệch (thừa/thiếu phần tử trên evaluation stack) hoặc nhảy nhãn (`OpCodes.Br`) tới vị trí không hợp lệ.
- `VerificationException`: Do ép kiểu sai giữa các kiểu dữ liệu không tương thích.

### Quy trình chẩn đoán:
1. Đọc kỹ signature của method được gọi qua `OpCodes.Call` hoặc `OpCodes.Callvirt` để đảm bảo số lượng tham số được đẩy lên stack khớp 100%.
2. Không tắt IL Emit khi debug: Sử dụng IL inspection và logging stack instruction để truy vết từng opcode được sinh ra.
3. Kiểm tra xem method đích có phải là `virtual` không; nếu là virtual method trên class/interface phải dùng `OpCodes.Callvirt`, nếu là static method hoặc value type struct method phải dùng `OpCodes.Call`.

---

## 2. Gỡ lỗi Connection Pool & Semaphore Deadlock
Khi hệ thống có hiện tượng timeout hoặc treo request:
1. Kiểm tra biến đếm `CurrentCount` của `SemaphoreSlim` trong `DbFactoryWrapper`. Nếu `CurrentCount == 0` kéo dài, chứng tỏ có kết nối lấy ra từ factory nhưng không được `Release()` trong khối `try...finally`.
2. Kiểm tra `ObjectPoolStore`: Đảm bảo mọi `Get()` đều có lệnh `Return()` đối ứng để tránh cạn kiệt pool wrapper dẫn đến việc cấp phát mới trên heap.
3. Kiểm tra Distributed Tracing giữa Gateway và Microservices: Tra cứu trace log qua gRPC header metadata (`CommonHeaderConfig`).

