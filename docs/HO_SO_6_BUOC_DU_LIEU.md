# Hồ sơ 6 bước: dữ liệu lưu ở đâu (USAS-640)

Tài liệu cho người làm phần tính điểm / gợi ý trường (`docs/cach_tinh_extracted.txt`).
Mọi thứ học sinh nhập ở `/analysis` được lưu vào DB khi bấm **"Nhận gợi ý trường"** (POST `/api/v1/profile/flow/commit`).
Bộ tính điểm chỉ cần đọc các bảng dưới đây, không cần đọc nháp.

- Schema: `app`.
- Bảng chính: `student_profiles` (1 dòng/học sinh), `transcript_scores` (1 dòng/môn/học kỳ), `profile_activities` (1 dòng/hoạt động hoặc giải).
- Hồ sơ đã gửi xong khi `student_profiles.flow_submitted_at IS NOT NULL`.

## Bước 1: Bạn & mục tiêu → `student_profiles`

| Trên UI | Cột | Giá trị | Dùng cho |
|---|---|---|---|
| Cấp học hiện tại | `current_education_level` | `middle_school` \| `high_school` \| `college_university` \| `master` | Biết hồ sơ đang ở hệ nào |
| Lớp / năm | `current_grade` | `Lớp 6`…`Lớp 12`, `Năm 1`…`Năm 4`, `Năm cuối`, `Đã tốt nghiệp` | |
| Trường đang học | `current_school` | chữ tự do, có thể NULL | |
| Năm tốt nghiệp dự kiến | `graduation_year` | int, có thể NULL | Suy ra kỳ nhập học, xét **hạn nộp** (tầng 1) |
| Bậc muốn học ở Mỹ | `target_level` | key `studyLevels` trong metadata (`undergraduate`, `community_college`, `master`…) | Chọn nhánh chấm (CĐ không chấm khả năng đậu) |
| Ngành | `intended_major` | chữ tự do, có thể NULL (= mọi ngành) | Lọc **ngành** (tầng 1) |
| Bang | `preferred_states` | mảng mã bang, `{}` = không lọc | Lọc địa điểm |
| Loại trường | `target_school_type` | `all` \| `public` \| `private` (mặc định `all`) | Lọc loại trường |

`education_system` giữ nghĩa cũ (hệ chương trình). Luồng 6 bước không ghi vào cột này.

## Bước 2: Bảng điểm → `transcript_scores` + `student_profiles`

`transcript_scores` (khoá ngoại `student_profile_id`):

| Cột | Giá trị |
|---|---|
| `grade_name` | `Lớp 10`, `Lớp 11`, `Năm 1`… |
| `semester` | `1` hoặc `2`. Không còn phải đoán từ chuỗi |
| `term_name` | Luôn có dạng `"{grade_name} HK{semester}"` |
| `term_order` | Thứ tự kỳ tăng dần theo UI (Lớp 11 HK1 = 1, Lớp 11 HK2 = 2, …) |
| `subject`, `score` | Tên môn, điểm thang 10 (0–10) |

`student_profiles`:
- `overall_gpa`: GPA **thang 4** do BE tính. Mỗi môn lấy trung bình các học kỳ có điểm, rồi lấy trung bình các môn ra GPA10 (làm tròn 2 số), sau đó nhân `/10*4`.
  - Đây là cách quy đổi tạm, tuyến tính. Doc tính điểm (mục "Lưu ý về GPA đầu vào") yêu cầu ghi rõ cách quy đổi. Nếu đổi công thức thì tính lại từ `transcript_scores`.
- `grade_scale`: luôn `"10"`.
- `transcript_file_url`: file bảng điểm gốc, nếu có tải lên.

## Bước 3: Chứng chỉ → `student_profiles`

| Trên UI | Cột | Giá trị |
|---|---|---|
| Loại chứng chỉ tiếng Anh | `english_test_type` | `ielts` \| `toefl` \| `duolingo` \| `none` |
| Điểm tiếng Anh | `ielts` / `toefl` / `duolingo` | Chỉ cột đúng loại có giá trị, 2 cột còn lại NULL |
| Ngày thi (tháng) | `english_test_date` | date, ngày luôn là 01 (UI chỉ chọn tháng). Dùng để kiểm tra chứng chỉ còn hạn |
| SAT | `sat` | 400–1600 |
| ACT | `act` | 1–36 |
| AP | `other_tests_json` | `{"ap": <điểm 1–5>}` (một điểm) |

Dùng cho: lọc **tiếng Anh** (tầng 1), điểm vị trí **SAT** (tầng 2, bước 2). Không có SAT thì trọng số SAT bằng 0.

## Bước 4: Tài chính → `student_profiles`

| Trên UI | Cột | Giá trị |
|---|---|---|
| Mức ngân sách / năm | `budget_tier` | `tier_under_20k` \| `tier_20k_35k` \| `tier_35k_50k` \| `tier_50k_70k` \| `tier_above_70k` |
| | `budget_min_usd`, `budget_max_usd` | Lấy theo mức trong metadata (`app_settings`, key profile metadata). **`tier_above_70k` có `budget_max_usd = NULL`**, nghĩa là không giới hạn trên |
| Cần học bổng | `scholarship_need` | `mandatory` (bắt buộc) \| `preferred` (nên có) \| `optional` (không cần) |
| | `needs_scholarship` | bool, giữ cho code cũ: `true` khi `mandatory` hoặc `preferred` |

Dùng cho:
- Lọc **ngân sách** (tầng 1): so chi phí với `budget_max_usd`. Nếu NULL thì không loại trường nào vì ngân sách.
- Cờ **"Cần học bổng lớn"** (tầng 1, 3): trường vượt ngân sách vẫn được giữ khi `scholarship_need IN ('mandatory','preferred')`. Phân biệt bắt buộc và nên có là quyết định của người làm tính điểm.

Các cột cũ `max_expected_tuition_usd`, `funding_source`, `financial_notes` **không** được luồng 6 bước ghi.

## Bước 5: Hoạt động & giải thưởng → `profile_activities`

| Trên UI | Cột | Hoạt động (`kind = 'extracurricular'`) | Giải thưởng (`kind = 'award'`) |
|---|---|---|---|
| Tên | `title` | ✓ | ✓ |
| Phạm vi ảnh hưởng | `impact_level` | 1..5 | 1..5 |
| Vai trò | `role` | `founder` \| `leader` \| `vice_leader` \| `member` | NULL |
| Từ tháng | `start_date` | date (ngày 01) | NULL |
| Đến tháng | `end_date` | date (ngày 01), NULL nếu đang tham gia | NULL |
| Đang tham gia | `is_ongoing` | bool | false |
| Số tháng | `duration_months` | int, tính cả 2 đầu (09/2024 → 05/2026 = 21). Nếu đang tham gia thì tính đến tháng gửi hồ sơ | NULL |
| Năm đạt giải | `award_year` | NULL | int |

`impact_level`: 1 = Trường, 2 = Quận/huyện, 3 = Tỉnh/TP, 4 = Quốc gia, 5 = Quốc tế. DB có CHECK ràng buộc 1..5.

Đối chiếu với công thức điểm 1 hoạt động (tầng 2, bước 4):

| Ký hiệu trong doc | Lấy từ |
|---|---|
| **S** phạm vi (1–5) | `impact_level`. Đây là giá trị học sinh tự khai. LLM chỉ được giữ hoặc hạ |
| **R** giá trị vai trò | `role` → tra bảng quy ước R của doc (mã cố định 4 giá trị, không còn chữ tự do) |
| **M** số tháng | `duration_months` |
| **B** tổ chức uy tín | **Chưa thu thập**, xem bên dưới |
| Thưởng giải (tối đa 3 giải) | Các dòng `kind = 'award'`: dùng `impact_level` (cấp giải) + `award_year` |

`organization` và `description` không được UI 6 bước nhập (luôn NULL).

## Bước 6: Xem lại

Không có dữ liệu riêng. Khi bấm gửi, BE validate (thiếu thì trả 400 kèm tên bước), ghi toàn bộ trong 1 transaction, đặt `flow_submitted_at = now()` và xoá nháp.

## Những gì doc tính điểm cần nhưng hồ sơ chưa thu thập

1. **B: tổ chức có uy tín hay không.** Không có trường nhập. Theo doc, LLM suy ra từ `title` (tên hoạt động).
2. **Kỳ nhập học dự kiến** (để xét hạn nộp). Chưa hỏi trực tiếp. Tạm suy: nhập học mùa thu năm `graduation_year`, nếu NULL thì coi là kỳ gần nhất.
3. **GPA có trọng số (honors/AP)**: không phân biệt. Bảng điểm VN chỉ có thang 10.
4. **Điểm tiếng Anh từng kỹ năng** (nghe/nói/đọc/viết): chỉ lưu điểm tổng.

## Nháp (không dùng cho tính điểm)

`student_profiles.draft_flow_state_json` = `{ currentStep, completedSteps, draft, draftTranscript, updatedAt }`, trong đó `draft` đúng shape FE (`frontend/src/types/profile-flow.ts`, BE: `ProfileFlowDraftDto`). Sau khi gửi thì cột này là NULL. Khi mở lại `/analysis`, BE dựng lại `draft` từ các bảng trên.
