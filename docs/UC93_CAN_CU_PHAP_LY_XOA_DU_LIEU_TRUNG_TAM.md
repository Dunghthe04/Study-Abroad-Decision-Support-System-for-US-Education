# UC-93: căn cứ pháp lý của việc xóa dữ liệu trung tâm

Cập nhật 21/11/2026. Căn cứ: Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 (hiệu lực 01/01/2026) và Nghị định 356/2025/NĐ-CP ngày 31/12/2025, đọc từ bản PDF chính thức. Đây là bản đối chiếu văn bản với hành vi của hệ thống, không phải tư vấn pháp lý.

## 1. Hệ thống làm gì khi Admin xóa hẳn một trung tâm

| Hành vi | Căn cứ | Ghi chú |
|---|---|---|
| Trung tâm gửi yêu cầu xóa, hệ thống gửi email xác nhận kèm hạn xử lý | NĐ 356 Điều 5 khoản 4: phản hồi trong 02 ngày làm việc | Email "đã nhận" gửi ngay khi nhận yêu cầu |
| Xóa trong 20 ngày kể từ khi nhận; gia hạn tối đa một lần, không quá 20 ngày, có nêu lý do | NĐ 356 Điều 5 khoản 4 | Bên kiểm soát phải chứng minh việc gia hạn là cần thiết, hợp lý (hệ thống lưu lý do gia hạn) |
| Xóa hồ sơ xác minh, tài liệu, lịch sử sửa hồ sơ, ảnh logo, ảnh bìa, thư viện ảnh (kể cả tệp trên Cloudinary) | Luật Điều 14 khoản 1 điểm a (chủ thể yêu cầu xóa) | URL ảnh được đọc trước khi cột bị xóa trắng để còn gỡ được tệp trên CDN. Chỉ xóa ảnh nằm trong thư mục riêng của tài khoản chủ (`centers/{id}`), vì URL trong hồ sơ có thể do trung tâm tự nhập và trỏ tới ảnh của người khác |
| Xóa nội dung và ảnh đính kèm tin nhắn phía trung tâm | Luật Điều 14 khoản 1 điểm a; Điều 3 khoản 3 (chỉ lưu trong thời gian phù hợp mục đích) | Tin được thay bằng câu "đã được xóa theo yêu cầu" |
| Ẩn danh hóa tài khoản chủ và dòng trung tâm, giữ lại bản ghi để khóa ngoại còn tham chiếu | Luật Điều 2 khoản 1 và khoản 11: dữ liệu đã khử nhận dạng không còn là dữ liệu cá nhân; Điều 14 khoản 6 | Không được tái nhận dạng sau khi khử nhận dạng |
| Giữ tin nhắn đang bị báo cáo mà Admin chưa xử lý | Luật Điều 14 khoản 2 (không thực hiện yêu cầu xóa trong các trường hợp Điều 19, hoặc khi việc xóa vi phạm Điều 4 khoản 3); Điều 4 khoản 3 điểm c; Điều 19 khoản 1 điểm a, b | Bên kiểm soát có trách nhiệm chứng minh (Điều 19 khoản 1 điểm a). Sau khi Admin xử lý xong, job nền xóa nốt |
| Không xóa khi trung tâm hoặc tài khoản chủ đang bị giữ pháp lý | Luật Điều 14 khoản 2, Điều 19 khoản 1 điểm a, b | Admin phải ghi lý do; mọi lần bật, gỡ đều có nhật ký |
| Báo cho trung tâm khi chưa xóa được toàn bộ | Luật Điều 14 khoản 5 | Email "held" khi giữ pháp lý; email "completed_partial" khi còn tin giữ vì báo cáo. Nội dung trung lập, không nêu lý do chi tiết |

## 2. Dữ liệu giữ lại sau khi xóa hẳn

| Dữ liệu | Lý do giữ | Cách xử lý |
|---|---|---|
| Tin nhắn của học sinh và phụ huynh trong lead | Là dữ liệu của họ, họ chưa yêu cầu xóa; trung tâm không phải chủ thể của dữ liệu này | Giữ nguyên |
| Tin phía trung tâm đang bị báo cáo chưa xử lý | Xem bảng trên | Xóa nốt khi báo cáo được xử lý |
| Nhật ký (`audit_logs`) | Truy vết thao tác quản trị | Chỉ lưu mã, không lưu tên hay email đã xóa |
| Bản ghi yêu cầu xóa | Chứng minh đã xử lý đúng hạn | Không còn thông tin nhận diện |
| Đánh giá và lead cũ của người dùng | Là dữ liệu của người dùng | Trung tâm hiển thị là "Trung tâm đã xóa" |

## 3. Điều chưa làm hoặc chưa kiểm tra

1. **Bản sao lưu cơ sở dữ liệu và lưu trữ phía Cloudinary** chưa được kiểm tra. Luật Điều 14 khoản 3 yêu cầu xóa bằng biện pháp an toàn, ngăn khôi phục trái phép; khoản 4 cấm cố ý khôi phục trái phép dữ liệu đã xóa.
2. **Cloudinary là dịch vụ đám mây ở nước ngoài.** Lưu ảnh ở đó thuộc trường hợp chuyển dữ liệu xuyên biên giới (Luật Điều 20 khoản 1; NĐ 356 Điều 17 khoản 1 điểm a), thường phải lập hồ sơ đánh giá tác động chuyển dữ liệu (Luật Điều 20 khoản 2, gửi trong 60 ngày). NĐ 356 Điều 12 khoản 2 còn yêu cầu hợp đồng với bên cung cấp đám mây nêu rõ việc tuân thủ thời hạn xử lý và yêu cầu xóa. Chưa kiểm tra đồ án có thuộc trường hợp miễn hay không.
3. **Hồ sơ đánh giá tác động xử lý dữ liệu cá nhân** (Luật Điều 21, gửi trong 60 ngày kể từ khi bắt đầu xử lý). Doanh nghiệp nhỏ và khởi nghiệp được chọn không thực hiện trong 5 năm (Điều 38 khoản 2), trừ trường hợp xử lý dữ liệu nhạy cảm hoặc số lượng lớn chủ thể. Hệ thống có dữ liệu của học sinh dưới 16 tuổi, nên cần xác nhận với giảng viên hoặc BA.
4. **Cơ chế giám sát khi xử lý không cần sự đồng ý** (Luật Điều 19 khoản 2): cần có quy trình, đánh giá rủi ro, kiểm tra định kỳ và cơ chế tiếp nhận phản ánh. Hệ thống đáp ứng một phần qua quy trình Admin xử lý báo cáo và nhật ký giữ pháp lý; chưa có tài liệu quy trình riêng.
5. **Xóa trên Cloudinary không tự thử lại.** Nếu Cloudinary lỗi, URL chưa xóa được ghi vào log lỗi của job nền để dọn tay.
6. **Nghị định 356 có đòi hỏi khác chưa được đối chiếu hết**, ví dụ Điều 9 khoản 3 điểm c (ẩn danh hóa, trường hợp cần dữ liệu rõ để xử lý khiếu nại, tranh chấp). Phần này chỉ được đọc lướt.

## 4. Nguồn

- Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15: Điều 2, 3, 4, 14, 19, 20, 21, 38.
- Nghị định 356/2025/NĐ-CP: Điều 5, 12, 17, 42 (khoản 2: Nghị định 13/2023/NĐ-CP hết hiệu lực từ 01/01/2026).
