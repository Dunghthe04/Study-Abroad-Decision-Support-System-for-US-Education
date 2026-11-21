# Deploy lên VPS

Toàn bộ hệ thống chạy trên **một VPS** bằng `docker-compose.prod.yml`. Chỉ Nginx mở cổng 80/443.

## 1. Chuẩn bị

**VPS**: Ubuntu 22.04/24.04.
- Có `ANTHROPIC_API_KEY`: chat/giải thích chạy Claude cloud; VPS chỉ cần Ollama cho SLM phân loại + embedding (+ OCR vision). Tối thiểu khoảng 4 vCPU / 8 GB RAM / 60 GB SSD thường đủ.
- Không có key Claude (fallback Ollama LLM): tối thiểu 4 vCPU / **16 GB RAM**; `qwen3:14b` quantized ~9–10 GB RAM (CPU chậm). VPS yếu hơn thì `LLM_MODEL=qwen3:8b` (~6 GB RAM).
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
| `ANTHROPIC_API_KEY` | API key Anthropic (Claude viết chat / giải thích / đọc bảng điểm bước B) |
| `ANTHROPIC_MODEL` | để trống = `claude-haiku-5-5`; có thể đặt model khác nếu cần |
| `ANTHROPIC_FALLBACK_MODEL` | tùy chọn, model dự phòng khi model chính quá tải |
| `LLM_MODEL` | để trống khi dùng Claude; chỉ đặt (vd. `qwen3:8b`) nếu không có key Anthropic |
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
| Chat trả lỗi 503 | `logs advisor`; kiểm tra `ANTHROPIC_API_KEY` đã vào container chưa (`docker compose ... exec advisor env \| grep ANTHROPIC`); không dùng Claude thì xem `ollama` đã pull model chưa |
| Chat rất chậm | Đang fallback Ollama LLM trên CPU → thêm `ANTHROPIC_API_KEY`, hoặc hạ `LLM_MODEL=qwen3:8b` |
| Lỗi SSL | DNS đã trỏ đúng chưa; chạy lại `./scripts/init-letsencrypt.sh` |

## 9. Server SLM + OCR (VPS CPU riêng)

SLM phân loại (`usas-slm-v5`) và OCR bảng điểm (`glm-ocr`) cùng chạy trên một VPS CPU (4 vCPU / 8 GB): Ollama nghe `127.0.0.1:11434`, Caddy ở trước giữ HTTPS và chỉ cho request có `Authorization: Bearer <token>` (token nằm ở `/root/.usas-slm-api-key` trên VPS).

Cấu hình Ollama (`/etc/systemd/system/ollama.service.d/override.conf`): `OLLAMA_MAX_LOADED_MODELS=1`, `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_KEEP_ALIVE=30m`. RAM chỉ đủ một model một lúc nên OCR xong (`VISION_KEEP_ALIVE=0`) là nhả `glm-ocr` để SLM nạp lại.

Thêm hoặc cập nhật model trên VPS: `ollama pull glm-ocr` hoặc `ollama create usas-slm-v5 -f /opt/usas-models/Modelfile`; kiểm tra bằng `ollama list`, `ollama ps`.

Phía advisor (`.env` gốc cho Docker, `advisor/.env` khi chạy ngoài Docker), dùng chung URL và token cho cả hai:

```
SLM_BASE_URL=https://slm.usdss.site
SLM_API_KEY=<token>
VISION_BASE_URL=https://slm.usdss.site
VISION_API_KEY=<token>
```

Kiểm tra nhanh: `curl -H "Authorization: Bearer <token>" https://slm.usdss.site/api/tags` phải thấy cả `usas-slm-v5` và `glm-ocr`.

Trên CPU, đọc một trang bảng điểm mất khoảng 40 giây (gồm nạp model). Nginx và backend chờ OCR tối đa 600 giây, đủ cho khoảng 10 trang một lần tải lên.
