# Kiến trúc

## Service

| Service | Port (container) | Public? | Ghi chú |
|---|---|---|---|
| `nginx` | 80, 443 | Có | TLS, routing `/api/*` → api, `/*` → web, rate limit chat |
| `web` | 3000 | Qua Nginx | Next.js standalone |
| `api` | 8080 | Qua Nginx | ASP.NET Core, EF Core migrations, Swagger |
| `advisor` | 8000 | Không | FastAPI, chỉ API gọi tới (header `X-Api-Key`) |
| `db` | 5432 | Không | PostgreSQL 16 + pgvector |
| `ollama` | 11434 | Không | Qwen3 (LLM) + BGE-M3 (embedding) |

## Backend (.NET) – Clean Architecture

```
StudyAbroad.Api            Controllers, Program.cs, health checks, Swagger
   └─► StudyAbroad.Application   Use case/service, DTO, interface (IStudyCenterRepository, IAdvisorClient)
          └─► StudyAbroad.Domain       Entity (không phụ thuộc gì)
StudyAbroad.Infrastructure  EF Core DbContext, repository, migration, HttpClient tới advisor
```

Thêm một tính năng mới (ví dụ: Booking):
1. `Domain/Entities/Booking.cs`
2. `Application/Bookings/`: DTO, `IBookingRepository`, `BookingService`
3. `Infrastructure/Persistence/`: `Configurations/BookingConfiguration.cs`, `Repositories/BookingRepository.cs`, đăng ký DI
4. `Api/Controllers/BookingsController.cs`
5. `dotnet ef migrations add AddBooking ...`
6. Unit test trong `tests/StudyAbroad.UnitTests`

## Advisor (RAG)

**Offline** (`scripts/ingest.py`): JSONL đã làm sạch → chia chunk khoảng 500 token, overlap → embedding BGE-M3 → bảng `advisor.documents` (vector 1024 chiều + `tsvector`).

**Online** (`POST /api/v1/chat`):
1. Embed câu hỏi.
2. Hybrid search: vector (HNSW cosine) + full-text, gộp bằng Reciprocal Rank Fusion. Nếu request có `studyLevel` thì chỉ lấy tài liệu của bậc đó cộng với tài liệu `general`; không có thì tìm trên mọi bậc.
3. Prompt gồm quy tắc, bậc học người dùng chọn, ngữ cảnh đánh số (mỗi đoạn ghi rõ bậc) và lịch sử chat → Qwen3.
4. Trả về câu trả lời, citations và disclaimer.

## Database

Một Postgres, hai schema:
- `app`: do EF Core quản lý.
- `advisor`: do advisor quản lý (`advisor/sql/*.sql`, idempotent).
