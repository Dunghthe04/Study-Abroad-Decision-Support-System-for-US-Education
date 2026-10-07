# Giải thích database USAS

Tài liệu này giải thích từng bảng: bảng dùng để làm gì, cột nào quan trọng, nối với bảng nào và ai trong nhóm dùng nhiều nhất. Đọc kèm sơ đồ ERD ở [database.drawio](database.drawio). Quy ước thêm hoặc sửa bảng nằm ở [DATABASE.md](DATABASE.md).

## Đọc nhanh

- Có **27 bảng**, đều nằm trong schema `app` của PostgreSQL. Code C# ở `backend/src/StudyAbroad.Domain/Entities`, mỗi bảng một file.
- Bảng `users` là trung tâm: hầu hết dữ liệu đều thuộc về một người dùng.
- Ngoài 27 bảng này còn có `advisor.documents` (kho vector cho AI), do dịch vụ Python quản lý. Phía .NET không đọc hay ghi bảng đó.

### Cột có ở mọi bảng

| Cột | Ý nghĩa |
|---|---|
| `id` (uuid) | Khóa chính, C# tự sinh (`Guid.NewGuid()`) |
| `created_at` | Thời điểm tạo, tự gán |
| `updated_at` | Thời điểm sửa gần nhất. `AppDbContext.SaveChangesAsync` tự cập nhật, không cần gán tay |

### Quy ước dữ liệu

- **Bậc học** (`study_level`, `target_level`) chỉ nhận 5 giá trị trong `StudyLevels`: `secondary` (THPT), `community_college` (cao đẳng cộng đồng), `undergraduate` (đại học), `master` (thạc sĩ), `phd` (tiến sĩ). Một số bảng có thêm `general` nghĩa là dùng chung mọi bậc.
- **Trạng thái** (`status`, `kind`, `role`...) lưu dạng chữ thường tiếng Anh, ví dụ `open`, `done`. Các giá trị hợp lệ ghi ngay trong comment của từng cột trong code.
- **Cột `..._json`** có kiểu `jsonb`. Trong C# là `string`; muốn đọc thì `JsonSerializer.Deserialize`.
- **Cột mảng** (`text[]`) như `preferred_states`, `majors`, `study_levels` là `List<string>` trong C#.
- **Tiền** luôn tính bằng USD, cột có đuôi `_usd`.

### Ký hiệu quan hệ trong tài liệu

- **1–1**: một bản ghi bên này ứng với đúng một bản ghi bên kia.
- **1–n**: một bản ghi cha có nhiều bản ghi con.
- **Xóa cha thì sao**:
  - **Xóa theo**: xóa cha thì xóa luôn con.
  - **Để trống**: xóa cha thì cột khóa ngoại của con thành `null`.
  - **Chặn**: không cho xóa cha khi còn con.

---

## 1. Tài khoản

### `users` – Người dùng
Mọi tài khoản trong hệ thống: học sinh, phụ huynh, trung tâm, admin.

| Cột | Ý nghĩa |
|---|---|
| `email` | Duy nhất, dùng để đăng nhập |
| `password_hash` | Mật khẩu đã băm bằng BCrypt. Không bao giờ lưu mật khẩu gốc |
| `full_name` | Họ tên |
| `role` | `student`, `parent`, `center`, `admin` |
| `status` | `active`, `pending` (trung tâm chờ admin duyệt), `locked` (bị khóa) |
| `parent_acknowledged` | Học sinh dưới 18 tuổi đã xác nhận có phụ huynh đồng ý hay chưa |

Quan hệ: là bảng cha của gần như mọi bảng khác (xem từng bảng bên dưới).
Người làm chính: Việt (#1 đăng ký/đăng nhập, #16 admin quản lý tài khoản, #17 bảo mật).

### `otp_tokens` – Mã OTP
Mã xác thực một lần để xác minh email hoặc đặt lại mật khẩu.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Mã gửi cho ai |
| `purpose` | `verify_email` hoặc `reset_password` |
| `code_hash` | Mã đã băm. Không lưu mã gốc, so sánh bằng hash |
| `expires_at` | Hết hạn lúc nào |
| `used_at` | Đã dùng lúc nào. `null` nghĩa là chưa dùng |
| `attempts` | Số lần nhập sai, để khóa sau vài lần |

Quan hệ: `users` 1–n `otp_tokens`, xóa user thì xóa theo.
Người làm chính: Việt (#17).

### `consents` – Sự đồng ý
Ghi lại việc người dùng đã đồng ý điều gì. Đây là bằng chứng khi chia sẻ dữ liệu cá nhân.

| Cột | Ý nghĩa |
|---|---|
| `purpose` | `privacy` (chính sách dữ liệu), `share_with_center` (chia sẻ hồ sơ cho trung tâm), `forum_rules` (nội quy diễn đàn) |
| `granted` | `true` là đồng ý, `false` là rút lại |
| `policy_version` | Đồng ý với phiên bản chính sách nào, ví dụ `v1.0` |

Quan hệ: `users` 1–n `consents`. Bảng `leads` trỏ tới đây để biết lần gửi hồ sơ dựa trên sự đồng ý nào.
Người làm chính: Việt (#17), Dương dùng khi gửi lead (#24).

### `audit_logs` – Nhật ký thao tác
Ghi lại ai làm gì. Gồm cả sự kiện đăng nhập và thao tác của admin.

| Cột | Ý nghĩa |
|---|---|
| `actor_user_id` | Ai làm. `null` nếu là hệ thống hoặc khách chưa đăng nhập |
| `action` | Tên sự kiện, ví dụ `login_ok`, `login_failed`, `user.lock`, `center.verify`, `post.hide` |
| `entity_type`, `entity_id` | Tác động lên đối tượng nào, ví dụ `study_center` và id của trung tâm |
| `detail_json` | Chi tiết thêm, ví dụ lý do khóa |
| `ip_address`, `user_agent` | Thông tin trình duyệt, dùng cho sự kiện đăng nhập |

Quan hệ: **không có khóa ngoại**, cố ý như vậy để xóa user vẫn giữ được lịch sử.
Người làm chính: Việt (#16, #17). Đức và Dương ghi log khi kiểm duyệt (#28) và xác minh trung tâm (#25).

### `notifications` – Thông báo
Thông báo hiện trong ứng dụng, ví dụ "Trung tâm X đã nhận yêu cầu của bạn".

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Gửi cho ai |
| `type` | Loại thông báo, ví dụ `lead_status`, `deadline` |
| `title`, `body` | Nội dung |
| `link_url` | Bấm vào thì đi đến trang nào |
| `read_at` | Đã đọc lúc nào. `null` là chưa đọc |

Quan hệ: `users` 1–n `notifications`, xóa theo.
Người làm chính: Dương (#24).

---

## 2. Hồ sơ học sinh

### `student_profiles` – Hồ sơ học sinh
Toàn bộ thông tin học sinh tự nhập, gom vào một bảng: học thuật, điểm thi, tài chính, tiến độ lộ trình. **Mỗi user có tối đa một hồ sơ.**

| Nhóm | Cột | Ý nghĩa |
|---|---|---|
| Mục tiêu | `target_level`, `intended_major` | Muốn đi bậc nào, ngành gì |
| Học thuật | `current_school`, `current_grade` | Đang học ở đâu, lớp mấy hoặc năm mấy |
| | `grade_scale` | Thang điểm đang dùng: `10`, `4` hoặc `100` |
| | `overall_gpa` | Điểm trung bình theo thang ở trên |
| Điểm thi | `ielts`, `toefl`, `duolingo`, `sat`, `act`, `gre`, `gmat` | Điểm tổng từng kỳ thi. `null` nếu chưa thi |
| | `other_tests_json` | Điểm thành phần, ngày thi, kỳ thi khác (SSAT, AP...) |
| Tài chính | `annual_budget_usd` | Ngân sách mỗi năm |
| | `funding_source` | `family`, `loan`, `scholarship`, `other` |
| | `needs_scholarship` | Có cần học bổng không |
| | `preferred_states` | Bang muốn học, ví dụ `{CA, TX}` |
| Lộ trình | `roadmap_progress_json` | Tiến độ từng bước lộ trình, ví dụ `{"B1":"done","B2":"doing"}` |

Quan hệ:
- `users` 1–1 `student_profiles` (cột `user_id` không được trùng), xóa user thì xóa hồ sơ.
- Là bảng cha của `transcript_scores`, `profile_activities` và `analysis_results`, cả ba đều xóa theo.
- `recommendations` trỏ tới đây theo kiểu không bắt buộc.

Người làm chính: Việt (#2 hồ sơ học thuật, #21 lộ trình), Đức (#3 tài chính).

### `transcript_scores` – Bảng điểm chi tiết
Điểm từng môn theo từng học kỳ. Một học sinh có nhiều dòng.

| Cột | Ý nghĩa |
|---|---|
| `term_name` | Tên kỳ, ví dụ `Lớp 10 HK1` |
| `term_order` | Số thứ tự để sắp xếp kỳ (1, 2, 3...) |
| `subject` | Môn học |
| `score` | Điểm |
| `credits` | Số tín chỉ (bậc đại học), có thể để trống |

Ví dụ: học sinh lớp 12 nhập 6 kỳ × 8 môn thì có 48 dòng.
Quan hệ: `student_profiles` 1–n `transcript_scores`, xóa theo.
Người làm chính: Việt (#2 nhập điểm), Đức (#4 tính GPA từ bảng này).

### `profile_activities` – Hoạt động trong hồ sơ
Ngoại khóa, kinh nghiệm làm việc và giải thưởng, gom chung một bảng, phân biệt bằng `kind`.

| Cột | Ý nghĩa |
|---|---|
| `kind` | `extracurricular` (ngoại khóa), `experience` (kinh nghiệm), `award` (giải thưởng) |
| `title` | Tên hoạt động hoặc giải |
| `organization` | Câu lạc bộ, công ty hoặc đơn vị trao giải |
| `role` | Vai trò, hoặc cấp giải (trường, tỉnh, quốc gia) |
| `description` | Mô tả |
| `start_date`, `end_date` | Thời gian |

Quan hệ: `student_profiles` 1–n `profile_activities`, xóa theo.
Người làm chính: Đức (#3).

---

## 3. Trường và học bổng

### `universities` – Trường
Danh sách trường ở Mỹ (đại học, cao đẳng cộng đồng, THPT). Chỉ chứa thông tin chung, không phụ thuộc bậc học.

| Cột | Ý nghĩa |
|---|---|
| `code` | Mã trường, duy nhất, dùng khi nhập dữ liệu từ Excel |
| `name`, `city`, `state`, `website` | Thông tin cơ bản. `state` là mã bang 2 chữ, ví dụ `CA` |
| `control` | `public` hoặc `private` |
| `acceptance_rate` | Tỷ lệ nhận, dạng 0–1, ví dụ `0.45` |
| `international_student_count` | Số sinh viên quốc tế |
| `is_active` | `false` thì ẩn khỏi danh sách và gợi ý |

Quan hệ: là bảng cha của `university_offerings` (xóa theo), `target_schools` (xóa theo) và `scholarships` (để trống).
Người làm chính: Dương (#5 nhập dữ liệu, #13 admin quản lý), Đức (#14 trang chi tiết).

### `university_offerings` – Thông tin trường theo bậc học
Một trường có thể có nhiều bậc học, mỗi bậc có ngành, học phí và điều kiện khác nhau. **Mỗi cặp (trường, bậc học) là một dòng.** Đây là bảng mà thuật toán gợi ý trường đọc nhiều nhất.

| Nhóm | Cột | Ý nghĩa |
|---|---|---|
| Khóa | `university_id`, `study_level` | Trường nào, bậc nào. Cặp này không được trùng |
| Ngành | `majors` | Danh sách ngành, ví dụ `{Computer Science, Business}` |
| | `stem_majors` | Các ngành STEM (liên quan OPT 3 năm) |
| Chi phí | `academic_year` | Năm học của số liệu, ví dụ `2026-27` |
| | `tuition_usd`, `living_usd`, `fees_usd` | Học phí, sinh hoạt, phí khác mỗi năm |
| Điều kiện | `min_gpa4`, `min_ielts`, `min_toefl`, `min_duolingo` | Điểm tối thiểu, `null` nếu trường không công bố |
| | `sat_policy` | `required`, `optional`, `not_accepted` |
| Mặt bằng trúng tuyển | `avg_gpa4` | GPA trung bình của sinh viên trúng tuyển, thang 4 (Common Data Set mục C12). `null` nếu trường không công bố |
| | `sat25`, `sat75` | Mốc SAT 25% và 75% của sinh viên trúng tuyển (400–1600). Dùng để xếp Reach/Match/Safety ở chức năng gợi ý trường (#6): dưới `sat25` là Reach, từ `sat75` trở lên là Safety |
| Hạn nộp | `deadlines_json` | Danh sách vòng nộp, ví dụ `[{"round":"EA","date":"2026-11-01"}]` |
| Nguồn | `source_url`, `retrieved_at` | Lấy số liệu ở đâu, ngày nào. Bắt buộc ghi để kiểm chứng |

Quan hệ: `universities` 1–n `university_offerings`, xóa theo.
Người làm chính: Dương (#5), Dũng đọc khi gợi ý trường (#6).

### `scholarships` – Học bổng
Danh sách học bổng. Có thể gắn với một trường cụ thể hoặc là học bổng chung (Fulbright...).

| Cột | Ý nghĩa |
|---|---|
| `university_id` | Học bổng của trường nào. `null` nếu không thuộc trường nào |
| `name`, `provider` | Tên học bổng, đơn vị cấp |
| `study_level` | Dành cho bậc nào |
| `amount_usd` | Giá trị mỗi năm |
| `coverage_type` | `full` (toàn phần), `partial`, `tuition` (chỉ học phí), `stipend` (sinh hoạt phí) |
| `min_gpa4`, `min_ielts` | Điều kiện tối thiểu |
| `deadline`, `eligibility_notes` | Hạn nộp, điều kiện khác |
| `source_url`, `retrieved_at`, `is_active` | Nguồn, ngày lấy, còn hiển thị không |

Quan hệ: `universities` 1–n `scholarships`, không bắt buộc. Xóa trường thì học bổng vẫn còn, cột `university_id` thành `null`.
Người làm chính: Đức (#19 học bổng, #26 đủ hoặc thiếu tiền sau học bổng).

### `target_schools` – Trường mục tiêu
Danh sách trường người dùng lưu lại để theo dõi, giống "giỏ hàng" các trường muốn nộp.

| Cột | Ý nghĩa |
|---|---|
| `user_id`, `university_id` | Ai lưu trường nào. Một người không lưu một trường hai lần |
| `category` | `reach` (khó đỗ), `match` (vừa sức), `safety` (chắc đỗ) |
| `status` | `considering` → `applying` → `submitted` → `admitted` hoặc `rejected` |
| `priority` | Thứ tự ưu tiên |
| `notes` | Ghi chú riêng |

Quan hệ: `users` 1–n `target_schools` và `universities` 1–n `target_schools`, cả hai đều xóa theo. Về bản chất đây là bảng nối nhiều–nhiều giữa người dùng và trường.
Người làm chính: Đức (#20).

---

## 4. AI

### `recommendations` – Lần gợi ý trường
Mỗi lần học sinh bấm "Gợi ý trường", AI lưu lại một dòng: đầu vào là gì và kết quả ra sao.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Ai yêu cầu |
| `student_profile_id` | Dựa trên hồ sơ nào (không bắt buộc) |
| `study_level` | Gợi ý cho bậc nào |
| `criteria_json` | 3 tiêu chí đầu vào, ví dụ `{"major":"Computer Science","states":["CA"],"budgetUsd":40000}` |
| `items_json` | Danh sách trường kết quả, mỗi trường có `universityId`, `rank`, `category`, `score`, `reason` |
| `algorithm_version` | Phiên bản thuật toán, để so sánh khi cải tiến |

Kết quả được lưu dạng JSON để giữ nguyên "ảnh chụp" lúc gợi ý. Sau này dữ liệu trường có đổi thì lần gợi ý cũ vẫn hiển thị đúng như lúc đó.
Quan hệ: `users` 1–n (xóa theo), `student_profiles` 1–n (để trống).
Người làm chính: Dũng (#6 thuật toán), Dương (#8 trang hiển thị danh sách).

### `analysis_results` – Kết quả phân tích hồ sơ
AI hoặc thuật toán phân tích hồ sơ rồi lưu kết quả ở đây.

| Cột | Ý nghĩa |
|---|---|
| `student_profile_id` | Hồ sơ nào |
| `kind` | `gpa` (quy đổi GPA), `strengths_weaknesses` (điểm mạnh, điểm yếu), `strategy` (chiến lược cải thiện) |
| `result_json` | Nội dung kết quả, cấu trúc tùy `kind` |
| `model_version` | Model hoặc phiên bản tạo ra kết quả |

Quan hệ: `student_profiles` 1–n `analysis_results`, xóa theo. Phân tích lại thì thêm dòng mới, lấy dòng mới nhất theo `created_at`.
Người làm chính: Đức (#4 GPA, #15 chiến lược), Dũng (#7 AI phân tích).

### `skills` – Flow tư vấn
"Kịch bản" tư vấn mà AI làm theo, sinh từ sheet 07_Flow của file dữ liệu tư vấn. Ví dụ skill `visa_f1` gồm các bước B0–B6, câu hỏi cần hỏi và luật chuyển bước.

| Cột | Ý nghĩa |
|---|---|
| `key` | Mã skill, ví dụ `visa_f1`, `undergrad_admission` |
| `version` | Phiên bản. Cặp `key` và `version` không được trùng |
| `name`, `description` | Tên, mô tả |
| `definition_json` | Nội dung flow: các bước, câu hỏi, luật |
| `is_active` | Mỗi `key` chỉ có một phiên bản đang dùng |

Sửa flow thì **thêm dòng mới** với `version` tăng lên, rồi bật `is_active` cho bản mới và tắt bản cũ. Không sửa đè, để các cuộc chat cũ vẫn biết đã dùng bản nào.
Quan hệ: `skills` 1–n `chat_sessions`. Xóa skill thì `chat_sessions.skill_id` thành `null`.
Người làm chính: Dũng (#10).

### `chat_sessions` – Cuộc chat AI
Mỗi cuộc trò chuyện của học sinh với AI tư vấn.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Của ai |
| `title` | Tiêu đề hiển thị trong danh sách chat |
| `study_level` | Đang tư vấn bậc nào |
| `current_step` | Đang ở bước nào của flow (B0–B6) |
| `skill_id` | Đang theo skill nào, không bắt buộc |

Quan hệ: `users` 1–n (xóa theo), `skills` 1–n (để trống), là bảng cha của `chat_messages`.
Người làm chính: Dũng (#10).

### `chat_messages` – Tin nhắn
Từng tin nhắn trong cuộc chat.

| Cột | Ý nghĩa |
|---|---|
| `chat_session_id` | Thuộc cuộc chat nào |
| `role` | `user` (học sinh), `assistant` (AI), `system` |
| `content` | Nội dung tin nhắn |
| `step` | Tin nhắn thuộc bước nào của flow |
| `sources_json` | Nguồn trích dẫn của câu trả lời, ví dụ `[{"title":"Student Visa","url":"https://travel.state.gov/..."}]` |
| `suggested_centers_json` | Danh sách trung tâm AI gợi ý khi câu hỏi vượt phạm vi hệ thống (#23) |
| `review_status` | `null` nếu AI trả lời được. `open` nếu AI không trả lời được (chờ admin bổ sung tài liệu), sau đó thành `resolved` hoặc `ignored` |

Câu AI không trả lời được: admin lọc `review_status = 'open'`, bổ sung tài liệu vào `knowledge_documents`, rồi đổi thành `resolved`.
Quan hệ: `chat_sessions` 1–n `chat_messages`, xóa theo.
Người làm chính: Dũng (#10, #11, #23), Dương (#12 màn hình admin xem câu chưa trả lời được).

### `knowledge_documents` – Kho kiến thức đã kiểm chứng
Bản gốc của kiến thức AI dùng để trả lời. Mỗi dòng "Đã kiểm chứng" ở sheet 05 của file dữ liệu tư vấn thành một bản ghi.

| Cột | Ý nghĩa |
|---|---|
| `title`, `content` | Tiêu đề, nội dung |
| `source_url`, `retrieved_at` | Nguồn chính thức, ngày lấy |
| `doc_type` | Chủ đề: `visa`, `ho_so`, `chi_phi`, `hoc_bong`... |
| `study_level` | Áp dụng cho bậc nào, `general` là mọi bậc |
| `status` | `draft` → `verified` (đã duyệt) hoặc `rejected` |
| `verified_by_user_id`, `verified_at` | Ai duyệt, lúc nào |
| `ingested_at` | Lần gần nhất nạp vào kho vector `advisor.documents`. `null` là chưa nạp |

Luồng dữ liệu: admin thêm tài liệu → duyệt (`verified`) → script nạp tạo embedding và ghi vào `advisor.documents` → gán `ingested_at`. **Chỉ tài liệu `verified` mới được nạp.**
Quan hệ: `users` 1–n (người duyệt, để trống).
Người làm chính: Dương (#12 màn hình quản lý), Dũng (#11 nạp và dùng cho AI).

### `ai_calls` – Nhật ký gọi AI
Mỗi lần backend gọi dịch vụ AI thì ghi một dòng, để đo tốc độ, lỗi và token.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Ai gây ra lần gọi, có thể `null`. Không có khóa ngoại để giữ log khi xóa user |
| `kind` | `chat`, `recommend`, `analyze`, `embed`, `search` |
| `model` | Model đã gọi, ví dụ `qwen3:8b` |
| `prompt_tokens`, `completion_tokens`, `latency_ms` | Số token, thời gian phản hồi |
| `success`, `error` | Thành công không, lỗi gì |

Quan hệ: không có.
Người làm chính: Dũng.

### `eval_cases` – Câu hỏi đánh giá AI
Bộ câu hỏi chuẩn để chấm AI trước khi demo, giống bộ đề thi.

| Cột | Ý nghĩa |
|---|---|
| `question` | Câu hỏi |
| `expected_answer` | Đáp án mong đợi |
| `study_level`, `category` | Phân loại câu hỏi |
| `expected_sources_json` | Nguồn mà AI nên trích dẫn |
| `is_active` | Còn dùng trong bộ đề không |

### `eval_runs` – Lần chạy đánh giá
Mỗi lần chạy bộ đề là một dòng.

| Cột | Ý nghĩa |
|---|---|
| `name` | Tên lần chạy, ví dụ `Sprint 2 - qwen3:8b` |
| `model_version`, `skill_id` | Chạy với model và skill nào |
| `total_cases`, `passed_cases` | Tổng số câu, số câu đạt |
| `results_json` | Kết quả từng câu: `caseId`, `passed`, `answer`, `latencyMs` |
| `started_at`, `finished_at`, `notes` | Thời gian, ghi chú |

Quan hệ: cả hai bảng đánh giá đều không có khóa ngoại. `eval_runs.results_json` chứa `caseId` trỏ tới `eval_cases.id`.
Người làm chính: Dũng (#30), Phương Anh soạn câu hỏi.

---

## 5. Lộ trình, trung tâm, diễn đàn, cấu hình

### `roadmap_steps` – Các bước lộ trình du học
Danh sách bước chuẩn cho từng bậc học, ví dụ bậc đại học: B1 chọn trường → B2 thi IELTS/SAT → B3 nộp hồ sơ → B4 xin visa...

| Cột | Ý nghĩa |
|---|---|
| `study_level`, `step_key` | Bậc nào, bước nào. Cặp này không được trùng |
| `title`, `description` | Tên bước, mô tả |
| `sort_order` | Thứ tự hiển thị |
| `months_before_start` | Nên làm trước ngày nhập học bao nhiêu tháng |
| `resources_json` | Tài liệu tham khảo, ví dụ `[{"title":"DS-160","url":"..."}]` |

Tiến độ của từng học sinh **không lưu ở đây** mà lưu ở `student_profiles.roadmap_progress_json`, theo `step_key`.
Quan hệ: không có khóa ngoại.
Người làm chính: Việt (#21).

### `study_centers` – Trung tâm tư vấn
Các trung tâm tư vấn du học (lấy từ khảo sát thị trường của nhóm), để AI chuyển người dùng sang khi cần.

| Cột | Ý nghĩa |
|---|---|
| `code` | Mã khảo sát, ví dụ `CTY_012`, duy nhất |
| `name`, `website`, `address`, `city`, `phone`, `email`, `description` | Thông tin liên hệ |
| `study_levels` | Các bậc trung tâm tư vấn |
| `services` | Dịch vụ, ví dụ `{CHON_TRUONG, LUYEN_PHONG_VAN}` |
| `verification_status` | `unverified` → `verified`, `rejected` hoặc `suspended` |
| `verified_at`, `verified_by_user_id` | Admin nào xác minh, lúc nào |
| `owner_user_id` | Tài khoản vai trò `center` quản lý hồ sơ này |
| `surveyed_at`, `is_active` | Ngày khảo sát, còn hiển thị không |

Chỉ trung tâm `verified` và `is_active = true` mới được AI gợi ý.
Quan hệ: `users` 1–n theo hai cột (người xác minh, chủ sở hữu), đều để trống khi xóa user. Là bảng cha của `leads`.
Người làm chính: Dương (#22 danh sách trung tâm, #25 xác minh).

### `leads` – Yêu cầu liên hệ trung tâm
Học sinh bấm "Liên hệ trung tâm" thì tạo một lead.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Ai gửi |
| `study_center_id` | Gửi tới trung tâm nào |
| `consent_id` | Lần đồng ý chia sẻ dữ liệu tương ứng, không bắt buộc |
| `study_level`, `contact_phone`, `message` | Nội dung yêu cầu |
| `shared_profile` | Có gửi kèm hồ sơ không. Chỉ được `true` khi đã có `consent_id` |
| `status` | `new` → `contacted` → `closed`, hoặc `spam` |

Quan hệ:
- `users` 1–n `leads`, xóa theo.
- `study_centers` 1–n `leads`, **chặn**: không xóa được trung tâm khi còn lead, muốn ẩn thì đặt `is_active = false`.
- `consents` 1–n `leads`, để trống.

Người làm chính: Dương (#24).

### `forum_posts` – Bài viết diễn đàn
Chủ đề và câu trả lời đều nằm trong bảng này:
- **Bài gốc** (`parent_post_id = null`) là chủ đề, có `title`.
- **Bài con** (`parent_post_id` trỏ tới bài gốc) là câu trả lời, `title` để trống.

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Người viết. `null` nếu tài khoản đã bị xóa |
| `parent_post_id` | Trả lời cho bài nào |
| `title`, `content` | Tiêu đề (chỉ bài gốc), nội dung |
| `study_level`, `category` | Phân loại để lọc |
| `is_ai_generated` | `true` nếu AI tự trả lời (#29) |
| `is_pinned` | Ghim lên đầu |
| `status` | `visible`, `hidden` (bị ẩn), `removed`, `closed` (khóa chủ đề) |

Ví dụ: lấy một chủ đề kèm câu trả lời bằng `WHERE id = X OR parent_post_id = X`.
Quan hệ:
- `users` 1–n, để trống: xóa tài khoản thì bài vẫn còn, chỉ mất tên tác giả.
- `forum_posts` 1–n chính nó (tự tham chiếu): xóa chủ đề thì xóa luôn các câu trả lời.

Người làm chính: Việt (#27), Dũng (#29 AI trả lời).

### `forum_reports` – Báo cáo vi phạm
Người dùng báo cáo một bài viết vi phạm.

| Cột | Ý nghĩa |
|---|---|
| `forum_post_id` | Bài bị báo cáo |
| `reporter_user_id` | Người báo cáo |
| `reason`, `detail` | Lý do (ví dụ `spam`, `quang_cao`, `xuc_pham`), chi tiết |
| `status` | `open` → `actioned` (đã xử lý) hoặc `dismissed` (bỏ qua) |
| `handled_by_user_id`, `handled_at` | Admin nào xử lý, lúc nào |

Quan hệ: `forum_posts` 1–n (xóa theo), `users` 1–n theo hai cột (người báo cáo xóa theo, người xử lý để trống).
Người làm chính: Đức (#28).

### `app_settings` – Cấu hình hệ thống
Các tham số admin chỉnh được mà không cần sửa code, lưu dạng key và giá trị JSON.

| `key` | `value_json` ví dụ | Dùng ở |
|---|---|---|
| `recommend.weights` | `{"major":0.4,"state":0.2,"budget":0.4}` | Trọng số 3 tiêu chí gợi ý trường (#6) |
| `grade_scale.10` | `[{"min":9,"max":10,"gpa4":4.0},{"min":8,"max":8.99,"gpa4":3.5}]` | Quy đổi điểm thang 10 sang GPA 4 (#4, #13) |
| `major_weights` | `{"Computer Science":{"Toán":0.4,"Tin":0.3}}` | Môn quan trọng theo ngành (#15) |
| `moderation.rules` | `[{"pattern":"zalo","action":"flag"}]` | Luật kiểm duyệt diễn đàn (#28) |

Quan hệ: không có. `key` là duy nhất.
Người làm chính: Dương (#13 màn hình admin). Dũng và Đức đọc cấu hình.

---

## 6. Sơ đồ quan hệ tổng thể

```
users ─┬─1:1── student_profiles ─┬─1:n── transcript_scores
       │                         ├─1:n── profile_activities
       │                         ├─1:n── analysis_results
       │                         └─1:n── recommendations (không bắt buộc)
       ├─1:n── recommendations
       ├─1:n── target_schools ──n:1── universities ─┬─1:n── university_offerings
       │                                            └─1:n── scholarships (không bắt buộc)
       ├─1:n── chat_sessions ─1:n── chat_messages
       │            └──n:1── skills (không bắt buộc)
       ├─1:n── otp_tokens / consents / notifications
       ├─1:n── leads ──n:1── study_centers (chặn xóa)
       │         └──n:1── consents (không bắt buộc)
       ├─1:n── forum_posts ─1:n── forum_posts (trả lời)
       │                    └─1:n── forum_reports
       ├─1:n── study_centers (chủ sở hữu, người xác minh)
       └─1:n── knowledge_documents (người duyệt)

Bảng độc lập: audit_logs, ai_calls, eval_cases, eval_runs, roadmap_steps, app_settings
```

## 7. Ví dụ luồng dữ liệu

**Học sinh đăng ký rồi xin gợi ý trường**
1. Đăng ký: tạo một dòng `users` và một dòng `consents` (`privacy`).
2. Nhập hồ sơ: tạo `student_profiles`, nhiều dòng `transcript_scores` và `profile_activities`.
3. Bấm "Phân tích": đọc `app_settings` (`grade_scale.10`) để quy đổi GPA, ghi `analysis_results` (`kind = gpa`).
4. Bấm "Gợi ý trường": đọc `university_offerings` để lọc theo ngành, bang và ngân sách, ghi một dòng `recommendations` với `items_json`. Ghi thêm `ai_calls`.
5. Bấm "Lưu trường": tạo `target_schools`.

**Học sinh chat với AI và được chuyển sang trung tâm**
1. Mở chat: tạo `chat_sessions` gắn `skill_id` của flow đang active.
2. Mỗi câu hỏi và câu trả lời là một dòng `chat_messages`. AI tìm kiến thức trong `advisor.documents` (nạp từ `knowledge_documents`) và ghi nguồn vào `sources_json`.
3. Câu hỏi vượt phạm vi (ví dụ cần làm hồ sơ hộ): AI trả lời kèm `suggested_centers_json`.
4. Học sinh bấm liên hệ: tạo `consents` (`share_with_center`) và `leads`. Trung tâm nhận được, gửi `notifications` cho học sinh.

**Admin bổ sung kiến thức cho AI**
1. Lọc `chat_messages` có `review_status = 'open'` để xem AI đang bí câu nào.
2. Thêm `knowledge_documents` (`draft`) → duyệt (`verified`) → chạy script nạp → gán `ingested_at`.
3. Đổi `review_status` của tin nhắn thành `resolved`.

## 8. Câu hỏi thường gặp

**Sao không có bảng `roles` hay `sessions`?**
Vai trò chỉ có 4 giá trị nên lưu thẳng ở `users.role`. Đăng nhập dùng JWT, không cần lưu phiên trong database.

**Sao dùng JSON mà không tách bảng?**
Chỉ dùng JSON cho dữ liệu luôn đọc cả cụm và ít khi lọc theo từng phần tử: danh sách trường gợi ý, hạn nộp, kết quả đánh giá, cấu hình. Như vậy bớt được hơn 10 bảng phụ. Dữ liệu cần lọc hoặc nối bảng (trường, học bổng, điểm từng môn) vẫn để thành bảng riêng.

**Muốn thêm cột hoặc bảng thì làm sao?**
Làm theo mục "Quy ước khi thêm hoặc sửa bảng" trong [DATABASE.md](DATABASE.md): sửa entity và configuration, tạo migration mới, không sửa migration đã merge.

**Muốn xem dữ liệu thật?**
Chạy `docker compose up -d db`, rồi kết nối DBeaver hoặc pgAdmin tới `localhost:5432` (user, mật khẩu và database đều là `studyabroad`), mở schema `app`.
