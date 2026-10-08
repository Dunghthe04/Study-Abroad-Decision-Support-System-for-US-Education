# QUY CHUẨN PHÁT TRIỂN & BẢO VỆ ĐỒ ÁN CAPSTONE (USAS)

Tài liệu này là "kim chỉ nam" cho việc lập trình, giải thích kiến trúc và bảo trì mã nguồn trong suốt dự án USAS, đặc biệt phục vụ cho việc **bảo vệ trước Giảng viên hướng dẫn (Supervisor) và Hội đồng chấm đồ án**.

---

## 1. NGUYÊN TẮC "SỬA ĐÚNG CHỖ - TRÁNH LAN MAN" (Scope Isolation)

1. **Tuân thủ đúng phạm vi phân công:**
   - Mỗi thành viên phụ trách các chức năng riêng theo Product Backlog & Jira.
   - **Nguyễn Xuân Đức (Sprint 1):**
     - `USAS-364`: Hồ sơ tài chính và ngoại khóa.
     - `USAS-365`: Phân tích điểm học thuật.
   - Tuyệt đối **không tự ý sửa đổi** các file core, config dùng chung hoặc code của thành viên khác (ví dụ: phần Auth, Bảng điểm của Tuấn Việt, AI Chat của Tuấn Dũng) trừ khi có sự thảo luận và thống nhất interface.

2. **Quy chuẩn đặt tên Commit (bắt buộc cho Nguyễn Xuân Đức):**
   Mỗi lần commit phải tách riêng rẽ theo từng phần và đặt tên đúng cú pháp:
   - `[DucNX]: BE- <nội dung>` : Cho các file Backend (.NET Domain, Application, Infrastructure, Controller).
   - `[DucNX]: FE- <nội dung>` : Cho các file Frontend (Next.js, React components, Tailwind, Types, API client).
   - `[DucNX]: Test- <nội dung>` : Cho các file kiểm thử Unit Test.
   - `[DucNX]: Docs- <nội dung>` : Cho tài liệu kỹ thuật, báo cáo Report 3, guideline.
   *Quy tắc:* Cùng là BE thì gom commit chung 1 lần và ghi rõ, tương tự với FE, Test và Docs. Tuyệt đối không gom chung BE + FE + Test vào 1 commit duy nhất.

3. **Tuân thủ Clean Architecture (Backend .NET 8):**
   - **Domain:** Chỉ chứa Entity, Enums, Domain Exceptions. Không phụ thuộc thư viện ngoài.
   - **Application:** Chứa DTOs, Service Interfaces, Service Implementations, Validators.
   - **Infrastructure:** Chứa EF Core Configurations, Repositories, Database Context, Migration.
   - **Api:** Chứa Controllers (chỉ làm nhiệm vụ nhận Request, gọi Service, trả về HTTP status code tương ứng).
   - *Quy tắc vàng:* Controller không bao giờ chứa business logic hay gọi trực tiếp DbContext.

3. **Tuân thủ Cấu trúc Frontend (Next.js 16 App Router + React 19 + Tailwind v4):**
   - Đặt đúng thư mục:
     - Giao diện trang: `src/app/<feature>/page.tsx`
     - Component tái sử dụng: `src/components/<feature>/<ComponentName>.tsx`
     - Gọi API: viết hàm trong `src/lib/api.ts` hoặc service riêng trong `src/lib/<feature>.ts`
     - Types/Interfaces: `src/types/<feature>.ts`
   - Giữ vững hệ màu thống nhất: Neutral Slate (`slate-50`, `slate-100`, `slate-200`, `slate-600`, `slate-900`) và Primary Blue (`blue-600`, `blue-700`).

---

## 2. QUY CHUẨN COMMENT THÔNG MINH (Smart Commenting & Tagging)

Mục đích: Giúp dễ dàng tìm kiếm mã nguồn bằng `Ctrl + Shift + F` và hiểu ngay lý do viết code.

### Format Tag bắt buộc:
Mỗi class, function, hoặc khối logic nghiệp vụ phải có tag Jira tương ứng:
- `// [USAS-364]` cho Hồ sơ tài chính & ngoại khóa.
- `// [USAS-365]` cho Phân tích điểm học thuật.

### Quy tắc viết comment:
1. **Comment "Tại sao (Why)" thay vì "Làm gì (What)":**
   - ❌ *Không viết:* `// tính điểm trung bình`
   - ✅ *Viết:* `// [USAS-365] Tính Unweighted GPA theo thang 4.0 chuẩn WES (mặc định Điểm hệ 10 * 0.4 theo quy định trong Scope of Work)`
2. **XML Docstring chuẩn cho Backend (C#):**
   ```csharp
   /// <summary>
   /// [USAS-365] Quy đổi điểm trung bình môn học hệ 10 sang thang điểm 4.0.
   /// </summary>
   /// <param name="rawScore">Điểm hệ 10 (từ 0.0 đến 10.0)</param>
   /// <returns>Điểm tương ứng hệ 4.0</returns>
   /// <exception cref="ArgumentOutOfRangeException">Khi điểm nằm ngoài khoảng [0, 10]</exception>
   public static double ConvertToGpa4(double rawScore)
   ```
3. **JSDoc chuẩn cho Frontend (TypeScript):**
   ```typescript
   /**
    * [USAS-364] Gửi dữ liệu ngân sách và nguồn tài chính của học sinh lên máy chủ
    * @param profile Dữ liệu hồ sơ tài chính
    */
   export async function submitFinancialProfile(profile: FinancialProfileDto): Promise<void>
   ```

---

## 3. QUY TRÌNH "DEFENSE-READY" (Sẵn sàng trả lời khi Thầy hỏi)

Mỗi khi hoàn thành một chức năng hoặc một API/màn hình, agent và lập trình viên phải nắm rõ 4 câu trả lời sau:

1. **Luồng dữ liệu (Data Flow):**
   - Dữ liệu đi từ đâu đến đâu? (Client form -> API POST -> Validation -> Service tính toán -> Repository lưu Postgres -> Response trả về Client).
2. **Công thức & Nghiệp vụ (Business Rules):**
   - Công thức tính toán lấy từ nguồn nào? (Ví dụ: Scope of Work v6, Bảng quy đổi điểm WES, dữ liệu tư vấn thực tế từ trung tâm).
3. **Lý do thiết kế (Design Decision):**
   - Tại sao lại tách bảng này? Tại sao dùng kiểu dữ liệu này (ví dụ: `decimal` cho tiền tệ, `float/double` cho GPA)?
4. **Xử lý tình huống biên (Edge Cases):**
   - Nếu học sinh nhập điểm âm? Nếu học sinh chưa thi SAT/IELTS? Nếu ngân sách gia đình là 0đ? Hệ thống xử lý ra sao?

---

## 4. CHIẾN LƯỢC SPRINT 1 (Nguyễn Xuân Đức)

| Mã Jira | Tên chức năng | Đầu việc Backend | Đầu việc Frontend |
| :--- | :--- | :--- | :--- |
| **USAS-364** | Hồ sơ tài chính & ngoại khóa | Entities (`FinancialProfile`, `ExtracurricularActivity`), DTOs, Service CRUD, Controller | Màn hình nhập Form khai báo ngân sách, Form thêm/sửa/xóa hoạt động ngoại khóa & giải thưởng |
| **USAS-365** | Phân tích điểm học thuật | Algorithm quy đổi thang 4.0, tính GPA / Weighted GPA, gom nhóm môn (Tự nhiên, Xã hội, Ngoại ngữ), phân tích xu hướng 3 năm, Unit Tests 100% | Trang hiển thị Dashboard kết quả phân tích: biểu đồ xu hướng điểm, bảng radar/thống kê nhóm môn, dòng khuyến cáo tham khảo |

---

## 5. QUY CHUẨN BẢO MẬT (SECURITY) & VALIDATION BẮT BUỘC

Khi thầy cô kiểm tra mã nguồn hoặc hỏi về bảo mật, sinh viên cần trình bày rõ cơ chế bảo vệ 2 lớp (Defense-in-depth):

### A. Nguyên tắc Validate 2 lớp (Two-layer Validation)
* **Lớp 1 - Frontend (Trải nghiệm người dùng UX):**
  - Bắt lỗi ngay tại màn hình (Inline validation) trước khi gửi request.
  - Kiểm tra trường bắt buộc, định dạng số, độ dài chuỗi tối đa/tối thiểu.
  - Vô hiệu hóa nút bấm (Disable Button) khi đang gọi API để chống double-click (Race Condition / Duplicate Submission).
* **Lớp 2 - Backend (Lớp bảo vệ cốt lõi):**
  - **Nguyên tắc "Never Trust Client":** Dù Frontend đã validate, Backend bắt buộc phải validate lại 100% dữ liệu đầu vào trong DTO.
  - Trả về mã lỗi `400 Bad Request` kèm thông báo chi tiết cho từng trường vi phạm theo chuẩn RFC 7807 (`ProblemDetails`).

### B. Bảo mật Backend (.NET 8)
1. **Chống IDOR (Insecure Direct Object References - Xem/Sửa trộm dữ liệu người khác):**
   - API tuyệt đối **không** dùng `userId` do client gửi trong Request Body hoặc Query String để truy vấn dữ liệu.
   - `userId` bắt buộc phải được trích xuất từ Token/Context đăng nhập an toàn (`User.Claims` hoặc Current User Session).
   - Học sinh chỉ có quyền đọc/ghi trên chính hồ sơ thuộc sở hữu của mình.
2. **Chống SQL Injection:**
   - 100% câu truy vấn sử dụng Entity Framework Core LINQ và Parameterized Queries.
   - Tuyệt đối không dùng string interpolation để ghép chuỗi vào câu lệnh SQL.
3. **Chống Mass Assignment (Overposting Attack):**
   - Không bind trực tiếp Database Entity vào Controller action.
   - Luôn sử dụng DTO riêng biệt cho Input (Request DTO) và Output (Response DTO).
4. **Giới hạn biên & Chống tràn dữ liệu (Denial of Service):**
   - Ngân sách hàng năm (`AnnualBudget`): Bắt buộc `>= 0` và `<= 10,000,000 USD`.
   - Điểm số học thuật (`RawScore`): Bắt buộc trong khoảng `[0.0, 10.0]` (hệ 10) hoặc `[0.0, 4.0]` (hệ 4.0). Hệ số tín chỉ `> 0`.
   - Giới hạn độ dài chuỗi: Tên hoạt động `<= 150 ký tự`, vai trò `<= 100 ký tự`, mô tả chi tiết `<= 1000 ký tự`.
   - Luôn gọi `.Trim()` để loại bỏ khoảng trắng thừa đầu/cuối.

### C. Bảo mật Frontend (Next.js 16 + React 19)
1. **Chống XSS (Cross-Site Scripting):**
   - Tận dụng cơ chế Auto-escaping của React JSX để hiển thị dữ liệu an toàn.
   - Tuyệt đối không sử dụng `dangerouslySetInnerHTML` với bất kỳ dữ liệu nào do người dùng nhập hoặc từ API trả về.
2. **Bảo vệ Route (Client Auth Guard):**
   - Các trang cá nhân hóa (`/profile/*`, `/academic/*`) phải kiểm tra trạng thái xác thực. Nếu chưa đăng nhập, tự động chuyển hướng (Redirect) về `/login` hoặc hiển thị thông báo yêu cầu đăng nhập.
3. **Ẩn danh dữ liệu nhạy cảm (PII Anonymization):**
   - Tuân thủ quy chuẩn Scope of Work: Dữ liệu cá nhân (Họ tên, SĐT, Email) tuyệt đối không gửi sang mô hình AI (Advisor Service). Khi phân tích hồ sơ hoặc gợi ý trường, chỉ gửi các chỉ số trừu tượng (GPA, Bậc học, Ngân sách, Ngoại khóa).

