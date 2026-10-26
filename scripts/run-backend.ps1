# Chạy backend ngoài Docker với biến môi trường lấy từ file .env ở gốc repo (SMTP_*, JWT_SECRET_KEY, SEED_ADMIN_PASSWORD...).
# Biến đã đặt sẵn trong terminal thì giữ nguyên, không bị .env ghi đè.
# Dùng: ./scripts/run-backend.ps1   (thêm -NoWatch để chạy dotnet run thay vì dotnet watch)
param([switch]$NoWatch)

$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root ".env"

if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile -Encoding utf8) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) { continue }
        $idx = $trimmed.IndexOf("=")
        if ($idx -lt 1) { continue }
        $name = $trimmed.Substring(0, $idx).Trim()
        $value = $trimmed.Substring($idx + 1).Trim().Trim('"').Trim("'")
        if ($value -eq "") { continue }
        if (-not [Environment]::GetEnvironmentVariable($name, "Process")) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
} else {
    Write-Warning "Không thấy $envFile - chạy với cấu hình mặc định (mail không được gửi đi)."
}

Set-Location (Join-Path $root "backend")
dotnet tool restore
if ($NoWatch) {
    dotnet run --project src/StudyAbroad.Api
} else {
    dotnet watch --project src/StudyAbroad.Api
}
