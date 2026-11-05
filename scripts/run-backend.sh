#!/usr/bin/env bash
# Chạy backend ngoài Docker với biến môi trường lấy từ file .env ở gốc repo (SMTP_*, JWT_SECRET_KEY, SEED_ADMIN_PASSWORD...).
# Biến đã đặt sẵn trong terminal thì giữ nguyên, không bị .env ghi đè.
# Dùng: ./scripts/run-backend.sh   (thêm --no-watch để chạy dotnet run thay vì dotnet watch)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
env_file="$root/.env"

if [[ -f "$env_file" ]]; then
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "${line// }" || "$line" =~ ^[[:space:]]*# ]] && continue
    [[ "$line" != *=* ]] && continue
    name="$(echo "${line%%=*}" | xargs)"
    value="$(echo "${line#*=}" | xargs)"
    [[ -z "$value" ]] && continue
    if [[ -z "${!name:-}" ]]; then export "$name=$value"; fi
  done < "$env_file"
else
  echo "Cảnh báo: không thấy $env_file - chạy với cấu hình mặc định (mail không được gửi đi)." >&2
fi

cd "$root/backend"
dotnet tool restore
if [[ "${1:-}" == "--no-watch" ]]; then
  dotnet run --project src/StudyAbroad.Api
else
  dotnet watch --project src/StudyAbroad.Api
fi
