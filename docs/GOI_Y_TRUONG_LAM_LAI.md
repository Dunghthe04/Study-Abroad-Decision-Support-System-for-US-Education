# Gợi ý trường: việc cần làm

Cập nhật 19/10/2026. Đánh dấu `[x]` khi xong. Làm đúng thứ tự mốc.

Luồng:

```text
Hồ sơ
  → Hard filter (backend)
    → Qwen + skill bậc ngoại khóa
    → Gemini chấm (skill_score) rồi chọn danh sách (skill_pick)
    → Backend lưu đúng kết quả cloud trả
```

Qwen không nhận danh sách trường. Cloud không đọc lại hoạt động thô, chỉ nhận bậc ngoại khóa 1–5. Backend không tính lại công thức. Đổi nút ưu tiên sau này chỉ gọi lại lượt chọn.

---

## Làm tiếp từ đây

Việc kế tiếp là **4.1**: tạo `backend/src/StudyAbroad.Application/Recommendations/RecommendationService.cs`.

Service nhận hồ sơ, hoạt động, danh sách trường. Việc của nó:

1. `HardFilter.Apply`, trần ngân sách × 1,1. Bỏ trường `Excluded`.
2. `IAdvisorClient.ReadEcAsync` → bậc 1–5.
3. `ScoreAsync` chỉ với trường `Keep` và `AidBlock`.
4. `PickAsync`. `blk`: `Keep` → `MAIN`, `AidBlock` → `AID`. Nút ưu tiên trống thì `[2, 2, 2]`.
5. Giữ nguyên `S`, `g2`, `F`, `H`, `Q`, `X`. Không tính lại.

DTO và lệnh gọi đã có:

- `backend/src/StudyAbroad.Application/Advisor/RecommendDtos.cs`
- `IAdvisorClient.ReadEcAsync` / `ScoreAsync` / `PickAsync`
- `backend/src/StudyAbroad.Infrastructure/Advisor/AdvisorClient.cs`

Chưa đăng ký service trong `DependencyInjection.cs`. Chưa có API, chưa ghi bảng `recommendations`.

Học bổng form → lượt chọn: `mandatory` → `required`, `preferred` → `preferred`, còn lại → `no`. SAT: `required` → `req`, `optional` → `opt`, `not_accepted` → `no`. `L` thiếu thì gửi 0.

Không commit `advisor/.env` (có khóa Gemini) và `backend/src/StudyAbroad.Api/appsettings.Development.json`. Gói Gemini miễn phí khoảng 20 request/ngày. Ngày 18/10 bộ đếm đã vượt 20. Hết lượt thì lượt chọn trả `FAILED`.

---

## Mốc 1. Hard filter (backend) — xong

Chỉ trả lời trường có được xét hay không. Không chấm điểm.

- [x] **1.1** `HardFilter`: ngành, bang, loại trường, tiếng Anh, ngân sách × 1,1, mức cần học bổng
- [x] **1.2** Mỗi trường: giữ, giữ ở khối học bổng, hoặc loại
- [x] **1.3** Thiếu số liệu thì giữ và gắn cờ
- [x] **1.4** Test An: Colgate sang khối học bổng, trường trong ngân sách thì giữ, sai ngành thì loại
- [x] **1.5** Test "Không cần học bổng": trường vượt ngân sách biến mất

File: `backend/src/StudyAbroad.Application/Recommendations/HardFilter.cs`.

---

## Mốc 2. Bậc ngoại khóa (Qwen) — xong

- [x] **2.1** `advisor/app/skills/skill_ec.md`, skill_version 2.0. Qwen chỉ đọc vai trò và phạm vi. Python tính `q`, `m`, bậc.
- [x] **2.2** `POST /api/v1/recommend/ec`
- [x] **2.3** Sai khuôn thì `SkillCall` gọi lại đúng 1 lần
- [x] **2.4** `check_read` chặn Qwen nâng `scope` cao hơn form
- [x] **2.5** Qwen lỗi thì `from_form`, `ai_used = false`
- [x] **2.6** Pytest `advisor/tests/test_ec_reader.py` (2 passed). Gọi Qwen thật: CLB trường khai quốc tế bị hạ `scope` 5 → 1, `ai_used = true`, bậc 2, khoảng 8,7 giây

`m = min(1, (tong_hd + bonus) / 4)` vì lấy tối đa 4 hoạt động. Giải thưởng là thưởng riêng, tối đa 3 giải.

---

## Mốc 3. Chấm và chọn (Gemini) — xong phần advisor

- [x] **3.1** `advisor/app/skills/skill_score.md`. Nhóm theo `S`: dưới 25 Khó vào, 25–75 Vừa sức, trên 75 An toàn hơn. `g2` là nhóm cuối sau khi xét du học sinh.
- [x] **3.2** `advisor/app/skills/skill_pick.md`. `[2,2,2]` Cân bằng, `[3,1,1]` chi phí thấp, `[1,3,1]` học bổng nhiều, `[1,1,3]` danh tiếng. Tối đa 3 / 5 / 4. Khối học bổng sắp theo `gap`.
- [x] **3.3** `GeminiClient` (`gemini-3.8-flash`, thinking `low`) và endpoint `POST /api/v1/recommend/score`, `POST /api/v1/recommend/pick`
- [x] **3.4** Chấm chia lô 8 trường (`scorer.py`)
- [x] **3.5** Sai khuôn gọi lại 1 lần. Lô vẫn sai thì mã trường vào `failed`, không bịa điểm
- [x] **3.6** Đã gọi thật: Colgate ra `g2 = RF`, `S = 12.9`. Lượt chọn: `C` Vừa sức `X = 0.414`, `B` An toàn hơn `X = 0.382`, Colgate ở khối học bổng. Hai hồ sơ này nằm trong ví dụ của skill, nên số có thể được chép từ ví dụ.

`F`, `H`, `Q` không quyết định nhóm. Chúng chỉ để lượt chọn tính `X`.

---

## Mốc 4. Nối và lưu — chưa làm

- [ ] **4.1** `RecommendationService`: lọc cứng → `/recommend/ec` → `/score` → `/pick`
- [ ] **4.2** Backend kiểm tra khuôn: trường thuộc tập đã lọc, nhóm hợp lệ, điểm trong khoảng
- [ ] **4.3** Giữ nguyên số cloud trả. Không tính lại bằng công thức ở backend
- [ ] **4.4** Lưu vào bảng `recommendations` kèm phiên bản skill và tên model. Entity và migration giữ nguyên
- [ ] **4.5** API tạo gợi ý và API lấy lần gợi ý gần nhất
- [ ] **4.6** Test với advisor giả: lọc xong mới gọi Qwen, có bậc rồi mới gọi cloud, kết quả lưu đúng

Xong khi: bấm gợi ý ra danh sách đã lưu, tải lại không tính lại.

---

## Mốc 5. Sau khi đã có danh sách — chưa làm

- [ ] **5.1** Đổi nút ưu tiên chỉ gọi lại lượt chọn, không lọc lại và không gọi lại Qwen
- [ ] **5.2** Mở chi tiết trường: Qwen viết lời tư vấn từ số đã lưu. Skill giải thích cũ là `advisor/app/skills/skill_explain.md`
- [ ] **5.3** Màn kết quả: bốn khối, bốn nút ưu tiên, mã lý do thành câu, cờ thiếu dữ liệu. Frontend hiện chưa có màn này
- [ ] **5.4** Hồ sơ xong ở form thì mở màn kết quả
