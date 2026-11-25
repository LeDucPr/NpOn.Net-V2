# ==============================================================================
# NpOn.Net-V2: Bộ Biên Dịch & Đóng Gói Thư Viện Chung (Granular Incremental Build)
# Tính toán MD5 Hash theo từng Project - Chỉ biên dịch & copy những project thay đổi
# Hỗ trợ toàn bộ Common + Các Shared Contracts (Command, Query, ReadModel, Enums)
# Usage:
#   .\build-common.ps1           # Biên dịch cục bộ từng project thay đổi
#   .\build-common.ps1 -Force    # Bắt buộc biên dịch lại toàn bộ
# ==============================================================================
param(
    [switch]$Force = $false,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$swTotal = [System.Diagnostics.Stopwatch]::StartNew()

$rootDir = $PSScriptRoot
$commonDir = Join-Path $rootDir "Common"
$artifactsDir = Join-Path $rootDir "artifacts"
$libsDir = Join-Path $artifactsDir "libs"
$hashFile = Join-Path $artifactsDir ".project_hashes.json"
$slnFile = Join-Path $commonDir "Common.slnx"

# Tạo thư mục đích nếu chưa có
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir -Force | Out-Null
}

# 1. Thu thập danh sách toàn bộ các project cần quản lý
$projects = [System.Collections.Generic.List[string]]::new()

# Thêm 52 project từ Common
$commonProjects = Get-ChildItem -Path $commonDir -Recurse -Filter "*.csproj" | Select-Object -ExpandProperty FullName
foreach ($p in $commonProjects) {
    $projects.Add($p)
}

# Thêm các Shared Contracts / Definitions / Services dùng chung từ MicroServices
$sharedContractPatterns = @(
    "MicroServices\Account\Contract\*\*.csproj",
    "MicroServices\Account\Definitions\*\*.csproj",
    "MicroServices\Account\Service\NpOn.IAccountService\*.csproj",
    "MicroServices\General\Contract\*\*.csproj",
    "MicroServices\General\Definitions\*\*.csproj",
    "MicroServices\General\Service\NpOn.IGeneralService\*.csproj",
    "MicroServices\Migration\NpOn.IMigrationService\*.csproj",
    "MicroServices\Tracker\Contract\*\*.csproj",
    "MicroServices\Tracker\Definitions\*\*.csproj",
    "MicroServices\Tracker\Service\NpOn.ITrackerService\*.csproj"
)

foreach ($pattern in $sharedContractPatterns) {
    $found = Get-ChildItem -Path (Join-Path $rootDir $pattern) -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    foreach ($f in $found) {
        if (-not $projects.Contains($f)) {
            $projects.Add($f)
        }
    }
}

# 2. Đọc bảng băm cũ từ file JSON
$previousHashes = @{}
if (Test-Path $hashFile) {
    try {
        $jsonContent = Get-Content $hashFile -Raw | ConvertFrom-Json
        foreach ($prop in $jsonContent.PSObject.Properties) {
            $previousHashes[$prop.Name] = $prop.Value
        }
    } catch {
        $previousHashes = @{}
    }
}

# 3. Tính toán hash từng project và phát hiện thay đổi
Write-Host "[COMMON] Kiem tra thay doi ma nguon tren $($projects.Count) project (Per-Project Hash Analysis)..." -ForegroundColor Cyan
$swHash = [System.Diagnostics.Stopwatch]::StartNew()

$currentHashes = @{}
$dirtyProjects = [System.Collections.Generic.List[string]]::new()
$md5 = [System.Security.Cryptography.MD5]::Create()

foreach ($projPath in $projects) {
    $projDir = [System.IO.Path]::GetDirectoryName($projPath)
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($projPath)
    $targetDll = Join-Path $libsDir "$projName.dll"

    # Lấy toàn bộ file nguồn của project
    $files = Get-ChildItem -Path $projDir -Recurse -Include "*.cs", "*.csproj" | Sort-Object FullName
    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)

    foreach ($f in $files) {
        $bw.Write($f.Name)
        $bw.Write($f.Length)
        $bw.Write($f.LastWriteTimeUtc.Ticks)
    }
    $bw.Flush()
    $hashBytes = $md5.ComputeHash($ms.ToArray())
    $hashStr = -join ($hashBytes | ForEach-Object { "{0:x2}" -f $_ })
    $currentHashes[$projPath] = $hashStr

    $isMissingDll = -not (Test-Path $targetDll)
    $isChanged = (-not $previousHashes.ContainsKey($projPath)) -or ($previousHashes[$projPath] -ne $hashStr)

    if ($Force -or $isMissingDll -or $isChanged) {
        $dirtyProjects.Add($projPath)
    }
}
$swHash.Stop()

# 4. Kiểm tra nếu không có project nào thay đổi -> Cache Hit tức thì
if ($dirtyProjects.Count -eq 0) {
    $swTotal.Stop()
    Write-Host "[COMMON CACHE HIT] Toan bo $($projects.Count) project khong co thay doi (Hash Check: $($swHash.ElapsedMilliseconds) ms)." -ForegroundColor Green
    Write-Host "[COMMON CACHE HIT] Bo qua bien dich, su dung truc tiep $libsDir (Tong: $($swTotal.ElapsedMilliseconds) ms)." -ForegroundColor Green
    exit 0
}

Write-Host "[COMMON INCREMENTAL] Phat hien $($dirtyProjects.Count)/$($projects.Count) project co thay doi ma nguon." -ForegroundColor Yellow

# 5. Tiến hành biên dịch có chọn lọc
$swBuild = [System.Diagnostics.Stopwatch]::StartNew()

# Nếu số lượng project thay đổi lớn (> 15) hoặc ép buộc (-Force), build toàn bộ giải pháp
if ($Force -or $dirtyProjects.Count -gt 15) {
    Write-Host "[COMMON BUILD FULL] Dang bien dich toan bo giai phap Common.slnx ($Configuration)..." -ForegroundColor Yellow
    $buildOutput = dotnet build $slnFile -c $Configuration --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[COMMON ERROR] Bien dich that bai!" -ForegroundColor Red
        $buildOutput | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        exit 1
    }
} else {
    # Biên dịch CHỈ những project bị thay đổi (Granular Incremental Build)
    foreach ($dirtyProj in $dirtyProjects) {
        $pName = [System.IO.Path]::GetFileNameWithoutExtension($dirtyProj)
        Write-Host "  -> Dang bien dich project: $pName ($Configuration)..." -ForegroundColor Yellow
        $out = dotnet build $dirtyProj -c $Configuration --nologo 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[COMMON ERROR] Bien dich $pName that bai!" -ForegroundColor Red
            $out | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            exit 1
        }
    }
}
$swBuild.Stop()

# 6. Sao chép các file nhị phân (DLL, PDB) của các project thay đổi vào artifacts/libs
$swCopy = [System.Diagnostics.Stopwatch]::StartNew()
$copiedCount = 0

foreach ($dirtyProj in $dirtyProjects) {
    $projDir = [System.IO.Path]::GetDirectoryName($dirtyProj)
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($dirtyProj)
    $binDir = Join-Path $projDir "bin\$Configuration\net10.0"

    $dllSource = Join-Path $binDir "$projName.dll"
    if (Test-Path $dllSource) {
        Copy-Item -Path $dllSource -Destination (Join-Path $libsDir "$projName.dll") -Force
        $copiedCount++
        $pdbSource = Join-Path $binDir "$projName.pdb"
        if (Test-Path $pdbSource) {
            Copy-Item -Path $pdbSource -Destination (Join-Path $libsDir "$projName.pdb") -Force
        }
    }
}
$swCopy.Stop()

# 7. Ghi lại bảng băm mới
$currentHashes | ConvertTo-Json -Depth 4 | Set-Content -Path $hashFile -Force

$swTotal.Stop()
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  NPON COMMON BUILD SUCCESS (GRANULAR INCREMENTAL)" -ForegroundColor Green
Write-Host "  - Hash Check: $($swHash.ElapsedMilliseconds) ms | Projects Rebuilt: $($dirtyProjects.Count)/$($projects.Count)" -ForegroundColor Green
Write-Host "  - Compilation: $($swBuild.ElapsedMilliseconds) ms | Binary Copy: $($swCopy.ElapsedMilliseconds) ms ($copiedCount DLLs)" -ForegroundColor Green
Write-Host "  - Total Duration: $($swTotal.ElapsedMilliseconds) ms" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
