# Chốt hợp đồng SLM và danh sách việc code (28/11)

Viết cho: thành viên nhóm làm advisor, backend và dữ liệu train.

**Trạng thái: ĐỀ XUẤT, chờ nhóm duyệt.** Chưa sửa code nào theo tài liệu này. Tài liệu dữ liệu train và kho kiến thức:
`CHOT_TEST_VA_KE_HOACH_DU_LIEU_28-11.md`. Chỗ nào hai tài liệu khác nhau về nhãn hoặc hợp đồng thì theo tài liệu này.
Ví dụ trong tài liệu chỉ để hiểu kiểu câu, **không chép vào dữ liệu train**.

---

## 1. Mục tiêu

Không thể làm cho mọi câu được trả lời đúng. Mục tiêu là mọi câu rơi vào **một trong bốn kết quả**:

1. **Trả lời đúng**, có nguồn hoặc tính từ dữ liệu.
2. **Hỏi lại** đúng chỗ còn thiếu.
3. **Nói thật là chưa làm được** hoặc chưa có dữ liệu, nói rõ phần nào.
4. **Từ chối trung lập**, không quy chụp học sinh.

Lỗi nặng nhất là **kết quả thứ năm: trả lời sai mà trông như đúng**, hoặc bỏ một phần câu mà không báo.
Code giảm được kết quả này khi biết mình không đọc được hoặc không làm được. **Code không phát hiện được mọi trường hợp
SLM gắn nhầm một nhãn hợp lệ**; phần đó chỉ giảm bằng dữ liệu train và phải đo.

### Nguyên tắc
- SLM quyết định ý của mọi câu học sinh tự gõ. Code chỉ kiểm hợp đồng và xử lý theo nhãn.
- Không dò từ khóa để đổi hoặc đoán nhãn.
- Nút bấm là lệnh do hệ thống tạo, không phải câu học sinh tự gõ: chạy theo lệnh đã gắn.
- Claude đọc giá trị, chọn bài trong kho và viết lời tư vấn. Câu kiến thức không có nguồn thì không tự trả lời.
- Phần thêm vào hợp đồng đều **không bắt buộc**, để v6 vẫn chạy.
- **Không sửa `app/skills/slm_intents.md` khi còn chạy v6** (đó là prompt của v6). Prompt mới viết ra file riêng.

---

## 2. Hợp đồng

### 2.1. Giữ nguyên
6 loại ý; 20 khóa hồ sơ; các `tasks` của `collect_profile`, `profile_review`, `center_referral`; các `subtype` của
`out_of_scope`. Không đổi tên khóa (`school`/`schools`, `target_group`/`group`).

### 2.2. Thêm và nới

| Mã | Thay đổi | Ví dụ kiểu câu | Việc phải làm | Cần .NET |
|---|---|---|---|---|
| H1 | `compare`, `compare_facts` không bắt buộc đủ 2 tên; `schools` được ghi cụm chỉ vị trí ("2 trường đầu") | so hai trường đầu danh sách | Advisor đổi cụm vị trí ra tên, hoặc lấy ngữ cảnh khi `follow_up: true`; thiếu thì hỏi "muốn so trường nào". Đúng 1 tên trường thật thì trả số liệu trường đó | Không |
| H2 | `changes.action`: `add`, `subtract` (chỉ `budget`) | thêm vài nghìn đô mỗi năm | `scenario.py` cộng trừ vào ngân sách hiện có | Không |
| H3 | `unsupported`: danh sách cụm chữ học sinh nói, cho `knowledge/search`, `profile_review/school_list`, `scenario/goal_seeking` | lọc theo điều kiện hệ thống không có (gần khu công nghệ, loại trừ một bang) | Chạy phần làm được, rồi nói rõ "chưa lọc được theo: …". Không còn điều kiện nào làm được thì là `knowledge` thường | Cần cho `school_list` |
| H4 | `sort`: `lowest_tuition`, `highest_tuition` | học phí thấp nhất | Advisor và .NET đã có hai cách xếp này, chỉ cần nhận từ hợp đồng | Không |
| H5 | `filters.max_tuition` | học phí dưới một mức | Thêm bộ lọc | **Có** |
| H6 | `group` thêm giá trị `aid` (nhóm cần học bổng lớn) | hỏi về nhóm cần học bổng lớn | Xem 4.1 | **Có** |
| H7 | `profile_review` + `school_list` nhận `filters` (chỉ `state`, `school_type`) và `group` | gợi ý theo hồ sơ ở một bang | Áp điều kiện vào gợi ý | **Có** |
| H8 | `limit` cho `search` và `school_list` | xin 3 trường | Cắt danh sách | **Có** |
| H9 | `knowledge` thêm task `fit_method` | hỏi cách tính điểm phù hợp | Thay phần dò từ khóa (chỉ gỡ sau v7) | Không |

**Cập nhật 28/11 (chiều): đợt này chỉ làm H1, H2, H3, H4, H9. HOÃN H5, H6, H7, H8** (phụ thuộc backend). Điều kiện kiểu
đó trong `school_list` (nhóm, số lượng, khu vực) ghi vào `unsupported`; riêng **bang và loại trường là mục hồ sơ**
(`collect_profile` với `preferred_states`, `school_type`, theo đúng dữ liệu cũ, vì gợi ý lọc được); nhóm cần học bổng lớn
ghi vào hạn chế. Sheet
`huong_dan_thu_thap` trong `du_lieu_slm.xlsx` theo bản này; dòng mẫu dùng hợp đồng mới nằm ở sheet
`mau_hop_dong_moi` và chỉ chuyển vào 6 sheet dữ liệu sau khi code nhận.

**Cập nhật 28/11 (tối), đã làm:** `schemas/slm.py` đã nhận H1, H2, H3, H4, H9 (chỉ phần kiểm và khuôn đầu ra);
`scenario.py` hỏi lại tổng ngân sách khi gặp `add` / `subtract`; prompt train mới `slm_intents_v7.md`, chat đang chạy
vẫn dùng `slm_intents.md`. **Chưa làm** phần xử lý trong chat cho các nhãn mới: phải xong trước khi dùng model v7
(H1: `relax_compare` hiện vẫn bỏ ý so sánh thiếu tên; H2: mới hỏi lại; H3: chưa nói phần chưa lọc được; H9: chưa nối
với câu trả lời cách tính điểm). Dữ liệu: câu hợp đồng mới đã nằm trong 6 sheet, chia 70/15/15. So v6 với v7 bằng
`test_so_sanh_v6_v7.jsonl`.

**Cần chốt:** "học phí dưới X" trong tìm trường: dữ liệu cũ ghi `max_cost` (vốn là tổng chi phí).

**Đính chính mục 3, dòng `span` của `knowledge`:** bộ kiểm dữ liệu bắt `span` nằm nguyên văn trong câu, nên **chưa**
viết dữ liệu thay "ngành này", "trường đó" bằng tên. Muốn làm phải đổi luật kiểm và code trước.

**Luật chốt:** hạng mục nào không kịp xong code trước khi viết dữ liệu thì **bỏ khỏi hợp đồng đợt này**, ghi vào hạn chế,
và không viết dữ liệu dùng khóa đó.

### 2.3. Khi nào được mở lại hợp đồng
Chỉ khi có **cả hai**: (a) học sinh nói một ý mà hợp đồng không ghi được; (b) bỏ ý đó thì kết quả sai mà học sinh không
biết. Model hiểu sai điều hợp đồng ghi được thì chỉ thêm dữ liệu.

---

## 3. Quy ước nhãn (chốt trước khi viết dữ liệu)

| Kiểu câu | Nhãn |
|---|---|
| Ai hỏi cũng trả lời như nhau | `knowledge` |
| Phải có số của chính học sinh mới trả lời được | `profile_review` |
| Hỏi chung "đi Mỹ cần chuẩn bị gì" | `knowledge` |
| So đi thẳng đại học với cao đẳng cộng đồng | `knowledge` (+ `collect_profile` nếu có kể hồ sơ) |
| Hỏi điểm một năm thấp có bị loại không, và bản thân nên làm gì | `knowledge` + `profile_review/roadmap` |
| Có chữ "nếu" nhưng không đưa giá trị mới để thử | không phải `what_if` |
| Nói không cần học bổng nữa | `scholarship: optional` |
| Hỏi cần điểm bao nhiêu để được học bổng | `knowledge`, không dùng `goal_seeking` |
| Điểm một môn | không phải `gpa` |
| Người thân bảo lãnh, vay tiền đóng học, từng trượt visa, lo bị nghi định cư | `knowledge`, không phải `sensitive` |
| Khai khống, mượn sổ tiết kiệm, giấu thông tin khi phỏng vấn, làm giả giấy tờ | `out_of_scope/sensitive` |
| Tư vấn hộ người thân; trung tâm cam kết đậu visa | `knowledge` |
| Câu vừa kể hồ sơ vừa hỏi | đủ cả ý hồ sơ và ý hỏi |
| `span` của `knowledge` | viết lại thành câu đủ nghĩa: thay "ngành này", "trường đó" bằng tên |
| Chỉ trường theo vị trí | `schools` giữ cụm học sinh nói (đã chốt 28/11) |

Phần còn lại theo `QUY_UOC_NHAN_BO_SUNG_NHAP.md`; dòng nào khác bảng trên thì theo bảng trên và sửa lại bản nháp.

---

## 4. Hai phát hiện khi kiểm trước lúc chốt (28/11)

### 4.1. Nhóm "cần học bổng lớn" không được gửi sang advisor
Theo code `ChatRecommendation.Schools`, mỗi trường gửi sang chỉ mang mã `g2` (nhóm điểm), **không mang tên khối đang
hiển thị**. Advisor không biết trường nào nằm trong khối "cần học bổng lớn". Chưa kiểm bằng dữ liệu chạy thật việc so
sánh theo nhóm Khó vào/Vừa sức/An toàn có lẫn trường của khối này hay không. **Người phụ trách backend cần xem.**

### 4.2. Mã dịch vụ "nộp hồ sơ" không khớp database
Hợp đồng `APPLICATION_SUBMISSION` được đổi thành mã `NOP_HO_SO`. Trên database local **không trung tâm nào có
`NOP_HO_SO`**; dữ liệu dùng `HO_SO_VISA` (12 trung tâm). Vì vậy tìm trung tâm "nộp hồ sơ" sẽ không ra kết quả trên dữ
liệu này. Năm mã còn lại đều có trung tâm. **Cần nhóm quyết định:** "hồ sơ visa" có cùng nghĩa "nộp hồ sơ" không; nếu
không thì dữ liệu trung tâm thiếu dịch vụ này.

---

## 5. Việc code

Mỗi việc: viết test tái hiện lỗi và thấy test trượt → sửa nguyên nhân → chạy toàn bộ test → thử bằng model thật khi được →
commit riêng.

### Đợt 1: không đụng hợp đồng, không chờ train

| # | Việc | Xong khi | Chủ yếu ở |
|---|---|---|---|
| C1 | Nút bấm mang sẵn lệnh | Test duyệt mọi nút: không nút nào phụ thuộc SLM; chữ học sinh tự gõ vẫn qua SLM | advisor + .NET |
| C2 | Khớp tên ngành có ngoặc | Các phần cùng chỉ một ngành thì nhận; chỉ hai ngành khác nhau thì hỏi lại | advisor |
| C3 | Claude đọc số: ngân sách nhiều năm, theo tháng; điểm một môn, GPA từng năm | Code tự nhân chia; có nhãn mà không đọc ra thì báo và hỏi, không chạy bằng số cũ; GPA từng năm thì hỏi lại GPA chung | advisor |
| C4 | Hoạt động, giải thưởng đang chờ | Hỏi tiếp ý còn thiếu; không còn báo "chưa thấy thông tin mới" | advisor |
| C5 | Luồng điền hồ sơ | Trả lời xong không bị hỏi lặp cùng câu; làm lại từ đầu phải xác nhận | advisor |
| C6 | Câu từ chối và câu báo lỗi trung lập | `sensitive`, `exception` không khẳng định điều học sinh không hỏi; Ollama lỗi thì báo lỗi hệ thống; không lặp dòng; ví dụ gợi ý đúng loại điều kiện; mỗi lượt chỉ yêu cầu một thao tác | advisor + .NET |
| C7 | Giữ ý hợp lệ khi một ý hỏng, và báo phần chưa đọc được | Không bao giờ giữ mà không báo | advisor |
| C8 | Đường tư vấn không trả lời kiến thức khi không có nguồn (nhất là quyền riêng tư) | **Cần trình thiết kế trước**: chỉ dựa vào nhãn; câu bị gắn nhầm nhãn là phần khó | advisor |
| C9 | Lưu đánh giá, lộ trình tạo từ chat | Tên loại hai bên khớp nhau (mới thấy trong code, chưa kiểm database) | .NET |
| C10 | Mã dịch vụ trung tâm (4.2) | Sau khi nhóm quyết định | .NET hoặc advisor |

### Đợt 2: hợp đồng
H1 → H2 → H3 → H4 → (H5, H6, H7, H8 khi backend sẵn sàng) → H9. Mỗi hạng mục sửa cùng lúc: `schemas/slm.py` (kiểm và
khuôn đầu ra), script kiểm dữ liệu, `ENTITIES_REFERENCE.md`, prompt mới (file riêng), nơi dùng giá trị, test.

### Đợt 3: sau khi có v7
Gỡ dò từ khóa phân biệt học phí với tổng chi phí và nhận câu hỏi cách tính điểm; đọc vị trí trường từ `schools`; đổi sang
prompt mới. Gỡ sớm hơn thì v6 hỏng lại.

---

## 6. Tiêu chí đóng băng code
- Đợt 1 và các hạng mục hợp đồng đã chọn xong, test tự động đạt.
- Chạy lại các bộ câu đã đo ngày 28/11: lỗi thuộc code đã hết, không phát sinh lỗi mới.
- Chạy kịch bản trên giao diện: không bị kẹt, không có số sai.
- Tài liệu (SRS, UC, SDD) khớp code.

Sau đó chỉ thêm dữ liệu, bổ sung kho và train; sửa code chỉ khi có lỗi chặn demo.

## 7. Hạn chế ghi vào báo cáo (không làm đợt này)
Xem thêm trường khác; lọc trong đúng danh sách đã gợi ý; giải thích cả một nhóm trường; tính mức cần đạt để được học
bổng; lọc theo thực tập hoặc vị trí; so sánh đặc điểm trung tâm; câu dài nhiều ý còn bị bỏ sót.

## 8. Cần nhóm chốt
1. Duyệt mục 2 và mục 3.
2. Backend có làm H5–H8 và 4.1 không, hạn nào.
3. Mã dịch vụ "nộp hồ sơ" (4.2).
4. Hạn train và hạn demo.
