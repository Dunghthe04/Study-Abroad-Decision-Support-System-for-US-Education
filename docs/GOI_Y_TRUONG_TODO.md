# Gợi ý trường: danh sách việc cần làm

Làm theo 2 tài liệu: `GOI_Y_TRUONG_TONG_QUAN (2).docx` (công thức) và `GOI_Y_TRUONG_AI_CACH_THUC_HIEN.docx` (cách AI chạy 3 lượt).

Đánh dấu `[x]` khi xong. Cột "Ai" là người phụ trách chính.

---

## Giai đoạn 0: Dọn dẹp và chuẩn bị (đã xong)

- [x] Xóa code gợi ý cũ (SAW, sai concept) ở backend, advisor và frontend
- [x] Giữ khung `RecommendationService`, `RecommendSettings`, DTO, repository
- [x] Cấu hình Ollama chạy 100% GPU (`num_gpu: 99`, `num_ctx: 6144`, `keep_alive: 30m`)
- [x] Đổi tiêu đề cột file Excel thu thập dữ liệu cho dễ đọc, thêm sheet `Chu_giai_cot`

---

## Giai đoạn 1: Dữ liệu trường và học bổng

- [x] **1.1** Chốt danh mục ngành: dùng 38 nhóm ngành CIP (CDS J1) cho DB và dropdown ở form (Nhóm), xem [Phụ lục A](#phụ-lục-a-38-nhóm-ngành-cip)
- [ ] **1.2** Chốt `data_quality` lưu dạng jsonb (Nhóm)
- [ ] **1.3** Migration thêm cột `university_offerings` (Dũng)
  - [ ] C1: `applicants_total`, `admitted_total`, `intl_applicants`, `intl_admitted`, `intl_acceptance_rate`
  - [ ] C7: `c7_gpa`, `c7_test`, `c7_ec_importance`
  - [ ] C9: `sat_submit_pct`, `act25`, `act75`
  - [ ] C11/C12: `gpa_submit_pct`, 7 cột `gpa_pct_*`
  - [ ] Chi phí: `other_expenses_usd`, `coa_total_usd`, `cost_year`
  - [ ] H6: `intl_aid_policy`, `intl_aid_avg_usd`, `intl_aid_recipients`
  - [ ] B: `retention_rate_1yr`, `grad_rate_6yr`
  - [ ] Nguồn: `cds_year`, `cds_source_file`, `cost_source_url`, `english_source_url`
  - [ ] `data_quality` (jsonb)
- [ ] **1.4** Migration thêm cột `scholarships`: `type`, `intl_eligible`, `renewable`, `num_awards`, `verified`, `source_basis` (Dũng)
- [ ] **1.5** Script nhập Excel vào DB, chạy lại được (upsert theo `code`), có báo cáo thêm/sửa/lỗi (Dũng)
- [ ] **1.6** Bổ sung dữ liệu còn thiếu: tiếng Anh tối thiểu, ngành, tỷ lệ giữ chân, tỷ lệ đỗ du học sinh, xử lý `Manual Review Queue` (Dương)

---

## Giai đoạn 2: Hồ sơ học sinh (đầu vào)

- [ ] **2.1** `profile_activities` thêm phạm vi (1–5), số tháng, đang tiếp diễn; giải thưởng có cấp giải (Đức)
- [ ] **2.2** GPA lưu thang 4 ở `overall_gpa`
- [ ] **2.3** Form: thêm ngành (dropdown CIP), bang, loại trường; ngân sách và 3 mức cần học bổng (Frontend)

---

## Giai đoạn 3: Công thức bằng C# (đáp án, dự phòng, kiểm tra)

- [x] **3.1** Tầng 1, lọc cứng: ngành, bang, loại trường, tiếng Anh, ngân sách × 1,1, mức học bổng (Dũng). Đã bỏ hạn nộp: hạn lặp hằng năm, chỉ hiển thị
- [ ] **3.2** Tầng 2, chấm điểm (Dũng)
  - [ ] Trọng số C7 (3/2/1/0), test-optional (nộp < 50% → L_SAT × 0,5)
  - [ ] Điểm vị trí SAT (nội suy 25/75)
  - [ ] Điểm vị trí GPA (phân bố C11 hoặc GPA trung bình + bảng z, SD 0,3)
  - [ ] Bậc ngoại khóa 1–5 và điều chỉnh `(bậc − 3) × L_EC`
  - [ ] Chia nhóm theo S (25/75), hạ nhóm khi r du học sinh < 0,5 ("Khó vào xa")
- [ ] **3.3** Tầng 3: F/H/Q, trọng số theo nút ưu tiên, chọn 3/5/4 (tối đa 12), khối AID (Dũng)
- [ ] **3.4** Tầng 4: mã lý do (GPA_*, SAT_*, BUDGET_*, INTL_LOW, EC_*) (Dũng)
- [ ] **3.5** Unit test theo ví dụ trong tài liệu: An, Colgate, Trường B, Trường C, Bình (Dũng)

---

## Giai đoạn 4: AI 3 lượt (advisor)

- [ ] **4.1** Bộ đánh giá 20–30 hồ sơ mẫu, đáp án lấy từ giai đoạn 3, lưu vào `EvalCase` (Dũng)
- [ ] **4.2** Lượt ②, chấm trường: `skill_score.md`, JSON schema, chia lô khoảng 8 trường (Dũng)
- [ ] **4.3** Đo trên bộ đánh giá: % nhóm đúng, sai số S, độ ổn định, thời gian; sửa skill theo kết quả (Dũng)
- [ ] ⚑ **4.4** Điểm quyết định: ≥ 90% nhóm đúng thì AI là phần chính; chưa đạt thì dùng code chấm, AI đọc hiểu và giải thích (Nhóm)
- [ ] **4.5** Lượt ①, bậc ngoại khóa: `skill_ec.md` (Dũng, cần 2.1)
- [ ] **4.6** Lượt ③, chọn danh sách: `skill_pick.md`, mã lý do (Dũng)
- [ ] **4.7** Endpoint advisor cho 3 lượt, kiểm tra JSON, gọi lại khi sai định dạng (Dũng)

---

## Giai đoạn 5: Ghép luồng trong .NET

- [ ] **5.1** `RecommendationService`: lọc → JSON gọn → gọi 3 lượt → so với kết quả code (lệch thì dùng code và gắn cờ) → dự phòng khi AI lỗi (Dũng)
- [ ] **5.2** Lưu kết quả kèm `skill_version`, `data_version`, model; ghi log `AiCall` (Dũng)
- [ ] **5.3** DTO màn 9: 4 khối, mã lý do, cờ thiếu dữ liệu; API đổi nút ưu tiên chỉ chạy lại lượt ③ (Dũng)
- [ ] **5.4** Tài liệu API cho frontend (Dũng)

---

## Giai đoạn 6: Giao diện, kiểm thử, báo cáo

- [ ] **6.1** Màn 9: 4 khối, 4 nút ưu tiên, mã lý do thành câu, cờ thiếu dữ liệu, tiến độ theo lô (Frontend)
- [ ] **6.2** Kiểm thử đầu cuối với hồ sơ thật, đo thời gian trên máy demo (Dũng, Phương Anh)
- [ ] **6.3** Báo cáo: phương pháp, AHP, kết quả đánh giá AI, cập nhật Report 3 (Dũng)
- [ ] **6.4** Commit, mở PR, review (Dũng)

---

## Thứ tự làm

```
1.1, 1.2 → 1.3, 1.4 → 1.5 → 3.1–3.5 → 4.1 → 4.2 → 4.3 → ⚑ 4.4 → 4.5, 4.6, 4.7 → 5.1–5.4 → 6.1 → 6.2 → 6.3, 6.4
Làm song song: 1.6 (Dương), 2.1 (Đức), 2.2, 2.3
```

---

## Phụ lục A: 38 nhóm ngành CIP

DB lưu tên gốc, form hiển thị tên tiếng Việt. Dropdown đưa nhóm phổ biến (11, 14, 52, 45, 26, 51) lên đầu.

| Mã CIP | Tên gốc (DB) | Tên tiếng Việt |
|---|---|---|
| 01 | Agriculture | Nông nghiệp |
| 03 | Natural resources and conservation | Tài nguyên thiên nhiên và bảo tồn |
| 04 | Architecture | Kiến trúc |
| 05 | Area, ethnic, and gender studies | Nghiên cứu khu vực, dân tộc và giới |
| 09 | Communication/journalism | Truyền thông và báo chí |
| 10 | Communication technologies | Công nghệ truyền thông |
| 11 | Computer and information sciences | Khoa học máy tính và công nghệ thông tin |
| 12 | Personal and culinary services | Dịch vụ cá nhân và ẩm thực |
| 13 | Education | Giáo dục và sư phạm |
| 14 | Engineering | Kỹ thuật |
| 15 | Engineering technologies | Công nghệ kỹ thuật |
| 16 | Foreign languages, literatures, and linguistics | Ngoại ngữ, văn học nước ngoài và ngôn ngữ học |
| 19 | Family and consumer sciences | Khoa học gia đình và tiêu dùng |
| 22 | Law/legal studies | Luật |
| 23 | English | Ngôn ngữ và văn học Anh |
| 24 | Liberal arts/general studies | Khai phóng và nghiên cứu tổng quát |
| 25 | Library science | Thư viện học |
| 26 | Biological/life sciences | Sinh học và khoa học sự sống |
| 27 | Mathematics and statistics | Toán và thống kê |
| 29 | Military science and military technologies | Khoa học quân sự |
| 30 | Interdisciplinary studies | Liên ngành |
| 31 | Parks and recreation | Công viên, giải trí và thể dục thể thao |
| 38 | Philosophy and religious studies | Triết học và tôn giáo học |
| 39 | Theology and religious vocations | Thần học |
| 40 | Physical sciences | Khoa học vật lý (Vật lý, Hóa học, Thiên văn…) |
| 41 | Science technologies | Công nghệ khoa học |
| 42 | Psychology | Tâm lý học |
| 43 | Homeland Security, law enforcement, firefighting, and protective services | An ninh, thực thi pháp luật và phòng cháy |
| 44 | Public administration and social services | Hành chính công và công tác xã hội |
| 45 | Social sciences | Khoa học xã hội (Kinh tế, Chính trị, Xã hội học…) |
| 46 | Construction trades | Nghề xây dựng |
| 47 | Mechanic and repair technologies | Cơ khí và sửa chữa |
| 48 | Precision production | Sản xuất chính xác |
| 49 | Transportation and materials moving | Vận tải và vận chuyển vật liệu |
| 50 | Visual and performing arts | Nghệ thuật thị giác và biểu diễn |
| 51 | Health professions and related programs | Y tế và các ngành sức khỏe |
| 52 | Business/marketing | Kinh doanh, quản trị và marketing |
| 54 | History | Lịch sử |
