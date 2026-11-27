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

## Tìm lỗi đợt 1 (27/11/2026)

Ngưỡng 0,72. Chỉ đo bước tìm bằng embedding, chưa chạy bước Claude chọn dòng. Đúng/sai ý đánh giá theo câu mẫu, chưa ai duyệt lại.

### Tìm đúng dòng nhưng dưới ngưỡng

| Câu hỏi | Dòng đầu tìm được | Điểm |
|---|---|---|
| rớt visa 2 lần rồi còn cơ hội ko hả shop | #900 Em từng bị từ chối visa ở bậc học trước, lần này nộp lại cần lưu ý gì? | 0.719 |
| sinh hoạt phí 1 tháng bên đó cỡ nào | #904 Chi phí sinh hoạt hằng tháng của du học sinh ở Mỹ gồm những khoản nào và ước lượng ra sao? | 0.672 |
| merit scholarship khác need based chỗ nào | #240 Scholarship / học bổng là gì? Merit và need-based khác nhau thế nào? | 0.712 |
| opt stem được mấy năm | #868 Có tài liệu ghi STEM OPT là 36 tháng, có tài liệu ghi 2 năm. Con số nào đúng? | 0.68 |
| essay viết về gì cho dễ đậu | #288 Bài luận Common App nên viết về chủ đề gì cho đỡ sến? | 0.657 |
| ed ea rd khác nhau sao | #250 Regular Decision (RD), Early Action (EA), Early Decision (ED) là gì? | 0.685 |
| bên đó có cộng đồng người việt đông k, ở đâu | #1054 Ở Mỹ có cộng đồng sinh viên Việt Nam để kết nối khi mới sang không? | 0.607 |
| có nên thuê trung tâm không hay tự làm | #785 Tự làm hồ sơ hay nhờ trung tâm có ảnh hưởng đến kết quả xét tuyển và xin visa không? | 0.701 |

### Dưới ngưỡng, dòng đầu chỉ đúng một phần

| Câu hỏi | Dòng đầu tìm được | Điểm |
|---|---|---|
| nhà mình thu nhập thấp thì có hỗ trợ gì k | #393 Hỗ trợ tài chính là gì? | 0.71 |
| cao đẳng cộng đồng rồi chuyển tiếp có ổn ko | #405 Cao đẳng cộng đồng là gì? | 0.707 |
| học 2+2 bằng cấp có giống học 4 năm không | #408 Bằng cử nhân ở Mỹ học mấy năm? | 0.701 |
| ở homestay hay ktx tốt hơn ạ | #139 Nên ở KTX (dorm) hay thuê nhà ngoài campus năm đầu? | 0.706 |

### Dưới ngưỡng, dòng đầu không trả lời câu hỏi (nghi thiếu mục, cần soát kho)

| Câu hỏi | Dòng đầu tìm được | Điểm |
|---|---|---|
| đang học bên đó mà về vn nghỉ hè thì có phải xin lại visa ko | #879 Em có nên mua vé máy bay hoặc lên kế hoạch du lịch trước khi có visa không? | 0.712 |
| làm chui bên mỹ có sao không | #595 Có nên trả tiền cho các chương trình hè đắt tiền ở Mỹ để làm đẹp hồ sơ không? | 0.605 |
| bang nào ở mỹ an toàn cho du học sinh | #664 Làm sao để an toàn khi sống ở Mỹ? | 0.685 |
| bao giờ em nên thi lại sat | #500 Em thi SAT lần 2 được tăng 20 điểm. Có đáng để thi thêm lần 3 không? | 0.707 |
| phí dịch vụ bên trung tâm khoảng bao nhiêu | #41 Chi phí dịch vụ làm hồ sơ tại PEC là bao nhiêu và có chính sách hoàn tiền không? | 0.625 |

### Trên ngưỡng nhưng dòng đầu lệch ý

| Câu hỏi | Dòng đầu tìm được | Điểm |
|---|---|---|
| xin visa du học mỹ khó không | #434 Giấy tờ cần có để xin visa du học Mỹ là gì? | 0.781 |
| không giỏi tiếng anh có đi du học được không | #567 Phụ huynh không biết tiếng Anh thì có thể hỗ trợ con làm hồ sơ du học không? | 0.78 |
| học bổng mỹ có dễ xin không ạ | #1069 Học bổng của trường Mỹ có bị đánh thuế không? | 0.737 |
| gap year có mất học bổng ko ạ | #522 Em có học bổng bên ngoài (không phải của trường). Có bị trường giảm học bổng của trường không? | 0.722 |
| nộp muộn deadline thì còn xét không | #892 Em đến muộn giờ hẹn phỏng vấn visa thì có được vào không? | 0.75 |

Thêm: "Đi du học mỹ có khó k" chưa có mục trả lời thẳng; dòng đầu là #773 về học lực trung bình (0.763).

## Luồng 210 câu và luồng backend (27/11/2026)

### Câu đơn nhận 'Hiện hệ thống chưa có thông tin' (cần soát kho; có thể do thiếu mục hoặc không tìm ra)

- Câu 2: Mình chưa biết gì về quy trình du học bên đó luôn.
- Câu 12: Computer Science có phải ngành chuyên về lập trình không?
- Câu 13: Ngành AI ở Mỹ có dễ xin việc sau khi tốt nghiệp không?
- Câu 18: Có ngành nào kết hợp giữa kinh doanh và công nghệ không?
- Câu 33: Nếu chỉ tham gia vài hoạt động ở trường thì có được tính là ngoại khóa không?
- Câu 49: Có bắt buộc phải chứng minh thu nhập hàng tháng không?
- Câu 93: Personal Statement có phải bài luận bắt buộc không?
- Câu 99: Sau khi tốt nghiệp có được ở lại Mỹ làm việc không?
- Câu 100: OPT với STEM OPT khác nhau như thế nào?
- Câu 123: Học ngành IT ở Mỹ có cần giỏi Toán không?

### Lỗi dữ liệu trường (đã kiểm trong DB)

Bảng `university_offerings` có 3 dòng số được nhận LỚN HƠN số nộp, khiến chat hiện tỷ lệ nhận trên 100% (Williams 115,3%) và xếp các trường này lên đầu:

| Trường | admitted_total | applicants_total |
|---|---|---|
| Illinois Wesleyan University | 12280 | 10290 |
| Amherst College | 17844 | 15800 |
| Williams College | 16856 | 14616 |

Cần đối chiếu nguồn rồi mới sửa.
