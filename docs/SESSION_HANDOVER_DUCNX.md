# TÀI LIỆU BÀN GIAO PHIÊN LÀM VIỆC & NGỮ CẢNH DỰ ÁN (SESSION HANDOVER)
**Người thực hiện:** Nguyễn Xuân Đức (`ducnxhe186870@fpt.edu.vn`)  
**Dự án:** USAS – Study Abroad Decision Support System for US Education (Đồ án tốt nghiệp Fall 2026)  
**Thời gian tạo:** 2026-10-04 21:55  
**Mục đích:** Đồng bộ ngữ cảnh làm việc cho AI Assistant khi chuyển đổi giữa Máy Nhà và Máy Công Ty.

---

## 1. PROMPT KÍCH HOẠT NHANH TRÊN MÁY MỚI (Copy & Paste vào Chat)

> *"Chào bạn, tôi là Nguyễn Xuân Đức. Hãy đọc ngay file `docs/SESSION_HANDOVER_DUCNX.md` và `docs/DEVELOPMENT_GUIDELINE.md` để nắm toàn bộ ngữ cảnh, phân công Sprint 1, các quy chuẩn lập trình, bảo mật và quy tắc commit của tôi từ phiên làm việc trước."*

---

## 2. THÔNG TIN THÀNH VIÊN & PHÂN CÔNG SPRINT 1 (01/10 - 08/10)

* **Thành viên:** Nguyễn Xuân Đức
* **Phạm vi phân công trong Sprint 1 (Bắt buộc):**
  1. **USAS-364 (Story 3):** Hồ sơ tài chính & ngoại khóa
     - Nhánh Git: `feature/USAS-364-financial-profile-ducnx` (ĐÃ XONG & ĐÃ PUSH LÊN GITHUB).
  2. **USAS-365 (Story 4):** Phân tích điểm học thuật & quy đổi GPA
     - Nhánh Git: `feature/USAS-365-academic-analysis-ducnx` (ĐÃ XONG & ĐÃ PUSH LÊN GITHUB).
  3. **Tài liệu Báo cáo Report 3 (SRS):**
     - Đã soạn xong Mục I.3 (ERD, Data Dictionary, Business Rules), Use Cases (UC-03, UC-04), Screen Specs (SCR-PROFILE-01, SCR-PROFILE-02).
     - Lưu tại: `docs/report3_sections_USAS-364.md` và `docs/report3_sections_USAS-365.md`.

---

## 3. CÁC QUY CHUẨN BẮT BUỘC ĐÃ THỐNG NHẤT

### A. Quy tắc đặt tên Commit (BẮT BUỘC):
Tách riêng từng phần, tuyệt đối không gom chung BE + FE + Test:
- `[DucNX]: BE- <nội dung>`: Backend .NET 8 (Domain, Application, Infrastructure, Controller).
- `[DucNX]: FE- <nội dung>`: Frontend Next.js (Components, Pages, Types, API Client).
- `[DucNX]: Test- <nội dung>`: Unit Tests (xUnit).
- `[DucNX]: Docs- <nội dung>`: Tài liệu Report 3, guideline kỹ thuật.

### B. Quy tắc Bảo mật & Phòng thủ:
1. **Anti-IDOR triệt để:** Không bao giờ tin cậy `userId` hay `profileId` từ request payload của client. Luôn trích xuất từ JWT Claims Principal (`User.FindFirst(ClaimTypes.NameIdentifier)`).
2. **Defensive Validation 2 lớp:**
   - Frontend: Validate form inline ngay khi nhập.
   - Backend: Kiểm tra biên chặt chẽ (Điểm hệ 10 phải $[0.0, 10.0]$, ngân sách $\ge 0$, tín chỉ $> 0$), trả về RFC 7807 `ProblemDetails`.

### C. Cơ chế Git & Phân nhánh:
- **Nguyên tắc "Đợi lệnh mới push":** AI chỉ được commit ở local, không tự ý push hay tạo PR trừ khi Đức yêu cầu rõ ràng.
- **Không commit tài liệu gốc dùng chung:** Các file `.xlsx`, `.docx`, `sheets/` chỉ để trên máy cục bộ cho AI đọc ngữ cảnh, không commit vào mã nguồn (đã cấu hình ẩn trong `.git/info/exclude`).

---

## 4. CHI TIẾT KỸ THUẬT ĐÃ HOÀN THÀNH

### A. USAS-364: Hồ sơ tài chính & ngoại khóa
* **Branch:** `feature/USAS-364-financial-profile-ducnx`
* **Backend:**
  - Entities ánh xạ chuẩn: `student_profiles` (`AnnualBudgetUsd`, `FundingSource`, `NeedsScholarship`), `profile_activities` (`Kind = "extracurricular" | "award"`).
  - Service & Repository: `FinancialProfileRepository.cs`, `FinancialProfileService.cs`, `FinancialProfileController.cs`.
* **Frontend:**
  - Route: `/profile/financial`
  - Components: `FinancialCard.tsx`, `ExtracurricularSection.tsx`, `AchievementsSection.tsx`.
* **Test:** 48 Unit tests passed 100%.

### B. USAS-365: Phân tích điểm học thuật & GPA
* **Branch:** `feature/USAS-365-academic-analysis-ducnx`
* **Backend:**
  - Quy đổi GPA 4.0 chuẩn WES đọc động từ `app_settings` (`grade_scale.10`), fallback công thức tuyến tính $raw \times 0.4$.
  - Tính Unweighted GPA & Weighted GPA (theo tín chỉ / môn chuyên).
  - Phân nhóm môn học: STEM (Khoa học Tự nhiên), Xã hội & Nhân văn, Ngoại ngữ.
  - Phân tích xu hướng 3 năm (Upward / Consistent / Downward).
  - Controller: `AcademicAnalysisController.cs` (`/api/v1/profile/academic`).
* **Frontend:**
  - Route: `/profile/academic`
  - Components: `GpaSummaryCard.tsx`, `SubjectGroupBreakdown.tsx`, `TermTrendChart.tsx`, `TranscriptScoreTable.tsx` (có sẵn nút Nạp dữ liệu mẫu 3 năm).
* **Test:** 91 Unit tests passed 100%.

---

## 5. HƯỚNG DẪN TEST TRÊN MÁY MỚI (MÁY CÔNG TY)

1. **Clone repo:**
   ```bash
   git clone https://github.com/Dunghthe04/Study-Abroad-Decision-Support-System-for-US-Education.git
   cd Study-Abroad-Decision-Support-System-for-US-Education
   ```
2. **Checkout nhánh cần test:**
   ```bash
   git checkout feature/USAS-365-academic-analysis-ducnx
   # hoặc git checkout feature/USAS-364-financial-profile-ducnx
   ```
3. **Chạy Database (Docker):**
   ```bash
   docker compose up -d db
   ```
4. **Chạy Test Backend:**
   ```bash
   cd backend
   dotnet test
   ```
5. **Chạy Frontend:**
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
