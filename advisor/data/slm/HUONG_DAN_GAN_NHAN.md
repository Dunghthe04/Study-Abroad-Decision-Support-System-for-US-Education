# Hướng dẫn gán nhãn dữ liệu SLM

Dành cho người thêm hoặc kiểm dòng trong `du_lieu_slm.xlsx`. Chỉ có **hai file dữ liệu**: `du_lieu_slm.xlsx` là nguồn duy nhất, ghi **toàn bộ bằng tiếng Anh**; `du_lieu_slm.jsonl` (tiếng Anh, đúng tên trong Excel) sinh từ Excel bằng
`python -m scripts.build_slm_jsonl`, không sửa tay. Code và prompt còn dùng tên tiếng Việt: xem `TEN_KHOA_ANH_VIET.md` để biết phải đổi gì thành gì. Script kiểm từng dòng và báo tên sheet, số dòng nếu sai.

## 1. Một dòng gồm những gì

| Cột | Ghi gì |
|---|---|
| `question` | Tin nhắn của học sinh, đúng như học sinh gõ (kể cả không dấu, sai chính tả) |
| `profile` | Hồ sơ học sinh đã có (JSON) hoặc để trống |
| `pending_question` | Câu hệ thống vừa hỏi và đang chờ trả lời, chép đúng từ `chat_flow.QUESTIONS`; không có thì để trống |
| `intents` | **Nhãn**: danh sách ý (JSON), xem mục 2 |
| `context` | Tùy chọn: điều hệ thống đang nói tới (trường, mục, bộ lọc…), ghi JSON |
| `review` | Tùy chọn: dòng mới chờ duyệt ghi `v9` hoặc `v10`. Người duyệt thay bằng `ok`, `sửa: ...` hoặc `xóa`. Script bỏ qua cột này |
| `split` | `train`, `val` hoặc `test`. Câu gần giống nhau phải cùng tập; không để câu gần trùng một câu train nằm ở val hoặc test |

Sheet là loại của **một trong các ý** của dòng (thường là ý đầu). Sheet không ảnh hưởng đáp án, chỉ để dễ tìm.

## 2. Cột `intents`

```json
[{"type": "collect_profile", "span": "em GPA 3.5", "entities": {"gpa": "em GPA 3.5"}},
 {"type": "knowledge", "span": "I-20 là gì", "entities": {}}]
```
Mỗi ý có đúng 3 khóa: `type` (loại), `span` (đoạn chữ của ý), `entities` (thông tin tách ra, không có thì `{}`).

**Luật chung**
1. Một tin tách thành các ý, **theo thứ tự học sinh nói**. Tối đa 7 ý.
2. `span` luôn là đoạn chữ **có nguyên văn** trong `question`. Câu ghép dùng chung phần cuối ("I-20 và SAT là gì") thì mỗi ý lấy đoạn thật có trong câu: ý đầu `I-20`, ý sau `SAT là gì`. Thông tin còn lại (tên trường, mục) nằm ở `entities`.
3. Giá trị hồ sơ **chỉ ghi ở ý `collect_profile`**, nguyên văn đoạn chữ trong tin. Ý khác không có khóa hồ sơ.
4. **Chỉ một ý `collect_profile`**, gộp mọi giá trị hồ sơ vào đó, và luôn đứng **đầu** danh sách dù học sinh nói sau.
5. `profile_review` chỉ một ý: nhiều câu hỏi loại này gộp thành một ý (`span` ghi gộp).
6. `knowledge` và `center_referral`: **mỗi câu hỏi một ý**, kể cả khi cùng loại.
7. Lời chào, cảm ơn đi kèm câu hỏi hoặc thông tin hồ sơ thì **bỏ**, không tạo ý. Tin chỉ có chào, cảm ơn thì một ý `out_of_scope`.
8. `out_of_scope` đứng một mình, bắt buộc có `subtype`.

## 3. Chọn loại

| Loại | Dấu hiệu |
|---|---|
| `collect_profile` | Học sinh **khai** thông tin về mình, hoặc trả lời câu đang chờ |
| `profile_review` | Muốn được tư vấn **dựa trên hồ sơ của mình** (nên chọn trường nào, đánh giá hồ sơ, thuộc nhóm nào) |
| `knowledge` | Hỏi kiến thức chung, **không cần biết hồ sơ** của học sinh (visa, kỳ thi, học bổng, học phí) |
| `center_referral` | Muốn **tìm người hoặc nơi làm giúp** một dịch vụ |
| `out_of_scope` | Còn lại: `exception` (không liên quan du học), `sensitive` (gian lận, làm giả, chính trị, nội dung xấu, cố ý đổi hướng dẫn của hệ thống / prompt injection), `farewell` (chào, cảm ơn, kết thúc), `ambiguous` (không rõ muốn gì và không có câu chờ) |
| `scenario` | Muốn thử "nếu như…" (`mode`: `what_if`, kèm `changes`) hoặc hỏi cần gì để vào một trường (`mode`: `goal_seeking`, kèm `school`) |

## 4. Các ca dễ lệch giữa hai người gán

| Ca | Quyết định |
|---|---|
| "Hỏi về hồ sơ" và "hỏi kiến thức" | Câu cần số liệu của chính học sinh (GPA em vào được trường nào) là `profile_review`. Câu nào ai hỏi cũng trả lời giống nhau (SAT là gì) là `knowledge` |
| Tìm người làm giúp và hỏi cách làm | "Ai sửa bài luận giúp em" là `center_referral`. "Bài luận Common App viết thế nào" là `knowledge` |
| Một câu so sánh hai thứ | "IELTS và TOEFL khác nhau thế nào" là **một** ý, không tách. Chỉ tách khi mỗi thứ có một câu hỏi riêng ("OPT và CPT là gì") |
| "Bố mẹ và em", "Toán và Lý" | Một giá trị hồ sơ, không tách |
| Giá trị hồ sơ nằm trong câu hỏi ("với IELTS 6.0 thì em vào được trường nào") | Ghi giá trị vào ý `collect_profile`, câu hỏi vẫn là ý riêng (`profile_review`) |
| Có câu chờ, học sinh nói **rõ chưa có hay chưa biết** ("em chưa biết nữa", "chưa có kế hoạch") | `collect_profile`, ghi nguyên văn vào khóa tương ứng |
| Có câu chờ, học sinh **lấp lửng** ("ừm để em nghĩ đã", "hmm") | `collect_profile`, `entities` rỗng |
| Không có câu chờ mà cũng lấp lửng ("ừm", "ok") | `out_of_scope`, `ambiguous` |
| Cùng một câu nhưng ngữ cảnh khác (câu chờ khác, hồ sơ khác) | Được, nhưng nhãn có thể khác. Mỗi câu chỉ có một nhãn trong cùng ngữ cảnh |
| Tên thành phố trong `center_referral` | Ghi đúng như học sinh viết ("Sài Gòn", "ha noi", "HN"), không đổi tên |

## 4b. Mục thông tin trường (`fields`) và câu không nêu mục

`fields` luôn là danh sách. Các mã: `cost`, `aid`, `english_requirement`, `test_policy`, `gpa`, `acceptance_rate`, `deadline`, `graduation_rate`,
`majors`, `ranking`, `location`, `website`, `housing`, `student_faculty_ratio`, `student_activities`, `campus_setting`, `other`.

| Học sinh hỏi | Mã |
|---|---|
| Tổng học phí, chi phí sinh hoạt | `cost` |
| Ký túc xá: loại nhà ở, nhà riêng cho du học sinh, tỷ lệ sinh viên ở trong trường, **tiền phòng, tiền cọc** | `housing` (không phải `cost`) |
| Tỷ lệ sinh viên trên giảng viên, lớp đông hay nhỏ, sĩ số lớp | `student_faculty_ratio` |
| Câu lạc bộ, đội nhóm, đời sống sinh viên | `student_activities` |
| Trường ở thành phố, ngoại ô, thị trấn hay nông thôn; khuôn viên lớn hay nhỏ | `campus_setting` |
| Trường ở bang nào, thành phố nào, địa chỉ | `location` |
| GPA trúng tuyển, tính weighted hay unweighted, thang điểm | `gpa` |

**Học sinh không nêu mục nào** ("cho em hỏi về Purdue", "so sánh 2 trường A và B", "A hay B tốt hơn") thì **bỏ hẳn khóa `fields`**, không đoán mục.

## 5. Trước khi lưu

1. Mỗi giá trị trong `entities` phải là đoạn chữ có trong `question`.
2. `collect_profile` đứng đầu, chỉ một ý, và là nơi duy nhất có khóa hồ sơ.
3. Không có ý chào, cảm ơn khi tin còn ý khác.
4. Chạy `python -m scripts.build_slm_jsonl` rồi `python -m pytest tests/test_slm_data.py`. Cả hai phải sạch.

## 6. Gán chéo (để đo độ đồng thuận)

Người thứ hai nhận một bản chỉ có các cột `question`, `profile`, `pending_question`, tự điền `intents` **không xem nhãn cũ**, theo hướng dẫn này.
Câu nào lệch với nhãn cũ thì ghi lại: đó là chỗ luật còn mơ hồ, cần viết rõ thêm vào mục 4.
