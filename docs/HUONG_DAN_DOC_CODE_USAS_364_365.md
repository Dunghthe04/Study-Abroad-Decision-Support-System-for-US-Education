# HƯỚNG DẪN ĐỌC CODE — USAS-364 (Hồ sơ tài chính & ngoại khóa) và USAS-365 (Phân tích GPA)

Tài liệu dành cho người chưa đọc code: giải thích **từng folder để làm gì, làm việc theo thứ tự nào, DB lưu ra sao, FE – BE – DB nối với nhau như thế nào**, kèm đường dẫn file cụ thể.

> Lưu ý nhánh Git:
> - Code **USAS-365** nằm trên nhánh `feature/USAS-365-academic-analysis-ducnx` (nhánh đang checkout).
> - Code **USAS-364** nằm trên nhánh `feature/USAS-364-financial-profile-ducnx`. Muốn xem thì `git checkout` nhánh đó, hoặc `git show feature/USAS-364-financial-profile-ducnx:<đường dẫn file>`.

---

## PHẦN 0. BẢN ĐỒ THƯ MỤC TOÀN REPO

```
Study-Abroad-Decision-Support-System-for-US-Education/
├── backend/                     ← API .NET 8 (C#)
│   ├── StudyAbroad.sln          ← solution gom 4 project + test
│   ├── src/
│   │   ├── StudyAbroad.Domain/          ← Tầng 1: Entity (bảng DB), hằng số
│   │   ├── StudyAbroad.Application/     ← Tầng 2: DTO, Interface, Service (nghiệp vụ)
│   │   ├── StudyAbroad.Infrastructure/  ← Tầng 3: EF Core, DbContext, Repository, Migration
│   │   └── StudyAbroad.Api/             ← Tầng 4: Controller, Program.cs, appsettings.json
│   └── tests/StudyAbroad.UnitTests/     ← Unit test xUnit
├── frontend/                    ← Web Next.js 15 (TypeScript, React, Tailwind)
│   └── src/
│       ├── app/                 ← Routing theo thư mục (App Router): app/profile/academic/page.tsx = URL /profile/academic
│       ├── components/          ← Component UI tái sử dụng
│       ├── lib/                 ← Hàm gọi API (api.ts, academic-api.ts, profile-api.ts)
│       ├── types/               ← Interface TypeScript (bản sao DTO của BE)
│       └── contexts/            ← React Context (AuthContext giữ user đăng nhập)
├── advisor/                     ← Service AI Python (FastAPI) — ngoài phạm vi 364/365
├── database/init/               ← SQL chạy 1 lần khi tạo container Postgres
├── deploy/nginx/                ← Cấu hình reverse proxy cho production
├── docker-compose.yml           ← Chạy db / api / web / advisor bằng Docker
└── docs/                        ← Tài liệu
```

---

## PHẦN 0B. TRA NHANH THEO UC: DB LƯU CỘT NÀO, FILE NẰM Ở ĐÂU

### UC-365 — Phân tích GPA

#### DB: 3 bảng đọc/ghi + 1 bảng chỉ đọc

**`app.transcript_scores`** — mỗi dòng = 1 môn của 1 học kỳ (UC-365 ghi/đọc toàn bộ)

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `id` | uuid | khóa chính |
| `student_profile_id` | uuid FK → `student_profiles.id` | của ai |
| `term_name` | varchar(64) | "Lớp 10 HK1" |
| `term_order` | int (1–20) | thứ tự kỳ, dùng vẽ biểu đồ |
| `subject` | varchar(128) | tên môn |
| `score` | numeric(6,2) | điểm hệ 10 |
| `credits` | numeric(4,1) null | tín chỉ/hệ số |
| `raw_score` | varchar(16) | điểm gốc (của UC-363, 365 không dùng) |
| `created_at`, `updated_at` | timestamp | |

**`app.analysis_results`** — mỗi lần bấm phân tích = 1 dòng mới

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `id` | uuid | |
| `student_profile_id` | uuid FK | |
| `kind` | varchar(32) | luôn `"gpa"` với UC-365 |
| `result_json` | **jsonb** | toàn bộ kết quả: unweightedGpa, weightedGpa, rawAverage, totalSubjects, totalTerms, trend, trendDescription, scaleSource, subjectGroups[], termAverages[], disclaimer |
| `model_version` | varchar(64) | `"usas-gpa-v1.0"` |
| `created_at` | timestamp | dùng lấy bản mới nhất |

**`app.student_profiles`** — UC-365 chỉ đụng 3 cột

| Cột | UC-365 làm gì |
|---|---|
| `user_id` | tìm hồ sơ theo user đăng nhập |
| `grade_scale` | gán `"10"` khi tự tạo hồ sơ mới |
| `overall_gpa` numeric(5,2) | **ghi lại = Unweighted GPA sau mỗi lần phân tích** |

**`app.app_settings`** — chỉ đọc dòng `key = 'grade_scale.10'`, cột `value_json` chứa bảng quy đổi. Không có dòng này thì dùng bảng WES mặc định trong code.

#### File (nhánh hiện tại `feature/USAS-365-academic-analysis-ducnx`)

```
backend/src/StudyAbroad.Domain/Entities/TranscriptScore.cs          ← cột bảng transcript_scores
backend/src/StudyAbroad.Domain/Entities/AnalysisResult.cs           ← cột bảng analysis_results
backend/src/StudyAbroad.Domain/Entities/StudentProfile.cs           ← cột bảng student_profiles
backend/src/StudyAbroad.Domain/Entities/AppSetting.cs               ← cột bảng app_settings
backend/src/StudyAbroad.Domain/Constants/AcademicConstants.cs       ← "gpa", nhóm môn, trend, disclaimer

backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/TranscriptScoreConfiguration.cs   ← FK, maxLength, precision, index
backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/AnalysisResultConfiguration.cs    ← jsonb, index
backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/StudentProfileConfiguration.cs
backend/src/StudyAbroad.Infrastructure/Persistence/AppDbContext.cs                                  ← DbSet, schema "app"
backend/src/StudyAbroad.Infrastructure/Persistence/Repositories/AcademicAnalysisRepository.cs       ← câu query thật
backend/src/StudyAbroad.Infrastructure/DependencyInjection.cs                                       ← dòng 31-32 đăng ký repo

backend/src/StudyAbroad.Application/Profile/Academic/AcademicDtos.cs                 ← DTO
backend/src/StudyAbroad.Application/Profile/Academic/IAcademicAnalysisRepository.cs
backend/src/StudyAbroad.Application/Profile/Academic/IAcademicAnalysisService.cs
backend/src/StudyAbroad.Application/Profile/Academic/AcademicAnalysisService.cs      ← thuật toán GPA
backend/src/StudyAbroad.Application/DependencyInjection.cs                           ← dòng 14-15 đăng ký service

backend/src/StudyAbroad.Api/Controllers/AcademicAnalysisController.cs                ← /api/v1/profile/academic/*
backend/tests/StudyAbroad.UnitTests/AcademicAnalysisServiceTests.cs                  ← 91 test

frontend/src/types/academic.ts                                      ← interface TS
frontend/src/lib/academic-api.ts                                    ← 6 hàm gọi API
frontend/src/components/academic/AcademicAnalysisView.tsx           ← fetch + ghép card
frontend/src/components/academic/GpaSummaryCard.tsx
frontend/src/components/academic/SubjectGroupBreakdown.tsx
frontend/src/components/academic/TermTrendChart.tsx
frontend/src/components/academic/TranscriptScoreTable.tsx           ← nhập/sửa/xóa điểm
frontend/src/components/academic/ReadOnlyTranscriptTable.tsx
frontend/src/app/profile/academic/page.tsx                          ← URL /profile/academic
frontend/src/app/profile/academic/analysis/page.tsx                 ← URL /profile/academic/analysis
```

### UC-364 — Hồ sơ tài chính & ngoại khóa

#### DB: 2 bảng

**`app.student_profiles`** — UC-364 ghi 5 cột tài chính (không tạo bảng riêng)

| Cột | Kiểu | Ý nghĩa |
|---|---|---|
| `annual_budget_usd` | numeric(12,2) | ngân sách/năm |
| `max_expected_tuition_usd` | numeric | học phí tối đa mong muốn |
| `funding_source` | varchar(32) | family / loan / scholarship / other |
| `needs_scholarship` | boolean | |
| `financial_notes` | text | ghi chú |

**`app.profile_activities`** — ngoại khóa và giải thưởng **chung 1 bảng**, phân biệt bằng `kind`

| Cột | Kiểu | Ngoại khóa (`kind='extracurricular'`) | Giải thưởng (`kind='award'`) |
|---|---|---|---|
| `id` | uuid | | |
| `student_profile_id` | uuid FK | | |
| `kind` | varchar | `extracurricular` | `award` |
| `title` | text | tên hoạt động | tên giải |
| `role` | text | vai trò | **category** (loại thành tích) |
| `organization` | text | tổ chức | **issuer** (đơn vị cấp) |
| `description` | text | mô tả | mô tả |
| `start_date` | date | ngày bắt đầu | **issue_date** (ngày cấp) |
| `end_date` | date | ngày kết thúc | — |
| `impact_level` | int 1–5 | 1 trường … 5 quốc tế | — |
| `duration_months` | int 1–120 | số tháng tham gia | — |
| `is_ongoing` | boolean | đang tham gia | — |

3 cột cuối và 2 cột `max_expected_tuition_usd`, `financial_notes` được thêm bởi migration `20261008052916_AddFinancialDetailsAndActivityDuration`.

#### File (nhánh `feature/USAS-364-financial-profile-ducnx` — chưa có trên nhánh 365)

```
backend/src/StudyAbroad.Domain/Entities/ProfileActivity.cs                       ← cột bảng profile_activities
backend/src/StudyAbroad.Domain/Entities/StudentProfile.cs                        ← 5 cột tài chính
backend/src/StudyAbroad.Domain/Constants/ProfileConstants.cs                     ← giới hạn validate

backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/ProfileActivityConfiguration.cs
backend/src/StudyAbroad.Infrastructure/Persistence/Migrations/20261008052916_AddFinancialDetailsAndActivityDuration.cs
backend/src/StudyAbroad.Infrastructure/Persistence/Repositories/FinancialProfileRepository.cs

backend/src/StudyAbroad.Application/Profile/Financial/FinancialDtos.cs
backend/src/StudyAbroad.Application/Profile/Financial/IFinancialProfileRepository.cs
backend/src/StudyAbroad.Application/Profile/Financial/IFinancialProfileService.cs
backend/src/StudyAbroad.Application/Profile/Financial/FinancialProfileService.cs

backend/src/StudyAbroad.Api/Controllers/FinancialProfileController.cs            ← /api/v1/profile/financial|extracurricular|achievements|summary
backend/tests/StudyAbroad.UnitTests/FinancialProfileServiceTests.cs              ← 48 test

frontend/src/types/profile.ts
frontend/src/lib/profile-api.ts
frontend/src/components/profile/FinancialCard.tsx
frontend/src/components/profile/ExtracurricularSection.tsx
frontend/src/components/profile/AchievementsSection.tsx
frontend/src/components/profile/FinancialProfileTabs.tsx
frontend/src/app/profile/financial/page.tsx                                      ← URL /profile/financial
```

Xem file 364 mà không đổi nhánh: `git show feature/USAS-364-financial-profile-ducnx:<đường dẫn>`.

### Quan hệ 2 UC trong DB

```
users ──1:1── student_profiles ──1:n── transcript_scores    (365)
                    │          ──1:n── analysis_results     (365, kind='gpa')
                    │          ──1:n── profile_activities   (364, kind='extracurricular'|'award')
                    └── cột overall_gpa (365 ghi), annual_budget_usd… (364 ghi)
app_settings  (365 đọc key 'grade_scale.10')
```

---

## PHẦN 1. BỨC TRANH TỔNG THỂ: FE ↔ BE ↔ DB NỐI VỚI NHAU RA SAO

```
 Trình duyệt (localhost:3000)                 .NET API (localhost:5080)               PostgreSQL (localhost:5432)
 ┌──────────────────────────┐   HTTP JSON    ┌──────────────────────────┐   SQL      ┌────────────────────────┐
 │ Next.js page/component   │ ─────────────► │ Controller               │ ─────────► │ schema "app"           │
 │  → lib/academic-api.ts   │  /api/v1/...   │  → Service (Application) │  EF Core   │  student_profiles      │
 │  → lib/api.ts (apiFetch) │ ◄───────────── │  → Repository (Infra)    │ ◄───────── │  transcript_scores     │
 │ cookie usas_access_token │  JSON / 4xx    │  → AppDbContext          │            │  analysis_results ...  │
 └──────────────────────────┘                └──────────────────────────┘            └────────────────────────┘
```

### 1.1. FE gọi BE bằng đường nào?

| Môi trường | FE gọi | Ai chuyển tiếp | File cấu hình |
|---|---|---|---|
| Dev (chạy `npm run dev`) | `fetch("/api/v1/...")` cùng origin `localhost:3000` | Next.js **rewrite** sang `API_PROXY_TARGET` (ví dụ `http://localhost:5080`) | `frontend/next.config.ts` dòng 10-12, biến môi trường trong `frontend/.env.example` |
| Server component (SSR) | `${API_INTERNAL_URL}/api/...` | gọi thẳng vào container `api:8080` | `frontend/src/lib/api.ts` dòng 19 |
| Production | `/api/*` | **Nginx** route `/api` → container api, `/` → container web | `deploy/nginx/*.conf`, `docker-compose.yml` service `proxy` |

Vì FE và BE **cùng origin** (đều qua `localhost:3000` hoặc Nginx) nên trình duyệt không chặn CORS. BE vẫn khai báo CORS phòng trường hợp gọi chéo: `backend/src/StudyAbroad.Api/Program.cs` dòng 136-141, danh sách origin lấy từ `appsettings.json` → `"Cors:AllowedOrigins": ["http://localhost:3000"]`.

### 1.2. Đăng nhập / xác thực nối thế nào?

1. Người dùng login → BE trả JWT nhưng **không trả trong body** mà set **HttpOnly cookie** tên `usas_access_token`.
2. Mọi request sau đó FE gửi kèm cookie nhờ `credentials: "include"` (`frontend/src/lib/api.ts` dòng 35).
3. BE đọc cookie thành token trong `Program.cs` dòng 67-76 (`OnMessageReceived`), kiểm tra phiên chưa bị thu hồi ở dòng 77-101.
4. Controller lấy `userId` từ claim `ClaimTypes.NameIdentifier` (`AcademicAnalysisController.cs` dòng 144-158). **Không bao giờ nhận userId từ body** → chống IDOR.
5. FE chặn trang theo role bằng `<ProtectedRoute allowedRoles={["student","parent"]}>` (`frontend/src/components/auth/ProtectedRoute.tsx`), đọc user từ `AuthContext`.

### 1.3. BE nối DB bằng gì?

- Connection string: `backend/src/StudyAbroad.Api/appsettings.json` dòng 2-4 → `Host=localhost;Port=5432;Database=studyabroad;Username=studyabroad;Password=studyabroad`.
- Khi chạy Docker, biến `ConnectionStrings__Default` trong `docker-compose.yml` dòng 69 ghi đè (`Host=db`).
- Đăng ký EF Core + Npgsql: `backend/src/StudyAbroad.Infrastructure/DependencyInjection.cs` dòng 22-25.
- Khi API khởi động, `await app.ApplyDatabaseMigrationsAsync()` (`Program.cs` dòng 174) tự chạy migration nếu `"Database:MigrateOnStartup": true`.

### 1.4. Cách chạy local

```bash
docker compose up -d db          # chỉ bật PostgreSQL
cd backend && dotnet run --project src/StudyAbroad.Api    # API tại http://localhost:5080, Swagger ở /swagger
cd frontend && npm install && npm run dev                  # Web tại http://localhost:3000
```

---

## PHẦN 2. DATABASE LƯU NHƯ THẾ NÀO

### 2.1. Công nghệ

- **PostgreSQL 16** (image `pgvector/pgvector:pg16` trong `docker-compose.yml`).
- Toàn bộ bảng nghiệp vụ nằm trong **schema `app`** (`AppDbContext.cs` dòng 42: `modelBuilder.HasDefaultSchema("app")`). Schema `advisor` do service AI dùng.
- **Code-First với EF Core**: không viết SQL tạo bảng bằng tay. Viết class C# (Entity) → chạy `dotnet ef migrations add Ten` → EF sinh file migration → khi chạy app thì migration apply vào DB.
- Tên bảng/cột **tự chuyển sang snake_case** nhờ `.UseSnakeCaseNamingConvention()` (`Infrastructure/DependencyInjection.cs` dòng 25). Ví dụ class `TranscriptScore` → bảng `transcript_scores`, property `StudentProfileId` → cột `student_profile_id`.

### 2.2. Ba nơi quyết định "bảng trông ra sao"

| Bước | Nơi khai báo | Ví dụ file |
|---|---|---|
| 1. Cột nào, kiểu gì | **Entity** (Domain) | `backend/src/StudyAbroad.Domain/Entities/TranscriptScore.cs` |
| 2. Khóa chính, khóa ngoại, độ dài, precision, index | **Configuration** (Infrastructure, Fluent API) | `backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/TranscriptScoreConfiguration.cs` |
| 3. Lịch sử thay đổi schema | **Migration** (Infrastructure) | `backend/src/StudyAbroad.Infrastructure/Persistence/Migrations/20261007082833_AddAcademicProfileFields.cs` |

Đăng ký DbSet (để `_context.TranscriptScores` dùng được): `backend/src/StudyAbroad.Infrastructure/Persistence/AppDbContext.cs` dòng 16-37.

### 2.3. Các bảng của UC-364 và UC-365

Mọi bảng đều có 3 cột kế thừa từ `BaseEntity` (`backend/src/StudyAbroad.Domain/Common/BaseEntity.cs`): `id uuid`, `created_at`, `updated_at`. `AppDbContext.SaveChangesAsync` tự set `updated_at` khi entity bị sửa (dòng 46-54).

#### `student_profiles` — 1 user có đúng 1 hồ sơ (unique index `user_id`)

Entity: `Domain/Entities/StudentProfile.cs`. Config: `Persistence/Configurations/StudentProfileConfiguration.cs`.

| Cột | Kiểu | Dùng cho | Ghi chú |
|---|---|---|---|
| `user_id` | uuid FK → `users.id` | chung | Cascade: xóa user thì xóa profile |
| `target_level` | varchar(32) | chung | undergraduate / graduate… |
| `grade_scale` | varchar(8) | 365 | "10", "100", "4", "letter" |
| `overall_gpa` | numeric(5,2) | 365 | **được service 365 ghi lại sau mỗi lần phân tích** |
| `annual_budget_usd` | numeric(12,2) | 364 | ngân sách/năm |
| `max_expected_tuition_usd` | numeric | 364 | học phí tối đa mong muốn (thêm ở migration `20261008052916_AddFinancialDetailsAndActivityDuration`) |
| `funding_source` | varchar(32) | 364 | family / loan / scholarship / other |
| `needs_scholarship` | boolean | 364 | |
| `financial_notes` | text | 364 | |
| `ielts, toefl, sat…` | numeric | khác (363) | |

#### `transcript_scores` — mỗi dòng là 1 môn trong 1 học kỳ (UC-365)

Entity: `Domain/Entities/TranscriptScore.cs`. Config: `Configurations/TranscriptScoreConfiguration.cs`.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `student_profile_id` | uuid FK → `student_profiles.id` | Cascade |
| `term_name` | varchar(64) NOT NULL | "Lớp 10 HK1" |
| `term_order` | int | 1..20, dùng để sắp xếp và vẽ biểu đồ |
| `subject` | varchar(128) NOT NULL | "Toán", "Vật lý" |
| `score` | numeric(6,2) | điểm hệ 10 |
| `raw_score` | varchar(16) | điểm gốc nhập thô (do UC-363 thêm) |
| `credits` | numeric(4,1) nullable | tín chỉ / hệ số |
| index | (`student_profile_id`, `term_order`) | truy vấn theo profile + kỳ nhanh |

#### `analysis_results` — kết quả phân tích, lưu JSON (UC-365)

Entity: `Domain/Entities/AnalysisResult.cs`. Config: `Configurations/AnalysisResultConfiguration.cs`.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `student_profile_id` | uuid FK | |
| `kind` | varchar(32) | `"gpa"` cho UC-365 (hằng `AcademicConstants.KindGpa`); các UC khác dùng `strengths_weaknesses`, `strategy` |
| `result_json` | **jsonb** | toàn bộ DTO kết quả serialize |
| `model_version` | varchar(64) | `"usas-gpa-v1.0"` |
| index | (`student_profile_id`, `kind`, `created_at`) | lấy "bản mới nhất theo kind" nhanh |

Vì sao lưu JSON thay vì cột cứng: cấu trúc kết quả thay đổi theo version thuật toán; mỗi lần bấm phân tích là **1 dòng mới** (giữ lịch sử); PostgreSQL vẫn index/query được bên trong jsonb.

#### `profile_activities` — ngoại khóa VÀ giải thưởng dùng chung 1 bảng (UC-364)

Entity: `Domain/Entities/ProfileActivity.cs` (bản trên nhánh 364 có thêm 3 cột cuối).

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `student_profile_id` | uuid FK | |
| `kind` | varchar | `"extracurricular"` = ngoại khóa, `"award"` = giải thưởng, `"experience"` |
| `title` | text NOT NULL | tên hoạt động / tên giải |
| `organization` | text | tổ chức / đơn vị cấp |
| `role` | text | vai trò (ngoại khóa) hoặc cấp giải (award) |
| `description` | text | |
| `start_date`, `end_date` | date | |
| `impact_level` | int 1..5 | 1 = cấp trường … 5 = quốc tế |
| `duration_months` | int 1..120 | |
| `is_ongoing` | boolean | |

Đây là kiểu **single-table**: tránh tạo 2 bảng gần giống nhau, phân biệt bằng `kind`.

#### `app_settings` — cấu hình admin dạng key/JSON (UC-365 đọc)

Entity: `Domain/Entities/AppSetting.cs`. Key `grade_scale.10` chứa bảng quy đổi hệ 10 → 4.0. Nếu chưa có dòng này, code dùng bảng WES mặc định (`AcademicAnalysisService.GetDefaultWesScaleConfig`).

### 2.4. Sơ đồ quan hệ

```
users 1 ──── 1 student_profiles 1 ──── n transcript_scores
                      │ 1
                      ├──── n analysis_results   (kind = 'gpa')
                      └──── n profile_activities (kind = 'extracurricular' | 'award')
app_settings (độc lập, key = 'grade_scale.10')
```

### 2.5. Quy trình khi cần thêm cột mới

1. Thêm property vào Entity (`Domain/Entities/*.cs`).
2. Nếu cần ràng buộc, sửa Configuration (`Infrastructure/Persistence/Configurations/*.cs`).
3. Chạy `dotnet ef migrations add TenMigration --project src/StudyAbroad.Infrastructure --startup-project src/StudyAbroad.Api`.
4. Chạy app (tự migrate) hoặc `dotnet ef database update`.

---

## PHẦN 3. BACKEND — TỪNG FOLDER LÀM GÌ, LÀM THEO THỨ TỰ NÀO

### 3.1. Quy tắc phụ thuộc giữa 4 project

```
Api ──► Application ──► Domain
 │            ▲
 └──► Infrastructure ──┘  (Infrastructure cũng tham chiếu Domain)
```

- **Domain không biết ai cả.**
- **Application chỉ biết Domain**, định nghĩa Interface nhưng không cài đặt truy cập DB.
- **Infrastructure cài đặt** các Interface của Application bằng EF Core.
- **Api** nối tất cả lại bằng Dependency Injection trong `Program.cs`.

Lợi ích để trả lời thầy: Service có thể unit test bằng cách mock Repository (không cần DB thật), đổi ORM/DB không phải sửa nghiệp vụ.

### 3.2. Thứ tự làm việc khi xây 1 tính năng (đúng thứ tự UC-365 đã làm)

| Bước | Folder | File UC-365 | File UC-364 (nhánh 364) | Làm gì |
|---|---|---|---|---|
| 1 | `Domain/Entities/` | `TranscriptScore.cs`, `AnalysisResult.cs`, `StudentProfile.cs`, `AppSetting.cs` | `ProfileActivity.cs`, `StudentProfile.cs` (thêm cột tài chính) | Khai báo/sửa class = bảng DB |
| 2 | `Domain/Constants/` | `AcademicConstants.cs` | `ProfileConstants.cs` (giới hạn validate) | Hằng số: kind, group, trend, disclaimer |
| 3 | `Infrastructure/Persistence/Configurations/` | `TranscriptScoreConfiguration.cs`, `AnalysisResultConfiguration.cs` | (dùng config sẵn có) | FK, maxLength, precision, index |
| 4 | `Infrastructure/Persistence/Migrations/` | (bảng đã có từ `CoreSchema`) | `20261008052916_AddFinancialDetailsAndActivityDuration.cs` | Sinh migration |
| 5 | `Application/Profile/<Feature>/` | `AcademicDtos.cs` | `Financial/FinancialDtos.cs` | DTO request/response (record) |
| 6 | `Application/Profile/<Feature>/` | `IAcademicAnalysisRepository.cs` | `Financial/IFinancialProfileRepository.cs` | Interface truy cập dữ liệu |
| 7 | `Application/Profile/<Feature>/` | `IAcademicAnalysisService.cs`, `AcademicAnalysisService.cs` | `Financial/IFinancialProfileService.cs`, `FinancialProfileService.cs` | Nghiệp vụ, validation, thuật toán |
| 8 | `Infrastructure/Persistence/Repositories/` | `AcademicAnalysisRepository.cs` | `FinancialProfileRepository.cs` | Cài đặt interface bước 6 bằng EF Core |
| 9 | `Api/Controllers/` | `AcademicAnalysisController.cs` | `FinancialProfileController.cs` | Endpoint HTTP |
| 10 | `Application/DependencyInjection.cs` và `Infrastructure/DependencyInjection.cs` | dòng 14-15 và 31-32 | tương tự | `AddScoped<IX, X>()` |
| 11 | `tests/StudyAbroad.UnitTests/` | `AcademicAnalysisServiceTests.cs` | `FinancialProfileServiceTests.cs` | Unit test service |

### 3.3. Chi tiết từng tầng với code thật

#### Tầng Domain — `backend/src/StudyAbroad.Domain/`

- `Common/BaseEntity.cs`: class cha, cho `Id`, `CreatedAt`, `UpdatedAt`.
- `Entities/*.cs`: POCO thuần, **không** attribute `[Table]`, **không** navigation property (chỉ lưu `StudentProfileId` kiểu Guid). Lý do: entity gọn, không lazy-load ngoài ý muốn; quan hệ được khai báo ở Configuration.
- `Constants/AcademicConstants.cs`: `KindGpa = "gpa"`, `GroupNaturalSciences = "natural_sciences"`, `TrendUpward = "upward"`, `DefaultDisclaimer`… Dùng hằng số để FE/BE/DB thống nhất chuỗi, tránh gõ sai.

#### Tầng Application — `backend/src/StudyAbroad.Application/Profile/Academic/`

**`AcademicDtos.cs`** — DTO là `record`:

- `TranscriptScoreDto` (trả ra): có thêm `Gpa4`, `SubjectGroup`, `SubjectGroupName` là field **tính toán**, không có trong DB.
- `UpsertTranscriptScoreItem` + `BatchTranscriptScoresRequest` (nhận vào): `Id` nullable → null = thêm mới, có giá trị = sửa.
- `AcademicAnalysisResponseDto`: kết quả phân tích đầy đủ.
- `GradeScaleConfigDto` + `GradeScaleRuleDto`: bảng quy đổi.

Vì sao không trả Entity thẳng: ẩn field nội bộ, gộp dữ liệu tính toán, thay đổi DB không làm vỡ API.

**`IAcademicAnalysisRepository.cs`** — 8 method: `GetProfileByUserIdAsync`, `EnsureProfileExistsAsync`, `GetScoresByProfileIdAsync`, `SaveScoresAsync`, `DeleteScoresAsync`, `GetAppSettingByKeyAsync`, `SaveAnalysisResultAsync`, `GetLatestAnalysisAsync`. Chỉ là hợp đồng, không có code DB.

**`AcademicAnalysisService.cs`** — toàn bộ nghiệp vụ:

| Method | Dòng | Làm gì |
|---|---|---|
| `GetScoresAsync` | 28-58 | Lấy điểm, tính `Gpa4` + nhóm môn cho từng dòng |
| `SaveScoresAsync` | 63-104 | Validate (term 1-20, điểm 0-10, tín chỉ 0-30), map DTO → Entity, gọi repo |
| `DeleteScoreAsync` | 109-116 | Xóa 1 điểm, 404 nếu chưa có profile |
| `AnalyzeAcademicProfileAsync` | 126-267 | **Thuật toán chính** (xem 3.4) |
| `GetLatestAnalysisAsync` | 272-308 | Đọc `result_json` mới nhất, parse lại thành DTO |
| `GetGradeScaleConfigAsync` | 314-335 | Đọc `app_settings`, fallback WES |
| `ConvertScoreToGpa4` | 340-360 | static, quy đổi 1 điểm |
| `ClassifySubjectGroup` | 389-432 | static, phân nhóm theo từ khóa đã bỏ dấu |
| `EvaluateTrend` | 459-495 | static, xu hướng |

Các method `static` là để unit test gọi trực tiếp không cần khởi tạo service.

#### Tầng Infrastructure — `backend/src/StudyAbroad.Infrastructure/`

- `Persistence/AppDbContext.cs`: khai báo `DbSet<T>` cho mọi bảng; `OnModelCreating` set schema `app` và `ApplyConfigurationsFromAssembly` (tự quét mọi class `IEntityTypeConfiguration`).
- `Persistence/Configurations/*.cs`: Fluent API cho từng bảng.
- `Persistence/Repositories/AcademicAnalysisRepository.cs`: cài đặt interface, chỉ dùng LINQ trên `_context`. Điểm đáng chú ý:
  - Mọi `Where` đều có `s.StudentProfileId == profileId` → không sửa/xóa được dữ liệu người khác (chống IDOR tầng DB).
  - `SaveScoresAsync` (dòng 86-118) là **batch upsert**: 1 query lấy các Id đã tồn tại thành Dictionary → sửa hoặc Add → **1 lần `SaveChangesAsync`** = 1 transaction.
  - `SaveAnalysisResultAsync` (dòng 148-162) vừa thêm `analysis_results` vừa cập nhật `student_profiles.overall_gpa` trong cùng 1 `SaveChangesAsync`.
  - `EnsureProfileExistsAsync` (dòng 33-69) tự tạo profile (và user dev) nếu chưa có.
- `Persistence/Migrations/`: file sinh tự động, mỗi file có `Up()` (áp dụng) và `Down()` (hoàn tác).
- `DependencyInjection.cs`: đăng ký DbContext + Repository + JWT + SMTP.

#### Tầng Api — `backend/src/StudyAbroad.Api/`

- `Program.cs`: lắp ráp mọi thứ (DI, JWT, CORS, Swagger, migration lúc khởi động).
- `appsettings.json`: connection string, CORS, JWT, Advisor URL.
- `Controllers/AcademicAnalysisController.cs`: `[Route("api/v1/profile/academic")]`, mỗi action chỉ: lấy userId → gọi service → trả `Ok` / `Problem(400)` / `NotFound`. Không có nghiệp vụ.

Bảng endpoint của 2 UC:

| UC | Method | Route | Trả về |
|---|---|---|---|
| 365 | GET | `/api/v1/profile/academic/scores` | `TranscriptScoreDto[]` |
| 365 | POST | `/api/v1/profile/academic/scores` | batch upsert, trả danh sách mới |
| 365 | DELETE | `/api/v1/profile/academic/scores/{scoreId}` | 204 |
| 365 | POST | `/api/v1/profile/academic/analyze` | `AcademicAnalysisResponseDto` |
| 365 | GET | `/api/v1/profile/academic/analysis` | bản mới nhất, 404 nếu chưa có |
| 365 | GET | `/api/v1/profile/academic/scale-config` | `GradeScaleConfigDto` |
| 364 | GET / PUT | `/api/v1/profile/financial` | `FinancialProfileDto` |
| 364 | GET / POST | `/api/v1/profile/extracurricular` | ngoại khóa |
| 364 | PUT / DELETE | `/api/v1/profile/extracurricular/{id}` | |
| 364 | GET / POST | `/api/v1/profile/achievements` | giải thưởng |
| 364 | DELETE | `/api/v1/profile/achievements/{id}` | |
| 364 | GET | `/api/v1/profile/summary` | tổng quan cho AI Advisor |

Khác biệt nhỏ giữa 2 controller: 364 dùng **primary constructor** C# 12 (`class X(IFinancialProfileService s)`) và validate bằng **DataAnnotations** (`[Range]`, `[Required]`) + `ModelState.IsValid`; 365 dùng constructor thường và validate thủ công trong Service. Cả hai đều trả lỗi chuẩn **RFC 7807 ProblemDetails** (`title`, `detail`, `status`).

### 3.4. Thuật toán UC-365 (`AnalyzeAcademicProfileAsync`) — tóm tắt để trình bày

1. **Lấy bảng quy đổi**: đọc `app_settings["grade_scale.10"]`; không có → WES mặc định: 9.0-10 → 4.0 (A); 8.5-8.99 → 3.7 (A-); 8.0-8.49 → 3.5 (B+); 7.0-7.99 → 3.0 (B); 6.5-6.99 → 2.5 (C+); 5.5-6.49 → 2.0 (C); 5.0-5.49 → 1.5 (D+); 4.0-4.99 → 1.0 (D); <4 → 0 (F). Hỗ trợ `Method = "linear_0.4"` (điểm × 0.4).
2. **Unweighted GPA** = trung bình `Gpa4` tất cả môn.
3. **Weighted GPA**: có tín chỉ → Σ(Gpa4 × credits) / Σcredits; không có tín chỉ → môn chứa "chuyên / nâng cao / AP / IB / Honors" được +0.5, trần 4.5.
4. **Phân nhóm môn** sau khi bỏ dấu tiếng Việt: kiểm Ngoại ngữ → Xã hội → STEM → Khác. Kiểm Xã hội trước để "Địa lý" không bị bắt nhầm vào "lý" (Vật lý).
5. **Trung bình theo kỳ** (group by `TermOrder`) → **xu hướng**: chia dãy kỳ làm 2 nửa, delta = TB nửa sau − TB nửa đầu; ≥ +0.15 → upward; ≤ −0.15 → downward; còn lại consistent; < 2 kỳ → chưa đủ dữ liệu.
6. **Lưu**: thêm dòng `analysis_results(kind="gpa", result_json)` và cập nhật `student_profiles.overall_gpa` = Unweighted GPA để module gợi ý trường dùng.
7. Trả về `AcademicAnalysisResponseDto` kèm `Disclaimer` bắt buộc (quy đổi tham khảo, mỗi trường tính riêng).

### 3.5. Luồng UC-364 (`FinancialProfileService`) — tóm tắt

- **Tài chính**: `SaveFinancialProfileAsync` validate ngân sách ≥ 0 và ≤ `MaxAnnualBudget`, `FundingSource` bắt buộc → `EnsureProfileExistsAsync` → ghi 5 cột tài chính vào `student_profiles` → `UpdateProfileAsync`. GET trả `null` nếu `AnnualBudgetUsd` chưa nhập.
- **Ngoại khóa**: tạo `ProfileActivity { Kind = "extracurricular", Title = ActivityName, Role, Organization, ImpactLevel, DurationMonths, IsOngoing, StartDate, EndDate }`. Khi sửa/xóa: lấy activity theo Id rồi **kiểm tra `activity.StudentProfileId == profile.Id`**, sai → trả false → 404.
- **Giải thưởng**: cùng bảng, `Kind = "award"`, `Role` lưu `Category`, `Organization` lưu `Issuer`.
- **Summary**: gộp tài chính + ngoại khóa + giải thưởng + `MaxImpactLevel` cho AI Advisor đọc 1 lần.

---

## PHẦN 4. FRONTEND — TỪNG FOLDER LÀM GÌ, LÀM THEO THỨ TỰ NÀO

### 4.1. Cấu trúc `frontend/src/`

| Folder | Vai trò | File của 365 | File của 364 (nhánh 364) |
|---|---|---|---|
| `types/` | Interface TS = bản sao DTO của BE, camelCase | `types/academic.ts` | `types/profile.ts` |
| `lib/` | Hàm gọi API; `api.ts` là wrapper chung | `lib/academic-api.ts` | `lib/profile-api.ts` |
| `components/<feature>/` | Component UI nhận props, không tự fetch (trừ View) | `components/academic/AcademicAnalysisView.tsx`, `GpaSummaryCard.tsx`, `SubjectGroupBreakdown.tsx`, `TermTrendChart.tsx`, `TranscriptScoreTable.tsx`, `ReadOnlyTranscriptTable.tsx` | `components/profile/FinancialCard.tsx`, `ExtracurricularSection.tsx`, `AchievementsSection.tsx`, `FinancialProfileTabs.tsx` |
| `app/<route>/page.tsx` | Trang, đường dẫn thư mục = URL | `app/profile/academic/page.tsx` (URL `/profile/academic`), `app/profile/academic/analysis/page.tsx` (URL `/profile/academic/analysis`) | `app/profile/financial/page.tsx` (URL `/profile/financial`) |
| `components/auth/` | Dùng chung | `ProtectedRoute.tsx` | |
| `contexts/` | Dùng chung | `AuthContext.tsx` (giữ user, gọi `/auth/me`) | |

### 4.2. Thứ tự làm việc khi xây 1 màn hình

1. **`types/academic.ts`** — chép DTO của BE sang TS. Quy tắc đổi kiểu: `Guid` → `string`; `decimal`/`int` → `number`; `decimal?` → `number | null`; `DateTime` → `string` (ISO); `List<T>` → `T[]`; tên PascalCase → camelCase (ASP.NET mặc định serialize camelCase nên khớp tự động).
2. **`lib/academic-api.ts`** — mỗi endpoint 1 hàm async, gọi `apiFetch<T>(path, init)`, bắt `ApiError` để đổi thành thông báo tiếng Việt. Ví dụ `getLatestAcademicAnalysis` trả `null` khi 404 (chưa phân tích = hợp lệ), ném lỗi với mã khác.
3. **`components/academic/*.tsx`** — component hiển thị, nhận dữ liệu qua props (`GpaSummaryCard({ analysis })`). Component "View" (`AcademicAnalysisView.tsx`) là nơi duy nhất gọi API trong `useEffect`, giữ state `loading / error / data`, rồi truyền xuống các card.
4. **`app/profile/academic/analysis/page.tsx`** — bọc `<ProtectedRoute allowedRoles={["student","parent"]}>` + breadcrumb + render View.

### 4.3. `lib/api.ts` — trái tim kết nối FE → BE

```ts
const baseUrl = typeof window === "undefined" ? (process.env.API_INTERNAL_URL ?? "http://localhost:5080") : "";
```
- Trong trình duyệt `baseUrl = ""` → gọi `/api/v1/...` cùng origin → Next rewrite / Nginx chuyển tiếp.
- Khi render trên server → gọi thẳng `API_INTERNAL_URL`.

```ts
export async function apiFetch<T>(path, init) {
  const res = await fetch(`${baseUrl}${path}`, { ...init, credentials: "include", headers: { "Content-Type": "application/json", ...init?.headers }, cache: "no-store" });
  if (!res.ok) { const body = await res.json().catch(() => null); throw new ApiError(res.status, body?.detail ?? body?.title ?? body?.message ?? res.statusText); }
  if (res.status === 204) return {} as T;
  return res.json();
}
```
- `credentials: "include"` → gửi cookie `usas_access_token` (JS không đọc được → chống XSS).
- Lỗi được gói thành `ApiError(status, message)`; `message` lấy từ `detail` của ProblemDetails do BE trả → hiện thẳng cho người dùng.
- `cache: "no-store"` → không cache dữ liệu hồ sơ.

### 4.4. Luồng trong `AcademicAnalysisView.tsx`

1. `useEffect` chạy 1 lần khi mount → `Promise.all([getTranscriptScores(), getLatestAcademicAnalysis()])` gọi song song 2 API.
2. Tính `isStale = scores.length > 0 && (!analysis || analysis.totalSubjects !== scores.length)` → nếu bảng điểm đã đổi so với lần phân tích trước thì tự gọi `triggerAcademicAnalysis()` (POST `/analyze`).
3. Cờ `ignore` trong cleanup để không `setState` sau khi component unmount.
4. Có `handleRetry` khi lỗi mạng; phân biệt "chưa có dữ liệu" (hiện hướng dẫn nhập) và "lỗi tải" (hiện nút thử lại).
5. Truyền `analysis` xuống `GpaSummaryCard`, `SubjectGroupBreakdown`, `TermTrendChart`; truyền `scores` xuống `ReadOnlyTranscriptTable`.

### 4.5. FE của UC-364 (`lib/profile-api.ts`, nhánh 364)

Cùng mẫu: mỗi endpoint 1 hàm; GET trả `null`/`[]` khi 404 hoặc 401; POST/PUT/DELETE ném lỗi để form hiển thị. Có thêm `getAuthHeaders()` gắn header `X-User-Id` từ `localStorage("usas_user")` để test không cần cookie (BE chấp nhận header này ở môi trường dev).

---

## PHẦN 5. ĐI THEO 1 REQUEST TỪ ĐẦU ĐẾN CUỐI

### 5.1. UC-365: người dùng mở `/profile/academic/analysis`

| # | Ở đâu | File | Việc xảy ra |
|---|---|---|---|
| 1 | Trình duyệt | `app/profile/academic/analysis/page.tsx` | Next render page; `ProtectedRoute` kiểm tra user từ `AuthContext`, không có → redirect `/login` |
| 2 | Trình duyệt | `components/academic/AcademicAnalysisView.tsx` | `useEffect` gọi `getTranscriptScores()` và `getLatestAcademicAnalysis()` |
| 3 | Trình duyệt | `lib/academic-api.ts` → `lib/api.ts` | `fetch("/api/v1/profile/academic/scores", { credentials: "include" })` |
| 4 | Next dev server | `next.config.ts` | rewrite sang `http://localhost:5080/api/v1/profile/academic/scores` |
| 5 | .NET | `Program.cs` | middleware: CORS → Authentication (đọc cookie → JWT → kiểm tra session chưa thu hồi) → Authorization → routing |
| 6 | .NET | `Controllers/AcademicAnalysisController.cs` `GetScores` | `GetCurrentUserId()` từ claim → `_service.GetScoresAsync(userId)` |
| 7 | .NET | `Application/.../AcademicAnalysisService.cs` `GetScoresAsync` | `_repository.GetProfileByUserIdAsync` → `GetScoresByProfileIdAsync` → `GetGradeScaleConfigAsync` → map từng điểm sang DTO kèm `Gpa4`, nhóm môn |
| 8 | .NET | `Infrastructure/.../AcademicAnalysisRepository.cs` | LINQ `_context.TranscriptScores.Where(s => s.StudentProfileId == profileId)` → EF sinh SQL `SELECT ... FROM app.transcript_scores WHERE student_profile_id = @p0 ORDER BY term_order, subject` |
| 9 | PostgreSQL | bảng `app.transcript_scores` | trả rows |
| 10 | .NET → FE | | Controller `Ok(list)` → JSON camelCase → `apiFetch` parse → `setScores` → React render `ReadOnlyTranscriptTable` |
| 11 | FE | View | Nếu `isStale` → POST `/analyze` → Service chạy thuật toán → Repository ghi `analysis_results` + `student_profiles.overall_gpa` → trả DTO → render 3 card |

### 5.2. UC-364: người dùng bấm "Lưu ngân sách" ở `/profile/financial`

| # | File | Việc xảy ra |
|---|---|---|
| 1 | `components/profile/FinancialCard.tsx` | Form validate inline (ngân sách ≥ 0, chọn nguồn tài chính), gọi `saveFinancialProfile(data)` |
| 2 | `lib/profile-api.ts` | `apiFetch("/api/v1/profile/financial", { method: "PUT", body: JSON.stringify(data) })` |
| 3 | `Controllers/FinancialProfileController.cs` `SaveFinancialProfile` | `GetCurrentUserId()`; `ModelState.IsValid` (DataAnnotations trên `SaveFinancialProfileRequest`) sai → 400 `ValidationProblem` |
| 4 | `Application/Profile/Financial/FinancialProfileService.cs` | Validate lại lần 2 → `EnsureProfileExistsAsync(userId)` → gán `AnnualBudgetUsd`, `MaxExpectedTuitionUsd`, `FundingSource`, `NeedsScholarship`, `FinancialNotes` → `UpdateProfileAsync` |
| 5 | `Infrastructure/.../FinancialProfileRepository.cs` | `db.StudentProfiles.Update(profile); SaveChangesAsync()` → SQL `UPDATE app.student_profiles SET annual_budget_usd=..., updated_at=... WHERE id=...` |
| 6 | FE | Nhận `FinancialProfileDto` → hiện trạng thái đã lưu |

---

## PHẦN 6. BẢNG TRA NHANH: FILE → VAI TRÒ (UC-365, nhánh hiện tại)

| Đường dẫn | Tầng | 1 câu mô tả |
|---|---|---|
| `backend/src/StudyAbroad.Domain/Common/BaseEntity.cs` | Domain | Id/CreatedAt/UpdatedAt cho mọi entity |
| `backend/src/StudyAbroad.Domain/Entities/TranscriptScore.cs` | Domain | Bảng điểm từng môn |
| `backend/src/StudyAbroad.Domain/Entities/AnalysisResult.cs` | Domain | Kết quả phân tích JSON |
| `backend/src/StudyAbroad.Domain/Entities/StudentProfile.cs` | Domain | Hồ sơ học sinh (có `OverallGpa`, cột tài chính) |
| `backend/src/StudyAbroad.Domain/Entities/AppSetting.cs` | Domain | Cấu hình key/JSON (bảng quy đổi) |
| `backend/src/StudyAbroad.Domain/Constants/AcademicConstants.cs` | Domain | Hằng số kind/group/trend/disclaimer |
| `backend/src/StudyAbroad.Application/Profile/Academic/AcademicDtos.cs` | Application | 8 record DTO |
| `backend/src/StudyAbroad.Application/Profile/Academic/IAcademicAnalysisRepository.cs` | Application | Hợp đồng truy cập DB |
| `backend/src/StudyAbroad.Application/Profile/Academic/IAcademicAnalysisService.cs` | Application | Hợp đồng nghiệp vụ |
| `backend/src/StudyAbroad.Application/Profile/Academic/AcademicAnalysisService.cs` | Application | Thuật toán GPA/nhóm môn/xu hướng |
| `backend/src/StudyAbroad.Application/DependencyInjection.cs` | Application | `AddScoped<IAcademicAnalysisService, AcademicAnalysisService>` |
| `backend/src/StudyAbroad.Infrastructure/Persistence/AppDbContext.cs` | Infrastructure | DbSet + schema `app` |
| `backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/TranscriptScoreConfiguration.cs` | Infrastructure | FK/precision/index bảng điểm |
| `backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/AnalysisResultConfiguration.cs` | Infrastructure | jsonb + index |
| `backend/src/StudyAbroad.Infrastructure/Persistence/Configurations/StudentProfileConfiguration.cs` | Infrastructure | unique `user_id`, precision các cột |
| `backend/src/StudyAbroad.Infrastructure/Persistence/Repositories/AcademicAnalysisRepository.cs` | Infrastructure | LINQ/EF truy vấn thật |
| `backend/src/StudyAbroad.Infrastructure/Persistence/Migrations/*.cs` | Infrastructure | Lịch sử schema |
| `backend/src/StudyAbroad.Infrastructure/DependencyInjection.cs` | Infrastructure | DbContext + `AddScoped<IAcademicAnalysisRepository, AcademicAnalysisRepository>` |
| `backend/src/StudyAbroad.Api/Program.cs` | Api | Lắp ráp DI, JWT cookie, CORS, Swagger, auto-migrate |
| `backend/src/StudyAbroad.Api/appsettings.json` | Api | Connection string, CORS, JWT |
| `backend/src/StudyAbroad.Api/Controllers/AcademicAnalysisController.cs` | Api | 6 endpoint `/api/v1/profile/academic/*` |
| `backend/tests/StudyAbroad.UnitTests/AcademicAnalysisServiceTests.cs` | Test | 91 test cho service |
| `frontend/next.config.ts` | FE | Rewrite `/api` → BE khi dev |
| `frontend/src/lib/api.ts` | FE | `apiFetch`, `ApiError`, cookie |
| `frontend/src/types/academic.ts` | FE | Interface = DTO |
| `frontend/src/lib/academic-api.ts` | FE | 6 hàm gọi 6 endpoint |
| `frontend/src/components/academic/AcademicAnalysisView.tsx` | FE | Fetch + state + lắp card |
| `frontend/src/components/academic/GpaSummaryCard.tsx` | FE | Card GPA + nhãn xu hướng |
| `frontend/src/components/academic/SubjectGroupBreakdown.tsx` | FE | Điểm theo nhóm môn |
| `frontend/src/components/academic/TermTrendChart.tsx` | FE | Biểu đồ cột theo kỳ |
| `frontend/src/components/academic/TranscriptScoreTable.tsx` | FE | Bảng nhập/sửa/xóa điểm |
| `frontend/src/components/academic/ReadOnlyTranscriptTable.tsx` | FE | Bảng điểm chỉ đọc |
| `frontend/src/app/profile/academic/page.tsx` | FE | Route `/profile/academic` |
| `frontend/src/app/profile/academic/analysis/page.tsx` | FE | Route `/profile/academic/analysis` |
| `frontend/src/components/auth/ProtectedRoute.tsx` | FE chung | Chặn chưa login / sai role |

File UC-364 (nhánh `feature/USAS-364-financial-profile-ducnx`): `Domain/Entities/ProfileActivity.cs`, `Application/Profile/Financial/{FinancialDtos, IFinancialProfileRepository, IFinancialProfileService, FinancialProfileService}.cs`, `Infrastructure/Persistence/Repositories/FinancialProfileRepository.cs`, `Infrastructure/Persistence/Migrations/20261008052916_AddFinancialDetailsAndActivityDuration.cs`, `Api/Controllers/FinancialProfileController.cs`, `tests/.../FinancialProfileServiceTests.cs`, `frontend/src/types/profile.ts`, `frontend/src/lib/profile-api.ts`, `frontend/src/components/profile/{FinancialCard, ExtracurricularSection, AchievementsSection, FinancialProfileTabs}.tsx`, `frontend/src/app/profile/financial/page.tsx`.

---

## PHẦN 7. CÂU HỎI THẦY HAY HỎI — TRẢ LỜI NGẮN

1. **Kiến trúc BE là gì?** Clean Architecture 4 tầng Domain → Application → Infrastructure → Api; phụ thuộc hướng vào trong; Interface ở Application, cài đặt ở Infrastructure.
2. **Entity khác DTO chỗ nào?** Entity ánh xạ bảng DB (Domain). DTO là hợp đồng với client (Application), ẩn field nội bộ, thêm field tính toán (`Gpa4`, `SubjectGroup`).
3. **Vì sao Repository có Interface?** Để Service không phụ thuộc EF Core, có thể mock khi unit test, đổi DB không sửa nghiệp vụ.
4. **DI đăng ký ở đâu?** `Application/DependencyInjection.cs` (service) và `Infrastructure/DependencyInjection.cs` (repository, DbContext); `Program.cs` gọi `AddApplication()` + `AddInfrastructure()`. Vòng đời `Scoped` = 1 instance/request.
5. **Tên bảng snake_case từ đâu?** `UseSnakeCaseNamingConvention()` của EFCore.NamingConventions; schema `app` set trong `AppDbContext.OnModelCreating`.
6. **Migration là gì, chạy khi nào?** File C# ghi `Up/Down` thay đổi schema, sinh bằng `dotnet ef migrations add`; `Program.cs` tự apply khi khởi động nếu `MigrateOnStartup = true`.
7. **Chống IDOR như thế nào?** Controller lấy userId từ JWT claim; Service tìm profile theo userId; Repository luôn lọc `StudentProfileId == profile.Id`. Body không bao giờ chứa userId/profileId.
8. **Validation mấy lớp?** FE inline; BE DataAnnotations (364) hoặc kiểm tra trong Service (365); lỗi trả ProblemDetails RFC 7807; FE đọc `detail` hiển thị.
9. **JWT lưu ở đâu?** HttpOnly cookie `usas_access_token`; FE gửi bằng `credentials: "include"`; BE đọc trong `OnMessageReceived`; có bảng `user_sessions` để thu hồi phiên.
10. **FE gọi BE qua đâu?** Cùng origin `/api/*`; dev dùng Next rewrite (`next.config.ts`), prod dùng Nginx; CORS chỉ là dự phòng.
11. **Vì sao `analysis_results` lưu jsonb?** Cấu trúc kết quả thay đổi theo `model_version`, giữ lịch sử nhiều lần phân tích, Postgres vẫn query được.
12. **Vì sao ngoại khóa và giải thưởng chung bảng?** Cấu trúc giống nhau, phân biệt bằng `kind`; giảm số bảng và join.
13. **Weighted khác Unweighted GPA?** Unweighted = trung bình đều; Weighted = theo tín chỉ hoặc +0.5 cho môn chuyên/nâng cao (trần 4.5).
14. **Xu hướng tính sao?** So trung bình GPA nửa kỳ sau với nửa kỳ đầu, ngưỡng ±0.15.
15. **Unit test test gì?** Các method static (`ConvertScoreToGpa4`, `ClassifySubjectGroup`, `EvaluateTrend`) và Service với Repository mock; 91 test cho 365, 48 test cho 364.
