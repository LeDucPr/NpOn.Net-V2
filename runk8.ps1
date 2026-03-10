#pre setup (mở PowerShell gốc set)
# nerdctl -n k8s.io images
# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser 
# hoặc không cần set để chạy lệnh .\runk8.sp1 all
# powershell -ExecutionPolicy Bypass -File .\runk8.ps1 all
#-- nếu cần
### taskkill /F /IM VBCSCompiler.exe
### taskkill /F /IM dotnet.exe
### taskkill /F /IM MSBuild.exe

#lệnh tìm ip service  -    kubectl get svc sso -n default


$projects = @(
    "MicroServices/Account/Service/NpOn.AccountService",
    "MicroServices/General/Service/NpOn.GeneralService",
    "Controllers/NpOn.SSO"
)

# Tên file YAML chứa cấu hình K8s
$k8sFile = "rancherk8-deploy.yaml"

# ==========================================
# 1. PARSE THAM SỐ (Xác định Hành động & Mục tiêu)
# ==========================================
$doBuild = $args -contains "build"
$doUp    = $args -contains "up"
$isDown  = $args -contains "down"
$isStatus= $args -contains "status"

# Lấy các chữ không phải lệnh làm target (vd: sso, account)
$targets = $args | Where-Object { $_ -notin @("build", "up", "down", "status") }

# Lọc danh sách project sẽ chạy dựa trên target
$targetProjects = $projects
if ($targets.Count -gt 0) {
    $targetProjects = @()
    foreach ($p in $projects) {
        foreach ($t in $targets) {
            if ($p -match $t) {
                $targetProjects += $p
                break
            }
        }
    }
}

# Hàm lấy tên deployment từ đường dẫn (VD: NpOn.SSO -> sso)
function Get-DeployName($path) {
    return ($path -split "\.")[-1].ToLower()
}

# ==========================================
# 2. HÀM BUILD ĐA LUỒNG
# ==========================================
function Build-Targets($list) {
    Write-Host "`n--- Dang build SONG SONG $($list.Count) Microservices ---" -ForegroundColor Cyan
    $overall_start = Get-Date

    $jobs = foreach ($path in $list) {
        Start-Job -ScriptBlock {
            param($p, $root)
            cd $root
            $start = Get-Date
            
            $projectName = Split-Path $p -Leaf
            $tarFile = "$root\$projectName.tar.gz"
            
            # 1. Build
            $output = dotnet publish $p -t:PublishContainer -c Release -p:ContainerArchiveOutputPath=$tarFile --nologo 2>&1
            
            # 2. Load
            if ($LASTEXITCODE -eq 0 -and (Test-Path $tarFile)) {
                $loadOutput = cmd.exe /c "nerdctl -n k8s.io load < ""$tarFile""" 2>&1
                $output += "`n[Rancher] Đã nạp image: $loadOutput"
                Remove-Item $tarFile -Force
            }
            
            $end = Get-Date
            $elapsed = "{0:N2}" -f ($end - $start).TotalSeconds
            
            return [PSCustomObject]@{
                Path    = $p
                Elapsed = $elapsed
                Output  = $output
                Success = ($LASTEXITCODE -eq 0)
            }
        } -ArgumentList $path, $pwd.Path
    }

    Write-Host "Dang xu ly... Vui long doi..." -ForegroundColor Gray
    $jobResults = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job

    Write-Host "`n--- THONG KE THOI GIAN BUILD ---" -ForegroundColor Yellow
    $anyError = $false

    foreach ($res in $jobResults) {
        if ($res.Success) {
            Write-Host " [OK] " -NoNewline -ForegroundColor Green
            Write-Host "$($res.Path.PadRight(60)) : $($res.Elapsed) giay"
        } else {
            Write-Host " [FAIL]" -NoNewline -ForegroundColor Red
            Write-Host "$($res.Path.PadRight(60)) : Loi build!"
            Write-Host "   -> Chi tiet:" -ForegroundColor Red
            Write-Host ($res.Output | Out-String) -ForegroundColor DarkRed
            $anyError = $true
        }
    }

    $overall_duration = "{0:N2}" -f ((Get-Date) - $overall_start).TotalSeconds
    Write-Host "--------------------------------------------------------"
    if ($anyError) {
        Write-Host "--- KET THUC: Co loi xay ra! ---" -ForegroundColor Red
        return $false
    } else {
        Write-Host "--- SUCCESS: Xong trong $overall_duration giay! ---" -ForegroundColor Green
        return $true
    }
}

# ==========================================
# 3. ĐIỀU HƯỚNG THỰC THI
# ==========================================
if ($isDown) {
    Write-Host "--- Dang go bo he thong khoi K8s ---" -ForegroundColor Yellow
    kubectl delete -f $k8sFile
    exit
}
if ($isStatus) {
    Write-Host "--- Trang thai cac Pods ---" -ForegroundColor Cyan
    kubectl get pods -o wide
    exit
}

# Nếu không có file YAML hoặc sai lệnh
if (-not $doBuild -and -not $doUp) {
    Write-Host "`n--- MENU HUONG DAN CHUYEN NGHIEP ---" -ForegroundColor Yellow
    Write-Host " .\runk8.ps1 build up        : Build tất cả, nạp K8s và Restart Pods"
    Write-Host " .\runk8.ps1 build sso       : CHỈ build và nạp image cho SSO"
    Write-Host " .\runk8.ps1 up sso          : CHỈ restart Pod SSO để nhận code mới"
    Write-Host " .\runk8.ps1 build up sso    : Build -> Nạp -> Restart riêng SSO"
    Write-Host " .\runk8.ps1 down            : Gỡ toàn bộ khỏi K8s"
    Write-Host " .\runk8.ps1 status          : Xem trạng thái Pods"
    Write-Host "----------------------------------`n"
    exit
}

if ($targetProjects.Count -eq 0) {
    Write-Host "Loi: Khong tim thay service nao khop voi tu khoa: $targets" -ForegroundColor Red
    exit
}

# 1. Thực hiện BUILD nếu có cờ
$buildSuccess = $true
if ($doBuild) {
    $buildSuccess = Build-Targets $targetProjects
}

# 2. Thực hiện UP nếu có cờ và Build thành công
if ($doUp -and $buildSuccess) {
    Write-Host "`n--- [UP] Kiem tra cau hinh K8s YAML ---" -ForegroundColor Cyan
    # Luôn apply để cập nhật nếu lỡ bro có sửa file yaml
    kubectl apply -f $k8sFile

    Write-Host "`n--- [UP] Ép K8s chay lai (Restart) voi Image moi ---" -ForegroundColor Cyan
    foreach ($p in $targetProjects) {
        $deployName = Get-DeployName $p
        Write-Host "Restarting Deployment: $deployName ..." -ForegroundColor Gray
        # Lệnh ma thuật ép K8s giết pod cũ, kéo pod mới lên
        kubectl rollout restart deployment $deployName
    }
    Write-Host "--- SUCCESS: Qua trinh deploy hoan tat! ---" -ForegroundColor Green
}
