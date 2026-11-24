# Chốt đặc tả bản 4: chat AI, gợi ý trường, kho tri thức

**Nguyên tắc: đặc tả là chuẩn, code làm theo.** Số UC theo bản 4 (24/11). Mỗi mục ghi: chốt thế nào, căn cứ (đã kiểm trong code hoặc dữ liệu), việc phải làm.

---

## 1. Sửa tài liệu

**T1. UC-156 và S11: bộ thông tin trường hỏi được.**
- 10 mục mới: học phí in-state, học phí ngoài bang, chi phí sinh hoạt, bảo hiểm y tế, phí nộp hồ sơ, dải điểm ACT, dải điểm SAT, tỷ lệ nộp SAT, tỷ lệ nhận của sinh viên quốc tế, danh sách học bổng có tên của trường (tối đa 3, kèm liên kết xem tất cả).
- "Hỗ trợ cho sinh viên quốc tế" không còn là mục riêng: gộp vào hỗ trợ tài chính (`aid`). Căn cứ: backend trả mục `aid` từ các cột hỗ trợ cho sinh viên quốc tế.
- Luồng 3a chỉ còn: thông tin hệ thống không lưu (thư giới thiệu, phỏng vấn tuyển sinh, việc làm sau tốt nghiệp…) và thông tin CSDL có mà chat chưa hỗ trợ (tiêu chí xét tuyển, vòng nộp hồ sơ, số sinh viên quốc tế, chỉ số giá theo bang). Bỏ "dải điểm SAT" và "học bổng có tên" khỏi 3a.

**T2. Định nghĩa "trừ token" (Mục 1, bảng định tuyến Mục 6, UC-36, 39, 127, 156, 157).**
- Trừ token = trừ số token của các lần gọi **Claude** trong lượt chat. SLM, tạo vector (embedding) và phần do code dựng **không** trừ token. Căn cứ: code chỉ tính `ai_calls.kind = "claude"` vào hạn mức (`BillingConstants.MeteredAiKind`).
- Sửa cột "Trừ token" của bảng định tuyến:

| Nhãn | Trừ token |
|---|---|
| collect_profile | Có (Claude đọc câu trả lời) |
| profile_review: assessment, roadmap, explain_school, explain_fit, school_list, compare | Có |
| profile_review không có task (hỏi lại bằng nút) | Không |
| scenario | Có (Claude diễn giải kết quả) |
| knowledge: general | Có khi có mục tri thức đạt ngưỡng 0,72 (Claude chọn mục); không có mục nào đạt ngưỡng thì không |
| knowledge: school_info, search | Không (code tra CSDL); trừ khi phải dùng Kho tri thức |
| knowledge: compare_facts | Có (Claude viết nhận xét) |
| center_referral | Có (Claude xác định nhu cầu) |
| out_of_scope | Không |

- Tài khoản Miễn phí dùng được tri thức, tra cứu trường, câu ngoài phạm vi; hạn mức 50.000 token chỉ bị trừ khi lượt đó có gọi Claude.

**T3. UC-155 và UC-77: danh sách yếu tố mô phỏng.** (cần nhóm xác nhận)
- Giữ và ghi rõ cách tính: GPA, SAT, ACT, IELTS/TOEFL/Duolingo, ngân sách, mức cần học bổng, nút ưu tiên; **tỉ giá** (đổi ngân sách VND sang USD theo tỉ giá giả định, chỉ nhận mức tăng); **bang** và **ngành** (thay điều kiện lọc ở bước 3 của UC-22 trên bản sao hồ sơ).
- **Bỏ "vòng nộp (ED, EA, RD)" và "bậc ngoại khóa" khỏi danh sách yếu tố**, vì công thức UC-22 không dùng hai yếu tố này (năm thành phần điểm: học thuật, ngân sách, chất lượng trường, học bổng, tiếng Anh; ngoại khóa chỉ hiển thị tham khảo theo B9). Chat vẫn nhận diện được câu hỏi loại này và trả lời rằng kết quả gợi ý không đổi theo vòng nộp hay ngoại khóa, kèm kiến thức về ED, EA, RD từ Kho tri thức. Goal_seeking chỉ tìm mức cần đạt cho GPA, SAT/ACT, tiếng Anh.

**T4. UC-08: hai thông tin tùy chọn chỉ hỏi trong chat.** Bỏ "bang có người thân ở Mỹ" và "từng bị từ chối visa" khỏi bước 2 của UC-08 (form Bước 1); giữ ở UC-35 với nút Bỏ qua. Giữ câu về Chính sách quyền riêng tư (UC-97) và thông tin visa không chia sẻ cho trung tâm. Căn cứ: form hiện không có hai thông tin này, chỉ chat có.

**T5. UC-24, quy tắc phân loại số 2.** "Còn X thì sao" là `explain_school` chỉ **khi ngay sau một lượt explain_school**; nếu không thì theo loại của lượt trước (kiến thức, so sánh, mô phỏng…), như UC-19 mục 1b.

**T6. UC-22 bước 4a: giới hạn mức hỗ trợ.** Mức hỗ trợ trừ vào chi phí không vượt quá học phí. Nếu mức hỗ trợ trung bình của trường lớn hơn học phí thì coi là dữ liệu bất thường: không dùng để trừ (trường vẫn bị loại với lý do ngân sách) và đưa vào danh sách cho Moderator kiểm tra (UC-62). Căn cứ: báo cáo soát dữ liệu ghi khoảng 44 trường có mức hỗ trợ trung bình lớn hơn học phí.

**T7. UC-117: file nhập tri thức.** Giữ 10 cột, kho tri thức chỉ trả lời bằng chữ, không có ảnh. Thêm quy tắc: cột thừa ngoài 10 cột (ví dụ cột `image_url` của file cũ) được bỏ qua, không phải lỗi của tệp.

**T8. UC-95: nhập mẫu SLM.** Ghi rõ: file dữ liệu huấn luyện của nhóm AI (`usas_train_model_v13.xlsx`) không nhập qua UC-95 (nhãn rà soát của file dài quá 16 ký tự); UC-95 dùng cho file xuất từ UC-96.

Giữ nguyên, không sửa: UC-19, UC-35, UC-37, UC-153, UC-154, UC-157, bảng định tuyến (trừ cột Trừ token ở T2), hạn mức 50.000 token.

---

## 2. Việc cho code (để khớp đặc tả)

| # | Việc | Phạm vi | Căn cứ hiện tại |
|---|---|---|---|
| C1 | Bỏ câu hỏi bậc học (B1.2), mặc định undergraduate; bỏ `target_level` khỏi khóa hồ sơ của SLM (UC-08, UC-35) | advisor | chat vẫn hỏi B1.2 với nút Thạc sĩ, Tiến sĩ |
| C2 | Thêm nút Bỏ qua cho câu hỏi visa (UC-35) | advisor | câu visa chỉ có "Chưa từng" và "Đã từng bị từ chối" |
| C3 | Tách nhánh: `explain_fit` → UC-153 (AI diễn giải số của UC-24); `explain_school` → UC-19 (mẫu skill + AI) | advisor, backend | hai nhãn đang đi chung một đường |
| C4 | `explain_fit`: trường chưa có trong kết quả gợi ý mới nhất thì nhắc tạo gợi ý trước, không tự chạy gợi ý (UC-153 E4) | advisor | code đang tự chạy gợi ý hoặc phân tích khoảng cách |
| C5 | `profile_review: compare` (UC-154) tính điểm phù hợp và nhóm của từng trường | advisor, backend | code đang so số liệu như `compare_facts` |
| C6 | Mô phỏng trong chat và trang UC-77: thêm tỉ giá, bang, ngành theo T3 | advisor, backend, frontend | chat chỉ thử được GPA, SAT, ACT, tiếng Anh, ngân sách, học bổng, ưu tiên; bang, ngành đang bị coi là "sửa hồ sơ thật" |
| C7 | Nối 4 mục nhà ở, tỷ lệ sinh viên trên giảng viên, hoạt động, khuôn viên (UC-156) | backend | DB đã có cột; chat vẫn trả "chưa có thông tin" |
| C8 | Gỡ ảnh minh họa: bỏ `image_url` khỏi file mẫu, bộ đọc Excel (chấp nhận cột thừa theo T7), màn quản trị, hiển thị ảnh trong chat và trường `media`; giữ cột trong DB (migration chỉ thêm) | advisor, backend, frontend | code vẫn hỗ trợ ảnh |
| C9 | Nguồn của câu trả lời hiển thị đủ mọi liên kết (UC-37) | frontend | đang chỉ lấy liên kết đầu |
| C10 | Giới hạn mức hỗ trợ theo T6 | backend | chưa có giới hạn |
| C11 | Hạn mức Miễn phí 50.000 token trên database đang chạy | vận hành | seed đã đổi; database đang chạy cần Admin sửa ở UC-74 (Quản lý gói và giá) hoặc chạy SQL |

---

## 3. Đã khớp (đã có trong code)
- Hợp đồng SLM v14: 10 mã mục mới, `explain_fit`, prompt 3.2 (`docs/HOP_DONG_SLM_v14.md`).
- Chat trả lời 10 mục thông tin trường mới từ số thật hoặc nói "chưa có số liệu"; danh sách học bổng có tên với liên kết `/scholarships?search=<tên trường>`.
- Cách tính token chỉ tính Claude (khớp T2).
- Dữ liệu huấn luyện đã chốt: 20.333 dòng, chia 70/15/15, kiểm tra tự động không còn lỗi định dạng.

## 4. Cần nhóm xác nhận
- T3: bỏ vòng nộp và bậc ngoại khóa khỏi mô phỏng (hoặc nhóm đưa ra công thức cho hai yếu tố này).
- T4: hai thông tin tùy chọn chỉ hỏi trong chat.
- T6: cách xử lý trường có mức hỗ trợ lớn hơn học phí.
