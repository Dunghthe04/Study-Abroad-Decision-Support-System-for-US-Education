# USAS – Study Abroad Decision Support System for US Education

Nền tảng hỗ trợ học sinh, sinh viên và phụ huynh ra quyết định **du học Mỹ ở mọi bậc học**: THCS/THPT, cao đẳng cộng đồng, đại học, thạc sĩ, tiến sĩ.

- **Tư vấn AI**: chatbot trả lời về visa, hồ sơ, chi phí, học bổng, có trích nguồn (RAG) và phân biệt theo bậc học.
- **Tra cứu thông tin** du học.
- **Tìm trung tâm tư vấn** theo dịch vụ, khu vực, bậc học.

> **Thành viên mới:** đọc hết file này một lượt (khoảng 15 phút), rồi làm theo mục [5. Chạy local](#5-chạy-local). Nếu bị kẹt, xem mục [9. Lỗi thường gặp](#9-lỗi-thường-gặp) trước khi hỏi nhóm.

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Tech stack](#2-tech-stack)
3. [Cấu trúc thư mục](#3-cấu-trúc-thư-mục)
4. [Cài đặt công cụ](#4-cài-đặt-công-cụ)
5. [Chạy local](#5-chạy-local)
6. [Quy trình làm việc (Git)](#6-quy-trình-làm-việc-git)
7. [Thêm tính năng mới](#7-thêm-tính-năng-mới)
8. [Kiểm thử và CI](#8-kiểm-thử-và-ci)
9. [Lỗi thường gặp](#9-lỗi-thường-gặp)
10. [Quy tắc bắt buộc](#10-quy-tắc-bắt-buộc)

---

## 1. Kiến trúc tổng quan

```
                         ┌──────────────────────── Docker network ─────────────────────────┐
Trình duyệt ──HTTPS──► Nginx ──/api/*──► .NET API ─────────────► PostgreSQL (schema app)
                         │                  │
                         └──/*──► Next.js   └──HTTP + X-Api-Key──► Advisor (FastAPI)
                                                                     ├──► PostgreSQL + pgvector (schema advisor)
                                                                     └──► Ollama (Qwen3 + BGE-M3)
                         └─────────────────────────────────────────────────────────────────┘
```

| Thành phần | Vai trò |
|---|---|
| **Nginx** | Cổng vào duy nhất. Nhận HTTPS, chuyển `/api/*` sang backend, phần còn lại sang frontend |
| **Next.js** (`frontend/`) | Giao diện web |
| **.NET API** (`backend/`) | Nghiệp vụ chính: tài khoản, trung tâm, đặt lịch, thanh toán…; là gateway tới AI |
| **Advisor** (`advisor/`) | Service AI: tìm tài liệu liên quan (RAG) rồi nhờ LLM viết câu trả lời có trích nguồn |
| **PostgreSQL + pgvector** | Một database, hai schema: `app` (dữ liệu nghiệp vụ) và `advisor` (kho vector cho RAG) |
| **Ollama** | Chạy model open-source ngay trên máy hoặc VPS, không gọi API trả phí |

**Nguyên tắc thiết kế:**

- **Trình duyệt chỉ gọi .NET API.** Advisor, DB và Ollama không mở ra internet.
- **Không fine-tune LLM.** Kiến thức nằm trong kho RAG, có nguồn và cập nhật được. LLM chỉ đọc ngữ cảnh rồi viết câu trả lời.
- **Hỗ trợ mọi bậc học.** Mỗi tài liệu trong kho RAG và mỗi trung tâm đều được gắn bậc học. Người dùng chọn bậc thì AI chỉ tìm tài liệu của bậc đó, cộng với tài liệu chung (visa F-1, SEVIS, chuẩn bị trước khi bay…). Thông tin của bậc này không được dùng để trả lời cho bậc khác: SAT áp dụng cho đại học, còn thạc sĩ/tiến sĩ thường xét GRE/GMAT, SOP, học bổng RA/TA.

### Mã bậc học (dùng chung cho cả 3 phần)

| Mã | Ý nghĩa |
|---|---|
| `secondary` | THCS/THPT |
| `community_college` | Cao đẳng cộng đồng (gồm lộ trình 2+2) |
| `undergraduate` | Đại học |
| `master` | Thạc sĩ |
| `phd` | Tiến sĩ |
| `general` | Chỉ dùng cho tài liệu RAG áp dụng mọi bậc (visa, SEVIS, nhập cảnh…) |

Danh sách này được khai báo ở 3 nơi và **phải giữ giống nhau**:
- `backend/src/StudyAbroad.Domain/Constants/StudyLevels.cs`
- `advisor/app/core/study_levels.py`
- `frontend/src/lib/study-levels.ts`

## 2. Tech stack

| Nhóm | Công nghệ | Phiên bản | Dùng để | Lý do chọn |
|---|---|---|---|---|
| Frontend | **Next.js** (App Router) + **React** | 16 / 19 | Giao diện web, render phía server | SEO tốt, có cả server component lẫn client component, cộng đồng lớn |
| | **TypeScript** | 5 | Ngôn ngữ frontend | Bắt lỗi kiểu dữ liệu khi code, khớp với DTO của backend |
| | **Tailwind CSS** | 4 | Styling | Viết nhanh, không cần file CSS riêng cho từng component |
| Backend | **ASP.NET Core Web API** | .NET 8 (LTS) | REST API nghiệp vụ | Hiệu năng cao, phiên bản hỗ trợ dài hạn, nhóm đã quen C# |
| | **Entity Framework Core** + Npgsql | 8 | ORM, migration database | Thao tác DB bằng C#, quản lý version schema bằng migration |
| | **Clean Architecture** | – | Tổ chức code thành 4 tầng | Tách nghiệp vụ khỏi hạ tầng, dễ test, dễ trình bày |
| | **Serilog**, **Swagger** | – | Log, tài liệu API | Xem request/lỗi dễ dàng; frontend tra cứu API trên Swagger |
| AI | **Python FastAPI** | 0.142 | Service AI (advisor) | Hệ sinh thái AI chủ yếu dùng Python; FastAPI nhẹ, async, có docs tự động |
| | **Qwen3-8B** (dev) / **Qwen3-14B** (prod) qua **Ollama** | – | LLM sinh câu trả lời | Open-source, tiếng Việt tốt, chạy local được, bản quantized nhẹ |
| | **BGE-M3** | – | Embedding (vector 1024 chiều) | Đa ngôn ngữ, hỗ trợ tiếng Việt tốt |
| | **Hybrid search** (vector + full-text, RRF) | – | Tìm tài liệu cho RAG | Vector hiểu ngữ nghĩa; full-text bắt đúng thuật ngữ như F-1, I-20, SEVIS |
| Database | **PostgreSQL** + **pgvector** | 16 | Dữ liệu nghiệp vụ + kho vector | Một DB cho cả hai, đỡ phải vận hành thêm vector DB riêng |
| Hạ tầng | **Docker**, **Docker Compose** | – | Đóng gói và chạy toàn hệ thống | Máy ai cũng chạy giống nhau, deploy chỉ cần một lệnh |
| | **Nginx** + **Let's Encrypt** | 1.27 | Reverse proxy, HTTPS miễn phí | Chuẩn phổ biến khi tự host |
| | **GitHub Actions** | – | CI: lint, test, build mỗi PR; deploy bằng nút bấm | Miễn phí, tích hợp sẵn GitHub |
| | **VPS** (Ubuntu) | – | Môi trường production | Một máy chạy toàn bộ bằng Docker |
| Thanh toán | **payOS** (sandbox) | – | Thanh toán | *Sẽ tích hợp* |
| Kiểm thử | **xUnit**, **pytest**, **ESLint**, **ruff** | – | Unit test, kiểm tra code | Chuẩn của từng ngôn ngữ |
| Quản lý | **Jira**, Google Drive, Zalo, Google Meet | – | Backlog/sprint, tài liệu, liên lạc | – |

## 3. Cấu trúc thư mục

```
.
├── frontend/                 ① Giao diện web (Next.js)
├── backend/                  ② API nghiệp vụ (.NET)
├── advisor/                  ③ Service AI – RAG (Python)
├── database/init/            Script khởi tạo Postgres (chỉ chạy lần đầu tạo DB)
├── deploy/nginx/             Cấu hình Nginx: định tuyến, HTTPS
├── scripts/                  Script vận hành: deploy, backup, nạp dữ liệu AI, cấp SSL
├── docs/                     Tài liệu chi tiết: ARCHITECTURE.md, DEPLOYMENT.md
├── .github/workflows/        CI/CD (GitHub Actions)
├── docker-compose.yml        Chạy hệ thống ở máy dev
├── docker-compose.prod.yml   Chạy hệ thống trên VPS
└── .env.example              Mẫu biến môi trường (copy thành .env, KHÔNG commit .env)
```

### ① `frontend/` – Next.js

```
frontend/src/
├── app/                      Mỗi folder là một URL
│   ├── layout.tsx            Khung chung (header, font)
│   ├── page.tsx              "/"          trang chủ
│   ├── advisor/page.tsx      "/advisor"   chat AI
│   ├── centers/page.tsx      "/centers"   danh sách trung tâm
│   └── healthz/route.ts      "/healthz"   để Docker kiểm tra web còn sống
├── components/               Component dùng lại (ChatBox, SiteHeader…)
├── lib/api.ts                Hàm gọi API dùng chung – mọi request đi qua đây
└── types/api.ts              Kiểu dữ liệu, PHẢI khớp với DTO bên backend
```

### ② `backend/` – .NET, Clean Architecture

```
backend/
├── StudyAbroad.sln                     Mở bằng Visual Studio 2022
├── src/
│   ├── StudyAbroad.Domain/             Entity (StudyCenter…). Không phụ thuộc gì
│   ├── StudyAbroad.Application/        Service nghiệp vụ, DTO, interface
│   ├── StudyAbroad.Infrastructure/     EF Core (DbContext, Migrations, Repositories), HttpClient gọi advisor
│   └── StudyAbroad.Api/                Controllers, Program.cs, appsettings*.json
└── tests/StudyAbroad.UnitTests/        Unit test (xUnit)
```

Chiều phụ thuộc giữa các tầng: `Api → Application → Domain`, và `Infrastructure → Application`.
Tầng Application chỉ biết **interface** (ví dụ `IStudyCenterRepository`), không biết EF Core hay Postgres. Nhờ vậy test được bằng repository giả.

### ③ `advisor/` – FastAPI (RAG)

```
advisor/
├── app/
│   ├── main.py               Khởi tạo app, kết nối DB, tạo service
│   ├── core/                 Cấu hình (.env), DB, kiểm tra API key
│   ├── api/routes/           /api/v1/chat, /health/live, /health/ready
│   ├── schemas/              Định dạng request/response (Pydantic)
│   ├── services/             ollama.py (gọi LLM + embedding), advisor.py (pipeline RAG)
│   └── rag/                  retriever.py (hybrid search), prompts.py (system prompt)
├── sql/                      Tạo bảng advisor.documents (vector + index)
├── scripts/ingest.py         Nạp kiến thức: JSONL → chunk → embedding → DB
├── data/raw/                 Dữ liệu thô – KHÔNG commit (có thông tin cá nhân)
├── data/processed/           Dữ liệu đã làm sạch (.jsonl) để ingest
└── tests/                    pytest (dùng LLM giả, không cần GPU)
```

**Hai luồng của advisor:**
- **Offline** (`scripts/ingest.py`): nạp tài liệu vào kho. Chỉ chạy khi có tài liệu mới.
- **Online** (`POST /api/v1/chat`): embed câu hỏi → tìm top-k đoạn liên quan → LLM trả lời dựa trên các đoạn đó, kèm nguồn và disclaimer.

### Hạ tầng

| Đường dẫn | Mục đích |
|---|---|
| `database/init/01-extensions.sql` | Bật pgvector, tạo schema `app` và `advisor`. Docker mount file vào container Postgres; file chỉ chạy khi DB tạo lần đầu |
| `deploy/nginx/` | `local.conf` (dev, cổng 8088), `templates/default.conf.template` (prod, HTTPS), `snippets/locations.conf` (định tuyến dùng chung) |
| `scripts/deploy.sh` | Trên VPS: kéo code mới → build → chạy lại |
| `scripts/backup-db.sh` | Sao lưu DB, giữ 14 bản gần nhất |
| `scripts/init-letsencrypt.sh` | Cấp chứng chỉ HTTPS lần đầu trên VPS |
| `scripts/ingest-knowledge.sh` | Nạp dữ liệu RAG trong container |

> Code của nhóm (`frontend/`, `backend/`, `advisor/`) được **build thành image** qua Dockerfile. Cấu hình (`deploy/`, `database/`) được **mount vào** container có sẵn. Dữ liệu DB và model AI nằm trong **Docker volume** (`pgdata`, `ollama`), không nằm trong repo.

## 4. Cài đặt công cụ

Cài một lần trên máy của mỗi thành viên:

| Công cụ | Phiên bản | Bắt buộc? | Ghi chú |
|---|---|---|---|
| [Git](https://git-scm.com/) | mới nhất | ✅ | Kèm Git Bash để chạy các file `.sh` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | mới nhất | ✅ | Bật WSL 2. Cấp cho Docker ≥ 8 GB RAM (Settings → Resources) |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 | Người làm backend | Kèm Visual Studio 2022 hoặc VS Code + C# Dev Kit |
| [Node.js](https://nodejs.org/) | 22 LTS | Người làm frontend | |
| [Python](https://www.python.org/downloads/) | 3.12+ | Người làm AI | Khi cài, tick "Add to PATH" |

Mở repo bằng VS Code thì cài các extension được gợi ý (VS Code tự hỏi, danh sách nằm trong `.vscode/extensions.json`).

Kiểm tra:
```powershell
git --version; docker --version; dotnet --version; node -v; python --version
```

## 5. Chạy local

### Bước 0 – Lấy code và tạo file môi trường (làm một lần)

```powershell
git clone <repo-url>
cd <thu-muc-repo>
cp .env.example .env
```

File `.env` mặc định đã chạy được ở local, không cần sửa gì.

### Cách A – Chạy toàn bộ bằng Docker (để xem thử, không cần cài .NET/Node/Python)

```powershell
docker compose up -d --build
```

Lần đầu mất khoảng 10–20 phút: build image và tải model AI khoảng 6 GB. Xong thì mở:

| URL | Là gì |
|---|---|
| http://localhost:8088 | **Web** (đi qua Nginx, giống production) |
| http://localhost:5080/swagger | Swagger của .NET API |
| http://localhost:8000/docs | Swagger của advisor |

Dừng: `docker compose down`. Xem log: `docker compose logs -f api` (thay `api` bằng tên service khác nếu cần).

### Cách B – Docker chỉ chạy hạ tầng, code chạy từ IDE (dùng hằng ngày khi dev)

Cách này cho phép debug, đặt breakpoint và hot reload.

**1. Chạy hạ tầng (DB + Ollama + tải model):**
```powershell
docker compose up -d db ollama ollama-init
```

**2. Chạy phần mình phụ trách**, mỗi phần một cửa sổ terminal:

<details open>
<summary><b>Backend (.NET)</b> → http://localhost:5080/swagger</summary>

- Visual Studio 2022: mở `backend/StudyAbroad.sln`, chọn profile **http**, nhấn F5.
- Hoặc dùng terminal:
  ```powershell
  cd backend
  dotnet tool restore
  dotnet watch --project src/StudyAbroad.Api
  ```

Lần chạy đầu, API tự tạo bảng (migration) và seed 2 trung tâm demo.
</details>

<details open>
<summary><b>Frontend (Next.js)</b> → http://localhost:3000</summary>

```powershell
cd frontend
cp .env.example .env.local
npm install
npm run dev
```
Next.js tự chuyển các request `/api/*` sang backend ở cổng 5080, nên **cần chạy backend trước**.
</details>

<details open>
<summary><b>Advisor (Python)</b> → http://localhost:8000/docs</summary>

```powershell
cd advisor
python -m venv .venv
.venv\Scripts\Activate.ps1          # Git Bash: source .venv/Scripts/activate | macOS/Linux: source .venv/bin/activate
pip install -r requirements-dev.txt
cp .env.example .env
uvicorn app.main:app --reload --port 8000
```

Nạp dữ liệu mẫu vào kho RAG:
```powershell
python -m scripts.ingest --reset
```
</details>

**3. Kiểm tra nhanh:**
- http://localhost:3000/centers hiện 2 trung tâm demo: frontend → backend → DB đã thông.
- http://localhost:3000/advisor, gửi câu hỏi "SEVIS là gì?" và nhận câu trả lời có nguồn: backend → advisor → Ollama đã thông. Cần chạy cả advisor và đã ingest dữ liệu.

> Không làm AI cũng **không cần chạy advisor**. Trang chat sẽ báo lỗi 503, các trang khác vẫn chạy bình thường.
> Máy yếu (RAM < 16 GB): đặt `LLM_MODEL=qwen3:4b` trong `.env` (cả `.env` ở thư mục gốc lẫn `advisor/.env`) để dùng model nhỏ hơn.

### Cổng sử dụng

| Cổng | Service |
|---|---|
| 3000 | Frontend |
| 5080 | Backend API |
| 8000 | Advisor |
| 5432 | PostgreSQL (user / pass / db: `studyabroad`) |
| 11434 | Ollama |
| 8088 | Nginx (chỉ có ở Cách A) |

Có thể xem DB bằng DBeaver, pgAdmin hoặc extension của VS Code: kết nối `localhost:5432`, user, mật khẩu và database đều là `studyabroad`.

## 6. Quy trình làm việc (Git)

### Nhánh

| Nhánh | Dùng cho |
|---|---|
| `main` | Bản ổn định để demo và deploy. **Không push thẳng** |
| `develop` | Tích hợp code của cả nhóm. **Không push thẳng** |
| `feature/<JIRA-KEY>-mo-ta` | Tính năng mới, ví dụ `feature/USAS-12-center-search` |
| `fix/<JIRA-KEY>-mo-ta` | Sửa lỗi |

### Luồng làm một task

```powershell
git checkout develop
git pull
git checkout -b feature/USAS-12-center-search
# ... code, commit nhỏ và thường xuyên ...
git push -u origin feature/USAS-12-center-search
```
Sau đó lên GitHub tạo **Pull Request vào `develop`**, gắn link task Jira, nhờ ít nhất **1 người review**. **CI phải xanh** mới được merge.

### Commit message

Định dạng: `<type>(<scope>): <mô tả ngắn>`.

- type: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`
- scope: `web`, `api`, `advisor`, `infra`

Ví dụ:
```
feat(api): add study center search endpoint
fix(web): handle empty advisor response
docs: update local setup guide
```

## 7. Thêm tính năng mới

### Backend – ví dụ thêm "Booking"

Làm theo mẫu có sẵn của `StudyCenter`:

1. `Domain/Entities/Booking.cs`: entity.
2. `Application/Bookings/`: DTO, `IBookingRepository`, `BookingService`. Đăng ký service trong `Application/DependencyInjection.cs`.
3. `Infrastructure/Persistence/Configurations/BookingConfiguration.cs`, `Repositories/BookingRepository.cs`, thêm `DbSet` vào `AppDbContext`. Đăng ký trong `Infrastructure/DependencyInjection.cs`.
4. `Api/Controllers/BookingsController.cs`, route theo dạng `api/v1/bookings`.
5. Tạo migration:
   ```powershell
   cd backend
   dotnet ef migrations add AddBooking -p src/StudyAbroad.Infrastructure -s src/StudyAbroad.Api -o Persistence/Migrations
   ```
6. Viết unit test trong `tests/StudyAbroad.UnitTests/`.

### Frontend

1. Thêm kiểu dữ liệu vào `src/types/api.ts`, khớp với DTO vừa tạo.
2. Tạo trang `src/app/<ten-trang>/page.tsx`, component đặt trong `src/components/<nhom>/`.
3. Gọi API qua `apiFetch` trong `src/lib/api.ts`. Không gọi `fetch` trực tiếp.

### Advisor (dữ liệu RAG)

1. Làm sạch tài liệu từ **nguồn chính thức** (travel.state.gov, trang của trường…).
2. Lưu thành file `.jsonl` trong `advisor/data/processed/`, theo định dạng của `sample.jsonl`. Mỗi dòng gồm `title`, `source_url`, `doc_type`, `study_level`, `retrieved_at`, `content`.
   - `study_level` lấy từ bảng [mã bậc học](#mã-bậc-học-dùng-chung-cho-cả-3-phần). Tài liệu áp dụng cho mọi bậc thì ghi `general`.
   - Tài liệu nói về nhiều bậc mà nội dung khác nhau theo bậc (ví dụ yêu cầu đầu vào): **tách thành nhiều dòng**, mỗi dòng một bậc.
3. Chạy `python -m scripts.ingest`.

## 8. Kiểm thử và CI

Chạy trước khi tạo PR (đây cũng là các bước CI chạy):

```powershell
# Frontend
cd frontend
npm run lint
npm run typecheck
npm run build

# Backend
cd backend
dotnet test

# Advisor (đã activate .venv)
cd advisor
ruff check .
ruff format .
pytest
```

GitHub Actions ([ci.yml](.github/workflows/ci.yml)) chạy toàn bộ các bước trên và build Docker image cho **mọi Pull Request**. Deploy lên VPS bằng tay qua workflow **Deploy**, chi tiết trong [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## 9. Lỗi thường gặp

| Lỗi | Cách xử lý |
|---|---|
| `failed to connect to the docker API` | Docker Desktop chưa mở. Mở lên, chờ biểu tượng chuyển xanh |
| `port is already allocated` (5432, 3000…) | Cổng đang bị chiếm, thường do đã cài sẵn Postgres trên máy. Tắt service đó, hoặc đổi cổng bên trái trong `docker-compose.yml` (`"5433:5432"`) và sửa connection string tương ứng |
| Backend báo `Failed to connect to 127.0.0.1:5432` | DB chưa chạy: `docker compose up -d db` |
| Trang `/centers` báo "Không tải được dữ liệu" | Backend chưa chạy, hoặc thiếu `frontend/.env.local` |
| Chat báo 503 | Advisor chưa chạy, hoặc model chưa tải xong (`docker compose logs ollama-init`) |
| Chat trả lời rất chậm | Chạy bằng CPU nên chậm là bình thường. Dùng `qwen3:4b` để nhanh hơn |
| `Activate.ps1 cannot be loaded… running scripts is disabled` | Chạy một lần: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |
| Sửa `database/init/*.sql` nhưng không thấy tác dụng | Script chỉ chạy khi DB tạo lần đầu. Reset bằng `docker compose down -v` (**xóa hết dữ liệu local**) |
| Muốn xóa DB local làm lại từ đầu | `docker compose down -v`, rồi `docker compose up -d db` |

## 10. Quy tắc bắt buộc

- ❌ **Không commit `.env`**, mật khẩu, API key. Chỉ commit `.env.example`.
- ❌ **Không commit dữ liệu khảo sát thô** (có tên, trường, năm sinh…). Dữ liệu này để trên Google Drive.
- ❌ **Không sửa migration đã merge** vào `develop`. Muốn đổi schema thì tạo migration mới.
- ❌ **Không push thẳng** vào `main` hoặc `develop`.
- ✅ Đổi DTO ở backend thì cập nhật luôn `frontend/src/types/api.ts`.
- ✅ Thêm biến môi trường mới thì thêm vào `.env.example` (kèm comment) và vào docker-compose.
- ✅ Kiến thức đưa vào RAG phải có `source_url` và `retrieved_at`.

---

📚 Tài liệu chi tiết: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)
