# Gợi ý trường: việc cần làm

Đánh dấu `[x]` khi xong. Làm đúng thứ tự mốc. Mốc trước có test rồi mới sang mốc sau.

Luồng:

```text
Hồ sơ
  → Hard filter (backend)
  → Qwen + skill bậc ngoại khóa
  → Cloud API + skill chấm
  → Backend lưu đúng kết quả cloud trả
```

Qwen không nhận danh sách trường. Cloud không đọc lại hoạt động thô, chỉ nhận bậc ngoại khóa 1–5. Backend không tính lại công thức.

---

## Mốc 1. Hard filter (backend)

Chỉ trả lời trường có được xét hay không. Không chấm điểm.

- [x] **1.1** Viết `HardFilter`: ngành, bang, loại trường, tiếng Anh, ngân sách × 1,1, mức cần học bổng
- [x] **1.2** Mỗi trường ra một trong ba kết quả: giữ, giữ ở khối học bổng, loại
- [x] **1.3** Thiếu số liệu thì giữ và gắn cờ, không loại chỉ vì không có số
- [x] **1.4** Test ví dụ An: Colgate sang khối học bổng, trường trong ngân sách thì giữ, trường sai ngành thì loại
- [x] **1.5** Test chọn "Không cần học bổng" thì trường vượt ngân sách biến mất

Xong khi: test lọc chạy đúng, chưa gọi Qwen hay cloud.

---

## Mốc 2. Bậc ngoại khóa (Qwen + skill)

Một hồ sơ, một lần gọi. Qwen đọc mô tả và tính bậc 1–5.

- [ ] **2.1** Viết `skill_ec.md`: vai trò, phạm vi, cách ra bậc 1–5, khuôn JSON
- [ ] **2.2** Endpoint `POST /recommend/ec` trên advisor, gọi Qwen
- [ ] **2.3** Sai khuôn JSON thì gọi lại đúng 1 lần
- [ ] **2.4** Code chặn Qwen nâng phạm vi cao hơn mức học sinh đã chọn ở form
- [ ] **2.5** Qwen lỗi thì dùng vai trò học sinh đã chọn, không chặn cả lần gợi ý
- [ ] **2.6** Test: đội trưởng CLB trường không bị nâng lên quốc tế; hồ sơ mẫu ra đúng bậc

Xong khi: một hồ sơ trả bậc trong khoảng 1–2 giây, chưa gửi trường lên cloud.

---

## Mốc 3. Chấm và chia nhóm (cloud + skill)

Cloud tự tính công thức, điểm, nhóm và danh sách.

- [ ] **3.1** Viết skill chấm: trọng số, điểm vị trí, điểm sức mạnh, nhóm Khó vào / Vừa sức / An toàn hơn / cần học bổng lớn
- [ ] **3.2** Viết skill chọn: theo nút ưu tiên, lấy tối đa 3 Khó vào, 5 Vừa sức, 4 An toàn hơn, cộng khối học bổng
- [ ] **3.3** Client gọi cloud và endpoint advisor nhận hồ sơ + bậc ngoại khóa + trường đã qua lọc
- [ ] **3.4** Chia lô để không vượt trần token của một lần gọi
- [ ] **3.5** Sai khuôn thì gọi lại đúng 1 lần. Lô vẫn sai thì ghi trường đó lỗi, không bịa điểm
- [ ] **3.6** Đo một hồ sơ mẫu trên cloud trước khi gắn vào API thật

Xong khi: hồ sơ mẫu nhận được nhóm và danh sách từ cloud.

---

## Mốc 4. Nối và lưu

- [ ] **4.1** `RecommendationService`: lọc cứng → `/recommend/ec` → cloud → lưu
- [ ] **4.2** Backend kiểm tra khuôn: trường thuộc tập đã lọc, nhóm hợp lệ, điểm trong khoảng cho phép
- [ ] **4.3** Giữ nguyên số cloud trả. Không tính lại bằng công thức ở backend
- [ ] **4.4** Lưu vào bảng `recommendations` kèm phiên bản skill và tên model
- [ ] **4.5** API tạo gợi ý và API lấy lần gợi ý gần nhất
- [ ] **4.6** Test luồng với advisor giả: lọc xong mới gọi Qwen, có bậc rồi mới gọi cloud, kết quả lưu đúng

Xong khi: bấm gợi ý ra danh sách đã lưu, tải lại không tính lại.

---

## Mốc 5. Sau khi đã có danh sách

- [ ] **5.1** Đổi nút ưu tiên chỉ gọi lại skill chọn, không lọc lại và không gọi lại Qwen
- [ ] **5.2** Mở chi tiết trường: Qwen viết lời tư vấn từ số đã lưu
- [ ] **5.3** Màn kết quả: bốn khối, bốn nút ưu tiên, mã lý do thành câu, cờ thiếu dữ liệu
- [ ] **5.4** Hồ sơ xong ở form thì mở màn kết quả
