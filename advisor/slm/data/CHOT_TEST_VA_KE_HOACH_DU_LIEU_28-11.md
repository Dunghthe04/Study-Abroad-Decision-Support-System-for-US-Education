# Chốt test 27–28/11 và kế hoạch bổ sung dữ liệu train, kho kiến thức

Viết cho: thành viên nhóm làm dữ liệu train SLM và kho kiến thức.

Chi tiết từng lỗi: sheet `model_errors` trong `du_lieu_slm.xlsx` (lỗi SLM), `LOI_KHO_KIEN_THUC.md` (kho và dữ liệu),
`TONG_HOP_LOI_27-11.md` (các đợt test ngày 27/11). Model đang đo: `usas-slm-v6`.

Câu hỏi test do thành viên nhóm và trợ lý AI đặt, **chưa có câu của học sinh thật**. Nhãn đúng **chưa có người thứ hai duyệt**.

---

## 1. Chốt test

### 1.1. Lỗi code đã sửa và đã merge
| Lỗi | Trạng thái |
|---|---|
| Tính trên hồ sơ chưa xác nhận; xác nhận xong vẫn dùng lộ trình cũ | Đã sửa |
| Mắc kẹt ở câu ngành; nhắc câu cũ không giới hạn; nói lại giá trị cũ bị hỏi lại | Đã sửa |
| Từ khóa đè nhãn "tại sao đề xuất trường này"; tự gắn trường cũ | Đã sửa |
| "Trường đó", "trường đầu tiên / thứ N / cuối cùng" | Đã sửa; phần đọc thứ tự là **ngoại lệ tạm, gỡ sau khi train** |
| Câu ngắn bị coi là tên tỉnh | Đã sửa, hỏi lại kèm ô chọn |
| Chưa che thông tin cá nhân trước khi gửi Claude | Đã sửa, che theo mẫu (không che được tên viết tự do) |

Test tự động advisor lúc merge PR cuối: 2.477 đạt. **Phần lớn chưa thử trên giao diện.**

### 1.2. Còn lại, không thuộc dữ liệu train
- Một câu đáp chung cho mọi câu `exception`.
- Danh sách trống vì thiếu tiếng Anh nhưng chat không nói lý do.
- Bài đánh giá, lộ trình từ chat không được lưu (.NET chờ `skill_danh_gia`, `skill_lo_trinh`; advisor gửi tên loại ý). Thấy
  trong code, chưa kiểm database.
- Một ý sai khuôn làm bỏ cả tin. Làm lại hồ sơ chưa hỏi xác nhận (UC-35). Quyền tài khoản Free. Vài luật từ khóa chưa đo.
- Bấm nút giải thích thì chat chưa nhớ trường. Ngân sách (form, mức trên 70.000, khóa phụ huynh).

### 1.3. Lỗi SLM đã ghi: 167 dòng
| Trạng thái trong sheet | Số dòng |
|---|---|
| Open, chờ thêm dữ liệu train | 93 |
| Data fixed, waiting for retrain (ghi từ trước 27/11) | 43 |
| Open, cần nhóm chốt sửa hợp đồng | 13 |
| Open, cần chốt quy ước nhãn | 12 |
| Trạng thái khác | 6 |

**Việc đầu tiên:** chạy lại 43 dòng "Data fixed" trên v6. Chưa kiểm v6 đã học dữ liệu sửa đó hay chưa.

### 1.4. Câu dài (tính sau, giữ làm bộ đo)
90 câu 45–79 chữ, chỉ đo SLM, mỗi câu không có lịch sử chat, trợ lý AI tự chấm: đúng 41, một phần 8, sai 41 (trong đó 9
sai khuôn). Dữ liệu train: trung vị 9 chữ, 99% tối đa 29 chữ, 24/18.533 câu từ 45 chữ.

---

## 2. Chốt nhãn TRƯỚC khi viết dữ liệu

Chưa chốt mục nào thì **không viết dữ liệu cho mục đó**. Bản nháp quy ước: `QUY_UOC_NHAN_BO_SUNG_NHAP.md`.

| # | Cần chốt | Đề xuất |
|---|---|---|
| 1 | Quy tắc mục 1–6, 8, 10 của bản nháp (what_if, sensitive, exception, center, hỏi chung có hoàn cảnh, mở đầu) | Duyệt theo bản nháp |
| 2 | Điều kiện hệ thống không lọc được (thực tập, cộng đồng người Việt, gần biển) | `knowledge`, entities rỗng; không lọc thiếu mà không báo |
| 3 | "Em muốn đi Mỹ thì cần chuẩn bị gì" | Chọn một: `knowledge` hoặc `profile_review/roadmap` |
| 4 | "Cộng thêm 10k ngân sách" | Cần thêm `action: add` (sửa code) hoặc chưa train |
| 5 | Số lượng trường ("3 trường", "10 trường"), "xem thêm" | Cần khóa mới nếu muốn hỗ trợ (sửa hợp đồng + code) |
| 6 | Học phí tách khỏi tổng chi phí (lọc và xếp) | Cần chốt có khóa riêng không |
| 7 | Gợi ý theo hồ sơ kèm điều kiện (bang, rẻ nhất, nhóm Reach/Match/Safety) | Cần chốt cách ghi trong `profile_review` |
| 8 | Hỏi cách tính điểm phù hợp; xin giải thích lại; hỏi nguồn và ngày số liệu | Cần chốt nhãn |
| 9 | Tư vấn hộ người thân; trung tâm cam kết đậu visa | Cần chốt |
| 10 | Chỉ vị trí trong danh sách | **Đã chốt 28/11:** giữ khóa `schools`, ghi nguyên cụm học sinh nói, ví dụ `["trường thứ 2"]`, kể cả khi context có trường |

Mục 5–7 đụng hợp đồng (`app/schemas/slm.py`, prompt, code, .NET). **Không kịp sửa code thì bỏ khỏi đợt train này.**

---

## 3. Dữ liệu train cần thêm

Số câu là **ước lượng ban đầu**, đo lại sau khi train. Ví dụ trong bảng chỉ để hiểu kiểu câu, **không chép vào train**.

| Ưu tiên | Nhóm | Phải dạy model | Cặp đối chiếu cần có | Số câu gợi ý | Chờ chốt |
|---|---|---|---|---|---|
| 1 | Câu hỏi chung bị coi là ngoài phạm vi | Hỏi chung về du học Mỹ bậc đại học là `knowledge` (khó không, visa bị rớt, học y, IELTS 5.0) | Cùng kiểu câu nhưng xin việc, định cư, thạc sĩ, nước khác là `exception`; làm giả, mua bằng là `sensitive` | 150–200 | Mục 1 |
| 1 | "Nếu…" nhưng là câu hỏi | Không đưa giá trị mới để thử thì không phải `what_if` | "Nếu SAT em lên 1450…" vẫn là `what_if` | 100–150 | Mục 1 |
| 1 | Câu hỏi chung bị coi là chuyện riêng hoặc số liệu một trường | Không gắn `profile_review`, `collect_profile`, `school_info` khi ai hỏi cũng trả lời như nhau (làm thêm, bảo hiểm, sinh hoạt phí, hạn nộp muộn) | Câu cần số của chính học sinh là `profile_review` | 100–150 | Mục 1 |
| 1 | Xin danh sách trường bị bỏ sót | `profile_review` + `school_list`, cả khi đang chờ câu hồ sơ, cả câu vừa khai điểm vừa xin trường (hai ý) | "Chọn trường công" khi đang trả lời câu loại trường là `collect_profile` | 100 | |
| 2 | Câu nối tiếp và không nối tiếp | `follow_up: true` chỉ khi câu thiếu thông tin phải lấy từ context | Câu chung khi context có trường: KHÔNG `follow_up`; "còn học bổng?", "cái nào rẻ hơn?" thì có | 150 | |
| 2 | Chỉ trường theo vị trí | `schools` giữ cụm "trường đầu tiên", "trường thứ 2", "trường cuối", "2 trường đầu", kể cả khi context có tên trường | "Trường đó" lấy tên trong context | 80–100 | Đã chốt (mục 10) |
| 2 | Tách giá trị hồ sơ | Giá trị đủ số, không cắt cụt, không lấy cả câu; "sửa SAT thành 1400" là `edit` + `sat`; "800 triệu một năm" là `budget` | Câu về nơi đang sống không phải `relatives_state` (người thân ở Mỹ) | 100–150 | |
| 2 | Kịch bản what-if | Đủ `mode`; câu ngắn nối tiếp ("còn IELTS 7.5?"); không chép số từ context; chưa nói đổi gì thì không có `changes` | "Em tăng SAT lên 1500 rồi" là `collect_profile` | 100 | Mục 4 nếu có "cộng thêm" |
| 3 | Trung tâm và gặp người thật | Không bịa `service`, `city` ("chiều thứ Bảy" không phải thành phố); gặp người thật là `human_handoff`; chào, cảm ơn sau lượt trung tâm vẫn là chào, cảm ơn | "Có nên thuê trung tâm không" là `knowledge` | 80–100 | |
| 3 | Mở đầu hội thoại | "Em muốn tìm hiểu du học Mỹ" không phải lời chào | "Hello" vẫn là `greeting` | 40–60 | Mục 1, 3 |
| 4 | Sai khuôn | Không tự đặt khóa (`filters.other`, `filters.intake`), không trộn `tasks` vào `scenario`, một `service` một mã | Câu có điều kiện không lọc được theo mục 2 | Lồng vào các nhóm trên | Mục 2 |
| Sau | Câu dài 30–80 chữ, nhiều ý | Kể hoàn cảnh rồi hỏi phải giữ CẢ ý hỏi; "nếu" trong câu dài vẫn theo quy tắc trên | Câu dài chỉ khai hồ sơ | 300+ | Nếu đủ thời gian thì train cùng đợt |

### Quy tắc viết dữ liệu
1. Sửa trong `du_lieu_slm.xlsx` (6 sheet theo loại ý). Cột: `question`, `profile`, `pending_question`, `intents`, `context`,
   `review`, `split`. Nhãn theo `HUONG_DAN_GAN_NHAN.md` và `ENTITIES_REFERENCE.md`.
2. **Không chép** câu trong `model_errors`, `cau_hoi_100.py`, `cau_hoi_tu_nhien.py`, bộ 210 câu, bộ 90 câu dài, kịch bản test.
3. Mỗi nhóm viết nhiều kiểu câu: có dấu và không dấu, viết tắt, tiếng lóng, ngắn và vừa. Không chỉ đổi tên trường hay con số.
4. Cùng một kiểu câu ở nhiều ngữ cảnh: không hồ sơ, có hồ sơ, đang chờ câu hồ sơ, có `context`.
5. Luôn có cặp đối chiếu để model không lệch sang chiều ngược lại.
6. Câu gần giống nhau phải cùng một tập `split`. Dòng mới ghi `v9`/`v10` ở cột `review` chờ người **khác người viết** duyệt.
7. Đóng file Excel trước khi chạy script.

### Train và đo
Chạy trong thư mục `advisor`:
```
python -m slm.prepare.build_slm_jsonl
python -m slm.prepare.check_slm_data        (phải 0 lỗi)
python -m slm.prepare.export_slm_chat
slm/train/train_slm.ipynb (Colab) → ollama create usas-slm-v7 -f Modelfile
python -m slm.eval.eval_slm --model usas-slm-v7 --prompt slm_intents --split test
python -m slm.eval.slm_report ...
```
Train **một lần** sau khi đủ nhãn đã chốt. So v6 và v7 trên **cùng** các bộ: (a) tập test; (b) bộ hồi quy gồm các câu trong
`model_errors`; (c) 90 câu dài; (d) câu mới do người không viết dữ liệu train soạn. Tập test cùng nguồn với train nên điểm
cao hơn thực tế. Sau khi v7 trả đúng cụm vị trí thì **gỡ ngoại lệ tạm** trong `conversation_context.py`.

---

## 4. Kho kiến thức

### 4.1. Định dạng nhập
File Excel, sheet `knowledge_base`, 10 cột: `id`, `doc_type`, `label`, `question`, `answer`, `source`, `source_url`,
`retrieved_at`, `topic_code`, `note`. Moderator nhập bằng màn quản lý kho tri thức; sau khi nhập, hệ thống tạo vector.
Trước khi dùng `id` mới phải kiểm trong database vì có thể đã có người thêm.

### 4.2. Cách viết một mục
- Câu đầu của `answer` **trả lời thẳng** `question`; đọc riêng vẫn hiểu.
- `question` viết như học sinh hỏi.
- `source` và `source_url` là nguồn thật, mở được; ghi `retrieved_at`. Không để mã nội bộ ("RAW…", "CẦN HỘI ĐỒNG DUYỆT").
- Không thêm số liệu không có trong nguồn. **Người duyệt đọc nguồn** trước khi nhập. Không nhập câu AI viết mà chưa duyệt.

### 4.3. Việc cần làm
| Ưu tiên | Loại việc | Danh sách (chi tiết trong `LOI_KHO_KIEN_THUC.md`) |
|---|---|---|
| 1 | **Duyệt, ĐỔI MÃ rồi mới nạp 4 mục soạn sẵn** (`D:\DoAn\kho_kien_thuc_bo_sung_27-11.xlsx`, ngoài git, AI ghép từ mục có sẵn, chưa ai duyệt). File đang ghi mã 1196–1199 nhưng ngày 28/11 database đã có mục KHÁC ở mã 1196, 1197 (hai câu về trung tâm tư vấn): **không nạp nguyên file** | Chuẩn bị gì; xin học bổng thế nào; học xong có được ở lại làm việc (OPT); chưa có IELTS có nộp được không |
| 1 | **Viết mục còn thiếu** | Du học Mỹ có khó không; học sinh Việt Nam thường đi lúc nào; xin học bổng có khó không |
| 1 | **Sửa mục trả lời lệch** | Thư giới thiệu nên xin ai; đi làm thêm có được không; chứng minh tài chính cần giấy tờ gì |
| 2 | **Soát rồi mới viết** (nghi thiếu) | Về Việt Nam nghỉ hè có phải xin lại visa; làm chui; bang nào an toàn; bao giờ thi lại SAT; phí dịch vụ trung tâm; 10 câu nhận "chưa có thông tin" trong đợt 210 câu (quy trình; ngành Computer Science, AI, ngành kết hợp kinh doanh và công nghệ; ngoại khóa; chứng minh thu nhập; Personal Statement; ở lại làm việc; OPT và STEM OPT; học IT có cần giỏi Toán) |
| 2 | **Có mục đúng nhưng điểm dưới 0,72** | Sinh hoạt phí; merit và need-based; STEM OPT mấy năm; chủ đề bài luận; ED/EA/RD; cộng đồng người Việt; tự làm hay thuê trung tâm; pathway |
| 3 | **Trên ngưỡng nhưng lệch ý** | Xin visa khó không; không giỏi tiếng Anh; học bổng dễ xin không; gap year; nộp muộn deadline |
| 3 | Bỏ mã nội bộ trong tên nguồn | |

Với mục "dưới ngưỡng": thử sửa `question` cho giống cách học sinh hỏi rồi đo lại. **Chưa đổi ngưỡng 0,72**: đợt đo 52 câu,
hạ xuống 0,65 chỉ thêm 3 câu trả lời được và không giải quyết mục lệch ý.

### 4.4. Đo lại sau khi sửa kho
Tra lại đúng các câu trong `LOI_KHO_KIEN_THUC.md` (xem `scripts/eval_knowledge_retrieval.py`), thêm câu hỏi mới cùng kiểu và
câu ngoài phạm vi để chắc không trả lời nhầm. Ghi cột "Đã xử lý". Câu cần SLM gán `knowledge` mà đang bị gán sai thì phải
train xong mới thấy kết quả trong chat.

### 4.5. Dữ liệu khác (không phải kho)
- `university_offerings`: Williams, Amherst, Illinois Wesleyan có số được nhận lớn hơn số nộp: đối chiếu nguồn rồi sửa.
- Gỡ "Demo Center" khỏi danh bạ trung tâm đã xác minh trước khi demo.
- Danh mục ngành chưa nhận "Artificial Intelligence", "Information Systems": nhóm duyệt rồi thêm bằng migration.

---

## 5. Thứ tự làm
```
1. Chạy lại 43 dòng "Data fixed" trên v6
2. Họp chốt mục 2
3. Song song: (a) kho kiến thức ưu tiên 1   (b) viết dữ liệu train ưu tiên 1 → 2 → 3
4. Người khác duyệt dữ liệu, soạn bộ câu đo mới (cất riêng)
5. Train MỘT lần → đo → gỡ ngoại lệ tạm
6. Chạy lại kịch bản từ đầu đến cuối trên giao diện
```
