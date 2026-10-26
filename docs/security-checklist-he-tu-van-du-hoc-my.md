# BẢNG CHECKLIST CHỨNG MINH AN TOÀN THÔNG TIN & BẢO MẬT HỆ THỐNG
## (Security Verification & Proof Checklist)

**Dự án:** Hệ tư vấn du học Mỹ – Study Abroad Decision Support System for US Education  
**Người phụ trách an toàn thông tin & xác thực:** Tuấn Việt (Module #1: Auth, #2: Hồ sơ học thuật, #16: Admin RBAC, #17: Bảo mật hệ thống)  
**Kiến trúc:** Defense-in-Depth (Phòng thủ đa tầng: Nginx Reverse Proxy → .NET 8 Backend API → PostgreSQL 16 + Next.js 16 Frontend)  
**Ngày cập nhật & nghiệm thu:** 2026-10-27 (rà soát sprint v0.1)  
**Trạng thái kiểm thử:** 216/216 Unit Tests PASSED (100% thành công, đếm lại sau khi gỡ API lưu hồ sơ cũ)

---

## I. LÀM RÕ NHIỆM VỤ BẢO MẬT (SECURITY RESPONSIBILITIES)

Nhiệm vụ bảo mật của thành viên **Tuấn Việt** được phân định rõ ràng theo mô hình **Phòng thủ đa chiều (Defense-in-Depth)** gồm 3 lớp độc lập:

1. **Lớp 1: Hạ tầng mạng & Reverse Proxy (Network & Transport Security)**
   - Thiết lập giao thức truyền thông an toàn **HTTPS (TLS 1.2/1.3)**, tự động chuyển hướng HTTP (port 80) sang HTTPS (port 443).
   - Kích hoạt cơ chế **HSTS (HTTP Strict Transport Security)** để ngăn chặn tấn công hạ cấp giao thức (SSL Stripping).
   - **Cô lập mạng nội bộ (Network Isolation)**: Chỉ công khai duy nhất Nginx (80/443); Database, AI service và Backend API chỉ chạy trên mạng nội bộ Docker.
   - Cấu hình **Rate Limiting** chống tấn công từ chối dịch vụ (DoS) tại tầng Gateway.

2. **Lớp 2: Xác thực, Phân quyền & Logic Backend (Application & Database Security)**
   - **Mật khẩu an toàn**: Băm mật khẩu bằng thuật toán **BCrypt** với Work Factor = 12 (chống tấn công vét cạn / Rainbow Table).
   - **Quản lý phiên & Chống XSS**: Sử dụng **HttpOnly Cookie** cho JWT Token, tuyệt đối không lưu Token trong `localStorage`.
   - **Thu hồi phiên đăng nhập (Session Revocation - AC-3)**: Quản lý phiên qua bảng `user_sessions`, vô hiệu hóa phiên ngay lập tức khi đăng xuất hoặc đổi mật khẩu.
   - **Chống Brute-Force**: Tự động tạm khóa tài khoản 15 phút nếu nhập sai mật khẩu 5 lần liên tiếp; mã OTP bị vô hiệu sau 5 lần nhập sai, tối đa 5 lần gửi OTP mỗi giờ; giới hạn tần suất theo IP cho toàn bộ API xác thực.
   - **Chống dò tài khoản (Account Enumeration)**: Đăng ký, đăng nhập, OTP, quên/đặt lại mật khẩu trả cùng một thông báo dù email có tồn tại hay không.
   - **Phân quyền vai trò (RBAC)**: Kiểm soát quyền hạn nghiêm ngặt theo 4 vai trò (`student`, `parent`, `center`, `admin`).
   - **Chống lỗi IDOR (Insecure Direct Object References)**: 100% API tài nguyên cá nhân trích xuất `userId` từ Token Claims, không nhận `userId` từ Client.
   - **Chống SQL Injection**: Sử dụng 100% Entity Framework Core LINQ Parameterized Queries.
   - **Chống rò rỉ thông tin (Information Disclosure)**: Trả về lỗi theo chuẩn RFC 7807 (`ProblemDetails`), ẩn Stack Trace và mã lỗi nội bộ ở môi trường Production. Kiểm tra khóa ký số JWT Secret Key >= 32 ký tự.

3. **Lớp 3: Tầng Trình duyệt & AI Guardrails (Client & AI Safety)**
   - **Chống XSS phía Client**: Tận dụng Auto-escaping của React 19 JSX, nghiêm cấm `dangerouslySetInnerHTML`.
   - **Bảo vệ tuyến điều hướng (Route Guard)**: Thành phần `<ProtectedRoute>` kiểm tra vai trò tại Client trước khi render màn hình.
   - **Bảo vệ dữ liệu đầu ra AI (AI Output Guardrail)**: Kiểm duyệt phát hiện con số bịa đặt và ảo giác của mô hình LLM.

---

## II. BẢNG CHECKLIST CHỨNG MINH TỪNG BIỆN PHÁP BẢO MẬT (CÓ BẰNG CHỨNG)

Bảng này liệt kê chi tiết từng biện pháp kỹ thuật, hiểm họa tương ứng, vị trí code cụ thể, và **phương pháp/bằng chứng chứng minh (Verification & Proof)** để phục vụ kiểm tra mã nguồn và phản biện đồ án:

| STT | Biện pháp bảo mật | Hiểm họa phòng chống (Threat Mitigated) | Vị trí file cấu hình / Mã nguồn | Cơ chế triển khai chi tiết | Bằng chứng chứng minh thực tế (Proof / Verification) | Trạng thái |
|:---:|---|---|---|---|---|:---:|
| **1** | **Bắt buộc HTTPS & HSTS** | Man-in-the-Middle (MitM), Sniffing dữ liệu mạng, SSL Stripping | `deploy/nginx/templates/default.conf.template`<br>(dòng 8-14, 16-29)<br>`docker-compose.prod.yml`<br>(dòng 98-124)<br>`scripts/init-letsencrypt.sh` | - Nginx lắng nghe port 80 và trả về mã `301 Moved Permanently` chuyển hướng sang HTTPS.<br>- Nginx 443 bật SSL giao thức `TLSv1.2 TLSv1.3`.<br>- Header `Strict-Transport-Security "max-age=31536000" always`.<br>- Certbot container tự động renew chứng chỉ Let's Encrypt mỗi 12h. | **Lệnh kiểm tra 1 (Redirect 301):**<br>`curl -I http://<domain>`<br>→ Trả về `HTTP/1.1 301 Moved Permanently`<br>`Location: https://<domain>/`<br><br>**Lệnh kiểm tra 2 (HSTS Header):**<br>`curl -I https://<domain>`<br>→ Trả về `Strict-Transport-Security: max-age=31536000` | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **2** | **Cô lập mạng nội bộ (Network Isolation)** | Tấn công trực tiếp vào Database hoặc AI Server từ Internet | `docker-compose.prod.yml`<br>(dòng 10-124) | - Chỉ có duy nhất container `nginx` publish cổng `80:80` và `443:443` ra ngoài Internet.<br>- Postgres (`db:5432`), Ollama (`ollama:11434`), Backend API (`api:8080`), Advisor (`advisor:8000`) hoàn toàn không mở cổng ra host, giao tiếp thông qua Docker Virtual Bridge Network. | **Kiểm tra Ports:**<br>Chạy `docker compose -f docker-compose.prod.yml ps`<br>hoặc `nmap <vps-ip>`<br>→ Chỉ duy nhất cổng 80 và 443 mở, cổng 5432 (Postgres) và 8080 (API) bị đóng hoàn toàn với mạng công cộng. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **3** | **Băm mật khẩu bằng BCrypt (Work Factor = 12)** | Lộ mật khẩu gốc khi cơ sở dữ liệu bị rò rỉ (Data Breach), Rainbow Table | `backend/src/StudyAbroad.Infrastructure/Auth/PasswordHasher.cs`<br>`backend/tests/StudyAbroad.UnitTests/PasswordHasherTests.cs` | - Sử dụng thư viện `BCrypt.Net-Next`.<br>- Thiết lập `WorkFactor = 12` (thực hiện $2^{12} = 4096$ vòng băm).<br>- Tự sinh salt ngẫu nhiên cho từng tài khoản, không bao giờ lưu mật khẩu gốc. | **1. Unit Test:**<br>`PasswordHasherTests.cs` kiểm tra hash bắt đầu bằng tiền tố `$2`, độ dài đúng 60 ký tự, xác minh mật khẩu đúng trả về `true`, mật khẩu sai trả về `false`.<br>**2. Bằng chứng DB:**<br>Kiểm tra cột `app.users.password_hash` đều là chuỗi băm 60 ký tự. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **4** | **Lưu trữ Token bằng HttpOnly Cookie (Chống XSS)** | Cross-Site Scripting (XSS đánh cắp Token từ `localStorage` hoặc từ body phản hồi) | `backend/src/StudyAbroad.Api/Controllers/AuthController.cs` (`SetSessionCookie`, `CreateCookieOptions`)<br>`frontend/src/contexts/AuthContext.tsx`<br>`frontend/src/lib/api.ts` | - Backend đặt JWT qua `Response.Cookies.Append` với `HttpOnly = true`, `SameSite = Lax`, `Path = "/"`; `Secure` luôn bật ngoài môi trường Development (không phụ thuộc `Request.IsHttps` vì sau proxy có thể sai).<br>- Body phản hồi đăng nhập / xác thực email chỉ có `expiresAt` và `user`, **không chứa token**.<br>- Trình duyệt tự động gửi cookie khi gọi API (`credentials: "include"`).<br>- Frontend chỉ lưu thông tin User trong state, không lưu token vào `localStorage` hay `sessionStorage`. | **Kiểm tra trên Trình duyệt:**<br>`console.log(document.cookie)` không hiển thị `usas_access_token`; tab Network của `POST /api/v1/auth/login` không có `accessToken` trong body. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **5** | **Thu hồi phiên tức thì (Session Revocation - AC-3)** | Phiên đăng nhập vẫn tồn tại sau khi đăng xuất hoặc sau khi bị chiếm quyền | `backend/src/StudyAbroad.Api/Program.cs` (`OnTokenValidated`)<br>`backend/src/StudyAbroad.Infrastructure/Auth/TokenService.cs`<br>`backend/src/StudyAbroad.Application/Auth/AuthService.cs` | - Khi đăng nhập, Token được băm bằng SHA-256 (`HashToken`) và lưu vào bảng `user_sessions`.<br>- Khi Đăng xuất: phiên hiện tại `IsRevoked = true`. Khi đặt lại mật khẩu qua OTP: thu hồi mọi phiên và gửi mail báo đổi mật khẩu.<br>- Tại middleware `Program.cs`, mỗi request đều băm token và tra cứu DB, nếu `session.IsRevoked == true` lập tức gọi `context.Fail()`. | **1. Unit Test:**<br>`AuthServiceTests.cs`: `Logout_ValidToken_RevokesSession`, `ForgotPassword_And_ResetPassword_Flow_SucceedsAndRevokesSessions`.<br>**2. Kiểm tra API:**<br>Gọi `POST /api/v1/auth/logout` thành công, sau đó dùng lại token cũ gọi `GET /api/v1/auth/me` → `401 Unauthorized`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **6** | **Chống Brute-Force & Gian lận OTP** | Dò quét mật khẩu (Credential Stuffing), vét cạn OTP, request song song vượt giới hạn đếm, spam mail | `backend/src/StudyAbroad.Application/Auth/AuthService.cs`<br>`backend/src/StudyAbroad.Api/RateLimiting/AuthRateLimit.cs`<br>`deploy/nginx/snippets/locations.conf` (zone `auth`)<br>`backend/src/StudyAbroad.Infrastructure/Persistence/Repositories/UserRepository.cs`<br>`backend/tests/StudyAbroad.UnitTests/AuthServiceTests.cs` | - Đăng nhập sai 5 lần liên tiếp: tự động `temp_locked` 15 phút (kiểm khóa **trước** khi so mật khẩu).<br>- OTP 6 số sinh bằng CSPRNG, lưu dạng hash, hết hạn sau 5 phút, dùng 1 lần.<br>- Nhập sai OTP 5 lần: mã bị vô hiệu hóa.<br>- Tối đa 5 lần gửi OTP mỗi giờ cho mỗi mục đích, cách nhau ít nhất 60 giây.<br>- Bộ đếm lần sai (mật khẩu, OTP) được ghi **trước** khi so sánh, có concurrency token (`xmin`): request song song không ghi được thì bị từ chối mà không được so.<br>- Rate limit theo IP: backend 10 request/phút cho mỗi API xác thực (429), Nginx 20 request/phút (burst 10). | **1. Unit Test:**<br>`AuthServiceTests.cs`: `Login_WrongPassword5Times_TempLocksUserFor15Minutes_AndSendsNoticeEmail`, `VerifyEmail_WrongOtp5Times_InvalidatesOtp_NeverLocksUserAccount`, `ResendOtp_MoreThan5TimesPerHour_SilentlyDoesNotSend`, `Login_ConcurrentAttemptConflict_RejectedBeforeCheckingPassword`, `VerifyEmail_ConcurrentAttemptConflict_RejectedWithoutConsumingOtp`.<br>**2. Bằng chứng phản hồi:**<br>Nhập sai mật khẩu lần thứ 5 → HTTP `423 Locked`; gửi quá 10 request/phút vào `/api/v1/auth/login` → HTTP `429`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **7** | **Phân quyền vai trò (Role-Based Access Control - RBAC)** | Người dùng trái phép truy cập tính năng quản trị / vượt quyền | `backend/src/StudyAbroad.Api/Controllers/AdminUsersController.cs`<br>`backend/src/StudyAbroad.Api/Controllers/AcademicProfileController.cs`<br>`frontend/src/components/auth/ProtectedRoute.tsx` | - Backend sử dụng thuộc tính `[Authorize(Roles = UserRoles.Admin)]` trên API Admin và `[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]` trên API Học thuật.<br>- Middleware ASP.NET Core tự động chặn và trả về HTTP `403 Forbidden` nếu sai vai trò.<br>- Frontend có component `<ProtectedRoute>` kiểm tra role và redirect/hiển thị màn hình thông báo không có quyền. | **1. Unit Test:**<br>`AdminUsersControllerTests.cs` kiểm tra bằng reflection xem class có gắn thuộc tính `[Authorize(Roles = "admin")]`.<br>**2. Kiểm tra API:**<br>Đăng nhập bằng tài khoản Student, gọi `GET /api/v1/admin/users` → Nhận mã HTTP `403 Forbidden`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **8** | **Chống IDOR (Insecure Direct Object References)** | Học sinh A sửa hoặc xem trộm hồ sơ học thuật, tài chính của Học sinh B | `backend/src/StudyAbroad.Api/Extensions/ClaimsPrincipalExtensions.cs`<br>`backend/src/StudyAbroad.Api/Controllers/AcademicProfileController.cs`<br>`backend/src/StudyAbroad.Api/Controllers/ProfileFlowController.cs`<br>`backend/tests/StudyAbroad.UnitTests/ClaimsPrincipalExtensionsTests.cs` | - API hồ sơ của Việt **không nhận** `userId` hay `profileId` từ Client qua Query string hoặc Request Body.<br>- Mọi thao tác truy vấn/cập nhật hồ sơ đều lấy `userId` từ Claims đã ký của Token (`User.GetUserId()`).<br>- Lưu ý: `FinancialProfileController`, `AcademicAnalysisController` (phần của Đức) còn tin header `X-User-Id`, đã ghi vào file chuyển giao. | **1. Unit Test:**<br>`ClaimsPrincipalExtensionsTests.cs` kiểm tra lấy đúng `Guid` từ claim, trả về `null` nếu thiếu hoặc claim bị giả mạo.<br>**2. Kiểm tra thiết kế API:**<br>`GET /api/v1/profile/academic` và các API `/api/v1/profile-flow/*` không chứa tham số id người dùng. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **9** | **Chống SQL Injection** | Kẻ tấn công chèn câu lệnh SQL độc hại phá hoại hoặc đánh cắp dữ liệu | `backend/src/StudyAbroad.Infrastructure/Persistence/Repositories/`<br>`backend/src/StudyAbroad.Infrastructure/Persistence/AppDbContext.cs` | - 100% câu truy vấn cơ sở dữ liệu được thực hiện thông qua **Entity Framework Core LINQ**.<br>- EF Core tự động biên dịch câu truy vấn thành các **Parameterized Queries** (`@p0, @p1`), dữ liệu người dùng nhập luôn được coi là tham số giá trị, không bao giờ được diễn giải thành lệnh SQL.<br>- Tuyệt đối không dùng string concatenation ghép chuỗi SQL. | **Rà soát mã nguồn (Static Code Analysis):**<br>Không có bất kỳ câu lệnh `FromSqlRaw` hay `ExecuteSqlRaw` nào trong toàn bộ project. Rà soát log EF Core ghi nhận toàn bộ truy vấn đều ở dạng tham số hóa (`@__userId_0`). | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **10** | **Kiểm soát lưu lượng (Rate Limiting) & Bảo vệ AI** | Tấn công từ chối dịch vụ (DoS), spam làm sập dịch vụ AI LLM | `deploy/nginx/templates/default.conf.template`<br>(dòng 3: `limit_req_zone`)<br>`deploy/nginx/snippets/locations.conf`<br>(dòng 10-21)<br>`backend/src/StudyAbroad.Application/Recommendations/AiOutputGuard.cs` | - Nginx cấu hình `limit_req_zone $binary_remote_addr zone=advisor:10m rate=10r/m;` và zone `auth` 20r/m (burst 10) cho `/api/v1/auth/`; backend có thêm policy `AuthRateLimit` 10 request/phút theo IP cho mỗi API xác thực.<br>- Áp dụng `limit_req zone=advisor burst=5 nodelay;` cho các endpoint tốn tài nguyên GPU/LLM (`/api/v1/advisor/` và `/api/v1/recommendations`).<br>- Module `AiOutputGuard` kiểm duyệt kết quả đầu ra của AI, chặn con số bịa đặt và ảo giác. | **1. Unit Test:**<br>`AiOutputGuardTests.cs` (152 dòng test kiểm tra loại bỏ số bịa, kiểm tra độ dài output, chuẩn hóa cấu trúc).<br>**2. Kiểm tra Nginx:**<br>Gửi liên tục 10 request trong 5 giây vào endpoint AI → Nginx trả về `HTTP 429 Too Many Requests`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **11** | **Chống rò rỉ thông tin máy chủ (Information Disclosure)** | Lộ cấu trúc hệ thống, mã lỗi nội bộ, Stack Trace, Secret Key | `backend/src/StudyAbroad.Api/Program.cs`<br>(dòng 32-44, 108, 157-165)<br>`docker-compose.prod.yml` | - Kích hoạt `AddProblemDetails()`, `UseExceptionHandler()`, `UseStatusCodePages()`.<br>- Ẩn Swagger và tắt Developer Exception Page khi chạy môi trường Production.<br>- Lỗi trả về luôn tuân thủ định dạng chuẩn RFC 7807 (`ProblemDetails`).<br>- Khóa ký số JWT: Bắt buộc `JWT_SECRET_KEY` có độ dài tối thiểu 32 ký tự (256-bit), từ chối khởi động nếu thiếu trên Production. | **Kiểm tra mã nguồn:**<br>Đoạn mã tại `Program.cs` dòng 41-43 ném ngoại lệ `InvalidOperationException` ngay khi khởi động nếu SecretKey không đủ 32 ký tự trong môi trường Production. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **12** | **Cấu hình CORS nghiêm ngặt** | Cross-Origin Request không mong muốn từ website độc hại | `backend/src/StudyAbroad.Api/Program.cs`<br>(dòng 136-141)<br>`docker-compose.prod.yml`<br>(dòng 74) | - Tuyệt đối không dùng `AllowAnyOrigin()` khi có `AllowCredentials()`.<br>- Chỉ đọc danh sách cho phép từ cấu hình `Cors:AllowedOrigins`.<br>- Trên môi trường Production, biến môi trường chỉ cho phép duy nhất tên miền chính thức của hệ thống (`https://${DOMAIN}`). | **Kiểm tra Header:**<br>Request từ origin lạ gọi API sẽ bị trình duyệt chặn theo chính sách Same-Origin Policy do thiếu header `Access-Control-Allow-Origin`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |
| **13** | **Chống dò tài khoản (Account Enumeration)** | Kẻ tấn công thử email/SĐT để biết ai có tài khoản, rồi dò mật khẩu hoặc lừa đảo có chủ đích | `backend/src/StudyAbroad.Application/Auth/AuthService.cs`<br>`backend/src/StudyAbroad.Infrastructure/Auth/QueuedEmailSender.cs`<br>`backend/tests/StudyAbroad.UnitTests/AuthServiceTests.cs` | - Đăng ký trùng email/SĐT: trả cùng thông báo như đăng ký mới, chủ hộp thư nhận mail báo riêng.<br>- Đăng nhập: email không tồn tại và sai mật khẩu cùng thông báo; email không tồn tại vẫn so với hash giả để thời gian phản hồi tương đương. Trạng thái chưa kích hoạt / bị khóa chỉ báo khi mật khẩu đúng.<br>- OTP sai / hết hạn / đã dùng / email không tồn tại: cùng một thông báo, không báo số lần thử còn lại.<br>- Gửi lại OTP, quên mật khẩu: luôn trả cùng thông báo; mail đi qua hàng đợi nền nên không lộ qua thời gian phản hồi.<br>- Email chuyển giữa các trang qua `sessionStorage`, không nằm trong URL. | **Unit Test:**<br>`AuthServiceTests.cs`: `Register_DuplicateEmail_ReturnsSameMessageAndMailsOwner`, `Register_WithDuplicatePhone_ReturnsGenericSuccessAndMailsNewEmail`, `Login_UnknownEmail_SameResultAsWrongPassword`, `Login_UnverifiedOrLocked_WrongPassword_DoesNotRevealStatusOrCountAttempts`, `VerifyEmail_UnknownEmail_SameMessageAsWrongCode`, `ResendOtp_UnknownEmail_ReturnsSameMessageAsExistingEmail`, `ForgotPassword_UnknownEmail_SameMessageAndNoMail`. | **ĐÃ HOÀN THÀNH & ĐÃ KIỂM THỬ** |

---

## III. BẢNG KIỂM TRA BẢO MẬT THEO TỪNG USER STORY ĐÃ HOÀN THÀNH

### 1. User Story #1: Đăng ký, Đăng nhập, Xác thực Email & Quản lý phiên (USAS-362)
*Người thực hiện: Tuấn Việt · Trạng thái: Đã nghiệm thu 100%*

- [x] **Xác thực 2 bước với OTP Email**: Sau khi đăng ký, tài khoản ở trạng thái `unverified`. Chỉ khi xác thực OTP thành công mới được kích hoạt `active`.
- [x] **Lưu mật khẩu bằng BCrypt WorkFactor 12**: Mật khẩu mã hóa 1 chiều với salt ngẫu nhiên.
- [x] **Token JWT gắn cờ HttpOnly**: Access token lưu trữ trong Cookie an toàn, trình duyệt tự động đính kèm, chặn XSS.
- [x] **Khóa tài khoản chống Brute-Force**: Tự động tạm khóa 15 phút sau 5 lần đăng nhập thất bại; OTP vô hiệu sau 5 lần sai, tối đa 5 lần gửi/giờ; rate limit theo IP.
- [x] **Chống dò tài khoản**: Thông báo chung cho đăng ký trùng, đăng nhập sai, OTP sai, quên mật khẩu.
- [x] **Thu hồi phiên tập trung (Session Revocation)**: Bảng `user_sessions` lưu vết phiên, hỗ trợ đăng xuất vô hiệu hóa tức thì (AC-3).
- [x] **Đổi mật khẩu khi đã đăng nhập (UC-06)**: `POST /api/v1/auth/change-password` bắt buộc mật khẩu hiện tại, mật khẩu mới 8–100 ký tự và khác mật khẩu cũ; giữ phiên hiện tại, thu hồi các phiên khác, gửi mail thông báo. Nhập sai mật khẩu hiện tại cũng tính vào bộ đếm 5 lần → tạm khóa và thu hồi mọi phiên. Test: `ChangePassword_*` trong `AuthServiceTests.cs`.
- [x] **Unit Test Suite**: 100% passed test cases trong `AuthServiceTests.cs`, `PasswordHasherTests.cs`.

### 2. User Story #2: Hồ sơ học thuật & Bảng điểm (USAS-363 / B1-01, B1-02, B1-08, B1-09)
*Người thực hiện: Tuấn Việt · Trạng thái: Đã nghiệm thu 100%*

- [x] **RBAC**: `[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]` (chặn tài khoản trung tâm và khách vãng lai).
- [x] **Chống IDOR**: Không nhận `userId` từ Client; trích xuất từ Token Claims qua `User.GetUserId()`.
- [x] **Validation 2 lớp & Whitelist thang điểm**: Kiểm tra nghiêm ngặt thang 10 (`0.0 - 10.0`), thang 100 (`0 - 100`), thang 4 (`0.0 - 4.0`), thang chữ (whitelist 13 ký tự chuẩn `A+` đến `F`). Chặn điểm vượt biên.
- [x] **Chặn trùng lặp môn học**: Bắt lỗi nếu cùng một học kỳ có 2 môn cùng tên (case-insensitive & trimmed).
- [x] **Ràng buộc điểm chứng chỉ quốc tế**: IELTS (0.0-9.0, bước 0.5), TOEFL (0-120), Duolingo (10-160), SAT (400-1600), ACT (1-36).
- [x] **Chống SQL Injection**: 100% Entity Framework Core LINQ Parameterized Queries.
- [x] **GPA chỉ tính phía server**: Khi commit hồ sơ, GPA tính tuyến tính (/10 × 4) từ điểm các môn, bỏ qua giá trị client gửi lên.
- [x] **Giới hạn nháp & độ dài trường**: Nháp tối đa 256K ký tự, 8 năm học, 40 môn/năm, 30 chứng chỉ, 50 hoạt động; độ dài tên trường/môn/khối/hoạt động theo cột DB, năm tốt nghiệp hợp lệ → trả 400 thay vì lỗi DB 500.
- [x] **Không lộ đường dẫn lưu file**: Thông tin file bảng điểm nháp trả về chỉ có tên gốc, kích thước, loại, thời điểm tải.
- [x] **Xác nhận bảng điểm trích xuất (UC-12)**: Ô điểm đọc từ ảnh, điểm bất thường, ô bị ghi đè / trùng môn được tô cam; còn ô chưa xác nhận thì frontend không cho sang bước 3 và backend từ chối commit. Test: `CommitFlowAsync_WhenTranscriptCellsStillFlagged_Throws_ThenSucceedsAfterConfirm`, `advisor/tests/test_transcript_ocr_api.py`.
- [x] **Unit Test Suite**: 100% passed test cases trong `ProfileFlowServiceTests.cs`, `AcademicProfileServiceTests.cs` (chỉ còn API đọc `GET /api/v1/profile/academic`; lưu hồ sơ đi qua `/api/v1/profile-flow/*`).

### 3. User Story #16: Quản trị tài khoản & Phân quyền Admin (AC-4)
*Người thực hiện: Tuấn Việt · Trạng thái: Đã nghiệm thu 100%*

- [x] **Bảo vệ Endpoint Admin**: `[Authorize(Roles = UserRoles.Admin)]` trên `AdminUsersController`.
- [x] **Unit Test Suite**: `AdminUsersControllerTests.cs` kiểm tra và chứng minh thuộc tính Authorize của controller.

---

## IV. QUY TRÌNH KIỂM CHỨNG KHI HOÀN THÀNH CODE & BẢO VỆ ĐỒ ÁN (VERIFICATION PROTOCOL)

Checklist này là **bắt buộc** mỗi khi hoàn thành code và chuẩn bị bảo vệ đồ án. Thực hiện 4 bước sau để chứng minh toàn bộ hệ thống đạt chuẩn an toàn:

### Bước 1: Chạy toàn bộ Unit Test Suite
Chạy lệnh kiểm thử tự động tại thư mục gốc của dự án:
```powershell
dotnet test backend/tests/StudyAbroad.UnitTests
```
*Kết quả chứng minh đạt chuẩn:*
- **Total: 216 tests**
- **Passed: 216 (100%)**
- **Failed: 0**

### Bước 2: Kiểm tra cấu hình HTTPS & Headers trên Production
Chạy các lệnh curl sau để chứng minh chuyển hướng HTTPS và tiêu chuẩn HSTS:
```bash
# 1. Chứng minh chuyển hướng HTTP sang HTTPS:
curl -I http://yourdomain.com
# Kỳ vọng: HTTP/1.1 301 Moved Permanently -> Location: https://yourdomain.com/

# 2. Chứng minh HSTS và Security Headers:
curl -I https://yourdomain.com
# Kỳ vọng có các headers:
# Strict-Transport-Security: max-age=31536000
# X-Content-Type-Options: nosniff
# X-Frame-Options: SAMEORIGIN
# Referrer-Policy: strict-origin-when-cross-origin
```

### Bước 3: Chứng minh chống XSS qua HttpOnly Cookie
1. Mở trình duyệt, truy cập trang web và đăng nhập tài khoản.
2. Mở **Chrome DevTools (F12)** → tab **Application** → mục **Cookies**:
   - Thấy cookie `usas_access_token` có tích chọn cột **HttpOnly** và **Secure**.
3. Chuyển sang tab **Console**, gõ:
   ```javascript
   document.cookie
   ```
   *Kết quả:* Không xuất hiện `usas_access_token`, chứng minh mã độc JavaScript không thể đọc cắp Token.

### Bước 4: Quét lỗ hổng phụ thuộc (Dependency Audit)
Trước khi đóng gói triển khai hoặc nghiệm thu:
```bash
dotnet list backend/src/StudyAbroad.Api package --vulnerable
npm --prefix frontend audit
```
*Kỳ vọng:* Không có lỗ hổng bảo mật mức độ High hoặc Critical.
