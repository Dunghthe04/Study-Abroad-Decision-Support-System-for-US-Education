# BIÊN BẢN CHI TIẾT PHIÊN LÀM VIỆC & LỊCH SỬ TRAO ĐỔI (SPRINT 1)
**Dự án:** USAS – Study Abroad Decision Support System for US Education  
**Sinh viên:** Nguyễn Xuân Đức (`ducnxhe186870@fpt.edu.vn`)  
**Ngày:** 04/10/2026

---

## I. TỔNG QUAN CÁC YÊU CẦU ĐÃ ĐẶT RA VÀ GIẢI QUYẾT

### 1. Phân chia phạm vi Sprint 1
* Bạn Đức phụ trách 2 User Story:
  - **USAS-364:** Hồ sơ tài chính & ngoại khóa
  - **USAS-365:** Phân tích điểm học thuật
* Quy tắc: Code nhánh riêng, không sửa lan man vào code của thành viên khác trong nhóm (Tuấn Việt làm Auth/Học thuật, Tuấn Dũng làm AI, Xuân Dương làm Dữ liệu trường).

### 2. Tiêu chuẩn commit (Quy định riêng cho Đức)
* Bắt buộc tách riêng commit cho từng phần:
  - `[DucNX]: BE- <nội dung>`
  - `[DucNX]: FE- <nội dung>`
  - `[DucNX]: Test- <nội dung>`
  - `[DucNX]: Docs- <nội dung>`
* Cùng là BE thì gom commit 1 lần ghi rõ, tương tự với FE, Test và Docs.
* Các file tài liệu gốc dùng chung (`Product_Backlog.xlsx`, `SOW_v6.docx`...) không commit vào git mà chỉ lưu ở local để AI đọc ngữ cảnh.

### 3. Vấn đề Cơ sở dữ liệu (Database Migration)
* **Hiện tượng:** Chạy `dotnet ef database update` bị lỗi `password authentication failed for user "studyabroad"`.
* **Nguyên nhân:** Máy tính đang chạy Windows service `postgresql-x64-18` (PostgreSQL 18) chiếm cổng 5432, trong khi dự án nhóm dùng container `pgvector/pgvector:pg16` qua Docker Compose.
* **Xử lý:** Tắt service `postgresql-x64-18` trong `services.msc`, bật Docker Desktop, chạy `docker compose up -d db` rồi chạy `dotnet ef database update`. Kết quả: Đã áp dụng đủ 3 migration (`InitialCreate`, `AddUsers`, `CoreSchema`).

### 4. Triển khai chức năng USAS-364 (Hồ sơ tài chính & ngoại khóa)
* **Backend:** Clean Architecture, Service + Repository, phòng chống IDOR (trích xuất userId từ Claims), kiểm tra hợp lệ ngân sách $\ge 0$.
* **Frontend:** Trang `/profile/financial`, form ngân sách, danh sách hoạt động ngoại khóa, bảng giải thưởng, inline validation.
* **Test:** 48 unit tests pass 100%.
* **Tài liệu:** Đã tạo `docs/report3_sections_USAS-364.md` (ERD Mermaid, UC-03, SCR-PROFILE-01).

### 5. Triển khai chức năng USAS-365 (Phân tích điểm học thuật & GPA)
* **Backend:**
  - Quy đổi GPA thang 4.0 chuẩn WES (đọc từ `app_settings` key `grade_scale.10`), fallback công thức tuyến tính $raw \times 0.4$.
  - Tính Unweighted GPA & Weighted GPA (theo tín chỉ / môn chuyên cộng thưởng 0.5).
  - Phân nhóm môn: STEM (Khoa học Tự nhiên), Xã hội & Nhân văn, Ngoại ngữ.
  - Phân tích xu hướng 3 năm (Upward / Consistent / Downward).
  - Đóng gói DTO và lưu vào `analysis_results` (`kind = "gpa"`).
* **Frontend:**
  - Trang `/profile/academic`.
  - Component: `GpaSummaryCard.tsx`, `SubjectGroupBreakdown.tsx`, `TermTrendChart.tsx`, `TranscriptScoreTable.tsx`.
  - Tích hợp nút *"📋 Nạp dữ liệu mẫu (3 năm)"* với 25 môn học chuẩn để demo ngay.
* **Test:** 91 unit tests pass 100%.
* **Tài liệu:** Đã tạo `docs/report3_sections_USAS-365.md` (ERD Mermaid, UC-04, SCR-PROFILE-02).

### 6. Đồng bộ Git lên GitHub
* Cả 2 nhánh đã được push an toàn lên remote `origin`:
  - `origin/feature/USAS-364-financial-profile-ducnx`
  - `origin/feature/USAS-365-academic-analysis-ducnx`
* Các nhánh `develop` và `main` hoàn toàn không bị ảnh hưởng.
