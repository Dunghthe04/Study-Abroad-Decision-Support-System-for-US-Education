# Khuôn chung của file skill

Mọi file trong `advisor/app/skills/*.md` (trừ prompt của SLM) có cùng một khuôn. Test `advisor/tests/test_skill_format.py` kiểm khuôn này, nên thêm hay sửa skill mà lệch khuôn thì test đỏ.

## Dòng đầu

```
# TÊN SKILL (mã) - skill_version X.Y
```

Quy tắc đổi số phiên bản: tăng Y khi đổi cách viết hoặc luật mà không đổi dữ liệu vào ra. Tăng X khi đổi JSON vào hoặc ra, và khi đó code với skill phải ra cùng lúc. `load_skill()` đọc phiên bản từ dòng này.

## Các mục, đúng thứ tự

| Mục | Viết gì |
|---|---|
| `NGUYÊN TẮC DỮ LIỆU (BẮT BUỘC)` | Mở đầu, không đánh số. Chỉ dùng dữ kiện có trong dữ liệu, không bịa, không hứa kết quả (skill viết lời). Skill đọc hiểu ghi "chỉ đọc hiểu, code lo phần còn lại" |
| `0. VAI TRÒ` | Bạn là ai, nói với ai, được làm gì và không được làm gì |
| `1. NHIỆM VỤ` | Việc cụ thể của lượt này |
| `2. DỮ LIỆU BẠN NHẬN` | JSON đầu vào và từng khóa nghĩa là gì |
| `3. CÁCH HIỂU DỮ LIỆU` | Nghĩa của các giá trị, các mức, các cờ do code đã tính |
| `4. NỘI DUNG CẦN NÓI` hoặc `4. CÁCH ĐỌC` | Skill viết lời: nói gì, theo thứ tự nào. Skill đọc hiểu: đọc từng trường thông tin ra giá trị nào |
| `5. GIỚI HẠN VÀ ĐIỀU CẤM` | Những điều không được làm |
| `6. ĐỊNH DẠNG TRẢ LỜI` | JSON đầu ra, giọng văn, độ dài |
| `7. VÍ DỤ` | Cặp Đầu vào, Đầu ra. Mỗi ví dụ phải qua đúng bộ kiểm của code |

Mục con dùng `###` (ví dụ từng trường thông tin trong mục 4 của skill đọc hiểu). Dùng `##` cho mục chính và chỉ cho mục chính.

## Ba loại skill

| Loại | Skill | Mục 4 |
|---|---|---|
| Viết lời | `skill_explain`, `skill_giai_thich_trung_tam`, `skill_goal_seek`, `skill_ho_so_khuon`, `skill_kich_ban`, `skill_neu_doi`, `skill_so_sanh`, `skill_tra_loi`, `viet_tra_loi_ho_so` | NỘI DUNG CẦN NÓI |
| Đọc hiểu | `skill_bac_hoc`, `skill_chon_truong`, `skill_ec`, `skill_ho_so` | CÁCH ĐỌC |
| Prompt SLM | `slm_intents` | Không đổi khuôn: model đã học đúng chữ này, đổi là phải huấn luyện lại |

Hai ngoại lệ có chủ ý:
- `skill_ho_so_khuon` có ba chỗ `{nhiem_vu}` (mục 1), `{du_lieu}` (mục 2), `{noi_dung}` (mục 4) do code điền (`skill_template.py`).
- `viet_tra_loi_ho_so` là luật chung đứng trước skill riêng của từng học sinh, nên có thêm mục cuối `SKILL CỦA HỌC SINH NÀY` để code nối skill riêng vào.

## Thêm skill mới

1. Chép khuôn trên, điền từng mục. Mục nào chưa có gì để nói thì ghi ngắn lý do, không bỏ mục.
2. Khai báo tên file vào `WRITERS` hoặc `READERS` trong `tests/test_skill_format.py`.
3. Thêm test kiểm mọi ví dụ trong skill qua bộ kiểm của code (xem `tests/test_recommend_writer.py`).
