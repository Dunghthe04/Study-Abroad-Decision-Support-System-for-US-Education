# Security Checklist — Setup & Quy trình làm tính năng

**Dự án:** Hệ tư vấn du học Mỹ – Study Abroad Decision Support System for US Education
**Stack:** Backend C#/.NET · Frontend Next.js · Database PostgreSQL

Checklist chia theo 3 loại tần suất:
- **Phần A**: làm 1 lần đầu dự án (cấu hình hạ tầng/middleware) — sau khi làm xong, mọi tính năng code sau đó tự động được bảo vệ, không cần lặp lại.
- **Phần B**: phải tự kiểm tra lại MỖI LẦN code 1 tính năng/API/trang mới — đây là checklist thao tác, không phải cấu hình 1 lần.
- **Phần C**: không gắn với tính năng cụ thể nào — làm theo chu kỳ (trước mỗi lần deploy, hoặc định kỳ hàng tuần/tháng).

---

## PHẦN A — SETUP MỘT LẦN ĐẦU DỰ ÁN

Làm 1 lần khi mới dựng khung project (Sprint 0 / tuần đầu code nền tảng). Sau khi làm xong, không cần làm lại cho từng tính năng.

### A1. Web / Backend

| ✓ | Việc cần setup | Làm ở đâu | Người phụ trách |
|---|---|---|---|
| [ ] | Cấu hình ASP.NET Core Identity (Authentication + Password Hashing tự động) | Program.cs | |
| [ ] | Bật AddAntiforgery() + CSRF middleware | Program.cs | |
| [ ] | Cấu hình AddCors() — chỉ whitelist domain Next.js | Program.cs | |
| [ ] | Bật UseHttpsRedirection() + UseHsts() + gắn chứng chỉ Let's Encrypt | Program.cs / hosting | |
| [ ] | Cấu hình AddRateLimiter() với policy mặc định | Program.cs | |
| [ ] | Cấu hình Global Exception Handler + tắt Developer Exception Page khi production | Program.cs | |
| [ ] | Cấu hình ILogger/Serilog (hạ tầng ghi log) | Program.cs | |
| [ ] | Cấu hình Polly retry policy cho các lời gọi ra ngoài (DB, API khác) | Program.cs / HttpClient setup | |

### A2. Database

| ✓ | Việc cần setup | Làm ở đâu | Người phụ trách |
|---|---|---|---|
| [ ] | Tạo ROLE riêng cho tài khoản kết nối DB, chỉ cấp đúng quyền cần thiết (không dùng superuser) | PostgreSQL | |
| [ ] | Bật extension pgcrypto trên database | PostgreSQL | |

### A3. Frontend

| ✓ | Việc cần setup | Làm ở đâu | Người phụ trách |
|---|---|---|---|
| [ ] | Cấu hình React Query với retry mặc định | App setup Next.js | |
| [ ] | Đảm bảo không dùng dangerouslySetInnerHTML trong codebase (giữ auto-escape mặc định của React) | Quy ước code chung | |

### A4. Payment & AI (nếu triển khai)

| ✓ | Việc cần setup | Làm ở đâu | Người phụ trách |
|---|---|---|---|
| [ ] | Tích hợp SDK cổng thanh toán (VNPay/Momo/Stripe) — Tokenization + 3DS + PCI DSS tự động theo SDK | Backend | |
| [ ] | Rate Limiting riêng cho endpoint gọi AI (dùng chung policy ở A1) | Program.cs | |

---

## PHẦN B — LÀM MỖI LẦN CODE TÍNH NĂNG MỚI

Chạy qua checklist này mỗi khi thêm 1 API/trang/chức năng mới — kể cả khi Phần A đã setup xong, các việc này vẫn phải làm lại cho từng tính năng vì gắn liền với logic riêng của tính năng đó.

| ✓ | Việc cần làm cho MỖI tính năng mới | Áp dụng khi nào | Người phụ trách |
|---|---|---|---|
| [ ] | Gắn đúng [Authorize] / [Authorize(Roles="...")] cho API/trang mới | Mọi tính năng có yêu cầu đăng nhập/phân quyền | |
| [ ] | Viết rule validate riêng (FluentValidation + zod) cho input của tính năng đó | Mọi tính năng có form/API nhận input | |
| [ ] | Dùng Entity Framework Core (không viết raw SQL ghép chuỗi) cho query mới | Mọi tính năng truy vấn DB | |
| [ ] | Áp dụng whitelist đuôi file + kiểm tra MIME type + giới hạn size + đổi tên GUID | Chỉ tính năng có upload file | |
| [ ] | Dùng Path.GetFileName(), không tự ghép chuỗi đường dẫn từ input | Chỉ tính năng xử lý đường dẫn/tên file | |
| [ ] | Cân nhắc mã hóa field mới bằng pgcrypto nếu là dữ liệu cá nhân nhạy cảm | Mọi field dữ liệu mới liên quan thông tin cá nhân | |
| [ ] | Viết validate input riêng chống Prompt Injection cho luồng chat/prompt mới | Chỉ tính năng liên quan AI/chatbot | |
| [ ] | Kiểm duyệt + ẩn danh hóa dữ liệu trước khi đưa vào training/fine-tune | Chỉ khi cập nhật dữ liệu training cho AI | |
| [ ] | Thêm log cho hành động nhạy cảm (đăng nhập, thanh toán, xóa dữ liệu, đổi quyền...) | Mọi hành động nhạy cảm mới | |

---

## PHẦN C — ĐỊNH KỲ (không gắn tính năng cụ thể)

Không làm theo tính năng — làm theo chu kỳ thời gian, ví dụ trước mỗi lần deploy bản mới hoặc định kỳ hàng tuần/tháng.

| ✓ | Việc cần làm định kỳ | Tần suất đề xuất | Người phụ trách |
|---|---|---|---|
| [ ] | Chạy `dotnet list package --vulnerable` và `npm audit` để kiểm tra thư viện có lỗ hổng đã biết | Trước mỗi lần deploy bản mới | |
| [ ] | Rà soát log bảo mật (đăng nhập thất bại, lỗi 500, truy cập bị từ chối) xem có dấu hiệu bất thường | Hàng tuần, hoặc trước buổi bảo vệ | |

---

## Hướng dẫn sử dụng

- **Phần A**: 1 người phụ trách setup nền tảng làm 1 lần, tick xong là xong vĩnh viễn (trừ khi đổi kiến trúc).
- **Phần B**: người code tính năng nào tự chạy qua checklist cho tính năng đó, tick + ghi tên vào trước khi coi là "xong" tính năng. Đây là checklist dùng lặp lại nhiều lần trong suốt dự án — không tick 1 lần duy nhất, mà copy lại bảng này (hoặc dùng làm template) cho mỗi tính năng mới.
- **Phần C**: 1 người (có thể luân phiên) phụ trách chạy định kỳ, đặc biệt bắt buộc trước buổi bảo vệ để đảm bảo không có lỗ hổng mới phát sinh từ thư viện cập nhật.
