# Quy ước nhãn bổ sung (BẢN NHÁP 27/11/2026, chờ nhóm chốt)

Bổ sung cho `HUONG_DAN_GAN_NHAN.md` mục 4. Mỗi quy tắc sinh ra từ lỗi thật khi thử 100 câu tự nhiên và 4 hội thoại
(`slm/eval/cau_hoi_100.py`, số trong ngoặc là số câu). Chốt xong thì chép vào mục 4 của hướng dẫn, rồi mới soạn dữ liệu train.

Cách duyệt: mỗi quy tắc ghi **Đồng ý** hoặc **Sửa: ...** ngay dưới. Mục có nhãn **CẦN CHỐT** có nhiều cách, nhóm chọn một.

## Nguyên tắc chung

1. **Ai hỏi cũng nhận cùng câu trả lời** thì là `knowledge`. **Cần số liệu của chính học sinh mới trả lời được** thì là `profile_review`.
2. Câu **có giá trị hồ sơ của chính mình** (GPA 3.0, IELTS 6.5, ngân sách 500 triệu) thì thêm ý `collect_profile` ghi giá trị đó.
3. Nhãn đi theo **điều học sinh muốn**, không theo một từ khóa ("nếu", "chứng minh tài chính", "trung tâm" không tự quyết định nhãn).

## 1. `scenario` chỉ khi muốn THỬ ĐỔI một giá trị

Chỉ gán `scenario/what_if` khi học sinh đưa ra **giá trị khác hồ sơ hiện tại để xem kết quả đổi thế nào**.

| Câu | Nhãn đúng | Nhầm thường gặp |
|---|---|---|
| Nếu SAT em lên 1450 thì danh sách có khác không | `scenario/what_if` (sat 1450) | |
| Nếu em chưa có hoạt động ngoại khóa thì có nên nộp ngay không (50) | `profile_review/roadmap` | `scenario` |
| Nếu gia đình em không quá khá giả thì có đi Mỹ được không (4) | `collect_profile` (budget nguyên văn) + `profile_review/assessment` | `scenario` |
| Em nên chọn trường thứ hạng cao hay học phí thấp (55) | `profile_review/school_list` | `scenario` |
| Em bị điểm Toán thấp thì có ảnh hưởng khi nộp ngành IT không (49) | `profile_review/assessment` | `scenario` thiếu giá trị (sai khuôn) |

Lý do: trong train có 473 câu mở đầu bằng "nếu" là `scenario`, chỉ 22 câu nhãn khác, nên model học "nếu" là `scenario`.

## 2. `out_of_scope/sensitive` chỉ khi có ý GIAN LẬN hoặc LÀM GIẢ

| Câu | Nhãn đúng |
|---|---|
| Có thể đi du học Mỹ mà không cần chứng minh tài chính không (33) | `knowledge` |
| Nếu bố mẹ em là người bảo lãnh tài chính thì cần giấy tờ gì (78) | `knowledge` |
| Mượn tiền bỏ vào sổ tiết kiệm vài tháng để chứng minh tài chính được không | `out_of_scope/sensitive` |
| Làm giả bảng điểm có bị phát hiện không | `out_of_scope/sensitive` |

Việc cần làm trước: đọc lại 10 dòng train nhắc "chứng minh tài chính" (7 dòng đang là `out_of_scope`).

## 3. `out_of_scope/exception` chỉ khi ngoài du học Mỹ bậc đại học

| Câu | Nhãn đúng |
|---|---|
| Du học Mỹ có khó không (3) | `knowledge` |
| Hồ sơ du học Mỹ cần những giấy tờ gì (45) | `knowledge` |
| Điểm học bạ lớp 10 có ảnh hưởng xét tuyển không (48) | `knowledge` |
| Nên đi du học Mỹ hay Canada (6), Du học Úc thì sao | `out_of_scope/exception` (hệ thống chỉ hỗ trợ Mỹ) |
| Em muốn học thạc sĩ ở Mỹ | `out_of_scope/exception` |
| Hôm nay Hà Nội có mưa không (97) | `out_of_scope/exception` |

## 4. `center_referral` chỉ khi muốn TÌM, SO SÁNH hoặc ĐẶT LỊCH với trung tâm

| Câu | Nhãn đúng |
|---|---|
| Em cần chuẩn bị gì khi phỏng vấn visa (72) | `knowledge` |
| Có chỗ nào ở Đà Nẵng luyện phỏng vấn visa không | `center_referral` (VISA_INTERVIEW_PREP, Đà Nẵng) |
| Có nên thuê trung tâm làm hồ sơ không (85) | `knowledge` |
| Giới thiệu em trung tâm làm hồ sơ uy tín | `center_referral` |
| Em muốn xin học bổng du học Mỹ (HT3) | `knowledge` (đề xuất; xem mục 9) |

## 5. Hỏi chung nhưng có nhắc hoàn cảnh của mình

| Câu | Nhãn đúng |
|---|---|
| GPA 3.0 có xin được học bổng không (35) | `collect_profile` (gpa) + `profile_review/assessment` |
| Em có SAT 1350, như vậy có ổn không (15) | `collect_profile` (sat) + `profile_review/assessment` (không để tasks rỗng) |
| Bố mẹ em làm kinh doanh tự do thì có chứng minh tài chính được không (17) | `knowledge` (hỏi quy định chung; nghề của bố mẹ không phải khóa hồ sơ) |
| Học bổng toàn phần có khó xin không (34) | `knowledge` |

## 6. Mở đầu hội thoại không phải lời chào

| Câu | Nhãn đúng | Đang bị |
|---|---|---|
| Em muốn đi du học Mỹ (HT1) | `profile_review`, tasks `[]` (muốn đi nhưng chưa nói cần gì: hệ thống bắt đầu hỏi hồ sơ) | `greeting` |
| Em muốn tìm hiểu về du học Mỹ (HT4) | `knowledge` (kho có mục tổng quan) | `greeting` |
| Em muốn đi du học Mỹ thì cần chuẩn bị những gì (1) | `knowledge` hoặc `profile_review/roadmap`: **CẦN CHỐT** một nhãn | |

Trong train hiện có 0 câu kiểu "em muốn (đi) du học Mỹ" đứng một mình.

## 7. Điều kiện tìm trường hệ thống KHÔNG lọc được — CẦN CHỐT

Ví dụ: cộng đồng người Việt đông (28), có co-op hoặc thực tập hưởng lương (30). Model đang tự đặt khóa `filters.other` nên sai khuôn.

- **A (đề xuất):** `knowledge` với `entities` rỗng. Kho trả lời nếu có, không có thì nói "chưa có thông tin". Không lọc thiếu điều kiện mà không báo.
- B: `knowledge/search` chỉ với điều kiện lọc được (bỏ phần không lọc được). Rủi ro: học sinh tưởng kết quả đã xét co-op.

## 8. So sánh chung giữa loại trường

| Câu | Nhãn đúng |
|---|---|
| Học phí trường công lập và tư thục khác nhau thế nào (40) | `knowledge`, `entities` rỗng (không dùng `group`: `group` chỉ là reach/target/safety) |
| Trường nào ở Mỹ nằm trong top 50 (HT2) | `knowledge/search` (filters `max_rank` 50) |
| Nếu em muốn học trường top 50 thì sao (HT2) | `knowledge/search` (filters `max_rank` 50); `max_rank` không phải yếu tố `what_if` |

## 9. "Cộng thêm" một khoản ngân sách — CẦN CHỐT, cần sửa code

"Nhà em ráng thêm được 10k nữa thì sao": nhãn hiện ghi `budget 10000`, code hiểu là **tổng** ngân sách 10.000 USD.

- **A (đề xuất):** thêm `action: "add"` vào `changes` (`{factor: budget, value: 10000, unit: USD, action: add}`); `scenario.py` cộng vào ngân sách hiện có. Phải sửa prompt, code, `ENTITIES_REFERENCE.md`.
- B: chưa train kiểu câu này; hệ thống hỏi lại "ngân sách mới là bao nhiêu".

Chưa chốt thì **không** thêm dữ liệu kiểu câu này.

## 10. Câu nối tiếp và tên riêng

| Câu | Nhãn đúng |
|---|---|
| Còn I-20 thì sao (không có context) (HT4) | `knowledge`, `entities` rỗng. `schools` chỉ chứa **tên trường thật**, không chứa mã giấy tờ (I-20, DS-160) |
| Trường nào có học phí rẻ nhất (sau khi đã lọc California) (HT2) | `knowledge/search`, sort `lowest_cost`, `follow_up` true (giữ điều kiện California từ context) |
| GPA của em là 3.4. | `collect_profile`, giá trị **có cả số** ("GPA của em là 3.4"), không được cắt thành "GPA của em là " |

## 11. Dữ liệu cần bổ sung sau khi chốt

Theo nhóm lỗi, mỗi nhóm vài chục câu (ước lượng, đo lại sau khi train):
- Cặp câu gần giống mà khác nhãn cho mục 1 đến 4.
- Cùng một câu ở nhiều ngữ cảnh: không hồ sơ, có hồ sơ, đang chờ câu hồ sơ, có context.
- Câu hỏi kiến thức **có kèm hồ sơ** (train hiện chỉ có 113 trong 4.201 dòng `knowledge` có hồ sơ).
- Mở đầu hội thoại (mục 6), câu nối tiếp (mục 10), số có dấu chấm cuối ("3.4.", "20.000").
- Viết nhiều kiểu câu khác nhau: không chỉ đổi tên trường hay con số trong cùng một khuôn.

Không chép câu trong `cau_hoi_100.py`, `cau_hoi_tu_nhien.py` vào train: đó là bộ đo.
