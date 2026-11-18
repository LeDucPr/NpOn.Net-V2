# ==============================================================================
# Script thiet lap Git Hooks cho Developer NpOn.Net-V2
# Usage:
#   .\setup-hooks.ps1
# ==============================================================================

Write-Host "`n--- Dang thiet lap Git Hooks cho NpOn.Net-V2 ---" -ForegroundColor Cyan

# Tro duong dan hooks cua Git vao thu muc .githooks
git config core.hooksPath .githooks

if ($LASTEXITCODE -eq 0) {
    Write-Host " [PASS] Da kich hoat thanh cong Git Hooks tai '.githooks'!" -ForegroundColor Green
    Write-Host " [INFO] Tu bay gio, moi lan chay 'git push', he thong se tu dong:" -ForegroundColor Yellow
    Write-Host "        1. Build thu nghiem toan bo Solution NpOnV2.sln (Release)" -ForegroundColor White
    Write-Host "        2. Chay toan bo Unit Tests & Dynamic IL Emit" -ForegroundColor White
    Write-Host "        Neu co bat ky loi nao, git push se tu dong bi CHAN de bao ve branch!`n" -ForegroundColor Yellow
} else {
    Write-Host " [FAIL] Khong the cau hinh Git hooksPath. Vui long kiem tra lai Git!" -ForegroundColor Red
}

