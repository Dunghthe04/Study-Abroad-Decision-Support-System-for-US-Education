# Bootstrap DB local full: EF (schema app) + SQL bo sung (bang/seed backlog).
# CANH BAO: docker compose down -v XOA TOAN BO du lieu Postgres local.
#
# Sau bootstrap, hang ngay:
#   docker compose up -d db
#   dotnet ef database update -p src\StudyAbroad.Infrastructure -s src\StudyAbroad.Api
#
# Chay tu thu muc goc repo:
#   powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-full-database.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $RepoRoot

$ComposeBootstrap = Join-Path $RepoRoot "docker-compose.ef-bootstrap.yml"
$InitDir = Join-Path $RepoRoot "database\init"
$BackendApi = Join-Path $RepoRoot "backend\src\StudyAbroad.Api"
$BackendInfra = Join-Path $RepoRoot "backend\src\StudyAbroad.Infrastructure"

function Invoke-SqlFile {
    param([string]$RelativePath)
    $FullPath = Join-Path $InitDir $RelativePath
    if (-not (Test-Path $FullPath)) {
        throw "Khong tim thay file SQL: $FullPath"
    }
    Write-Host ">> SQL: $RelativePath ..."
    Get-Content -Path $FullPath -Raw -Encoding UTF8 | docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "SQL that bai: $RelativePath" }
}

Write-Host "=== USAS: Bootstrap DB FULL (EF + SQL bo sung) ===" -ForegroundColor Cyan
Write-Host "Se xoa volume pgdata local. Nhan Ctrl+C trong 5 giay de huy..."
Start-Sleep -Seconds 5

Write-Host ">> Dung API/dotnet dang giu file DLL (neu co)..."
Get-Process -Name "StudyAbroad.Api" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host ">> Reset Postgres (chi volume, khong chay init Docker)..."
docker compose -f $ComposeBootstrap down -v
docker compose -f $ComposeBootstrap up -d db

Write-Host ">> Doi Postgres healthy..."
$deadline = (Get-Date).AddMinutes(2)
$status = $null
do {
    $status = docker compose -f $ComposeBootstrap ps db --format json 2>$null | ConvertFrom-Json
    if ($status.Health -eq "healthy") { break }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)
if ($null -eq $status -or $status.Health -ne "healthy") {
    throw "Postgres chua healthy - kiem tra Docker Desktop."
}

Write-Host ">> Buoc 1/6: EF migrations (schema app + migration seed truong)..."
Push-Location (Join-Path $RepoRoot "backend")
dotnet ef database update -p $BackendInfra -s $BackendApi
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "dotnet ef database update that bai" }
Pop-Location

Write-Host ">> Buoc 2/6: Extension + schema advisor (pgvector)..."
Invoke-SqlFile "01-extensions.sql"

Write-Host ">> Buoc 3/6: Can cot EF thieu so voi SQL blueprint..."
Invoke-SqlFile "02a-ef-gap-align.sql"

Write-Host ">> Buoc 4/6: Bo sung bang/seed cot loi (CREATE IF NOT EXISTS + ON CONFLICT)..."
Invoke-SqlFile "02-full-schema-base.sql"

Write-Host ">> Buoc 5/6: Du lieu Excel - co the vai phut..."
Invoke-SqlFile "03-base-data-from-excel.sql"

Write-Host ">> Buoc 6/6: Patch cot UC-83/90 va luat bo sung..."
Invoke-SqlFile "03-patch-rules-and-provenance.sql"

Write-Host ">> Buoc tuy chon: DbSeeder (profile.metadata) - chay API Development mot lan roi Ctrl+C:"
Write-Host "   dotnet run --project backend\src\StudyAbroad.Api" -ForegroundColor Yellow

Write-Host ""
Write-Host "=== Xong. Kiem tra nhanh ===" -ForegroundColor Green
docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -c "SELECT COUNT(*) AS so_bang_app FROM information_schema.tables WHERE table_schema='app' AND table_type='BASE TABLE';"
docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -c "SELECT key FROM app.app_settings ORDER BY key;"
docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -c "SELECT COUNT(*) AS expert_rules FROM app.expert_rules;"
docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -c "SELECT COUNT(*) AS universities FROM app.universities;"
docker compose -f $ComposeBootstrap exec -T db psql -U studyabroad -d studyabroad -c "SELECT COUNT(*) AS scholarships FROM app.scholarships;"

Write-Host ""
Write-Host "Dev hang ngay: docker compose up -d db + dotnet ef database update" -ForegroundColor Cyan
Write-Host "Volume pgdata dung chung project name usas."
