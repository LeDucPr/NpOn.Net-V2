# ==============================================================================
# NpOn.Net-V2: Script Xuất Toàn Bộ Hợp Đồng gRPC Sang Chuẩn Google Proto 3 Quốc Tế
# Xuất các file .proto độc lập không phụ thuộc BCL C#, tương thích đa ngôn ngữ
# (Go, Java, Python, Node.js, Rust, grpcurl, Postman)
# ==============================================================================

param(
    [string]$OutputDir = "proto"
)

$ErrorActionPreference = "Stop"
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$rootDir = $PSScriptRoot
$targetDir = Join-Path $rootDir $OutputDir

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  NPON.NET-V2 PROTO 3 UNIVERSAL SCHEMA EXPORT" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Đảm bảo thư mục proto/ tồn tại
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# Chạy test suite để xuất proto bằng generator
Write-Host "[INFO] Dang trich xuat va chuan hoa Schema Proto 3..." -ForegroundColor Yellow
$testOutput = dotnet test (Join-Path $rootDir "Test\NpOn.Core.Tests\NpOn.Core.Tests.csproj") `
    -c Release --filter "FullyQualifiedName~Proto3Export" --nologo 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Xuat Proto 3 that bai!" -ForegroundColor Red
    $testOutput | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}

# Đồng bộ các file proto sinh ra về thư mục gốc proto/
$binProtoDir = Join-Path $rootDir "Test\NpOn.Core.Tests\bin\Release\net10.0\proto"
if (Test-Path $binProtoDir) {
    Copy-Item -Path "$binProtoDir\*" -Destination $targetDir -Recurse -Force
}

# Liệt kê danh sách các file proto đã xuất
$protoFiles = Get-ChildItem -Path $targetDir -Recurse -Filter "*.proto"

$sw.Stop()
Write-Host "[PASS] Da xuat thanh cong $($protoFiles.Count) file .proto sang thu muc $OutputDir (${($sw.Elapsed.TotalMilliseconds).ToString('F1')} ms):" -ForegroundColor Green
foreach ($f in $protoFiles) {
    Write-Host "  - $($f.FullName.Substring($rootDir.Length + 1)) ($($f.Length) bytes)" -ForegroundColor Cyan
}

Write-Host "================================================================================" -ForegroundColor Cyan
