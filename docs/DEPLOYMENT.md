# Deploy lên VPS

Toàn bộ hệ thống chạy trên **một VPS** bằng `docker-compose.prod.yml`. Chỉ Nginx mở cổng 80/443.

## 1. Chuẩn bị

**VPS**: Ubuntu 22.04/24.04.
- Không có GPU: tối thiểu 4 vCPU / **16 GB RAM** / 60 GB SSD. `qwen3:14b` bản quantized cần khoảng 9–10 GB RAM; chạy CPU sẽ chậm (vài chục giây mỗi câu trả lời).
- Nếu VPS yếu hơn, đặt `LLM_MODEL=qwen3:8b` (khoảng 6 GB RAM).
- Có GPU NVIDIA: cài NVIDIA Container Toolkit và bỏ comment khối `deploy.resources` của service `ollama`.

**Tên miền**: tạo bản ghi DNS `A` cho `example.com` và `www.example.com` trỏ về IP của VPS.

## 2. Cài Docker và lấy code

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER && newgrp docker

sudo mkdir -p /opt/usas && sudo chown $USER /opt/usas
git clone <repo-url> /opt/usas && cd /opt/usas
```

## 3. Cấu hình

```bash
cp .env.example .env
nano .env
```

| Biến | Giá trị |
|---|---|
| `POSTGRES_PASSWORD` | mật khẩu mạnh (`openssl rand -hex 24`) |
| `ADVISOR_API_KEY` | `openssl rand -hex 32` |
| `LLM_MODEL` | `qwen3:14b` (hoặc `qwen3:8b`) |
| `DOMAIN`, `CERTBOT_EMAIL` | tên miền và email nhận cảnh báo SSL |
| `SWAGGER_ENABLED` | `true` nếu muốn mở Swagger khi bảo vệ đồ án |

## 4. Firewall

```bash
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw enable
```

## 5. Chạy lần đầu

```bash
./scripts/init-letsencrypt.sh                                        # cấp SSL + khởi động Nginx
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker compose -f docker-compose.prod.yml --env-file .env run --rm ollama-init   # tải model
./scripts/ingest-knowledge.sh prod --reset                          # nạp kho RAG
```

Kiểm tra:
```bash
docker compose -f docker-compose.prod.yml ps
curl https://$DOMAIN/health/ready
```

## 6. Cập nhật phiên bản

Trên VPS: `./scripts/deploy.sh`.

Hoặc từ GitHub: **Actions → Deploy → Run workflow**. Trước đó cần khai báo các secret `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_APP_DIR` (= `/opt/usas`).

## 7. Backup

```bash
crontab -e
0 3 * * * cd /opt/usas && ./scripts/backup-db.sh >> backups/backup.log 2>&1
```

Khôi phục:
```bash
gunzip -c backups/<file>.sql.gz | docker compose -f docker-compose.prod.yml exec -T db psql -U studyabroad -d studyabroad
```

## 8. Xử lý sự cố

| Triệu chứng | Kiểm tra |
|---|---|
| 502 Bad Gateway | `docker compose -f docker-compose.prod.yml logs api web` |
| Chat trả lỗi 503 | `logs advisor ollama`; model đã pull chưa (`docker compose ... exec ollama ollama list`) |
| Chat rất chậm | Model quá lớn so với CPU/RAM → dùng `qwen3:8b` |
| Lỗi SSL | DNS đã trỏ đúng chưa; chạy lại `./scripts/init-letsencrypt.sh` |
