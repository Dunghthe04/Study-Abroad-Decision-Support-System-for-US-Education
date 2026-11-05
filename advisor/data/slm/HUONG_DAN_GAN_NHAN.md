# Hướng dẫn gán nhãn dữ liệu SLM

Dành cho người thêm hoặc kiểm dòng trong `du_lieu_slm.xlsx`. Excel là nguồn duy nhất; file `du_lieu_slm.jsonl` sinh từ Excel bằng
`python -m scripts.build_slm_jsonl`, không sửa tay. Script kiểm từng dòng và báo tên sheet, số dòng nếu sai.

## 1. Một dòng gồm những gì

| Cột | Ghi gì |
|---|---|
| `cau_hoi` | Tin nhắn của học sinh, đúng như học sinh gõ (kể cả không dấu, sai chính tả) |
| `ho_so` | Hồ sơ học sinh đã có (JSON) hoặc để trống |
| `cau_he_thong_dang_hoi` | Câu hệ thống vừa hỏi và đang chờ trả lời, chép đúng từ `chat_flow.QUESTIONS`; không có thì để trống |
| `cac_y` | **Nhãn**: danh sách ý (JSON), xem mục 2 |
| `skill_md` | Chỉ sheet `ho_so`: skill mẫu của dòng, cần có `ho_so` |
| `tap` | `train`, `val` hoặc `test`. Câu gần giống nhau phải cùng tập; không để câu gần trùng một câu train nằm ở val hoặc test |

Sheet là loại của **một trong các ý** của dòng (thường là ý đầu). Sheet không ảnh hưởng đáp án, chỉ để dễ tìm.

## 2. Cột `cac_y`

```json
[{"type": "mac_dinh", "doan": "em GPA 3.5", "thuc_the": {"gpa": "em GPA 3.5"}},
 {"type": "kien_thuc", "doan": "I-20 là gì", "thuc_the": {}}]
```
Mỗi ý có đúng 3 khóa: `type` (loại), `doan` (đoạn chữ của ý), `thuc_the` (thông tin tách ra, không có thì `{}`).

**Luật chung**
1. Một tin tách thành các ý, **theo thứ tự học sinh nói**. Tối đa 7 ý.
2. `doan` chỉ dùng chữ có trong `cau_hoi`. `kien_thuc` và `trung_tam` được viết lại thành câu hỏi đầy đủ nhưng vẫn chỉ dùng chữ trong tin ("I-20 và SAT là gì" thành "I-20 là gì" và "SAT là gì").
3. Giá trị hồ sơ **chỉ ghi ở ý `mac_dinh`**, nguyên văn đoạn chữ trong tin. Ý khác không có khóa hồ sơ.
4. **Chỉ một ý `mac_dinh`**, gộp mọi giá trị hồ sơ vào đó, và luôn đứng **đầu** danh sách dù học sinh nói sau.
5. `ho_so` chỉ một ý: nhiều câu hỏi loại này gộp thành một ý (`doan` ghi gộp).
6. `kien_thuc` và `trung_tam`: **mỗi câu hỏi một ý**, kể cả khi cùng loại.
7. Lời chào, cảm ơn đi kèm câu hỏi hoặc thông tin hồ sơ thì **bỏ**, không tạo ý. Tin chỉ có chào, cảm ơn thì một ý `ngoai_pham_vi`.
8. `ngoai_pham_vi` đứng một mình, bắt buộc có `loai_con`.

## 3. Chọn loại

| Loại | Dấu hiệu |
|---|---|
| `mac_dinh` | Học sinh **khai** thông tin về mình, hoặc trả lời câu đang chờ |
| `ho_so` | Muốn được tư vấn **dựa trên hồ sơ của mình** (nên chọn trường nào, đánh giá hồ sơ, thuộc nhóm nào) |
| `kien_thuc` | Hỏi kiến thức chung, **không cần biết hồ sơ** của học sinh (visa, kỳ thi, học bổng, học phí) |
| `trung_tam` | Muốn **tìm người hoặc nơi làm giúp** một dịch vụ |
| `ngoai_pham_vi` | Còn lại: `ngoai_le` (không liên quan du học), `nhay_cam` (gian lận, làm giả, chính trị, nội dung xấu), `tam_biet` (chào, cảm ơn, kết thúc), `mo_ho` (không rõ muốn gì và không có câu chờ) |
| `kich_ban` | **Chưa gán nhãn** (làm sau cùng). Test `POSTPONED` sẽ báo nếu có dòng nào chứa loại này |

## 4. Các ca dễ lệch giữa hai người gán

| Ca | Quyết định |
|---|---|
| "Hỏi về hồ sơ" và "hỏi kiến thức" | Câu cần số liệu của chính học sinh (GPA em vào được trường nào) là `ho_so`. Câu nào ai hỏi cũng trả lời giống nhau (SAT là gì) là `kien_thuc` |
| Tìm người làm giúp và hỏi cách làm | "Ai sửa bài luận giúp em" là `trung_tam`. "Bài luận Common App viết thế nào" là `kien_thuc` |
| Một câu so sánh hai thứ | "IELTS và TOEFL khác nhau thế nào" là **một** ý, không tách. Chỉ tách khi mỗi thứ có một câu hỏi riêng ("OPT và CPT là gì") |
| "Bố mẹ và em", "Toán và Lý" | Một giá trị hồ sơ, không tách |
| Giá trị hồ sơ nằm trong câu hỏi ("với IELTS 6.0 thì em vào được trường nào") | Ghi giá trị vào ý `mac_dinh`, câu hỏi vẫn là ý riêng (`ho_so`) |
| Có câu chờ, học sinh nói **rõ chưa có hay chưa biết** ("em chưa biết nữa", "chưa có kế hoạch") | `mac_dinh`, ghi nguyên văn vào khóa tương ứng |
| Có câu chờ, học sinh **lấp lửng** ("ừm để em nghĩ đã", "hmm") | `mac_dinh`, `thuc_the` rỗng |
| Không có câu chờ mà cũng lấp lửng ("ừm", "ok") | `ngoai_pham_vi`, `mo_ho` |
| Cùng một câu nhưng ngữ cảnh khác (câu chờ khác, hồ sơ khác) | Được, nhưng nhãn có thể khác. Mỗi câu chỉ có một nhãn trong cùng ngữ cảnh |
| Tên thành phố trong `trung_tam` | Ghi đúng như học sinh viết ("Sài Gòn", "ha noi", "HN"), không đổi tên |

## 5. Trước khi lưu

1. Mỗi giá trị trong `thuc_the` phải là đoạn chữ có trong `cau_hoi`.
2. `mac_dinh` đứng đầu, chỉ một ý, và là nơi duy nhất có khóa hồ sơ.
3. Không có ý chào, cảm ơn khi tin còn ý khác.
4. Chạy `python -m scripts.build_slm_jsonl` rồi `python -m pytest tests/test_slm_data.py`. Cả hai phải sạch.

## 6. Gán chéo (để đo độ đồng thuận)

Người thứ hai nhận một bản chỉ có các cột `cau_hoi`, `ho_so`, `cau_he_thong_dang_hoi`, tự điền `cac_y` **không xem nhãn cũ**, theo hướng dẫn này.
Câu nào lệch với nhãn cũ thì ghi lại: đó là chỗ luật còn mơ hồ, cần viết rõ thêm vào mục 4.
