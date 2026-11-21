# Kiểm thử đầu cuối gói dịch vụ

Các script ở đây chạy API tách biệt khỏi môi trường dev: CSDL riêng `usas_e2e`, thư đi vào máy chủ SMTP giả, cổng thanh toán giả.

1. Postgres local đang chạy (container `usas-local-db-1`) và đã có CSDL `usas_e2e`
   (`docker exec usas-local-db-1 psql -U studyabroad -d studyabroad -c "CREATE DATABASE usas_e2e OWNER studyabroad"`,
   rồi `docker exec usas-local-db-1 psql -U studyabroad -d usas_e2e -c 'CREATE EXTENSION IF NOT EXISTS "pgcrypto"; CREATE EXTENSION IF NOT EXISTS "uuid-ossp"; CREATE EXTENSION IF NOT EXISTS vector; CREATE SCHEMA IF NOT EXISTS app; CREATE SCHEMA IF NOT EXISTS advisor;'`).
2. `python scripts/e2e/smtp_sink.py` (cửa sổ riêng).
3. `scripts/e2e/run-api.sh 5095` (cửa sổ riêng). API tự áp migration và seed 5 gói.
4. Chạy script kiểm thử (`python scripts/e2e/billing_e2e.py`).

Dừng API: `scripts/e2e/stop-api.sh 5095` (chỉ dừng tiến trình do run-api.sh chạy ở cổng đó).
Thư mục `scripts/e2e/.mail` và file `.api-*.pid` không đưa vào git.

5. Chat gõ chữ với advisor thật: chạy advisor của nhánh ở 127.0.0.1:8000 với DATABASE_URL trỏ tới usas_e2e (không dùng CSDL trong advisor/.env), rồi `python scripts/e2e/chat_e2e.py`. Script gọi SLM và Claude thật vài lượt.
