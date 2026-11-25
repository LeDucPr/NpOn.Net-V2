# ==============================================================================
# NpOn.SSO: Script Kiểm Thử Các Đầu API Của Controller
# Quét qua Account, Signup, Login, AccountInfo, v.v.
# ==============================================================================

$baseUrl = "http://localhost:14023"
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  NPON.SSO API ENDPOINT AUTOMATION TEST" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Test POST /api/Account/Login với thông tin hợp lệ
$loginPayload = @{
    UserName = "admin"
    Password = "Password123"
    AuthType = 1
} | ConvertTo-Json

Write-Host "`n[TEST 1] POST /api/Account/Login" -ForegroundColor Yellow
try {
    $res1 = Invoke-RestMethod -Uri "$baseUrl/api/Account/Login" -Method Post -Body $loginPayload -ContentType "application/json"
    Write-Host "Response Status: $($res1.status) | ErrorCode: $($res1.errorCode) | Messages: $($res1.errorMessages -join ',')" -ForegroundColor Green
} catch {
    Write-Host "HTTP Error: $_" -ForegroundColor Red
}

# 2. Test POST /api/Account/Signup (Đăng ký tài khoản)
$signupPayload = @{
    UserName = "agent_user_$(Get-Random -Minimum 1000 -Maximum 9999)"
    Password = "SecurePassword123"
    FullName = "AI Agent Automation"
    AuthType = 1
    Email = "test_agent@npon.vn"
} | ConvertTo-Json

Write-Host "`n[TEST 2] POST /api/Account/Signup" -ForegroundColor Yellow
try {
    $res2 = Invoke-RestMethod -Uri "$baseUrl/api/Account/Signup" -Method Post -Body $signupPayload -ContentType "application/json"
    Write-Host "Response Status: $($res2.status) | ErrorCode: $($res2.errorCode) | Messages: $($res2.errorMessages -join ',')" -ForegroundColor Green
} catch {
    Write-Host "HTTP Error: $_" -ForegroundColor Red
}

# 3. Test POST /api/AccountInfo/GetAccountInfo (Cần JWT token -> Ktra chặn quyền Unauthorized)
Write-Host "`n[TEST 3] POST /api/AccountInfo/GetAccountInfo (Kiem tra Bearer Guard)" -ForegroundColor Yellow
try {
    $res3 = Invoke-RestMethod -Uri "$baseUrl/api/AccountInfo/GetAccountInfo" -Method Post -ContentType "application/json"
    Write-Host "Unexpected success without token!" -ForegroundColor Red
} catch {
    Write-Host "Expected Unauthorized Guard: $($_.Exception.Message)" -ForegroundColor Green
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  HOAN TAT QUET API CONTROLLER SSO" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

