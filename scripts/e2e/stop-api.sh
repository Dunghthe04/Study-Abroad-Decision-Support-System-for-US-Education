#!/usr/bin/env bash
# Dừng đúng tiến trình API mà run-api.sh <cổng> đã chạy (theo PID đã ghi), không đụng tới API khác.
set -euo pipefail
port="${1:?Cần cổng, ví dụ: scripts/e2e/stop-api.sh 5095}"
pid_file="$(cd "$(dirname "$0")" && pwd)/.api-$port.pid"
[[ -f "$pid_file" ]] || { echo "Không có $pid_file"; exit 0; }
pid="$(cat "$pid_file")"
taskkill //PID "$pid" //T //F >/dev/null 2>&1 || kill "$pid" 2>/dev/null || true
rm -f "$pid_file"
echo "Đã dừng API PID $pid (cổng $port)"
