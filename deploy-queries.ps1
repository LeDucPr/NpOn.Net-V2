<#
.SYNOPSIS
    1-Command Atomic Zero-Downtime Query Deployment CLI for NpOn.GeneralService.
.DESCRIPTION
    Parses query definitions from PgSQLTable/general.sql (tblmaster + fld_query_master) or a custom JSON file,
    executes a single transactional BulkSyncQueries gRPC call to GeneralService (PostgreSQL MERGE + replace fields),
    triggers atomic write-through L2 cache swap, and publishes 1 Redis batch event to sync consumer L1 caches.
.EXAMPLE
    .\deploy-queries.ps1
    .\deploy-queries.ps1 -Codes account_get_by_id,account_exception_controller_delete_old_version_by_host_code
    .\deploy-queries.ps1 -Endpoint http://localhost:40000 -FullReload
#>
param(
    [string]$Endpoint = "http://localhost:40000",
    [string]$SqlPath = (Join-Path $PSScriptRoot "PgSQLTable\general.sql"),
    [string]$JsonPath = "",
    [string[]]$Codes = @(),
    [string]$DeployVersion = "",
    [string]$SecretKey = "NpOn-InterService-Secret-Key-2024-Change-In-Production",
    [switch]$FullReload = $false
)

$ErrorActionPreference = "Stop"

function Unescape-PgCopyValue([string]$raw) {
    if ($raw -eq "\N") { return $null }
    $sb = [System.Text.StringBuilder]::new($raw.Length)
    for ($i = 0; $i -lt $raw.Length; $i++) {
        $ch = $raw[$i]
        if ($ch -eq '\' -and ($i + 1) -lt $raw.Length) {
            $next = $raw[$i + 1]
            switch ($next) {
                'n' { [void]$sb.Append("`n"); $i++ }
                'r' { [void]$sb.Append("`r"); $i++ }
                't' { [void]$sb.Append("`t"); $i++ }
                '\' { [void]$sb.Append('\'); $i++ }
                default { [void]$sb.Append($next); $i++ }
            }
        } else {
            [void]$sb.Append($ch)
        }
    }
    return $sb.ToString()
}

function Parse-QueriesFromSql([string]$filePath, [string[]]$filterCodes) {
    if (-not (Test-Path $filePath)) {
        throw "SQL file not found: $filePath"
    }

    $lines = Get-Content -Path $filePath -Encoding UTF8
    $tblRows = @{}
    $fldByTbl = @{}
    $mode = ""

    foreach ($line in $lines) {
        if ($line -match '^COPY public\.tblmaster\s*\(') {
            $mode = "tblmaster"
            continue
        }
        if ($line -match '^COPY public\.fld_query_master\s*\(') {
            $mode = "fld_query_master"
            continue
        }
        if ($mode -ne "" -and $line -eq '\.') {
            $mode = ""
            continue
        }

        if ($mode -eq "tblmaster" -and -not [string]::IsNullOrEmpty($line)) {
            $cols = $line -split "`t"
            if ($cols.Count -ge 9) {
                $id = Unescape-PgCopyValue $cols[0]
                $code = Unescape-PgCopyValue $cols[1]
                $desc = Unescape-PgCopyValue $cols[2]
                $execFunc = Unescape-PgCopyValue $cols[3]
                $query = Unescape-PgCopyValue $cols[4]
                $execTypeStr = Unescape-PgCopyValue $cols[5]
                $serviceName = Unescape-PgCopyValue $cols[7]
                $dbTypeStr = Unescape-PgCopyValue $cols[8]

                if ($filterCodes.Count -gt 0 -and ($filterCodes -notcontains $code)) {
                    continue
                }

                $tblRows[$id] = @{
                    id = $id
                    code = $code
                    description = $desc
                    exec_func = $execFunc
                    query = $query
                    exec_type = [int]$execTypeStr
                    service_name = $serviceName
                    db_type = if ($null -ne $dbTypeStr) { [int]$dbTypeStr } else { $null }
                    fields = [System.Collections.Generic.List[object]]::new()
                }
            }
        }
        elseif ($mode -eq "fld_query_master" -and -not [string]::IsNullOrEmpty($line)) {
            $cols = $line -split "`t"
            if ($cols.Count -ge 8) {
                $id = Unescape-PgCopyValue $cols[0]
                $desc = Unescape-PgCopyValue $cols[1]
                $tblId = Unescape-PgCopyValue $cols[2]
                $fieldName = Unescape-PgCopyValue $cols[3]
                $fieldTypeStr = Unescape-PgCopyValue $cols[4]
                $fieldTypeString = Unescape-PgCopyValue $cols[6]
                $orderSortStr = Unescape-PgCopyValue $cols[7]

                if (-not $fldByTbl.ContainsKey($tblId)) {
                    $fldByTbl[$tblId] = [System.Collections.Generic.List[object]]::new()
                }
                $fldByTbl[$tblId].Add(@{
                    id = $id
                    description = $desc
                    field_name = $fieldName
                    field_type = if ($null -ne $fieldTypeStr) { [int]$fieldTypeStr } else { $null }
                    field_type_string = $fieldTypeString
                    order_sort = if ($null -ne $orderSortStr) { [int]$orderSortStr } else { 0 }
                })
            }
        }
    }

    $queries = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $tblRows.Values) {
        $tid = $entry.id
        if ($fldByTbl.ContainsKey($tid)) {
            $entry.fields.AddRange($fldByTbl[$tid])
        }
        $queries.Add($entry)
    }

    return ,$queries.ToArray()
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
if ([string]::IsNullOrWhiteSpace($DeployVersion)) {
    $DeployVersion = "v-" + (Get-Date -Format "yyyyMMddHHmmss") + "-" + ([guid]::NewGuid().ToString("N").Substring(0, 6))
}

$payloadObj = $null
if (-not [string]::IsNullOrWhiteSpace($JsonPath)) {
    if (-not (Test-Path $JsonPath)) { throw "JSON file not found: $JsonPath" }
    $raw = Get-Content -Path $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $qList = if ($raw.queries) { @($raw.queries) } else { @($raw) }
    if ($Codes.Count -gt 0) {
        $qList = @($qList | Where-Object { $Codes -contains $_.code })
    }
    $payloadObj = @{
        deploy_version = $DeployVersion
        is_full_reload = [bool]$FullReload
        queries = $qList
    }
}
else {
    $parsedQueries = Parse-QueriesFromSql -filePath $SqlPath -filterCodes $Codes
    $payloadObj = @{
        deploy_version = $DeployVersion
        is_full_reload = [bool]$FullReload
        queries = $parsedQueries
    }
}

if (-not $payloadObj.queries -or $payloadObj.queries.Count -eq 0) {
    throw "No queries matched for deployment."
}

# Materialize lightweight cached .NET 10 gRPC deployer in ignored artifacts/query-deployer
$deployerDir = Join-Path $PSScriptRoot "artifacts\query-deployer"
$deployerCsproj = Join-Path $deployerDir "NpOn.QueryDeployer.csproj"
$deployerProgram = Join-Path $deployerDir "Program.cs"
$deployerBin = Join-Path $deployerDir "bin\Release\net10.0\NpOn.QueryDeployer.dll"

if (-not (Test-Path $deployerDir)) {
    New-Item -ItemType Directory -Path $deployerDir -Force | Out-Null
}

$csprojXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="NpOn.General.Proto" Version="2.0.0" />
    <PackageReference Include="NpOn.CommonHeaderConfig" Version="2.0.0" />
  </ItemGroup>
</Project>
'@

$programCs = @'
using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Extensions.NpOn.HeaderConfig;
using Grpc.Core;
using Grpc.Net.Client;
using NpOn.Protos.General.V1;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: NpOn.QueryDeployer <endpoint> <secretKey> <payloadJsonPath>");
    return 2;
}

string endpoint = args[0];
string secretKey = args[1];
string payloadPath = args[2];

var jsonText = await File.ReadAllTextAsync(payloadPath);
var dto = JsonSerializer.Deserialize<DeployPayloadDto>(jsonText)
          ?? throw new InvalidOperationException("Invalid query deploy JSON payload.");

var request = new BulkSyncQueriesCommand
{
    DeployVersion = dto.DeployVersion ?? string.Empty,
    IsFullReload = dto.IsFullReload
};

foreach (var q in dto.Queries)
{
    var item = new TblMasterSyncItemCommand
    {
        Code = q.Code,
        ExecType = q.ExecType
    };
    if (!string.IsNullOrWhiteSpace(q.Id)) item.Id = q.Id;
    if (q.Description != null) item.Description = q.Description;
    if (q.ExecFunc != null) item.ExecFunc = q.ExecFunc;
    if (q.Query != null) item.Query = q.Query;
    if (q.ServiceName != null) item.ServiceName = q.ServiceName;
    if (q.DbType.HasValue) item.DbType = q.DbType.Value;

    if (q.Fields != null)
    {
        foreach (var f in q.Fields)
        {
            var fld = new FldQueryMasterSyncCommand
            {
                FieldName = f.FieldName,
                OrderSort = f.OrderSort
            };
            if (!string.IsNullOrWhiteSpace(f.Id)) fld.Id = f.Id;
            if (f.FieldType.HasValue) fld.FieldType = f.FieldType.Value;
            if (f.FieldTypeString != null) fld.FieldTypeString = f.FieldTypeString;
            if (f.Description != null) fld.Description = f.Description;
            item.Fields.Add(fld);
        }
    }

    request.Queries.Add(item);
}

const string serviceName = "QueryDeployCli";
var tokenService = new InterServiceTokenService(secretKey, TimeSpan.FromMinutes(15));
string token = tokenService.GenerateToken(serviceName);
var headers = new Metadata
{
    { DefaultHeaderConstant.GrpcInterServiceAuthToken, token },
    { DefaultHeaderConstant.GrpcInterServiceServiceName, serviceName }
};

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
using var channel = GrpcChannel.ForAddress(endpoint);
var client = new FldMasterProtoService.FldMasterProtoServiceClient(channel);
var reply = await client.BulkSyncQueriesAsync(request, headers);

Console.WriteLine(JsonSerializer.Serialize(new
{
    reply.Status,
    reply.SyncedCount,
    reply.DeployVersion,
    reply.BroadcastReceivers,
    reply.ErrorMessages
}));

return reply.Status ? 0 : 1;

file sealed class DeployPayloadDto
{
    [JsonPropertyName("deploy_version")] public string? DeployVersion { get; set; }
    [JsonPropertyName("is_full_reload")] public bool IsFullReload { get; set; }
    [JsonPropertyName("queries")] public List<QueryDto> Queries { get; set; } = [];
}

file sealed class QueryDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("code")] public required string Code { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("exec_func")] public string? ExecFunc { get; set; }
    [JsonPropertyName("query")] public string? Query { get; set; }
    [JsonPropertyName("exec_type")] public int ExecType { get; set; }
    [JsonPropertyName("service_name")] public string? ServiceName { get; set; }
    [JsonPropertyName("db_type")] public int? DbType { get; set; }
    [JsonPropertyName("fields")] public List<FieldDto>? Fields { get; set; }
}

file sealed class FieldDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("field_name")] public required string FieldName { get; set; }
    [JsonPropertyName("field_type")] public int? FieldType { get; set; }
    [JsonPropertyName("field_type_string")] public string? FieldTypeString { get; set; }
    [JsonPropertyName("order_sort")] public int OrderSort { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}
'@

$needBuild = -not (Test-Path $deployerBin)
if (-not (Test-Path $deployerCsproj) -or ((Get-Content $deployerCsproj -Raw) -ne $csprojXml)) {
    Set-Content -Path $deployerCsproj -Value $csprojXml -Encoding UTF8
    $needBuild = $true
}
if (-not (Test-Path $deployerProgram) -or ((Get-Content $deployerProgram -Raw) -ne $programCs)) {
    Set-Content -Path $deployerProgram -Value $programCs -Encoding UTF8
    $needBuild = $true
}

if ($needBuild) {
    Write-Host "[QUERY DEPLOY] Building cached gRPC deployer CLI..." -ForegroundColor Cyan
    dotnet build $deployerCsproj -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build NpOn.QueryDeployer." }
}

$tmpPayload = Join-Path $deployerDir "last_payload.json"
$payloadObj | ConvertTo-Json -Depth 10 | Set-Content -Path $tmpPayload -Encoding UTF8

Write-Host "[QUERY DEPLOY] Pushing $($payloadObj.queries.Count) query definition(s) to $Endpoint (DeployVersion=$DeployVersion)..." -ForegroundColor Cyan
$resultJson = dotnet $deployerBin $Endpoint $SecretKey $tmpPayload
$exitCode = $LASTEXITCODE
$sw.Stop()

if ($exitCode -ne 0) {
    Write-Host "[QUERY DEPLOY] FAILED ($($sw.ElapsedMilliseconds) ms): $resultJson" -ForegroundColor Red
    exit $exitCode
}

Write-Host "[QUERY DEPLOY] SUCCESS ($($sw.ElapsedMilliseconds) ms): $resultJson" -ForegroundColor Green
