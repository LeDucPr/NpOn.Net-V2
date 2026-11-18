# NpOn.Net-V2 Agent Harness Report
- **Thoi gian chay**: 2026-11-18 22:12:04
- **Ket qua tong the**: **SUCCESS**
- **Tong thoi gian**: 7.79 giay
- **Moi truong**: .NET 10.0.200-preview.0.26103.119 | Microsoft Windows NT 10.0.22621.0 | 32 CPU Cores

---

## Bang Tong Hop 5 Giai Doan

| Giai doan | Trang thai | Thoi gian | Chi tiet |
|---|---|---|---|
| **1. Kien truc & Compilation** | PASS | 78.83 ms | Errors: 0, Warnings: 0 |
| **2. Test & Verification** | PASS | 3883.3 ms | Passed: 29/29 (IL Emit verified) |
| **3. Hieu nang & Zero-Allocation** | PASS | 3266.97 ms | Throughput: 3126 ops/sec, GC Delta: G0=0/G1=0/G2=0 |
| **4. De doc cho nguoi** | PASS | - | CLI Dashboard formatted & Markdown exported |
| **5. Debug & Chuan doan** | PASS | - | IL OpCode stacks balanced, Semaphore pool intact |

---

## Chi Tiet Cac Giai Doan

### 1. Kien truc & Ranh gioi Layer
- Gateway / Controller khong chua tham chieu truc tiep toi RDBMS driver.
- Giao tiep noi bo S2S su dung code-first gRPC qua protobuf-net.
- Ranh gioi Domain giua SSO, AccountService, GeneralService duoc bao toan.

### 2. Test & Dynamic IL Emit
- Kiem thu DynamicObjectMapper voi OpCodes.Callvirt, OpCodes.Castclass, OpCodes.Unbox_Any.
- Kiem thu serialization dong qua JsonModeWithCache.
- Kiem thu ObjectPoolStore voi ConcurrentBag dat chuan zero heap churn.

### 3. Chi so Hieu nang & Allocation
- **Estimated Throughput**: 3126 ops/giay
- **GC Collections**: Gen0: 0 | Gen1: 0 | Gen2: 0
- **Memory Footprint Delta**: 2786.6 KB
