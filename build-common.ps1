# ==============================================================================
# NpOn.Net-V2: Incremental NuGet Package Feed Builder for Common & Shared Contracts
# Packs only changed libraries into artifacts/packages/*.nupkg and evicts stale
# entries from the local NuGet global-packages cache so Rider / CLI / Docker / CI
# resolve transitive dependencies cleanly without recompiling Common on every build.
# Usage:
#   .\build-common.ps1           # Incremental pack (skips unchanged libraries)
#   .\build-common.ps1 -Force    # Force re-pack all shared libraries
# ==============================================================================
param(
    [switch]$Force = $false,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$swTotal = [System.Diagnostics.Stopwatch]::StartNew()

$rootDir = $PSScriptRoot
$commonDir = Join-Path $rootDir "Common"
$protoDir = Join-Path $rootDir "Protos"
$artifactsDir = Join-Path $rootDir "artifacts"
$packagesDir = Join-Path $artifactsDir "packages"
$hashFile = Join-Path $artifactsDir ".package_hashes.json"

if (-not (Test-Path $packagesDir)) {
    New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null
}

# Read package version from Directory.Build.props
$pkgVersion = "2.0.0"
$buildPropsPath = Join-Path $rootDir "Directory.Build.props"
if (Test-Path $buildPropsPath) {
    $propsContent = Get-Content $buildPropsPath -Raw
    if ($propsContent -match "<NpOnPackageVersion>\s*([^<\s]+)\s*</NpOnPackageVersion>") {
        $pkgVersion = $matches[1]
    }
}

# Discover NuGet global-packages cache directory
$globalPackagesDir = $null
try {
    $localsOut = dotnet nuget locals global-packages --list 2>$null | Out-String
    if ($localsOut -match "global-packages:\s*(.+)") {
        $globalPackagesDir = $matches[1].Trim()
    }
} catch { }
if (-not $globalPackagesDir) {
    if ($env:NUGET_PACKAGES) {
        $globalPackagesDir = $env:NUGET_PACKAGES
    } elseif ($IsWindows -or $env:USERPROFILE) {
        $globalPackagesDir = Join-Path $env:USERPROFILE ".nuget\packages"
    } else {
        $globalPackagesDir = Join-Path $HOME ".nuget/packages"
    }
}

# Organize packable projects into 3 DAG phases:
#   Phase 1: Base Common libraries (no dependency on MicroServices)
#   Phase 2: Cross-service shared Contracts, Definitions, and Service Interfaces
#   Phase 3: Common TokenValidatorExtUse (depends on Phase 2 Account contracts via NuGet)
$tokenValidatorProj = [System.IO.Path]::GetFullPath((Join-Path $commonDir "Applications\ApplicationExtensions\NpOn.TokenValidatorExtUse\NpOn.TokenValidatorExtUse.csproj"))

$protoProjects = if (Test-Path $protoDir) {
    Get-ChildItem -Path $protoDir -Recurse -Filter "*.csproj" |
    ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) } |
    Sort-Object
} else { @() }

$phase1Projects = @(
    $protoProjects + @(
        Get-ChildItem -Path $commonDir -Recurse -Filter "*.csproj" |
        ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) } |
        Where-Object { $_ -ne $tokenValidatorProj } |
        Sort-Object
    )
)

$phase2RelPaths = @(
    "MicroServices/Account/Definitions/NpOn.AccountEnum/NpOn.AccountEnum.csproj",
    "MicroServices/Account/Definitions/NpOn.ShareAccountConstant/NpOn.ShareAccountConstant.csproj",
    "MicroServices/Account/Contract/NpOn.AccountServiceCommand/NpOn.AccountServiceCommand.csproj",
    "MicroServices/Account/Contract/NpOn.AccountServiceReadModel/NpOn.AccountServiceReadModel.csproj",
    "MicroServices/Account/Service/NpOn.IAccountService/NpOn.IAccountService.csproj",
    "MicroServices/General/Contract/NpOn.GeneralServiceCommand/NpOn.GeneralServiceCommand.csproj",
    "MicroServices/General/Contract/NpOn.GeneralServiceReadModel/NpOn.GeneralServiceReadModel.csproj",
    "MicroServices/General/Service/NpOn.IGeneralService/NpOn.IGeneralService.csproj",
    "MicroServices/Migration/NpOn.IMigrationService/NpOn.IMigrationService.csproj",
    "MicroServices/Tracker/Definitions/NpOn.TrackerEnum/NpOn.TrackerEnum.csproj",
    "MicroServices/Tracker/Definitions/NpOn.TrackerConstant/NpOn.TrackerConstant.csproj",
    "MicroServices/Tracker/Contract/NpOn.TrackerServiceCommand/NpOn.TrackerServiceCommand.csproj",
    "MicroServices/Tracker/Contract/NpOn.TrackerServiceReadModel/NpOn.TrackerServiceReadModel.csproj",
    "MicroServices/Tracker/Service/NpOn.ITrackerService/NpOn.ITrackerService.csproj",
    "MicroServices/Tracker/Service/NpOn.Tracker2WayService/NpOn.Tracker2WayService.csproj"
)
$phase2Projects = @(
    $phase2RelPaths |
    ForEach-Object { [System.IO.Path]::GetFullPath((Join-Path $rootDir $_)) } |
    Where-Object { Test-Path $_ }
)

$phase3Projects = @()
if (Test-Path $tokenValidatorProj) {
    $phase3Projects = @($tokenValidatorProj)
}

$allProjects = @($phase1Projects + $phase2Projects + $phase3Projects)

# Compute shared root config hash (Directory.Build.*, Directory.Packages.props, nuget.config)
$md5 = [System.Security.Cryptography.MD5]::Create()
$rootConfigFiles = @("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config") |
    ForEach-Object { Join-Path $rootDir $_ } |
    Where-Object { Test-Path $_ }
$rootMs = [System.IO.MemoryStream]::new()
$rootBw = [System.IO.BinaryWriter]::new($rootMs)
foreach ($rf in $rootConfigFiles) {
    $info = Get-Item $rf
    $rootBw.Write($info.Name)
    $rootBw.Write($info.Length)
    $rootBw.Write($info.LastWriteTimeUtc.Ticks)
}
$rootBw.Flush()
$rootHash = -join ($md5.ComputeHash($rootMs.ToArray()) | ForEach-Object { "{0:x2}" -f $_ })
$rootBw.Dispose()
$rootMs.Dispose()

# Read previous hashes
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

$rootConfigChanged = (-not $previousHashes.ContainsKey("__root_config__")) -or ($previousHashes["__root_config__"] -ne $rootHash)

Write-Host "[COMMON NUGET] Checking source hashes across $($allProjects.Count) packable libraries (v$pkgVersion)..." -ForegroundColor Cyan
$swHash = [System.Diagnostics.Stopwatch]::StartNew()

$currentHashes = [ordered]@{ "__root_config__" = $rootHash }
$dirtySet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($projPath in $allProjects) {
    $projDir = [System.IO.Path]::GetDirectoryName($projPath)
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($projPath)
    $targetNupkg = Join-Path $packagesDir "$projName.$pkgVersion.nupkg"

    $files = @(Get-ChildItem -Path $projDir -Recurse -Include "*.cs", "*.csproj", "*.proto" |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        Sort-Object FullName)
    if ((Test-Path $protoDir) -and $projPath.StartsWith($protoDir, [System.StringComparison]::OrdinalIgnoreCase)) {
        $sharedProtos = @(Get-ChildItem -Path $protoDir -Recurse -Filter "*.proto" | Sort-Object FullName)
        $files = @(@($files) + @($sharedProtos) | Sort-Object FullName -Unique)
    }
    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    foreach ($f in $files) {
        $bw.Write($f.Name)
        $bw.Write($f.Length)
        $bw.Write($f.LastWriteTimeUtc.Ticks)
    }
    $bw.Flush()
    $hashStr = -join ($md5.ComputeHash($ms.ToArray()) | ForEach-Object { "{0:x2}" -f $_ })
    $bw.Dispose()
    $ms.Dispose()

    $normalizedRoot = $rootDir.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $relKey = if ($projPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $projPath.Substring($normalizedRoot.Length).Replace("\", "/")
    } else {
        $projPath.Replace("\", "/")
    }
    $currentHashes[$relKey] = $hashStr

    $isMissingNupkg = -not (Test-Path $targetNupkg)
    $isChanged = (-not $previousHashes.ContainsKey($relKey)) -or ($previousHashes[$relKey] -ne $hashStr)

    if ($Force -or $rootConfigChanged -or $isMissingNupkg -or $isChanged) {
        [void]$dirtySet.Add($projPath)
    }
}
$swHash.Stop()

if ($dirtySet.Count -eq 0) {
    $swTotal.Stop()
    Write-Host "[COMMON CACHE HIT] All $($allProjects.Count) packages up-to-date in $packagesDir ($($swHash.ElapsedMilliseconds) ms)." -ForegroundColor Green
    exit 0
}

Write-Host "[COMMON PACK] Packing $($dirtySet.Count)/$($allProjects.Count) changed libraries into $packagesDir..." -ForegroundColor Yellow
$swPack = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-PackPhase {
    param(
        [string]$PhaseName,
        [string[]]$Projects
    )
    $dirtyInPhase = @($Projects | Where-Object { $dirtySet.Contains($_) })
    if ($dirtyInPhase.Count -eq 0) {
        return 0
    }

    Write-Host "  [$PhaseName] Packing $($dirtyInPhase.Count) project(s)..." -ForegroundColor Cyan

    # Clear global-packages cache for every dirty package before packing so consumers see fresh binaries
    foreach ($proj in $dirtyInPhase) {
        $pName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        if ($globalPackagesDir) {
            $cachedPkgDir = Join-Path $globalPackagesDir (Join-Path $pName.ToLowerInvariant() $pkgVersion)
            if (Test-Path $cachedPkgDir) {
                Remove-Item -Path $cachedPkgDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($dirtyInPhase.Count -eq 1) {
        $singleProj = $dirtyInPhase[0]
        $pName = [System.IO.Path]::GetFileNameWithoutExtension($singleProj)
        $out = dotnet pack $singleProj -c $Configuration -o $packagesDir -p:NuspecProperties="version=$pkgVersion" --nologo 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[COMMON PACK ERROR] Failed to pack $pName!" -ForegroundColor Red
            $out | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            exit 1
        }
    } else {
        # Batch pack in parallel using a temporary .slnx (single MSBuild restore + multi-core build)
        $tempSlnx = Join-Path $artifactsDir ".pack_phase_tmp.slnx"
        $sb = [System.Text.StringBuilder]::new()
        [void]$sb.AppendLine("<Solution>")
        foreach ($proj in $dirtyInPhase) {
            $escaped = [System.Security.SecurityElement]::Escape($proj)
            [void]$sb.AppendLine("  <Project Path=`"$escaped`" />")
        }
        [void]$sb.AppendLine("</Solution>")
        Set-Content -Path $tempSlnx -Value $sb.ToString() -Encoding UTF8

        try {
            $out = dotnet pack $tempSlnx -c $Configuration -o $packagesDir -p:NuspecProperties="version=$pkgVersion" -m:4 -nr:false --nologo 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[COMMON PACK ERROR] Failed during batch pack [$PhaseName]!" -ForegroundColor Red
                $out | ForEach-Object { Write-Host $_ -ForegroundColor Red }
                exit 1
            }
        } finally {
            if (Test-Path $tempSlnx) {
                Remove-Item -Path $tempSlnx -Force -ErrorAction SilentlyContinue
            }
        }
    }
    return $dirtyInPhase.Count
}

$packedCount = 0
$packedCount += Invoke-PackPhase -PhaseName "Phase 1/3 Base Common" -Projects $phase1Projects
$packedCount += Invoke-PackPhase -PhaseName "Phase 2/3 Shared Contracts" -Projects $phase2Projects
$packedCount += Invoke-PackPhase -PhaseName "Phase 3/3 TokenValidator" -Projects $phase3Projects

$swPack.Stop()

$currentHashes | ConvertTo-Json -Depth 4 | Set-Content -Path $hashFile -Force

$swTotal.Stop()
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  NPON COMMON NUGET PACK SUCCESS (v$pkgVersion)" -ForegroundColor Green
Write-Host "  - Hash Check: $($swHash.ElapsedMilliseconds) ms | Packed: $packedCount/$($allProjects.Count)" -ForegroundColor Green
Write-Host "  - Pack Duration: $($swPack.ElapsedMilliseconds) ms | Total: $($swTotal.ElapsedMilliseconds) ms" -ForegroundColor Green
Write-Host "  - Feed Path: $packagesDir" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
