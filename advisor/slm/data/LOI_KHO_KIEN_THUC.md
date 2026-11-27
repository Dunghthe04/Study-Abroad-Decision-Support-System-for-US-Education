# Kho kiến thức: lỗi và phần cần bổ sung

Ghi ngày 27/11/2026. Lỗi của SLM ghi ở sheet `model_errors` trong `du_lieu_slm.xlsx`.

**Không chép nguyên các câu ở đây hoặc trong bộ kịch bản vào bộ đo hay dữ liệu train.** Khi bổ sung cách hỏi, viết câu mới cùng kiểu.

Điểm khớp bên dưới đo bằng cách tra thẳng câu hỏi (chưa qua bước khôi phục dấu), ngưỡng đang dùng là 0.72. Kho thay đổi trong ngày
nên số dòng và điểm có thể khác khi tra lại. Cột "Đã xử lý" để trống tới khi có người làm.

## 1. Có bài gần đúng nhưng không trả lời được

| Câu hỏi | Bài gần đúng (điểm) | Nhận định | Đã xử lý |
|---|---|---|---|
| tiền sinh hoạt một tháng bên đó khoảng bao nhiêu | #904 (0.670) | Dưới ngưỡng do cách hỏi đời thường | |
| một năm học bên đó tốn cỡ bao nhiêu tiền | #286 (0.709) | Dưới ngưỡng | |
| financial aid với merit scholarship khác gì nhau | #395 (0.760), #106 (0.685), #240 (0.684) | Hai bài đúng ý nằm dưới ngưỡng; bài trên ngưỡng chỉ nói merit | |
| bài luận cá nhân thường dài bao nhiêu chữ | #613 (0.710) | Bài đúng ý dưới ngưỡng | |
| đi phỏng vấn visa thì lãnh sự hay hỏi mấy câu gì | #893 (0.799), #359, #889 | Đủ điểm, Claude không chọn bài nào: cần xem bài có liệt kê câu hỏi thường gặp không | |
| pathway là gì (gõ không dấu) | cao nhất khoảng 0.54; có #113 nói pathway là gì (0.46) | Hạ ngưỡng không cứu được, cần thêm cách hỏi | |
| qua bển làm thêm mấy tiếng một tuần | khoảng 0.60 | Từ địa phương | |
| điểm IELTS tối thiểu để xin học bổng là bao nhiêu | #2 (0.82) | Claude từ chối 4/4 lần; cần đọc bài để biết đúng hay sai | |

Đo trên 52 câu tự nhiên và 10 câu ngoài phạm vi (skill_tra_loi 3.1): ngưỡng 0.72 trả lời 35/52, ngưỡng 0.65 trả lời 38/52,
cả hai ngưỡng đều không trả lời câu ngoài phạm vi nào. Chưa đổi ngưỡng.

## 2. Chưa có bài trả lời thẳng

| Câu hỏi | Ghi chú | Đã xử lý |
|---|---|---|
| du học Mỹ có khó không | Ba kết quả đầu không bài nào đúng ý | |
| học sinh Việt Nam thường đi vào thời điểm nào | Gần nhất #1100, #995, #12 nhưng không trả lời thẳng | |
| xin học bổng du học Mỹ có khó không | Không có bài trực tiếp | |

## 3. Trả lời lệch ý (có trả lời nhưng sai trọng tâm)

| Câu hỏi | Hệ thống trả | Vấn đề | Đã xử lý |
|---|---|---|---|
| thư giới thiệu nên xin ai viết | Mở đầu "Không nên. Trường Mỹ cần thư của người đánh giá khách quan..." | Có vẻ là bài viết cho câu hỏi khác (nhờ người nhà viết) | |
| qua đó học có được đi làm thêm không | Mở đầu "Chỉ nên coi là khoản phụ..." | Trả lời việc tính vào ngân sách trước, không đáp được hay không | |
| chứng minh tài chính cần giấy tờ gì | "Hồ sơ DN: đăng ký, thuế GTGT/TNDN, sao kê..." | Chỉ nói trường hợp phụ huynh có doanh nghiệp | |

Hướng sửa chung: câu trả lời trong kho phải đọc hiểu được khi đứng một mình, mở đầu bằng ý đáp thẳng câu hỏi mẫu.

## 4. Việc khác của kho

| Việc | Đã xử lý |
|---|---|
| Một số tên nguồn lộ mã nội bộ cho học sinh thấy ("Mã: RAW…", "CẦN HỘI ĐỒNG DUYỆT") | |
| 4 mục soạn sẵn chờ duyệt và nạp (`D:\DoAn\kho_kien_thuc_bo_sung_27-11.xlsx`, mã 1196–1199, nằm ngoài git) | |
