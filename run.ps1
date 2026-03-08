#pre setup (mở PowerShell gốc set)
# Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser 
# hoặc không cần set để chạy lệnh .\run.sp1 all
# powershell -ExecutionPolicy Bypass -File .\run.ps1 all



# Danh sách các folder chứa project
$projects = @(
    "MicroServices\Account\Service\NpOn.AccountService",
    "MicroServices/General/Service/NpOn.GeneralService",
    "Controllers/NpOn.SSO"
)

function Build-All {
    Write-Host "--- Dang build SONG SONG (Multi-core) Microservices ---" -ForegroundColor Cyan
    $total = $projects.Count
    $overall_start = Get-Date

    # Tạo các tiến trình chạy ngầm
    $jobs = foreach ($path in $projects) {
        Start-Job -ScriptBlock {
            param($p, $root)
            cd $root
            $start = Get-Date
            
            # Thực hiện build
            $output = dotnet publish $p -t:PublishContainer -c Release --nologo 2>&1
            
            $end = Get-Date
            $elapsed = "{0:N2}" -f ($end - $start).TotalSeconds
            
            # Trả về đối tượng chứa thông tin
            return [PSCustomObject]@{
                Path    = $p
                Elapsed = $elapsed
                Output  = $output
                Success = ($LASTEXITCODE -eq 0)
            }
        } -ArgumentList $path, $pwd.Path
    }

    Write-Host "Dang build $total services... Vui long doi..." -ForegroundColor Gray

    # Đợi tất cả xong và lấy kết quả
    $jobResults = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job

    Write-Host "`n--- THONG KE THOI GIAN BUILD TUNG CON ---" -ForegroundColor Yellow
    $anyError = $false

    foreach ($res in $jobResults) {
        if ($res.Success) {
            Write-Host " [OK] " -NoNewline -ForegroundColor Green
            Write-Host "$($res.Path.PadRight(60)) : $($res.Elapsed) giay"
        } else {
            Write-Host " [FAIL]" -NoNewline -ForegroundColor Red
            Write-Host "$($res.Path.PadRight(60)) : Loi build!"
            Write-Host "   -> Chi tiet: $($res.Output | Select-String "error")" -ForegroundColor Red
            $anyError = $true
        }
    }

    $overall_end = Get-Date
    $overall_duration = "{0:N2}" -f ($overall_end - $overall_start).TotalSeconds
    
    Write-Host "--------------------------------------------------------"
    if ($anyError) {
        Write-Host "--- KET THUC: Co loi xay ra trong qua trinh build! ---" -ForegroundColor Red
        return $false
    } else {
        Write-Host "--- SUCCESS: Tong thoi gian build $total image: $overall_duration giay! ---" -ForegroundColor Green
        return $true
    }
}


# Thực thi
if ($args[0] -eq "up") {
    docker-compose up -d
} 
elseif ($args[0] -eq "down") {
    # Kiểm tra xem có tham số -v (xóa volume) không
    if ($args[1] -eq "-v") {
        Write-Host "--- Dang dung va XOA luon Volume (Data) ---" -ForegroundColor Red
        docker-compose down -v
    } else {
        Write-Host "--- Dang dung cac Container ---" -ForegroundColor Yellow
        docker-compose down
    }
}
elseif ($args[0] -eq "all") {
    $success = Build-All
    if ($success) { 
        Write-Host "--- Dang khoi chay he thong ---" -ForegroundColor Cyan
        docker-compose up -d 
    }
} 
elseif ($args[0] -eq "only") {
    $target = $args[1]
    if (-not $target) { 
        Write-Host "Loi: Vui long nhap ten service (vd: only cms)" -ForegroundColor Red
        return 
    }

    $matchedProject = $projects | Where-Object { $_ -match $target }
    
    if ($matchedProject) {
        Write-Host "--- Dang build RIENG: $matchedProject ---" -ForegroundColor Cyan
        $start = Get-Date
        dotnet publish $matchedProject -t:PublishContainer -c Release --nologo
        $elapsed = "{0:N2}" -f ((Get-Date) - $start).TotalSeconds
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "--- SUCCESS: Build xong trong $elapsed giay! ---" -ForegroundColor Green
            # Tu dong tim ten service tuong ung trong docker-compose
            # Gia su ten service trong compose trung voi chuoi tim kiem (vd: sso, cms)
            docker-compose up -d $target
        }
    } else {
        Write-Host "Khong tim thay service nao khop voi: $target" -ForegroundColor Red
    }
} 
else {
    Write-Host "`n--- MENU HUONG DAN ---" -ForegroundColor Yellow
    Write-Host " .\run.ps1 all      : Build tat ca & Up"
    Write-Host " .\run.ps1 up       : Chi Up (khong build)"
    Write-Host " .\run.ps1 only cms : Build & Up rieng con CMS"
    Write-Host " .\run.ps1 down     : Dung va xoa Container"
    Write-Host " .\run.ps1 down -v  : Dung va xoa sach Container + Volume (Data)"
    Write-Host "----------------------`n"
}
