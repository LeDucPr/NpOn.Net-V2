# ==============================================================================
# Script Khoi Dong Lai Cum ScyllaDB/Cassandra An Toan (Khong Mat Du Lieu)
# Usage:
#   .\restart-cluster-safely.ps1
# ==============================================================================

Write-Host "--- Dang khoi dong lai cum ScyllaDB/Cassandra (Preserving Data Volumes) ---" -ForegroundColor Cyan

$dockerDir = Split-Path -Parent $PSScriptRoot

# 1. Dung cac container hien tai an toan
Write-Host " [1/3] Stopping existing Scylla containers..." -ForegroundColor Yellow
docker compose -f "$dockerDir\compose.yaml" stop

# 2. Khoi dong Seed Node truoc (scylla-node1)
Write-Host " [2/3] Starting Seed Node (scylla-node1 at 172.28.0.11)..." -ForegroundColor Yellow
docker compose -f "$dockerDir\compose.yaml" up -d scylla-node1

Write-Host " Dang cho Seed Node hoan tat khoi tao gossip (20s)..." -ForegroundColor Gray
Start-Sleep -Seconds 20

# 3. Khoi dong cac node con lai (node2, node3)
Write-Host " [3/3] Starting node2 & node3..." -ForegroundColor Yellow
docker compose -f "$dockerDir\compose.yaml" up -d scylla-node2 scylla-node3

Write-Host " [PASS] Cum da duoc khoi dong lai voi IP tinh co dinh! Kiem tra nodetool status..." -ForegroundColor Green
docker exec scylla-node1 nodetool status

