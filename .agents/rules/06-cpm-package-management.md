# Rule 06: Central Package Management (CPM) Guidelines

## 1. Nguyên Tắc Quản Lý Gói Tập Trung
- Toàn bộ Solution `NpOnV2.sln` kích hoạt tính năng **Central Package Management (CPM)** của .NET.
- Mọi gói NuGet phụ thuộc và phiên bản tương ứng phải được khai báo duy nhất tại tập tin gốc:
  `Directory.Packages.props`

## 2. Quy Định Bắt Buộc Đối Với Từng Dự Án (`.csproj`)
- **TUYỆT ĐỐI CẤM** khai báo thuộc tính `Version="..."` trong thẻ `<PackageReference>` tại bất kỳ file `.csproj` nào.
  ```xml
  <!-- ĐÚNG CHUẨN: -->
  <PackageReference Include="Npgsql" />
  <PackageReference Include="protobuf-net.Grpc.AspNetCore" />

  <!-- VI PHẠM (SẼ BỊ HARNESS TỪ CHỐI): -->
  <PackageReference Include="Npgsql" Version="9.0.2" />
  ```
- **Khi thêm thư viện mới**:
  1. Thêm thẻ `<PackageVersion Include="TênGói" Version="x.y.z" />` vào file `Directory.Packages.props`.
  2. Tại file `.csproj` tương ứng, chỉ khai báo `<PackageReference Include="TênGói" />`.

