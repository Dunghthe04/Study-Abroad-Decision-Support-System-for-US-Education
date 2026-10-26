# Rà soát sprint v0.1 và ghi chú chuyển giao

*Ngày rà soát: 27/10/2026 · Branch: `fix/security-v0.1` · Người rà soát: Tuấn Việt*

Tài liệu này so 24 work item của sprint v0.1 (Jira USAS-1975 → 1996, 2064, 2065) với code hiện tại ở backend (.NET), frontend (Next.js) và advisor (Python), kèm phần kiểm tra bảo mật của từng chức năng.

- **Phần của Việt** (UC-01 → UC-15) đã sửa trên branch này. Mục 3 tóm tắt các thay đổi.
- **Phần của thành viên khác** chỉ được rà soát, không sửa code. Mục 4 liệt kê việc cần làm cho từng người.

## 1. Trạng thái work item

| Jira | Use case | Người | Trạng thái | Ghi chú |
|---|---|---|---|---|
| 1975 | UC-01 Đăng ký | Việt | ✅ | Đăng ký trùng → thông báo chung + mail báo chủ tài khoản |
| 1976 | UC-02 Xác thực OTP | Việt | ✅ | 5 lần sai / 5 lần gửi mỗi giờ |
| 1977 | UC-03 Đăng nhập | Việt | ✅ | Đã sửa lộ tồn tại tài khoản, thêm rate limit |
| 1978 | UC-04 Đăng xuất | Việt | ✅ | |
| 2064 | UC-05 Quên mật khẩu | Việt | ✅ | Đặt lại → đăng xuất mọi thiết bị + mail thông báo |
| 2065 | UC-06 Đổi mật khẩu | Việt | ✅ (mới) | Bắt mật khẩu cũ, giữ phiên hiện tại, đăng xuất thiết bị khác |
| 1979 | UC-07 Tổng quan hồ sơ | Việt | ✅ | Bước 6 Review của wizard |
| 1980 | UC-08 Thông tin học vấn | Việt | ✅ | Thêm validate độ dài, năm tốt nghiệp |
| 1981 | UC-09 Thêm điểm | Việt | ✅ | GPA chỉ tính phía server |
| 1982 | UC-10 Sửa/xóa điểm | Việt | ✅ | Qua nháp wizard |
| 1983 | UC-11 Upload bảng điểm | Việt | ✅ | Không lộ đường dẫn lưu file |
| 1984 | UC-12 Xác nhận trích xuất | Việt | ✅ | Ô cam + nút "Tôi đã kiểm tra bảng điểm" |
| 1985/1986 | UC-13/14 Điểm thi | Việt | ✅ | |
| 1987 | UC-15 Tài chính | Việt | ✅ (wizard) | API cũ của Đức xem mục 4.1 |
| 1988–1990 | UC-16/17/18 | Đức | ✅ wizard / ⚠️ API cũ | Xem mục 4.1 |
| 1991 | UC-22 | Dũng | ✅ | Xem mục 4.2 |
| 1992 | UC-23 | Dương | ✅ | |
| 1993 | UC-68 Bảng quy đổi GPA | Đức | ❌ | Đã chốt: quy đổi **tuyến tính** (/10 × 4) |
| 1994 | UC-69 Ngưỡng gợi ý | Dũng | ❌ | Đã chốt: ngưỡng theo dữ liệu từng trường, admin không sửa |
| 1995 | WI-09 | Phương Anh | ⚠️ | Xem mục 4.4 |
| 1996 | WI-10 | Phương Anh | ❌ | Chưa có phiếu Feedback Capture Grid |

## 2. Quyết định nghiệp vụ đã chốt

| # | Nội dung | Quyết định |
|---|---|---|
| Q1 | Giới hạn OTP | 5 lần sai, 5 lần gửi mỗi giờ (giữ code, sửa checklist) |
| Q2 | Độ dài mật khẩu | 8–100 ký tự |
| Q3 | Đăng ký trùng email/SĐT | Thông báo chung + mail "bạn đã có tài khoản" |
| Q4 | Đổi mật khẩu khi đã đăng nhập | Bắt nhập mật khẩu cũ; giữ phiên hiện tại, đăng xuất thiết bị khác, gửi mail |
| Q4 | Quên mật khẩu qua OTP | Đăng xuất mọi thiết bị + gửi mail thông báo |
| Q5 | Tài khoản trung tâm `pending` | Vẫn đăng nhập được |
| Q6 | JWT / khóa tài khoản | JWT 7 ngày; user bị khóa → thu hồi phiên ngay |
| Q7 | Quy đổi GPA | Tuyến tính /10 × 4 (áp dụng cho UC-18, UC-68) |
| Q8 | Ngưỡng gợi ý | Theo dữ liệu từng trường (UC-69) |
| Q9 | UC-12 | Tô cam ô đọc từ ảnh/scan, điểm bất thường, ô ghi đè/trùng môn; một nút xác nhận chung |
| Q10 | UC-07 | Bước 6 Review là đủ |
| Q11 | UC-01 đăng ký theo độ tuổi | Dưới 16 vẫn dùng được nhưng chưa lưu/phân tích hồ sơ cho tới khi phụ huynh đồng ý qua email; 16–17 dùng đủ trừ chia sẻ hồ sơ cho trung tâm; từ 18 như phụ huynh; tài khoản cũ chưa có ngày sinh không hạn chế; bỏ tự đăng ký trung tâm |

### Q11 chi tiết: độ tuổi và đồng ý của phụ huynh

**Căn cứ pháp lý:**

- Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (hiệu lực 01/01/2026), Điều 24: xử lý dữ liệu của trẻ em cần người đại diện theo pháp luật đồng ý; rút đồng ý thì phải ngừng xử lý.
- Luật Trẻ em 2016: trẻ em là người dưới 16 tuổi. Bộ luật Dân sự: dưới 18 là người chưa thành niên.
- Nghị định 356/2025/NĐ-CP, Điều 6: đồng ý phải kiểm chứng được (ai, lúc nào, nội dung gì) và không được tick sẵn.

**Bảng nhóm tuổi** (tuổi tính theo ngày sinh, giờ Việt Nam, ở mỗi request nên tự hết hạn chế khi đủ tuổi):

| Nhóm | Đăng ký | Bị chặn |
|---|---|---|
| Dưới 16, chưa có phụ huynh đồng ý | Bắt buộc ngày sinh + email phụ huynh | Lưu hồ sơ (wizard), upload bảng điểm, gợi ý trường, chat trợ lý AI, chia sẻ cho trung tâm |
| Dưới 16, phụ huynh đã đồng ý | | Chia sẻ cho trung tâm |
| 16–17 | Bắt buộc ngày sinh, email phụ huynh tùy chọn | Chia sẻ cho trung tâm |
| Từ 18 | Bắt buộc ngày sinh | Không |
| Phụ huynh | Bắt buộc ngày sinh, phải từ 18 tuổi | Không |

**Luồng liên kết:** học sinh xác thực OTP xong thì hệ thống gửi mail mời tới phụ huynh (token một lần, lưu dạng SHA-256, hết hạn 7 ngày). Phụ huynh đăng nhập/đăng ký tài khoản phụ huynh đúng email được mời, đọc nội dung đồng ý và bấm "Đồng ý". Hệ thống ghi `consents` (`granted_by_user_id`, `policy_version`, `granted_ip`) và chuyển link sang `active`. Phụ huynh rút đồng ý → `consents.granted = false`, link `revoked`, học sinh dưới 16 quay về chế độ hạn chế; dữ liệu cũ giữ nguyên. Lịch sử link và consent không bao giờ bị xóa.

**Code:** bảng quy tắc duy nhất ở `backend/src/StudyAbroad.Domain/Constants/AgeFeatures.cs`. Mỗi dòng tự thành một policy cùng tên (`Program.cs`); bị chặn trả 403 `{ code: "age_restricted", feature, needParentConsent }`. `GET /api/v1/auth/me` trả `ageGroup`, `parentConsentStatus`, `blockedFeatures` để frontend ẩn nút (`<AgeGate>`, `useAgeBlock`).

## 3. Đã sửa trên branch `fix/security-v0.1` (phần của Việt)

| Commit | Nội dung |
|---|---|
| `0280b7f` | Bỏ SMTP khỏi `appsettings.Development.json`, chỉ đọc `SMTP_*` từ `.env`; script `scripts/run-backend.*` nạp `.env` khi chạy ngoài Docker; mật khẩu admin seed lấy từ `SEED_ADMIN_PASSWORD` (thiếu thì không seed) |
| `0cc3112` | Xóa `PUT /api/v1/profile/academic` và form cũ `AcademicProfileForm` (không còn ai gọi) |
| `4fffb09` | Rate limit `/api/v1/auth/*` (backend + nginx); chống dò tài khoản; khóa đếm song song bằng `xmin`; token chỉ trong cookie; chặn open-redirect; email không nằm trong URL |
| `a4203a3` | UC-06 đổi mật khẩu khi đã đăng nhập |
| `6447717` | GPA chỉ tính phía server; giới hạn kích thước nháp; validate độ dài; ẩn đường dẫn file |
| `2203e2d` | UC-12 tô cam ô cần kiểm tra + nút "Tôi đã kiểm tra bảng điểm"; backend chặn commit khi còn ô chưa xác nhận |
| `f71900a` | Q11: cột `users.date_of_birth`, bảng `parent_student_links`, `consents` thêm `granted_by_user_id` / `granted_ip` |
| `b630f66` | Q11: đăng ký theo độ tuổi, mời phụ huynh qua email sau khi xác thực OTP, bỏ tự đăng ký trung tâm |
| `f2a7b43` | Q11: trang `/parent-consent`, gửi lại / đổi email mời, rút đồng ý, mục liên kết ở trang tài khoản |
| `c6e6c69` | Q11: chặn chức năng theo tuổi qua `AgeFeatures.Rules` (backend policy + frontend `AgeGate`) |

### ⚠️ Việc cần làm ngay (không sửa được bằng code)

Commit `4b3665d` (23/10) đã đẩy địa chỉ Gmail và **mật khẩu ứng dụng Gmail thật** vào `appsettings.Development.json` trên `origin/develop`. Branch này đã xóa khối đó, nhưng giá trị vẫn nằm trong lịch sử git. Cần làm:

1. Chủ tài khoản Gmail vào Google Account → Bảo mật → Mật khẩu ứng dụng, **thu hồi** mật khẩu cũ.
2. Tạo mật khẩu mới và chỉ đặt vào `.env` (không commit).
3. Nếu nhóm đồng ý, xóa khỏi lịch sử git bằng `git filter-repo`. Việc này phải force-push và mọi người phải clone lại, nên cần bàn trước.

## 4. Chuyển giao cho thành viên khác

### 4.1. Đức: UC-16/17/18, UC-68

**API cũ không có xác thực (Critical).** Hai controller `FinancialProfileController` và `AcademicAnalysisController`:

- không có `[Authorize]`;
- tin header `X-User-Id`, thiếu header thì dùng GUID mặc định;
- `AcademicAnalysisRepository.EnsureProfileExistsAsync` tự tạo user giả "Dev Test User".

Hệ quả: ai cũng đọc/sửa được hồ sơ tài chính và học thuật của người khác chỉ bằng cách đổi header. Có hai cách sửa:

- **A (khuyến nghị):** gỡ hai controller này cùng service/repository/DTO/test liên quan. Gỡ luôn các component frontend không route nào dùng: `FinancialCard`, `ExtracurricularSection`, `AchievementsSection`, `components/academic/AcademicAnalysisView`, `TranscriptScoreTable`, `lib/profile-api.ts`, `lib/academic-api.ts`. Khi đó wizard `/api/v1/profile-flow/*` là đường ghi hồ sơ duy nhất (đã có xác thực, chống IDOR, validate).
- **B (nếu cần giữ):**
  - thêm `[Authorize(Roles = "student,parent")]`;
  - lấy user bằng `User.GetUserId()`;
  - bỏ `X-User-Id`, GUID mặc định và user giả;
  - không nhận Id từ client;
  - không trả `ex.Message` ra ngoài.

**Ngoài ra:**

- **GPA:** API cũ ghi đè `overall_gpa` bằng bảng WES, trái với Q7 (tuyến tính). Wizard đã tính tuyến tính phía server (`ProfileFlowService.ComputeGpa4`).
- **UC-68 bảng quy đổi:** chưa làm. Theo Q7 chỉ cần công thức tuyến tính, không cần bảng.

### 4.2. Dũng: UC-22, UC-69

- **`GEMINI_API_KEY` không vào container:** advisor đọc `settings.gemini_api_key` nhưng `docker-compose*.yml` không truyền `GEMINI_API_KEY`, nên chạy Docker sẽ không có key. Cần thêm vào `environment` của service `advisor` và `.env.example`.
- **Rate limit:** gợi ý trường mới giới hạn theo IP ở nginx, cần thêm rate limit theo user ở backend.
- **UC-69:** đã chốt ngưỡng theo dữ liệu từng trường và admin không sửa. Cần xem lại phạm vi UC (có thể đổi thành "hiển thị ngưỡng", không phải "cấu hình").
- **Code chưa đụng:** `SchoolExplaination` để nguyên, chưa rà sâu.

### 4.3. Dương: UC-23

- Chức năng hoạt động.
- Không có lỗi bảo mật riêng ngoài các mục chung ở 4.5.

### 4.4. Phương Anh: WI-09, WI-10

- **WI-09:** có 6 hồ sơ mẫu (kế hoạch cần 3–5) nhưng mới đánh giá bước tính điểm, chưa đánh giá phần giải thích của AI.
- **WI-10:** chưa có phiếu Feedback Capture Grid.

### 4.5. Việc chung (hạ tầng)

- **Advisor API key fail-open:** `require_api_key` cho qua khi chưa cấu hình key. Production nên từ chối khởi động nếu thiếu key.
- **Cổng dev:** `docker-compose.yml` (dev) publish 5432 / 8000 / 11434 ra host. Nên bind `127.0.0.1` hoặc bỏ.
- **CSP:** thiếu header Content-Security-Policy ở nginx.
- **AuditLog:** bảng `AuditLog` có nhưng chưa ghi gì; nên ghi các sự kiện đăng nhập, khóa tài khoản, đổi mật khẩu, thao tác admin.
- **PII trên máy người dùng:** nháp hồ sơ còn lưu trong `localStorage` của trình duyệt (có PII); nên xóa khi đăng xuất.

### 4.6. Độ tuổi và phụ huynh (Q11): việc cho người làm tiếp

- **Thêm chặn theo tuổi cho chức năng mới**, chỉ 3 chỗ, không sửa handler hay DB:
  1. thêm một dòng vào `AgeFeatures.Rules`, ví dụ `[X] = new(MinAge: 18, ParentConsentUnlocks: false)`, và thêm tên vào `AGE_FEATURES` + `PARENT_CONSENT_UNLOCKS` trong `frontend/src/lib/age-features.ts`;
  2. gắn `[Authorize(Policy = AgeFeatures.X)]` lên endpoint;
  3. bọc trang hoặc nút bằng `<AgeGate feature="x">` (hoặc dùng `useAgeBlock`).
  Test `AgeFeaturesTests` tự chạy qua mọi dòng trong `Rules`.
- **Chia sẻ hồ sơ cho trung tâm (bảng `leads`):** chưa có API. Khi làm phải gắn `[Authorize(Policy = AgeFeatures.ShareWithCenter)]` và ghi `consent_id` (đồng ý riêng của người dùng cho mục đích `share_with_center`).
- **Tài khoản trung tâm:** không còn tự đăng ký. `AdminUsersController` mới có GET, cần thêm API admin tạo tài khoản trung tâm.
- **`APP_PUBLIC_URL`:** dùng để dựng link trong mail mời phụ huynh; production lấy `https://${DOMAIN}` (`docker-compose.prod.yml`), dev mặc định `http://localhost:3000`.
- **Token mời** đi trong body POST (`invite/lookup`, `invite/accept`), không nằm trong URL API để không lọt vào log máy chủ; link trong mail vẫn có `?token=` cho trang frontend.
- **Controller của Đức** (`FinancialProfileController`, `AcademicAnalysisController`) không có xác thực nên cũng không có chặn theo tuổi; xử lý theo mục 4.1 thì chặn sẽ chỉ còn qua wizard.
- **Tài khoản cũ** chưa có ngày sinh không bị hạn chế. Nếu sau này muốn áp dụng, cần màn hình bắt bổ sung ngày sinh khi đăng nhập.

## 5. Kiểm thử

Test không chạm DB thật, SMTP thật hay LLM thật. Repository, mail sender và Ollama đều là lớp giả.

```bash
dotnet test backend/tests/StudyAbroad.UnitTests
cd advisor && .venv/Scripts/python -m pytest tests
cd frontend && npm run lint && npm run build
```

Kiểm tra thủ công dùng stack riêng `docker compose -p usas-verify` và Mailpit làm hộp thư giả. Xong việc thì chạy `docker compose -p usas-verify down -v` để xóa volume, không đụng DB dev của nhóm.
