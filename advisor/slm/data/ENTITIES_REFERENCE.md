# Từ điển dữ liệu SLM: loại ý và khóa entities

Sinh tự động từ `app/schemas/slm.py` (khóa và giá trị hợp lệ) và `du_lieu_slm.jsonl` (26024 tin, 29585 ý: ví dụ và số lần gặp). Sửa schema thì chạy lại `python -m slm.prepare.build_entities_reference`. Luật gán nhãn: `HUONG_DAN_GAN_NHAN.md`.

## 1. Một ý

Mỗi tin tách thành danh sách ý. Mỗi ý có đúng ba khóa:

| Khóa | Nội dung |
|---|---|
| `type` | Một trong sáu loại ở mục 2 |
| `span` | Đoạn chữ của ý, có nguyên văn trong tin |
| `entities` | Thông tin tách ra (mục 3 đến 5), không có thì `{}` |

## 2. Sáu loại ý

| Loại | Khi nào dùng | Số ý trong dữ liệu |
|---|---|---|
| `collect_profile` | Học sinh khai hoặc sửa thông tin về mình, hoặc trả lời câu hệ thống đang chờ | 4930 (17%) |
| `profile_review` | Muốn được tư vấn dựa trên hồ sơ của chính mình (đánh giá hồ sơ, nên chọn trường nào) | 4637 (16%) |
| `scenario` | Thử "nếu như…" (what_if) hoặc hỏi cần gì để vào một trường (goal_seeking) | 4677 (16%) |
| `knowledge` | Hỏi kiến thức chung hoặc số liệu trường, không cần hồ sơ của học sinh | 6332 (21%) |
| `center_referral` | Tìm, so sánh hoặc đặt lịch với trung tâm tư vấn | 4775 (16%) |
| `out_of_scope` | Ngoài phạm vi: lời chào, cảm ơn, không rõ ý, nội dung nhạy cảm, hỏi về trợ lý | 4234 (14%) |

## 3. Khóa entities của từng loại

Cột "Số ý" là số ý có khóa đó. Khóa nào cũng có thể vắng; vắng nghĩa là học sinh không nhắc tới.

### `collect_profile`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `tasks` | danh sách, mã trong: `edit`, `status`, `restart` | Việc học sinh muốn làm (bảng nhiệm vụ bên dưới). Vắng là việc mặc định của loại | «Start over» → `["restart"]` | 1363 |
| `follow_up` | `true` hoặc `false` | true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được | «ý em là 32» → `true` | 345 |

Ngoài ra ý `collect_profile` mang các khóa hồ sơ ở mục 5.

### `profile_review`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `schools` | danh sách chữ | Tên trường, nguyên văn như học sinh gõ (danh sách) | «thế còn BU» → `["BU"]` | 2293 |
| `tasks` | danh sách, mã trong: `assessment`, `school_list`, `roadmap`, `compare`, `explain_school`, `explain_fit` | Việc học sinh muốn làm (bảng nhiệm vụ bên dưới). Vắng là việc mặc định của loại | «thế còn BU» → `["explain_school"]` | 4637 |
| `follow_up` | `true` hoặc `false` | true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được | «thế còn BU» → `true` | 711 |

### `scenario`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `changes` | danh sách object | Các thay đổi của what_if: danh sách {factor, value, unit}, hoặc {factor, action: skip} để bỏ một yếu tố | «vậy ACT 29» → `[{"factor": "act", "value": "29"}]` | 3237 |
| `factor` | danh sách chữ | Yếu tố cần thay đổi của goal_seeking (danh sách, ví dụ sat, gpa, activities) | «thế còn ASU» → `["gpa"]` | 713 |
| `mode` | một trong: `what_if`, `goal_seeking` | Kiểu kịch bản: thử "nếu như" hay hỏi cần gì để vào trường | «BU thì sao» → `"goal_seeking"` | 4677 |
| `school` | chữ nguyên văn | Trường mục tiêu của goal_seeking, nguyên văn | «BU thì sao» → `"BU"` | 1741 |
| `target_group` | một trong: `reach`, `target`, `safety` | Nhóm đích của goal_seeking | «BU thì sao» → `"safety"` | 534 |
| `follow_up` | `true` hoặc `false` | true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được | «BU thì sao» → `true` | 807 |

### `knowledge`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `fields` | danh sách mã (bảng mục thông tin) | Mục thông tin trường học sinh hỏi (danh sách mã). Học sinh không nêu mục thì bỏ khóa | «còn SAT?» → `["test_policy"]` | 3798 |
| `filters` | object (bảng điều kiện lọc) | Điều kiện tìm trường (object, bảng điều kiện lọc bên dưới) | «top 50 thôi» → `{"max_rank": "top 50"}` | 696 |
| `group` | một trong: `reach`, `target`, `safety` | Nhóm kết quả của học sinh, thay cho tên trường (đi với compare_facts) | «so sánh các trường nhóm khó vào» → `"reach"` | 45 |
| `schools` | danh sách chữ | Tên trường, nguyên văn như học sinh gõ (danh sách) | «còn SAT?» → `["Penn State"]` | 3917 |
| `sort` | một trong: `ranking`, `lowest_cost`, `highest_cost`, `easiest_admission`, `most_selective`, `most_aid` | Cách xếp danh sách học sinh muốn. Không nói thì bỏ khóa | «rẻ nhất lên đầu» → `"lowest_cost"` | 687 |
| `tasks` | danh sách, mã trong: `school_info`, `compare_facts`, `search` | Việc học sinh muốn làm (bảng nhiệm vụ bên dưới). Vắng là việc mặc định của loại | «còn SAT?» → `["school_info"]` | 4953 |
| `follow_up` | `true` hoặc `false` | true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được | «còn SAT?» → `true` | 565 |

### `center_referral`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `centers` | danh sách chữ | Tên các trung tâm cần so sánh (từ hai tên), nguyên văn | «IDP có gì khác PEC» → `["IDP", "PEC"]` | 666 |
| `city` | chữ nguyên văn | Thành phố, nguyên văn như học sinh viết ("Sài Gòn", "ha noi") | «em tìm ở HN» → `"HN"` | 2806 |
| `service` | một trong: `SCHOOL_SELECTION`, `ESSAY_EDITING`, `APPLICATION_SUBMISSION`, `VISA_INTERVIEW_PREP`, `TEST_PREP`, `SCHOLARSHIP_SEARCH` | Dịch vụ cần tìm (bảng dịch vụ bên dưới) | «em tìm ở HN» → `"APPLICATION_SUBMISSION"` | 3386 |
| `tasks` | danh sách, mã trong: `booking`, `compare` | Việc học sinh muốn làm (bảng nhiệm vụ bên dưới). Vắng là việc mặc định của loại | «cho em đặt lịch» → `["booking"]` | 1209 |
| `follow_up` | `true` hoặc `false` | true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được | «em tìm ở HN» → `true` | 1292 |

### `out_of_scope`

| Khóa | Giá trị hợp lệ | Ý nghĩa | Ví dụ thật | Số ý |
|---|---|---|---|---|
| `subtype` | một trong: `exception`, `sensitive`, `greeting`, `farewell`, `ambiguous`, `about_system`, `human_handoff` | Loại ngoài phạm vi (bắt buộc, bảng bên dưới) | «ừ» → `"ambiguous"` | 4234 |

## 4. Giá trị hợp lệ

### Nhiệm vụ (`tasks`)

| Loại | Nhiệm vụ | Nghĩa |
|---|---|---|
| `collect_profile` | (vắng) | Khai giá trị hồ sơ hoặc trả lời câu đang chờ |
| `collect_profile` | `edit` | Sửa một mục đã nhập (kèm mục hồ sơ đó) |
| `collect_profile` | `status` | Hỏi đã nhập gì, còn thiếu gì |
| `collect_profile` | `restart` | Nhập lại từ đầu |
| `profile_review` | (vắng) | Chưa rõ muốn gì (tasks rỗng): hệ thống hỏi lại bằng nút |
| `profile_review` | `assessment` | Đánh giá hồ sơ |
| `profile_review` | `school_list` | Gợi ý danh sách trường theo hồ sơ |
| `profile_review` | `roadmap` | Lộ trình chuẩn bị |
| `profile_review` | `compare` | So sánh trường theo hồ sơ (cần schools từ hai tên) |
| `profile_review` | `explain_school` | Trường này có hợp với hồ sơ em không, có nên nộp không |
| `profile_review` | `explain_fit` | Vì sao điểm phù hợp hoặc nhóm Reach, Match, Safety của một trường ra như vậy |
| `knowledge` | (vắng) | Kiến thức chung (entities rỗng) |
| `knowledge` | `school_info` | Số liệu của một trường (schools, fields) |
| `knowledge` | `compare_facts` | So sánh sự thật của từ hai trường (schools từ hai tên, hoặc group), kèm fields |
| `knowledge` | `search` | Tìm trường theo điều kiện (filters và/hoặc sort), không nêu trường nào |
| `center_referral` | (vắng) | Tìm trung tâm |
| `center_referral` | `booking` | Đặt lịch với trung tâm |
| `center_referral` | `compare` | So sánh trung tâm (centers) |

### Mục thông tin trường (`fields`)

| Mã | Nghĩa |
|---|---|
| `cost` | Tổng học phí, chi phí sinh hoạt |
| `aid` | Học bổng cho du học sinh |
| `english_requirement` | Yêu cầu tiếng Anh |
| `test_policy` | Chính sách SAT/ACT |
| `gpa` | GPA trúng tuyển, thang điểm |
| `acceptance_rate` | Tỷ lệ nhận |
| `deadline` | Hạn nộp hồ sơ |
| `graduation_rate` | Tỷ lệ tốt nghiệp |
| `majors` | Ngành đào tạo |
| `ranking` | Xếp hạng |
| `location` | Bang, thành phố, địa chỉ |
| `website` | Trang web của trường |
| `housing` | Ký túc xá: loại nhà ở, tiền phòng, tiền cọc |
| `student_faculty_ratio` | Tỷ lệ sinh viên trên giảng viên, sĩ số lớp |
| `student_activities` | Câu lạc bộ, đội nhóm, đời sống sinh viên |
| `campus_setting` | Khuôn viên ở thành phố, ngoại ô, thị trấn hay nông thôn |
| `tuition_in_state` | Học phí dành cho người trong bang (chỉ trường công) |
| `tuition_out_of_state` | Học phí ngoài bang, mức du học sinh thường phải trả |
| `living_cost` | Chi phí ăn ở, sinh hoạt mỗi năm |
| `insurance` | Bảo hiểm y tế bắt buộc cho du học sinh |
| `application_fee` | Lệ phí nộp hồ sơ (một lần) |
| `act_range` | Khoảng điểm ACT 25%-75% của sinh viên trúng tuyển |
| `sat_range` | Khoảng điểm SAT 25%-75% của sinh viên trúng tuyển |
| `sat_submit_rate` | Tỷ lệ sinh viên trúng tuyển có nộp SAT |
| `international_acceptance_rate` | Tỷ lệ nhận riêng của sinh viên quốc tế |
| `scholarships` | Liệt kê các học bổng có tên của trường |
| `other` | Điều hệ thống không lưu (việc làm...) |

### Điều kiện lọc (`filters`)

Giá trị dạng chữ là chữ học sinh gõ ("texas", "30 nghìn đô", "top 50"), code tự khớp và đổi đơn vị. Các điều kiện cùng có hiệu lực.

| Khóa | Giá trị | Nghĩa | Số lần |
|---|---|---|---|
| `state` | chữ học sinh gõ | Bang (một tên hoặc danh sách) | 197 |
| `city` | chữ học sinh gõ | Thành phố | 53 |
| `major` | chữ học sinh gõ | Ngành đào tạo | 138 |
| `max_cost` | chữ học sinh gõ | Tổng chi phí cả năm không vượt mức này | 65 |
| `max_ielts` | chữ học sinh gõ | Mức IELTS tối thiểu trường yêu cầu không vượt mức này | 35 |
| `max_toefl` | chữ học sinh gõ | Mức TOEFL tối thiểu trường yêu cầu không vượt mức này | 31 |
| `max_duolingo` | chữ học sinh gõ | Mức Duolingo tối thiểu trường yêu cầu không vượt mức này | 22 |
| `min_acceptance_rate` | chữ học sinh gõ | Tỷ lệ nhận từ mức này trở lên (dễ vào) | 22 |
| `max_acceptance_rate` | chữ học sinh gõ | Tỷ lệ nhận không quá mức này (chọn lọc) | 29 |
| `max_rank` | chữ học sinh gõ | Có hạng trong bảng xếp hạng chung từ hạng này trở lên (top N) | 55 |
| `min_graduation_rate` | chữ học sinh gõ | Tỷ lệ tốt nghiệp 6 năm từ mức này trở lên | 42 |
| `school_type` | `public`, `private` | Loại trường | 95 |
| `aid` | `yes`, `need_based`, `merit_based` | Học bổng cho du học sinh: có, theo nhu cầu, theo thành tích | 69 |
| `test_policy` | `optional`, `required` | Chính sách thi: test-optional hoặc bắt buộc | 72 |
| `ivy` | `yes` | Trường Ivy League | 43 |

### Cách xếp (`sort`)

| Mã | Nghĩa |
|---|---|
| `ranking` | Xếp hạng cao nhất (mặc định khi không nói) |
| `lowest_cost` | Rẻ nhất (hệ thống hiểu là học phí hay tổng chi phí tùy chữ học sinh dùng) |
| `highest_cost` | Đắt nhất (như trên) |
| `easiest_admission` | Dễ vào nhất (tỷ lệ nhận cao nhất) |
| `most_selective` | Khó vào nhất (tỷ lệ nhận thấp nhất) |
| `most_aid` | Học bổng trung bình lớn nhất |

### Kịch bản (`scenario`)

| Khóa hoặc giá trị | Nghĩa |
|---|---|
| `mode = what_if` | Thử "nếu như…" (kèm changes) |
| `mode = goal_seeking` | Cần gì để vào một trường (kèm school) |
| `target_group = reach` | Nhóm Khó vào |
| `target_group = target` | Nhóm Vừa sức |
| `target_group = safety` | Nhóm An toàn |

Trong `changes` (số lần gặp trong dữ liệu):

- `unit`: `(không có)` (2168), `ielts` (350), `USD` (322), `scale_4` (278), `scale_10` (247), `VND` (215), `percent` (83), `duolingo` (77), `toefl` (70)
- `factor`: `sat` (622), `gpa` (565), `english` (525), `budget` (453), `application_round` (272), `act` (245), `scholarship` (237), `major` (215), `state` (200), `exchange_rate` (192), `priority_aid` (103), `priority_cost` (91), `priority_prestige` (90)
- `value`: số đã chuẩn hóa (1k5 thành 1500, 40k thành 40000, 800 triệu thành 800000000); riêng `scholarship` là `optional`, `preferred`, `mandatory`; `action: skip` thay `value` để bỏ một yếu tố.

### Dịch vụ trung tâm (`service`)

| Mã | Nghĩa |
|---|---|
| `SCHOOL_SELECTION` | Chọn trường |
| `ESSAY_EDITING` | Sửa bài luận |
| `APPLICATION_SUBMISSION` | Nộp hồ sơ |
| `VISA_INTERVIEW_PREP` | Luyện phỏng vấn visa |
| `TEST_PREP` | Luyện thi |
| `SCHOLARSHIP_SEARCH` | Săn học bổng |

### Loại ngoài phạm vi (`subtype`)

| Giá trị | Nghĩa |
|---|---|
| `exception` | Không liên quan du học |
| `sensitive` | Gian lận, làm giả, nội dung xấu, cố đổi hướng dẫn của hệ thống |
| `greeting` | Lời chào mở đầu (hello, xin chào) |
| `farewell` | Cảm ơn, tạm biệt, kết thúc |
| `ambiguous` | Không rõ muốn gì và không có câu đang chờ |
| `about_system` | Hỏi về chính trợ lý ("bạn là ai", "bạn làm được gì") |
| `human_handoff` | Muốn gặp người thật |

## 5. Khóa hồ sơ (chỉ ở ý `collect_profile`)

| Khóa | Nghĩa | Câu hệ thống hỏi | Số ý |
|---|---|---|---|
| `current_level` | Đang học hay làm gì hiện tại | B1.1 | 39 |
| `current_grade` | Lớp đang học | B1.1 | 369 |
| `major` | Ngành định học | B1.3 | 472 |
| `intake` | Kỳ nhập học dự kiến | B1.4 | 268 |
| `education_system` | Chương trình học (THPT Việt Nam, IB, A-level, AP) | D1 | 166 |
| `gpa` | Điểm trung bình | D2 | 1023 |
| `sat` | Điểm SAT đã thi | D3 | 856 |
| `act` | Điểm ACT đã thi | D3 | 220 |
| `english` | Chứng chỉ tiếng Anh đã có, điểm và ngày thi | E1 | 849 |
| `english_plan` | Kế hoạch thi tiếng Anh | E2 | 61 |
| `standardized_plan` | Kế hoạch thi SAT, ACT | E3 | 112 |
| `budget` | Ngân sách mỗi năm (học phí và sinh hoạt) | F1 | 766 |
| `scholarship` | Mức cần học bổng: không cần, nên có, bắt buộc | F2 | 254 |
| `funding_source` | Ai chi trả chi phí du học | F3 | 107 |
| `activities` | Hoạt động ngoại khóa | D4 | 142 |
| `awards` | Giải thưởng | D5 | 132 |
| `relatives_state` | Người thân ở Mỹ (bang) | N1 | 74 |
| `preferred_states` | Bang đã tìm hiểu hoặc muốn học | N2 | 288 |
| `school_type` | Thích trường công hay trường tư | N3 | 100 |
| `visa_refused` | Từng bị từ chối visa chưa | N4 | 64 |

Giá trị hồ sơ luôn là chữ nguyên văn trong tin. Ý khác ngoài `collect_profile` không có khóa hồ sơ.

## 6. Ràng buộc (kiểm trong `app/schemas/slm.py` và luật gán nhãn)

- `collect_profile`, `profile_review`, `scenario`: một ý mỗi tin (gộp lại). `knowledge`, `center_referral`: mỗi câu hỏi một ý.
- `tasks` là danh sách không lặp. Chỉ `profile_review` được nhiều nhiệm vụ; các loại khác một nhiệm vụ mỗi ý.
- `status` và `restart` không kèm giá trị hồ sơ. `compare` (profile_review) và `compare_facts` cần `schools` từ hai tên; `group` đi với `compare_facts` và không kèm `schools`.
- `search` cần `filters` hoặc `sort`; `filters` và `sort` chỉ đi với `search`.
- `what_if` cần `changes`, mỗi change có `factor` và `value` hoặc `action`. `centers` từ hai tên và chỉ đi với `tasks = compare`.
- `out_of_scope` bắt buộc có `subtype` và đứng một mình (cả tin không có ý nào khác).
