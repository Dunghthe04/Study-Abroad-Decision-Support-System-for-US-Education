#!/usr/bin/env bash
# Chạy API cho chạy thử và kiểm thử đầu cuối, tách biệt khỏi môi trường dev:
#   - CSDL riêng usas_e2e (cùng máy chủ Postgres local), không đọc .env nên không bao giờ trỏ tới Neon
#   - mọi thư đi tới smtp_sink.py (127.0.0.1:2525), không gửi qua Gmail
#   - cổng thanh toán giả (PayOs:UseFake), job thanh toán chạy mỗi 5 giây
# Dùng: scripts/e2e/run-api.sh <cổng>      (từ thư mục gốc của worktree, ví dụ 5095)
# PID Windows của tiến trình API được ghi vào scripts/e2e/.api-<cổng>.pid; dừng bằng scripts/e2e/stop-api.sh <cổng>.
set -euo pipefail

port="${1:?Cần cổng, ví dụ: scripts/e2e/run-api.sh 5095}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
api="$root/backend/src/StudyAbroad.Api"
base_conn="$(grep -o '"Default": *"[^"]*"' "$api/appsettings.json" | head -1 | sed -E 's/"Default": *"([^"]*)"/\1/')"
e2e_db="${E2E_DB:-usas_e2e}"

# Không kế thừa chuỗi kết nối, SMTP hay khóa thanh toán thật từ terminal
unset ConnectionStrings__Default SMTP_USER SMTP_PASSWORD PAYOS_CLIENT_ID PAYOS_API_KEY PAYOS_CHECKSUM_KEY || true

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://127.0.0.1:$port"
export ConnectionStrings__Default="$(echo "$base_conn" | sed -E "s/Database=[^;]*/Database=$e2e_db/")"
export Database__MigrateOnStartup=true
export SMTP_HOST=127.0.0.1 SMTP_PORT=2525 Smtp__EnableSsl=false
export SMTP_FROM_EMAIL=no-reply@usas.local SMTP_FROM_NAME="USAS E2E"
export APP_PUBLIC_URL="${E2E_WEB_URL:-http://localhost:3100}"
export Cors__AllowedOrigins__0="${E2E_WEB_URL:-http://localhost:3100}"
export PayOs__UseFake=true PAYOS_USE_FAKE=true PayOs__ChecksumKey=dev-checksum-key
export PayOs__ReturnUrlBase="${E2E_WEB_URL:-http://localhost:3100}"
export Billing__JobIntervalSeconds="${E2E_JOB_SECONDS:-5}"
export Billing__OrderExpiryMinutes="${E2E_ORDER_MINUTES:-30}"
export Advisor__BaseUrl="${E2E_ADVISOR_URL:-http://127.0.0.1:8000}"

cd "$root/backend"
dotnet build src/StudyAbroad.Api -v q -nologo
cd "$api"
dotnet bin/Debug/net8.0/StudyAbroad.Api.dll &
pid=$!
winpid="$(cat /proc/$pid/winpid 2>/dev/null || echo "$pid")"
echo "$winpid" > "$root/scripts/e2e/.api-$port.pid"
echo "API PID $winpid on http://127.0.0.1:$port (db $e2e_db)"
wait "$pid"
