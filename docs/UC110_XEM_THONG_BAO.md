# UC-110 (USAS-2134): Xem thông báo

**Jira:** Tác nhân Học sinh, Phụ huynh, Trung tâm. Xem danh sách thông báo của mình trong hệ thống, đánh dấu đã đọc và mở tới màn hình liên quan.

Quy tắc: nguồn thông báo gồm trường do phụ huynh gợi ý (UC-30); yêu cầu liên hệ mới, cần xác nhận và đổi trạng thái (UC-42, UC-73); lời mời, hủy liên kết và mốc 16, 18 tuổi (UC-75); nhắc gia hạn và kết quả thanh toán (UC-78); kết quả duyệt hồ sơ xác minh và kiểm tra thay đổi của trung tâm (UC-59, UC-114). Mỗi người chỉ thấy thông báo của mình.

## 1. Luồng chạy

```
Chuông trên thanh điều hướng (SiteHeader)
  └─ useUnreadNotifications -> GET /api/v1/notifications/unread-count   (mỗi phút, khi mở lại tab, khi có thay đổi)
  └─ bấm chuông -> /notifications

Trang /notifications (NotificationsView)
  ├─ GET  /api/v1/notifications?unreadOnly=&page=&pageSize=
  ├─ POST /api/v1/notifications/{id}/read       (bấm một thông báo, rồi mở linkUrl nếu có)
  └─ POST /api/v1/notifications/read-all
       └─ NotificationsController  [Authorize: mọi tài khoản đã đăng nhập]
            └─ NotificationService -> INotificationRepository -> bảng app.notifications
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Api/Controllers/NotificationsController.cs` | `GetMine`, `GetUnreadCount`, `MarkRead`, `MarkAllRead` | Bốn endpoint ở trên. User id luôn lấy từ token, không nhận từ client. `KeyNotFoundException` thành 404. |
| `Application/Notifications/NotificationService.cs` | `GetMineAsync` | Một trang thông báo (mới nhất trước) kèm số chưa đọc. `page >= 1`, `pageSize` 1..50. |
| | `CountUnreadAsync` | Số chưa đọc cho huy hiệu chuông. |
| | `MarkReadAsync` | Ghi `read_at` lần đầu, đọc lại không đổi thời điểm. Thông báo của người khác trả 404. |
| | `MarkAllReadAsync` | Đánh dấu đã đọc mọi thông báo chưa đọc của người dùng. |
| | `NotifyAsync` | Tạo một thông báo. Các chức năng khác gọi hàm này. Cắt khoảng trắng, cắt độ dài theo cột DB, chỉ giữ đường dẫn nội bộ. |
| | `SafeLink` | Chỉ nhận đường dẫn bắt đầu bằng một dấu `/`. Từ chối `//host`, `https://...`, `javascript:...` để thông báo không dẫn người dùng ra trang ngoài. |
| `Infrastructure/.../NotificationRepository.cs` | | Cài đặt bằng EF Core. Mọi truy vấn lọc `user_id`. `MarkAllReadAsync` dùng một câu `UPDATE` (`ExecuteUpdateAsync`). |
| `Application/StudyCenters/StudyCenterService.cs` | `NotifyCenterInAppAsync`, `VerificationOutcomeTitle` | Nguồn thông báo UC-59: khi Moderator duyệt, từ chối, yêu cầu bổ sung hoặc thu hồi dấu xác minh, tài khoản trung tâm nhận thông báo loại `center_verification`, mở tới `/center/verification`. |

## 3. Lưu vào DB như thế nào

Bảng `app.notifications` (đã có sẵn, không cần migration):

| Cột | Ý nghĩa |
|---|---|
| `user_id` | Người nhận. FK `users`, xóa người dùng thì xóa thông báo. |
| `type` (tối đa 32) | Loại thông báo, ví dụ `center_verification`. |
| `title` (tối đa 256), `body` (tối đa 2000) | Nội dung. |
| `link_url` (tối đa 512) | Đường dẫn nội bộ tới màn hình liên quan, có thể null. |
| `read_at` | Null là chưa đọc. |
| `created_at`, `updated_at` | |

Có index `(user_id, read_at)` phục vụ đếm chưa đọc.

Đánh dấu đã đọc chỉ ghi `read_at` và `updated_at`. Không có chức năng xóa thông báo.

## 4. Frontend

| File | Chức năng |
|---|---|
| `lib/notification-api.ts` | `notificationsApi` (`list`, `unreadCount`, `markRead`, `markAllRead`), kiểu dữ liệu, `safeNotificationLink`, và sự kiện `usas:notifications-changed` để chuông cập nhật ngay. |
| `hooks/use-unread-notifications.ts` | Đếm chưa đọc: gọi lại mỗi phút, khi tab được mở lại, và khi trang thông báo báo có thay đổi. Chỉ chạy khi đã đăng nhập. |
| `components/layout/SiteHeader.tsx` | Chuông cạnh ảnh đại diện, huy hiệu số chưa đọc (trên 99 hiện `99+`). |
| `components/notifications/NotificationsView.tsx` | Danh sách có lọc Tất cả / Chưa đọc, phân trang, nút "Đánh dấu tất cả đã đọc". Bấm một thông báo thì đánh dấu đã đọc rồi mở màn hình liên quan. |
| `app/notifications/page.tsx` | Trang `/notifications`, bọc `ProtectedRoute`. |

## 5. Test

`NotificationServiceTests.cs`: chỉ thấy thông báo của mình và đúng thứ tự, lọc chưa đọc và chặn tham số phân trang, đánh dấu đã đọc giữ thời điểm đọc lần đầu, thông báo của người khác trả 404, đánh dấu tất cả chỉ đụng thông báo của mình, `NotifyAsync` cắt và lọc đường dẫn, `SafeLink` với đường dẫn ngoài và `javascript:`, tiêu đề theo kết quả duyệt, và các test controller (lấy user từ token, 401 khi chưa đăng nhập, không giới hạn role).

Toàn bộ 2428 test backend pass. Frontend kiểm bằng `tsc` và `eslint`, chưa chạy thử trên trình duyệt.

## 6. Nguồn thông báo: đã nối và chưa nối

| Nguồn theo Jira | Trạng thái |
|---|---|
| Kết quả duyệt hồ sơ xác minh (UC-59) | **Đã nối** |
| Kiểm tra thay đổi của trung tâm (UC-114) | Nối trong nhánh UC-114 |
| Kết quả xử lý báo cáo tin nhắn (UC-58) | Nhánh UC-58 đã ghi thẳng vào bảng `notifications`, hiển thị được ngay khi hai nhánh cùng vào `develop` |
| Trường do phụ huynh gợi ý (UC-30) | Chưa nối |
| Yêu cầu liên hệ mới, cần xác nhận, đổi trạng thái (UC-42, UC-73) | Chưa nối |
| Lời mời, hủy liên kết, mốc 16 và 18 tuổi (UC-75) | Chưa nối |
| Nhắc gia hạn, kết quả thanh toán (UC-78) | Chưa nối, chức năng thanh toán chưa có |

Các nguồn chưa nối chỉ cần gọi `INotificationService.NotifyAsync` tại nơi xảy ra sự kiện.

## 7. Giới hạn

- Jira nêu tác nhân là Học sinh, Phụ huynh, Trung tâm. Endpoint cho mọi tài khoản đã đăng nhập, vì Admin và Moderator cũng nhận thông báo (ví dụ báo cáo tin nhắn ở UC-58).
- Không có thông báo đẩy thời gian thực, chuông cập nhật theo chu kỳ một phút.
