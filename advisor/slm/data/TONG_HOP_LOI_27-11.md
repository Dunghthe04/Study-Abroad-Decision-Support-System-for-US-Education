# Tổng hợp lỗi chat tư vấn (27/11/2026)

Model `usas-slm-v6`. Câu hỏi do thành viên nhóm và trợ lý đặt, **chưa có câu của học sinh thật**, nhãn đúng chưa có người duyệt lại.

## Đã test gì

| Đợt | Số lượt | Đi qua | Chưa kiểm |
|---|---|---|---|
| Danh sách trường | 28 | SLM | Kho, Claude, backend |
| Câu đơn nhiều chủ đề | 149 | SLM, tìm kho bằng embedding | Claude, backend |
| Luồng A–Z | 33 | API thật (không bấm xác nhận hồ sơ) | Giao diện |
| Luồng dài | 210 | SLM, kho, Claude, danh bạ trung tâm; có bấm xác nhận | Backend .NET (danh sách trường là dữ liệu mẫu), giao diện |
| Chặng 7, 8, 9, 13 | 54 | API thật, có bấm xác nhận | Giao diện |

Cột "Chắc chắn": **Đã kiểm** = đã đối chiếu code hoặc DB. **Quan sát** = thấy trong kết quả, chưa tìm nguyên nhân.

Các lượt dùng API thật chạy trên tài khoản đã có hồ sơ cũ.

## A. Lỗi code

| Mã | Mức | Lỗi | Bằng chứng | Chắc chắn |
|---|---|---|---|---|
| A1 | Nặng | Chưa bấm xác nhận thì một cuộc chat dùng hai hồ sơ: lời đánh giá dùng một phần số mới, còn so trường mục tiêu, gợi ý, giả định dùng hồ sơ đã lưu. Không báo gì | A–Z lượt 11, 15, 22, 25. `ChatService.cs` chỉ lưu hồ sơ khi xác nhận ở bảng rà soát | Đã kiểm |
| A2 | Nặng | Câu hỏi hồ sơ đang chờ không được nhả: dính đuôi "Quay lại câu mình đang hỏi" và chặn mọi tư vấn theo hồ sơ | Luồng 210: câu ngành bị treo từ câu 66 đến câu 190; câu giải thưởng treo từ câu 31 đến 38. Backend: "3.6 nhé" bị hiểu là trả lời câu hỏi tỉnh / thành | Quan sát |
| A3 | Nặng | Học sinh nói ngân sách 25.000 thì bị đáp "chưa thấy thông tin mới", sau đó hệ thống tính theo 35.000 | Backend, lượt 4 và what-if ngân sách. Nghi do hồ sơ lưu theo khoảng 20–35 nghìn | Quan sát |
| A4 | Nặng | Luật từ khóa `asks_for_school_list` đè nhãn đúng: "Tại sao đề xuất trường này / đầu tiên" in lại y danh sách | A–Z lượt 18, backend. `branches.py` (`SCHOOL_LIST_WORDS` có "de xuat truong") | Đã kiểm |
| A5 | Nặng | Sau khi có danh sách, "trường đó", "trường này", "trường đầu tiên" không xác định được | Backend: hỏi thành phố, ngành, đầu vào, SAT đều "chưa có thông tin"; so sánh với USC hỏi lại tên. "Trường cuối cùng" thì đúng | Quan sát |
| A6 | Vừa | Nêu giá trị mới ngay trong câu sửa mà vẫn bị hỏi lại | Luồng 210 câu 155, 197. SLM đã tách được ngành ở câu 197 khi chạy riêng | Quan sát |
| A7 | Vừa | Câu ngoài phạm vi nào cũng đáp "chưa hỗ trợ nước khác, thạc sĩ hay tiến sĩ" (kể cả nấu phở, viết Python, em họ) | Câu 116, 171–173, 178 | Đã kiểm (một lời đáp chung cho nhãn `exception`) |
| A8 | Vừa | Trung tâm: hỏi lại dịch vụ dù vừa nói; gặp người thật thì liệt kê theo dịch vụ cũ | A–Z lượt 31; luồng 210 câu 101, 107 | Quan sát |
| A9 | Vừa | Trường cũ bị gắn vào câu hỏi chung | Câu 112, 113 gắn NYU | Quan sát |
| A10 | Vừa | "Ngân sách mới" chạy giả định 20.000 (số của câu tìm trường trước) thay vì 30.000 vừa nói | Câu 193, 200, 201 | Quan sát |
| A11 | Nhẹ | Câu trả lời trung tâm mở đầu bằng lời miễn trừ | A–Z lượt 30 | Quan sát |

## B. Lỗi SLM (sửa bằng dữ liệu train, không vá code)

| Mã | Kiểu lỗi | Ví dụ |
|---|---|---|
| B1 | Câu hỏi bị gán giả định `what_if` | Ít nhất 8 câu trong luồng 210: "Nếu có sổ tiết kiệm thì đủ chưa?" |
| B2 | Câu hỏi chung bị coi là chuyện riêng (đánh giá, khai hồ sơ, giả định) | "học sinh trung bình khá đi Mỹ được ko" còn bịa GPA 3.0 |
| B3 | Câu hỏi chung bị gán số liệu một trường | "sinh hoạt phí 1 tháng bên đó cỡ nào" |
| B4 | Từ chối câu hợp lệ | "bị từ chối visa có phỏng vấn lại không" bị coi là gian lận |
| B5 | Sai khuôn, học sinh bị đáp "chưa hiểu" | Luồng 210 có 5 câu: "Hạn nộp hồ sơ Fall 2027 là khi nào?" |
| B6 | Câu hỏi bị coi là lệnh | "Nếu muốn khai báo lại từ đầu thì làm thế nào?" làm hệ thống khai lại |
| B7 | Câu ngắn nối tiếp không được hiểu | "Còn nếu IELTS lên 7.5?", "Cái nào rẻ hơn?" |
| B8 | Bịa hoặc đặt sai giá trị | "10 trường khó vào" thêm top 10; "chiều thứ Bảy" thành địa danh |
| B9 | Tách giá trị hồ sơ lấy cả câu | Ngành = "Mình thích máy tính, công nghệ với lập trình" |

Từng câu nằm ở sheet `model_errors` của `du_lieu_slm.xlsx`.

## C. Thiếu trong hợp đồng SLM (nhóm cần chốt)

1. Số lượng trường ("3 trường", "10 trường") và "xem thêm".
2. Học phí tách khỏi tổng chi phí, cả lọc lẫn xếp (đang dựa vào luật từ khóa).
3. Gợi ý theo hồ sơ kèm điều kiện: bang, cách xếp, nhóm Reach / Match / Safety, ngân sách "trong danh sách đó".
4. Quy tắc phân biệt tìm trường chung với gợi ý theo hồ sơ.
5. Câu hỏi chung kiểu "nên bắt đầu từ đâu" khi đã có hồ sơ.
6. Xin giải thích lại; hỏi nguồn và ngày cập nhật số liệu.
7. Tư vấn hộ người thân; cách ghi "bỏ SAT" trong giả định.
8. Trung tâm cam kết đậu visa; cách tính điểm phù hợp.

Điều hệ thống không có dữ liệu (nhiều sinh viên Việt Nam, gần biển, cơ hội thực tập, gần công ty công nghệ) cần chốt cách trả lời "chưa có dữ liệu", thay vì trả một danh sách không liên quan.

## D. Kho kiến thức và dữ liệu

- Nhiều câu cơ bản không trả lời được, ví dụ "OPT với STEM OPT khác nhau thế nào?", "Personal Statement có bắt buộc không?". Chi tiết ở `LOI_KHO_KIEN_THUC.md`.
- Có mục đúng nhưng dưới ngưỡng 0,72, và có mục lệch ý nhưng điểm cao hơn, nên chỉ hạ ngưỡng là chưa đủ.
- 3 trường có số được nhận lớn hơn số nộp (Williams 115,3%) — đã kiểm trong DB.
- Chat liệt kê "Demo Center Hà Nội / TP.HCM" là trung tâm đã xác minh: cần gỡ dữ liệu thử trước khi demo.

## Nguyên nhân gốc (bổ sung sau khi đọc code; phần này thay cho cột "Chắc chắn" ở mục A)

Chưa lỗi nào có test tái hiện. Trước khi sửa mỗi lỗi phải viết test fail trước.

| Gốc | Lỗi | Nguyên nhân | Cách kiểm |
|---|---|---|---|
| G1. Hai nguồn hồ sơ | A1 | .NET chỉ lưu hồ sơ chính thức khi xác nhận ở bảng rà soát (`ChatService.cs`), còn phép tính của .NET đọc hồ sơ chính thức | Đọc code |
| G2. Ngân sách mất số chính xác | A3, và câu "đổi thành 30.000" ở A6 | Chat chỉ lưu mức (`validators.budget_tier`, `skill_ho_so.save_budget`). `ProfileFlowService.cs:1079` không có số chính xác thì lấy cận trên của mức. 25.000 và 30.000 cùng mức 20–35 nghìn nên bị coi là không đổi, và được tính là 35.000. `ChatProfileMapper` đã có sẵn `budget_usd`, nhưng khóa này đang dùng cho ngân sách phụ huynh nhập (có khóa sửa) | Đọc code |
| G3. Nói lại giá trị cũ bị coi là chưa trả lời | A6 | `profile_handler._edit`: mục nào học sinh nêu mà giá trị không đổi thì đưa vào hàng hỏi lại. Câu 155 không phải lỗi này: GPA đã được lưu, chat chỉ nhắc câu đang chờ | Đọc code |
| G4. Ngành không khớp bị bỏ im lặng | A2 | `skill_bac_hoc.save_said` bỏ kết quả `MajorMiss`, nên học sinh không được báo ngành không có trong danh mục. Danh mục không khớp "Artificial Intelligence", "Information Systems", cũng không gợi ý ngành gần giống. Ngành là câu bắt buộc, và `chat_flow` nhắc lại câu đang chờ ở mọi lượt, không giới hạn. Trong đợt 210 câu, hệ thống hỏi ngành là có lý do (chưa lưu được ngành) | Đọc code, đã tra danh mục |
| G5. Danh sách gợi ý không vào ngữ cảnh | A5 | `conversation_context.update_context` không ghi gì sau `recommend` và `explain`, còn `school_search` thì xóa trường. SLM không biết "trường đó" là trường nào | Đọc code |
| G6. Code đoán bằng chữ, đè hoặc bỏ qua SLM | A4, A9, "3.6 nhé" | (a) `asks_for_school_list` đè nhãn `explain_fit`. (b) `_INFO_WORDS` thấy chữ "gpa" thì gắn trường cũ khi SLM không trả `follow_up` (đã chạy lại: SLM trả `knowledge` trơn). (c) `skill_tinh.province_text` coi mọi câu tối đa 6 chữ, không có dấu hỏi, là tên tỉnh và không cho SLM đọc | Đọc code, đã tái hiện (b) |
| G7. Một lời đáp cho nhãn quá rộng | A7 | `exception` gồm nước khác, sau đại học và chuyện không liên quan, nhưng chỉ có một câu đáp | Đọc code |

Không phải lỗi code:

- **A10 là lỗi SLM:** câu không có số nào, SLM chép 20.000 từ bộ lọc trong ngữ cảnh (đã tái hiện). `keep_verbatim` không kiểm giá trị trong `changes`.
- **A8:** lượt hỏi trung tâm ở Bắc Ninh, SLM không trả `follow_up` nên dịch vụ cũ không được dùng. Việc chuyển gặp người thật dùng dịch vụ cũ là do code cố ý đặt như vậy, nhóm cần quyết có giữ không.
- **A11:** lời miễn trừ được chèn lên đầu khi một luật từ khóa khớp câu hỏi (`_NOT_PROVIDED_RE`).

## E. Thứ tự đề xuất

1. **Code nặng:** A1, A2, A3, A4, A5. Mỗi lỗi viết test tái hiện, tìm nguyên nhân rồi mới sửa (A2, A3, A5 chưa rõ nguyên nhân).
2. **Dữ liệu trường:** 3 dòng sai, dữ liệu trung tâm thử.
3. **Code còn lại:** A6–A11.
4. **Chốt hợp đồng** (mục C), rồi sửa code theo nhãn mới.
5. **Bổ sung kho.**
6. **Người không viết dữ liệu train** soạn bộ chấm điểm, cất riêng.
7. **Viết dữ liệu train** theo B1–B9 và mục C (câu mới, không chép câu đã test), train một lần, so với v6.
8. Chạy lại các luồng ở trên để đo.
