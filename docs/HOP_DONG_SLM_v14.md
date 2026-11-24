# Hợp đồng đầu ra của SLM (v14, chốt 24/11/2026)

Rút từ file dữ liệu `usas_train_model_v13.xlsx` (20.333 dòng) sau khi chốt. Phạm vi: chỉ bậc đại học. SLM chỉ **phân loại và rút thông tin**, code tra dữ liệu và dựng câu trả lời.

Đầu ra: `{"intents": [{"type", "span", "entities"}, ...]}`. `span` là chữ nguyên văn trong câu hỏi. Nhiều nhất 7 ý.

Cột "Code" cho biết `advisor/app/schemas/slm.py` đã nhận chưa: **có** = đã có, **MỚI** = cần Dũng thêm.

## 1. Sáu loại ý (`type`)
`collect_profile`, `profile_review`, `scenario`, `knowledge`, `center_referral`, `out_of_scope`.

## 2. collect_profile (khai và quản lý hồ sơ)
- `tasks` (tối đa 1, vắng = khai giá trị): `edit` (sửa), `status` (hỏi hồ sơ có gì, thiếu gì), `restart` (nhập lại từ đầu).
- `follow_up`: true / false.
- Khóa hồ sơ (20), giá trị là chữ nguyên văn: `current_level`, `current_grade`, `major`, `intake`, `education_system`, `gpa`, `sat`, `act`, `english`, `english_plan`, `standardized_plan`, `budget`, `scholarship`, `funding_source`, `activities`, `awards`, `relatives_state`, `preferred_states`, `school_type`, `visa_refused`.
- **BỎ `target_level`** (chat không hỏi bậc học nữa, mặc định undergraduate). Code: **MỚI** (bỏ khỏi danh sách khóa).
- Mỗi tin chỉ một ý `collect_profile`, luôn đứng đầu.

## 3. profile_review (tư vấn theo hồ sơ của chính học sinh)
- `tasks` (có thể nhiều): `assessment`, `school_list`, `roadmap`, `compare` (kèm ≥ 2 trường), `explain_school`, **`explain_fit`**.
  - `explain_fit` = vì sao kết quả gợi ý ra như vậy (điểm phù hợp, nhóm Reach / Match / Safety) cho một trường. Code: **MỚI**.
  - `explain_school` = trường này có hợp em không, có nên nộp không, thông tin trường theo hồ sơ em. Code: có.
  - Hỏi cả hai thì `tasks: ["explain_fit", "explain_school"]`.
  - Chưa rõ muốn gì thì `tasks: []`.
- `schools`: danh sách tên trường (luôn là list).
- `follow_up`: true / false.

## 4. scenario (what-if và đặt mục tiêu)
- `mode`: `what_if` hoặc `goal_seeking`.
- what_if: `changes: [{factor, value, unit | action}]`, thêm `school` nếu nêu trường.
  - `factor`: `gpa`, `sat`, `act`, `english`, `budget`, `scholarship`, `priority_cost`, `priority_aid`, `priority_prestige` (code: có); **`application_round`** (ED / EA / RD), **`state`**, **`major`**, **`exchange_rate`** (code: **MỚI**, kiểm tra thêm `state`, `major`).
  - `unit`: `scale_4`, `scale_10`, `USD`, `VND`, `ielts`, `toefl`, `duolingo`, **`percent`** (MỚI, dùng cho `exchange_rate`).
  - `action`: `skip` (bỏ một yếu tố).
- goal_seeking: `school`, `factor` (list: `gpa`, `sat`, `act`, `english`, `activities`), `target_group` (`reach`, `target`, `safety`).
- `target` trong hệ thống = nhóm Match (Vừa sức).
- `follow_up`: true / false.

## 5. knowledge (kiến thức và số liệu trường)
`tasks` (tối đa 1; vắng = kiến thức du học chung, đi kho tri thức):
- **`school_info`**: `schools` (1 trường), `fields` (list mã mục).
- **`compare_facts`**: `schools` (≥ 2) hoặc `group` (`reach` / `target` / `safety`), `fields`.
- **`search`**: `filters` và/hoặc `sort`.

**Mã mục `fields` (27):**

| Nhóm | Mã | Code |
|---|---|---|
| Đã có | `cost`, `aid`, `english_requirement`, `test_policy`, `gpa`, `acceptance_rate`, `deadline`, `graduation_rate`, `majors`, `ranking`, `location`, `website`, `housing`, `student_faculty_ratio`, `student_activities`, `campus_setting`, `other` | có |
| Mới | `tuition_in_state`, `tuition_out_of_state`, `living_cost`, `insurance`, `application_fee`, `act_range`, `sat_range`, `sat_submit_rate`, `international_acceptance_rate`, `scholarships` | **MỚI** |

- **Không còn `international_aid`** (gộp vào `aid`).
- `aid` = có học bổng / hỗ trợ không, mức trung bình. `scholarships` = liệt kê học bổng có tên của trường.
- `other` = thứ hệ thống chưa lưu (thư giới thiệu, phỏng vấn đầu vào, luận phụ, việc làm sau tốt nghiệp, xếp hạng theo ngành, thể thao, co-op, nghiên cứu, honors…): hệ thống trả "chưa có thông tin" và chỉ website. Không đoán mục: học sinh không nêu mục nào thì bỏ khóa `fields`.

**`filters` (15):** `state`, `city`, `major`, `school_type` (public / private), `aid` (yes / need_based / merit_based), `test_policy` (optional / required), `max_cost`, `max_ielts`, `max_toefl`, `max_duolingo`, `min_acceptance_rate`, `max_acceptance_rate`, `max_rank`, `min_graduation_rate`, `ivy` (yes).

**`sort` (6):** `ranking`, `lowest_cost`, `highest_cost`, `easiest_admission`, `most_selective`, `most_aid`.

`follow_up`: true / false.

## 6. center_referral (trung tâm tư vấn)
- `tasks` (tối đa 1): vắng = tìm; `booking`; `compare` (kèm `centers` ≥ 2).
- `service`: `SCHOOL_SELECTION`, `ESSAY_EDITING`, `APPLICATION_SUBMISSION`, `VISA_INTERVIEW_PREP`, `TEST_PREP`, `SCHOLARSHIP_SEARCH`.
- `city`, `centers`, `follow_up`.

## 7. out_of_scope (đứng một mình)
`subtype` bắt buộc: `exception` (ngoài phạm vi: nước khác, thạc sĩ, tiến sĩ, THPT tại Mỹ, EB-5, mua quốc tịch…), `sensitive` (gian lận, làm giả, làm chui, kết hôn giả, trốn ở lại), `greeting`, `farewell`, `about_system`, `ambiguous`, `human_handoff`.

Chat trả lời cho `exception` về bậc học: "hiện chỉ hỗ trợ bậc đại học". Cao đẳng cộng đồng 2+2 và chuyển tiếp 2+2 **không** bị coi là ngoài phạm vi.

## 8. Quy tắc chung
- `collect_profile`, `profile_review`, `scenario`: mỗi loại tối đa một ý trong một tin. `knowledge` và `center_referral` có thể lặp.
- Giá trị hồ sơ chỉ ghi trong ý `collect_profile`.
- Câu nối tiếp thiếu thông tin ("còn Yale thì sao") thì `follow_up: true`, lấy phần thiếu từ `context`.
- Câu có "vì sao / tại sao" + điểm, nhóm, Reach, Match, Safety → `explain_fit`. Câu có nên nộp, có hợp không → `explain_school`.
- "Cần gì để vào <trường>" luôn là `scenario/goal_seeking`, không phải `profile_review`.

## 9. Chưa vào hợp đồng đợt này (DB có dữ liệu nhưng chưa có nhãn)
Số sinh viên quốc tế, trường coi trọng yếu tố nào khi xét tuyển, trường có ED / EA hay không, task tìm học bổng theo bộ lọc, task chi phí sinh hoạt theo bang, và gợi ý học bổng theo hồ sơ. Hiện các câu này rơi vào `other` hoặc kho tri thức.
