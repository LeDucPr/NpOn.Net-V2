#pre setup (mở PowerShell gốc set)
# nerdctl -n k8s.io images
# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser 
# hoặc không cần set để chạy lệnh .\runk8.ps1 build up
# powershell -ExecutionPolicy Bypass -File .\runk8.ps1 build up

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
# 2. HÀM BUILD 2 GIAI ĐOẠN TỐI ƯU
# ==========================================
function Build-Targets($list) {
    Write-Host "`n--- [GIAI DOAN 1] BIEN DICH CODE (Dung chung Cache) ---" -ForegroundColor Cyan
    $overall_start = Get-Date

    foreach ($p in $list) {
        Write-Host "Dang bien dich: $p ..." -ForegroundColor DarkGray
        # Biên dịch thuần túy để tạo file DLL. 
        # Thằng đầu tiên sẽ build các project Share, các thằng sau bỏ qua nhờ cache MSBuild
        dotnet build $p -c Release --nologo
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "--- KET THUC: Loi bien dich tai project $p! ---" -ForegroundColor Red
            return $false
        }
    }

    Write-Host "`n--- [GIAI DOAN 2] DONG GOI IMAGE VA NAP VAO K8S (Song song) ---" -ForegroundColor Cyan
    # Tạo các tiến trình chạy ngầm
    $jobs = foreach ($path in $list) {
        Start-Job -ScriptBlock {
            param($p, $root)
            cd $root
            $start = Get-Date
            
            # Đặt tên file tar tạm thời dựa trên tên thư mục
            $projectName = Split-Path $p -Leaf
            $tarFile = "$root\$projectName.tar.gz"
            
            # 1. Đóng gói ra file tar (Dùng cờ --no-build để không biên dịch lại)
            $output = dotnet publish $p -t:PublishContainer -c Release --no-build --nologo -p:ContainerArchiveOutputPath=$tarFile 2>&1
            
            # 2. Nạp thẳng vào K8s namespace
            if ($LASTEXITCODE -eq 0 -and (Test-Path $tarFile)) {
                # Dùng cmd để bơm pipe thẳng file vào nerdctl
                $loadOutput = cmd.exe /c "nerdctl -n k8s.io load < ""$tarFile""" 2>&1
                $output += "`n[Rancher] Đã nạp image: $loadOutput"
                Remove-Item $tarFile -Force # Dọn rác
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

    Write-Host "Dang dong goi $($list.Count) services... Vui long doi..." -ForegroundColor Gray

    $jobResults = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job

    Write-Host "`n--- THONG KE THOI GIAN BUILD & LOAD ---" -ForegroundColor Yellow
    $anyError = $false

    foreach ($res in $jobResults) {
        if ($res.Success) {
            Write-Host " [OK] " -NoNewline -ForegroundColor Green
            Write-Host "$($res.Path.PadRight(60)) : $($res.Elapsed) giay"
        } else {
            Write-Host " [FAIL]" -NoNewline -ForegroundColor Red
            Write-Host "$($res.Path.PadRight(60)) : Loi dong goi!"
            Write-Host "   -> Chi tiet lỗi (Full Log):" -ForegroundColor Red
            Write-Host ($res.Output | Out-String) -ForegroundColor DarkRed
            $anyError = $true
        }
    }

    $overall_end = Get-Date
    $overall_duration = "{0:N2}" -f ($overall_end - $overall_start).TotalSeconds
    
    Write-Host "--------------------------------------------------------"
    if ($anyError) {
        Write-Host "--- KET THUC: Co loi xay ra trong qua trinh! ---" -ForegroundColor Red
        return $false
    } else {
        Write-Host "--- SUCCESS: Hoan thanh $($list.Count) image trong: $overall_duration giay! ---" -ForegroundColor Green
        return $true
    }
}

# ==========================================
# 3. ĐIỀU HƯỚNG THỰC THI (KUBERNETES)
# ==========================================
if ($isDown) {
    Write-Host "--- Dang go bo he thong khoi K8s ---" -ForegroundColor Yellow
    kubectl delete -f $k8sFile
    exit
}
if ($isStatus) {
    Write-Host "--- Kiem tra trang thai cac Pods & Services ---" -ForegroundColor Cyan
    kubectl get pods -o wide
    Write-Host ""
    kubectl get svc
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
    kubectl apply -f $k8sFile

    Write-Host "`n--- [UP] Ép K8s chay lai (Restart) voi Image moi ---" -ForegroundColor Cyan
    foreach ($p in $targetProjects) {
        $deployName = Get-DeployName $p
        Write-Host "Restarting Deployment: $deployName ..." -ForegroundColor Gray
        kubectl rollout restart deployment $deployName
    }
    Write-Host "--- SUCCESS: Qua trinh deploy hoan tat! ---" -ForegroundColor Green
}