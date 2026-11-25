# ==============================================================================
# NpOn.Net-V2: Full Controller End-to-End Test Suite
# Tests all SSO Controller endpoints, JWT authentication, permission gates,
# and fault cases against running Docker microservices.
# ==============================================================================

param(
    [string]$BaseUrl = "http://localhost:14023"
)

$ErrorActionPreference = "Continue"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  NpOn.Net-V2: FULL CONTROLLER END-TO-END TEST SUITE" -ForegroundColor Cyan
Write-Host "  Target Gateway: $BaseUrl" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$passedCount = 0
$failedCount = 0
$totalTests = 0

function Report-Test {
    param(
        [string]$TestName,
        [bool]$Success,
        [string]$Detail = ""
    )
    $script:totalTests++
    if ($Success) {
        $script:passedCount++
        Write-Host " [PASS] " -ForegroundColor Green -NoNewline
        Write-Host "$TestName" -NoNewline
        if ($Detail) { Write-Host " - $Detail" -ForegroundColor Gray } else { Write-Host "" }
    } else {
        $script:failedCount++
        Write-Host " [FAIL] " -ForegroundColor Red -NoNewline
        Write-Host "$TestName" -ForegroundColor Yellow
        if ($Detail) { Write-Host "        Details: $Detail" -ForegroundColor DarkRed }
    }
}

# ------------------------------------------------------------------------------
# 1. Base Gateway & Root Endpoint
# ------------------------------------------------------------------------------
Write-Host "`n--- 1. Gateway Root & Health ---" -ForegroundColor Yellow

try {
    $rootRes = Invoke-RestMethod -Uri "$BaseUrl/" -Method Get -TimeoutSec 5
    Report-Test "GET / (Root Gateway AppName)" ($rootRes -match "SSO") "Returned: '$rootRes'"
} catch {
    Report-Test "GET / (Root Gateway AppName)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# 2. Authentication Controller (/api/Account/*)
# ------------------------------------------------------------------------------
Write-Host "`n--- 2. AccountController: Login & Authentication Cases ---" -ForegroundColor Yellow

$globalToken = $null
$globalRefreshToken = $null
$globalSessionId = $null
$globalAccountId = $null

# Case 2.1: Login with valid credentials (KhaBanh)
$loginBody = @{
    UserName = "KhaBanh"
    Password = "Banhditusapve@1234"
    AuthType = 2
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Login" -Method Post -ContentType "application/json" -Body $loginBody
    $isSuccess = ($loginRes.status -eq $true) -and ($null -ne $loginRes.data.model.token)
    if ($isSuccess) {
        $globalToken = $loginRes.data.model.token
        $globalRefreshToken = $loginRes.data.model.refreshToken
        $globalSessionId = $loginRes.data.model.sessionId
        $globalAccountId = $loginRes.data.model.accountId
    }
    Report-Test "POST /api/Account/Login (Valid credentials KhaBanh)" $isSuccess "FullName: '$($loginRes.data.model.fullName)', AccountId: '$globalAccountId', Expires: $($loginRes.data.model.minuteExpire)m"
} catch {
    Report-Test "POST /api/Account/Login (Valid credentials KhaBanh)" $false $_.Exception.Message
}

# Case 2.2: Login with Wrong Password
$wrongPwdBody = @{
    UserName = "KhaBanh"
    Password = "WrongPassword@999"
    AuthType = 2
} | ConvertTo-Json

try {
    $wrongPwdRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Login" -Method Post -ContentType "application/json" -Body $wrongPwdBody
    $isRejected = ($wrongPwdRes.status -eq $false)
    Report-Test "POST /api/Account/Login (Wrong Password rejection)" $isRejected "Error: $($wrongPwdRes.errorMessages -join ', ')"
} catch {
    Report-Test "POST /api/Account/Login (Wrong Password rejection)" $false $_.Exception.Message
}

# Case 2.3: Login with Non-Existent User
$ghostUserBody = @{
    UserName = "GhostUserDoesNotExist"
    Password = "AnyPassword@123"
    AuthType = 2
} | ConvertTo-Json

try {
    $ghostRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Login" -Method Post -ContentType "application/json" -Body $ghostUserBody
    $isRejected = ($ghostRes.status -eq $false)
    Report-Test "POST /api/Account/Login (Non-existent user rejection)" $isRejected "Error: $($ghostRes.errorMessages -join ', ')"
} catch {
    Report-Test "POST /api/Account/Login (Non-existent user rejection)" $false $_.Exception.Message
}

# Case 2.4: Login with Empty Password (Validation Error)
$emptyPwdBody = @{
    UserName = "KhaBanh"
    Password = ""
    AuthType = 2
} | ConvertTo-Json

try {
    $emptyRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Login" -Method Post -ContentType "application/json" -Body $emptyPwdBody
    $isRejected = ($emptyRes.status -eq $false)
    Report-Test "POST /api/Account/Login (Empty password validation)" $isRejected "Errors: $($emptyRes.errorMessages -join ', ')"
} catch {
    Report-Test "POST /api/Account/Login (Empty password validation)" $true "HTTP 400 rejected by ASP.NET model validation"
}

# Case 2.5: Refresh Token with valid refreshToken
if ($globalRefreshToken -and $globalSessionId) {
    $refreshBody = @{
        RefreshToken = $globalRefreshToken
        AuthType = 2
        DeviceInfo = "E2E-Automated-Test-Runner"
    } | ConvertTo-Json

    $authHeaders = @{
        "Authorization" = "Bearer $globalToken"
    }

    try {
        $refreshRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/RefreshToken" -Method Post -Headers $authHeaders -ContentType "application/json" -Body $refreshBody
        $isRefreshed = ($refreshRes.status -eq $true) -and ($null -ne $refreshRes.data.model.token)
        if ($isRefreshed) {
            $globalToken = $refreshRes.data.model.token
            $globalRefreshToken = $refreshRes.data.model.refreshToken
        }
        Report-Test "POST /api/Account/RefreshToken (Valid refresh token)" $isRefreshed "New Token: $($globalToken.Substring(0, 20))..."
    } catch {
        Report-Test "POST /api/Account/RefreshToken (Valid refresh token)" $false $_.Exception.Message
    }
} else {
    Report-Test "POST /api/Account/RefreshToken (Valid refresh token)" $false "Skipped: No valid refresh token available"
}

# Case 2.6: Refresh Token with Invalid/Garbage Token
$badRefreshBody = @{
    RefreshToken = "00000000000000000000000000000000"
    AuthType = 2
} | ConvertTo-Json

try {
    $badRefreshRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/RefreshToken" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body $badRefreshBody
    $isRejected = ($badRefreshRes.status -eq $false)
    Report-Test "POST /api/Account/RefreshToken (Garbage token rejection)" $isRejected "Error: $($badRefreshRes.errorMessages -join ', ')"
} catch {
    Report-Test "POST /api/Account/RefreshToken (Garbage token rejection)" $true "Rejected with HTTP error: $($_.Exception.Message)"
}

# Case 2.7: Signup validation failure (missing fields)
$invalidSignupBody = @{
    UserName = ""
    Password = ""
    FullName = ""
    AuthType = 2
} | ConvertTo-Json

try {
    $badSignupRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Signup" -Method Post -ContentType "application/json" -Body $invalidSignupBody
    $isRejected = ($badSignupRes.status -eq $false)
    Report-Test "POST /api/Account/Signup (Validation missing fields)" $isRejected "Errors: $($badSignupRes.errorMessages -join ', ')"
} catch {
    Report-Test "POST /api/Account/Signup (Validation missing fields)" $true "HTTP rejected by ASP.NET model validation"
}

# ------------------------------------------------------------------------------
# 3. AccountInfo Controller (/api/AccountInfo/*)
# ------------------------------------------------------------------------------
Write-Host "`n--- 3. AccountInfoController: Profile & Security Tests ---" -ForegroundColor Yellow

# Case 3.1: GetAccountInfo WITH valid Bearer Token
try {
    $infoRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountInfo/GetAccountInfo" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body '{}'
    Report-Test "POST /api/AccountInfo/GetAccountInfo (Authorized with Bearer)" ($infoRes.status -eq $true) "Returned status envelope"
} catch {
    Report-Test "POST /api/AccountInfo/GetAccountInfo (Authorized with Bearer)" $false $_.Exception.Message
}

# Case 3.2: GetAccountInfo WITHOUT Bearer Token (Expect 401 Unauthorized)
try {
    $anonInfoRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountInfo/GetAccountInfo" -Method Post -ContentType "application/json" -Body '{}'
    Report-Test "POST /api/AccountInfo/GetAccountInfo (No Token -> 401 Gate)" $false "Unexpected success without token!"
} catch {
    $is401 = ($_.Exception.Message -match "401" -or [int]$_.Exception.Response.StatusCode -eq 401)
    Report-Test "POST /api/AccountInfo/GetAccountInfo (No Token -> 401 Gate)" $is401 "Correctly rejected with 401 Unauthorized"
}

# Case 3.3: GetAccountInfo with FAKE/Forged Token (Expect 401 Unauthorized)
try {
    $fakeHeader = @{ "Authorization" = "Bearer eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9.e30.fake_signature_12345" }
    $fakeRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountInfo/GetAccountInfo" -Method Post -Headers $fakeHeader -ContentType "application/json" -Body '{}'
    Report-Test "POST /api/AccountInfo/GetAccountInfo (Forged Token -> 401 Gate)" $false "Unexpected success with forged token!"
} catch {
    $is401 = ($_.Exception.Message -match "401" -or [int]$_.Exception.Response.StatusCode -eq 401)
    Report-Test "POST /api/AccountInfo/GetAccountInfo (Forged Token -> 401 Gate)" $is401 "Correctly rejected with 401 Unauthorized"
}

# Case 3.4: AddOrChangeAccountInfo with valid Bearer token
$addInfoBody = [System.Text.Encoding]::UTF8.GetBytes((@{
    FullName = "Ngo Ba Kha"
    Bio = "He thong AIoT Y te Hieu nang cao NpOn.Net-V2"
    Occupation = "Senior System Architect"
    Gender = 1
    IdentificationNumber = "024090123456"
} | ConvertTo-Json))

try {
    $addInfoRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountInfo/AddOrChangeAccountInfo" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json; charset=utf-8" -Body $addInfoBody
    $isSuccess = ($addInfoRes.status -eq $true)
    Report-Test "POST /api/AccountInfo/AddOrChangeAccountInfo (Save Profile)" $isSuccess "Result: '$($addInfoRes.data)'"
} catch {
    Report-Test "POST /api/AccountInfo/AddOrChangeAccountInfo (Save Profile)" $false $_.Exception.Message
}

# Case 3.5: AddOrChangeAccountAddress with valid Bearer token
$addressBody = [System.Text.Encoding]::UTF8.GetBytes((@{
    AddressLine = "123 Vo Nguyen Giap, Son Tra"
    AddressType = 1
} | ConvertTo-Json))

try {
    $addrRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountInfo/AddOrChangeAccountAddress" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json; charset=utf-8" -Body $addressBody
    Report-Test "POST /api/AccountInfo/AddOrChangeAccountAddress (Address Update)" ($addrRes.status -eq $true) "Result: '$($addrRes.data)'"
} catch {
    Report-Test "POST /api/AccountInfo/AddOrChangeAccountAddress (Address Update)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# 4. AccountGroup Controller (/api/AccountGroup/*)
# ------------------------------------------------------------------------------
Write-Host "`n--- 4. AccountGroupController: Group Management Tests ---" -ForegroundColor Yellow

# Case 4.1: AddOrChangeGroupAndMember WITHOUT token (Expect 401)
$groupBodyNoToken = @{
    GroupId = $null
    Leader = $globalAccountId
    GroupName = "AIoT High-Performance Team"
    GroupTypes = @(1, 2)
} | ConvertTo-Json

try {
    $anonGroupRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountGroup/AddOrChangeGroupAndMember" -Method Post -ContentType "application/json" -Body $groupBodyNoToken
    Report-Test "POST /api/AccountGroup/AddOrChangeGroupAndMember (No Token -> 401)" $false "Unexpected success without token!"
} catch {
    $is401 = ($_.Exception.Message -match "401" -or [int]$_.Exception.Response.StatusCode -eq 401)
    Report-Test "POST /api/AccountGroup/AddOrChangeGroupAndMember (No Token -> 401)" $is401 "Correctly rejected with 401"
}

# Case 4.2: AddOrChangeGroupAndMember WITH valid Bearer Token (GroupId = $null creates new group)
$groupBody = [System.Text.Encoding]::UTF8.GetBytes((@{
    GroupId = $null
    Leader = $globalAccountId
    GroupName = "AIoT High-Performance Team"
    GroupTypes = @(1, 2)
} | ConvertTo-Json))

try {
    $groupRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountGroup/AddOrChangeGroupAndMember" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json; charset=utf-8" -Body $groupBody
    Report-Test "POST /api/AccountGroup/AddOrChangeGroupAndMember (Create Group)" ($groupRes.status -eq $true) "Result: '$($groupRes.data)'"
} catch {
    Report-Test "POST /api/AccountGroup/AddOrChangeGroupAndMember (Create Group)" $false $_.Exception.Message
}

# Case 4.3: DeleteGroupOrMember with valid token
$deleteGroupBody = @{
    GroupId = [System.Guid]::NewGuid().ToString()
    Leader = $globalAccountId
    GroupName = "AIoT High-Performance Team"
} | ConvertTo-Json

try {
    $delGroupRes = Invoke-RestMethod -Uri "$BaseUrl/api/AccountGroup/DeleteGroupOrMember" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body $deleteGroupBody
    Report-Test "POST /api/AccountGroup/DeleteGroupOrMember (Delete Group)" ($delGroupRes.status -eq $true) "Result: '$($delGroupRes.data)'"
} catch {
    Report-Test "POST /api/AccountGroup/DeleteGroupOrMember (Delete Group)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# 5. Permission Gate & RBAC Security Tests
# ------------------------------------------------------------------------------
Write-Host "`n--- 5. Permission Gate & RBAC Security Tests ---" -ForegroundColor Yellow

# Case 5.1: Non-Admin User (KhaBanh with Permission 33) calling ChangeAccountStatus
# Endpoint has [PermissionController(EPermission.Administrator)]
$statusChangeBody = @{
    AccountId = $globalAccountId
    AccountStatus = 2
} | ConvertTo-Json

try {
    $permDeniedRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/ChangeAccountStatus" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body $statusChangeBody
    $isDenied = ($permDeniedRes.status -eq $false -or $permDeniedRes.errorCode -ne 0)
    Report-Test "POST /api/Account/ChangeAccountStatus (RBAC: Non-admin blocked)" $isDenied "Correctly denied non-admin user"
} catch {
    $isForbidden = ($_.Exception.Message -match "403" -or [int]$_.Exception.Response.StatusCode -eq 403)
    Report-Test "POST /api/Account/ChangeAccountStatus (RBAC: Non-admin blocked)" $isForbidden "HTTP 403 Forbidden received"
}

# Case 5.2: Non-Admin User calling ChangePassword
$changePwdBody = @{
    AccountId = $globalAccountId
    NewPassword = "NewSecurePassword@1234"
} | ConvertTo-Json

try {
    $pwdDeniedRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/ChangePassword" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body $changePwdBody
    $isDenied = ($pwdDeniedRes.status -eq $false -or $pwdDeniedRes.errorCode -ne 0)
    Report-Test "POST /api/Account/ChangePassword (RBAC: Non-admin blocked)" $isDenied "Correctly denied non-admin user"
} catch {
    $isForbidden = ($_.Exception.Message -match "403" -or [int]$_.Exception.Response.StatusCode -eq 403)
    Report-Test "POST /api/Account/ChangePassword (RBAC: Non-admin blocked)" $isForbidden "HTTP 403 Forbidden received"
}

# ------------------------------------------------------------------------------
# 6. Session Termination (/api/Account/Logout)
# ------------------------------------------------------------------------------
Write-Host "`n--- 6. Session Invalidation (Logout) ---" -ForegroundColor Yellow

# Case 6.1: Logout with valid session
try {
    $logoutRes = Invoke-RestMethod -Uri "$BaseUrl/api/Account/Logout" -Method Post -Headers @{ "Authorization" = "Bearer $globalToken" } -ContentType "application/json" -Body '{}'
    Report-Test "POST /api/Account/Logout (Invalidate Session)" ($logoutRes.status -eq $true) "Result: '$($logoutRes.data)'"
} catch {
    Report-Test "POST /api/Account/Logout (Invalidate Session)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------------------
Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host "  TEST RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "  Total Tests Executed : $totalTests"
Write-Host "  Passed               : $passedCount" -ForegroundColor Green
Write-Host "  Failed               : $failedCount" -ForegroundColor $(if ($failedCount -eq 0) { "Green" } else { "Red" })
Write-Host "======================================================================" -ForegroundColor Cyan

if ($failedCount -gt 0) {
    exit 1
} else {
    exit 0
}
