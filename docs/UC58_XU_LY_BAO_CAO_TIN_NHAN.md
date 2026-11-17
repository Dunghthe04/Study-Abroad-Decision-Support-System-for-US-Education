# UC-58 (USAS-2080): Xử lý báo cáo tin nhắn hai chiều

**Jira:** Tác nhân Administrator.

Luồng chính: (1) Administrator mở hàng đợi báo cáo từ UC-44 và UC-74 (trung tâm báo người dùng, người dùng báo trung tâm). (2) Đọc tin bị báo cáo và một số tin ngữ cảnh liền kề (số lượng là tham số). (3) Chọn xử lý: gỡ tin nhắn vi phạm, cảnh cáo, khóa trung tâm hoặc người dùng (UC-61), hoặc bác bỏ báo cáo. (4) Hệ thống thông báo kết quả cho người báo cáo.

Quy tắc: Administrator không đọc toàn bộ cuộc trò chuyện. Mỗi lần đọc ghi vào nhật ký truy cập (UC-97).

## 1. Đối chiếu Jira và code

| Yêu cầu Jira | Trạng thái | Nằm ở đâu |
|---|---|---|
| Hàng đợi báo cáo hai chiều | Có sẵn | `GET /admin/lead-message-reports` |
| Đọc tin kèm tin ngữ cảnh, số lượng là tham số | **Bổ sung** | `GET .../{leadId}/{messageId}/context`, cấu hình `lead_message.report_context_size` |
| Gỡ riêng tin nhắn vi phạm | **Bổ sung** | `POST .../remove` |
| Cảnh cáo | **Bổ sung** | `POST .../warn` |
| Khóa người gửi (UC-61) | Có sẵn | `POST .../lock-sender` |
| Bác bỏ báo cáo | Có sẵn | `POST .../dismiss` |
| Thông báo kết quả cho người báo cáo | **Bổ sung** | cả 4 cách xử lý đều gửi thông báo `lead_message_report_result` |
| Admin không đọc toàn bộ cuộc trò chuyện | **Bổ sung** | bỏ role Admin khỏi `GET/POST /leads/received/{id}/messages` |
| Mỗi lần đọc ghi nhật ký truy cập | **Bổ sung** | `lead_message.report_queue_view`, `lead_message.report_view` |

## 2. Luồng chạy

```
Học sinh / phụ huynh:  POST /api/v1/leads/sent/{id}/messages/{messageId}/report
Trung tâm:             POST /api/v1/leads/received/{id}/messages/{messageId}/report
   └─ LeadService.ReportMessageAsync -> ghi reported_by_user_id, reported_at, report_reason
                                     -> nhật ký lead_message.report, thông báo cho mọi Admin

Admin (trang /admin/lead-message-reports, LeadMessageReportsView):
   GET  /api/v1/admin/lead-message-reports?status=pending|resolved|all     (hàng đợi)
   GET  .../{leadId}/{messageId}/context                                   (tin + ngữ cảnh)
   POST .../{leadId}/{messageId}/remove       { reason }                   (gỡ riêng tin)
   POST .../{leadId}/{messageId}/warn         { reason }                   (cảnh cáo)
   POST .../{leadId}/{messageId}/lock-sender  { reason }                   (khóa + gỡ mọi tin)
   POST .../{leadId}/{messageId}/dismiss                                   (bác bỏ)
```

## 3. Backend

`Api/Controllers/AdminLeadMessageReportsController.cs` (chỉ role `admin`):

| Hàm | Chức năng |
|---|---|
| `List` | Hàng đợi báo cáo, truyền id Admin để ghi nhật ký truy cập. |
| `GetContext` | Tin bị báo cáo kèm tin ngữ cảnh. |
| `Remove`, `Warn` | Gỡ riêng tin, cảnh cáo. Lý do bắt buộc. |
| `LockSender` | Gọi `IAuthService.LockUserAccountAsync`, rồi `RemoveAllMessagesOfLockedSenderAsync`, rồi `MarkReportResolvedByLockAsync`. |
| `Dismiss` | Bác bỏ báo cáo. |

`Application/Leads/LeadService.cs`:

| Hàm | Chức năng |
|---|---|
| `ReportSentLeadMessageAsync`, `ReportReceivedLeadMessageAsync` | Hai chiều báo cáo. Chỉ báo được tin của phía bên kia, không báo tin đã gỡ, báo lại không ghi đè. |
| `GetReportedMessagesAsync(filter, page, pageSize, ct, viewerAdminId)` | Hàng đợi. Có `viewerAdminId` và có kết quả thì ghi nhật ký `lead_message.report_queue_view` kèm danh sách mã tin đã đọc. |
| `GetReportContextAsync` | Lấy số tin ngữ cảnh từ cấu hình (mặc định 3, giới hạn 0..10), trả tin bị báo cáo cùng các tin liền trước và liền sau, ghi nhật ký `lead_message.report_view`. Tin không có báo cáo thì trả 404 và không ghi nhật ký. |
| `ParseReportContextSize(json)` | Đọc cấu hình: chấp nhận số (`3`) hoặc đối tượng (`{"size": 3}`), sai định dạng thì trả null để dùng mặc định. |
| `RemoveReportedMessageAsync` | Gỡ riêng tin: `status = removed`, ghi người gỡ, đánh dấu `report_resolution = removed`. Nhật ký `lead_message.remove`. Thông báo cho người gửi và người báo cáo. Gỡ lại không đổi gì. |
| `WarnReportedSenderAsync` | Cảnh cáo: tin vẫn hiện, `report_resolution = warned`. Nhật ký `lead_message.warn`. Thông báo cảnh cáo cho người gửi, thông báo kết quả cho người báo cáo. Báo cáo đã xử lý thì từ chối. |
| `MarkReportResolvedByLockAsync` | Sau khi khóa tài khoản: `report_resolution = locked`, thông báo cho người báo cáo. |
| `DismissReportAsync` | Xóa thông tin báo cáo trên tin (tin có thể bị báo cáo lại), nhật ký `lead_message.report_dismiss`, thông báo cho người báo cáo. |
| `RemoveAllMessagesOfLockedSenderAsync` | Gỡ mọi tin của tài khoản bị khóa, nhật ký `lead_message.remove_all_of_locked_user`. |

`Infrastructure/.../LeadRepository.cs`:

| Hàm | Chức năng |
|---|---|
| `GetReportedMessagesAsync` | JOIN `lead_messages`, `leads`, `study_centers`, `users`. `pending` là chưa gỡ và chưa có kết quả xử lý; `resolved` là đã gỡ hoặc đã có kết quả xử lý. |
| `GetReportContextMessagesAsync` | Lấy tin mục tiêu, N tin trước (theo `created_at` giảm dần) và N tin sau, ghép lại theo thời gian, kèm tên người gửi. |
| `GetReportContextSizeSettingAsync` | Đọc dòng `lead_message.report_context_size` trong `app_settings`. |

## 4. Lưu vào DB như thế nào

Bảng `app.lead_messages`:

| Cột | Ý nghĩa |
|---|---|
| `reported_by_user_id`, `reported_at`, `report_reason` | Ghi khi có người báo cáo. Bác bỏ thì ba cột này bị xóa về null. |
| `status` | `visible` hoặc `removed`. Nội dung gốc không bị xóa. |
| `removed_by_user_id` | Admin đã gỡ tin. |
| `report_resolution` (mới) | `removed`, `warned`, `locked`. Null là chưa xử lý. |
| `report_resolved_at` (mới) | Thời điểm xử lý. |

Hai cột mới do migration `20261117030059_AddLeadMessageReportResolution` tạo. **Cần chạy migration trước khi dùng.**

Bảng `app.app_settings`: dòng có `key = lead_message.report_context_size`, `value_json` là `3` hoặc `{"size": 3}`. Chưa có dòng này thì dùng mặc định 3. Hiện chưa có màn hình chỉnh, Admin sửa trực tiếp trong DB.

Bảng `notifications`: `lead_message_report` (cho Admin khi có báo cáo), `lead_message_report_result` (kết quả cho người báo cáo), `lead_message_removed` và `lead_message_warning` (cho người gửi).

Bảng `audit_logs`: `lead_message.report`, `lead_message.report_queue_view`, `lead_message.report_view`, `lead_message.remove`, `lead_message.warn`, `lead_message.report_dismiss`, `lead_message.remove_all_of_locked_user`.

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/admin/lead-messages/LeadMessageReportsView.tsx` | Bảng hàng đợi có lọc và phân trang. Mỗi dòng có "Xem ngữ cảnh" và, khi chưa xử lý, các nút "Bỏ qua báo cáo", "Cảnh cáo", "Gỡ tin nhắn", "Khóa tài khoản người gửi". Ba cách xử lý cần lý do dùng chung một hộp thoại. Hộp thoại ngữ cảnh tô nổi tin bị báo cáo và ghi rõ đây không phải toàn bộ cuộc trò chuyện. |
| `lib/api.ts` | `leadMessageReportsApi`: `list`, `getContext`, `remove`, `warn`, `lockSender`, `dismiss`. |
| `types/api.ts` | `AdminLeadMessageReportDto` (thêm `reportResolution`), `AdminLeadMessageReportContextDto`, `AdminReportContextMessageDto`. |

## 6. Test

`LeadServiceTests.cs` thêm: ngữ cảnh chỉ trả tin liền kề theo số mặc định và ghi nhật ký, số tin theo cấu hình có chặn trên và dưới, tin không có báo cáo trả 404 và không ghi nhật ký, đọc cấu hình JSON, đọc hàng đợi ghi nhật ký và báo cáo đã cảnh cáo tính là đã xử lý, gỡ riêng tin (chỉ tin đó, nhật ký, hai thông báo), gỡ lại không đổi gì, cảnh cáo, bác bỏ có thông báo người báo cáo, đánh dấu xử lý bằng khóa.

`LeadsControllerTests.cs`: hai endpoint luồng tin nhắn không còn nhận role Admin.

Toàn bộ 2365 test backend pass. Frontend được kiểm bằng `tsc` và `eslint`, chưa chạy thử trên trình duyệt.

## 7. Giới hạn và điểm cần nhóm xác nhận

- **Cảnh cáo:** Jira không định nghĩa. Bản này hiểu là gửi thông báo cho người gửi, ghi nhật ký, không khóa tài khoản và không đếm số lần cảnh cáo.
- **Khóa trung tâm:** "khóa trung tâm hoặc người dùng" hiện là khóa tài khoản người gửi. Nếu người gửi là tài khoản trung tâm thì tài khoản đó bị khóa, còn hồ sơ trung tâm không tự động bị ẩn khỏi danh bạ.
- **Admin và luồng tin nhắn:** Admin không còn gọi được `GET/POST /leads/received/{id}/messages`. Nếu có màn hình nào của Admin đang mở khung trò chuyện thì sẽ nhận lỗi 403.
- **Hàng đợi:** danh sách vẫn hiện nội dung tin bị báo cáo ngay trong bảng, nên mỗi lần tải danh sách được ghi một dòng nhật ký.
- **Tham số ngữ cảnh:** chưa có màn hình chỉnh, chưa có dòng seed sẵn.
- **Nhật ký truy cập (UC-97):** dùng bảng `audit_logs` chung, chưa có bảng nhật ký truy cập riêng.
