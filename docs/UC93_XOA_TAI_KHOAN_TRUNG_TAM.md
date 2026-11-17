# UC-93 (USAS-2121): Xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm

**Jira:** Tác nhân Administrator. Administrator nhận yêu cầu xóa do trung tâm gửi từ UC-92, kiểm tra danh tính người yêu cầu, thực hiện xóa tài khoản và dữ liệu trung tâm theo thời hạn đã công bố và báo kết quả qua email.

Quy tắc: các yêu cầu liên hệ đang mở của trung tâm được đóng và người dùng liên quan nhận thông báo. Tài khoản Học sinh và Phụ huynh do người dùng tự xóa ở UC-80, không qua Administrator.

## 1. Luồng chạy

```
Trung tâm (trang /center/profile, CenterDeletionRequestCard)
  GET    /api/v1/center/deletion-request        (yêu cầu gần nhất, 204 nếu chưa có)
  POST   /api/v1/center/deletion-request        { reason }
  DELETE /api/v1/center/deletion-request        (rút yêu cầu đang chờ)

Administrator (trang /admin/center-deletion-requests, CenterDeletionRequestsView)
  GET  /api/v1/admin/center-deletion-requests?status=pending|completed|rejected|cancelled|all
  POST /api/v1/admin/center-deletion-requests/{id}/complete  { identityVerified, note }
  POST /api/v1/admin/center-deletion-requests/{id}/reject    { reason }
       └─ CenterDeletionService
            ├─ ICenterDeletionRepository.ExecuteDeletionAsync   (một giao dịch DB)
            ├─ nhật ký center.deleted / center.deletion_rejected
            ├─ thông báo cho người có yêu cầu liên hệ bị đóng
            └─ IEmailSender.SendCenterDeletionResultEmailAsync
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Api/Controllers/CenterDeletionRequestsController.cs` | `CenterDeletionRequestsController` | Role `center`: xem, gửi, rút yêu cầu của chính mình. |
| | `AdminCenterDeletionRequestsController` | Role `admin`: danh sách, thực hiện xóa, từ chối. |
| `Application/StudyCenters/CenterDeletion.cs` | `RequestAsync` | Tạo yêu cầu `pending`, chụp lại tên và email trung tâm, đặt hạn xử lý là 30 ngày. Mỗi trung tâm chỉ một yêu cầu đang chờ. Ghi nhật ký và thông báo cho mọi Admin. |
| | `GetMineAsync`, `CancelAsync` | Xem yêu cầu gần nhất; rút yêu cầu đang chờ. |
| | `GetAsync` | Danh sách cho Admin. Hàng đợi `pending` xếp gần hết hạn lên đầu. |
| | `CompleteAsync` | Bắt buộc `identityVerified = true`. Gọi `ExecuteDeletionAsync`, đánh dấu `completed`, ghi nhật ký, thông báo cho người có yêu cầu liên hệ bị đóng, gửi email kết quả. |
| | `RejectAsync` | Bắt buộc lý do. Đánh dấu `rejected`, dữ liệu giữ nguyên, thông báo và email cho trung tâm. |
| `Infrastructure/.../CenterDeletionRepository.cs` | `ExecuteDeletionAsync` | Xem mục 3. |
| | `ScrubCenter`, `ScrubUser` | Xóa trắng dữ liệu trên dòng trung tâm và dòng tài khoản. |
| `Application/Auth/IEmailSender.cs`, `Infrastructure/Auth/SmtpEmailSender.cs` | `SendCenterDeletionResultEmailAsync` | Email báo đã xóa hoặc bị từ chối, kèm ghi chú hoặc lý do. |

## 3. "Xóa" làm gì với dữ liệu

`ExecuteDeletionAsync` chạy trong một giao dịch:

1. **Yêu cầu liên hệ đang mở** của trung tâm (trạng thái khác `closed`) chuyển thành `closed`. Người gửi và học sinh của từng yêu cầu được gom lại để gửi thông báo.
2. **Dòng trung tâm** (`study_centers`): tên thành "Trung tâm đã xóa", `is_active = false`, trạng thái xác minh về `unverified`, gỡ liên kết tài khoản chủ, và xóa trắng website, fanpage, địa chỉ, điện thoại, email, mô tả, phí, logo, ảnh, dịch vụ, thông tin pháp lý, người đăng ký.
3. **Hồ sơ xác minh** (`center_verification_requests` kèm tài liệu) và **thay đổi chờ kiểm tra** (`center_profile_changes`) của trung tâm bị xóa hẳn.
4. **Tài khoản chủ trung tâm** (`users`): trạng thái `deleted`, email thay bằng `deleted-<id>@deleted.invalid`, họ tên thành "Tài khoản đã xóa", xóa điện thoại và ngày sinh, mật khẩu thay bằng chuỗi không dùng được. Mọi phiên đăng nhập bị thu hồi, mã OTP bị xóa.

**Dòng trung tâm và dòng tài khoản không bị xóa khỏi bảng.** Lý do: `leads` và `center_reviews` tham chiếu tới trung tâm với xóa dây chuyền, nên xóa hẳn dòng trung tâm sẽ xóa luôn lịch sử yêu cầu liên hệ và đánh giá của học sinh, phụ huynh. Dòng được giữ lại không còn chứa dữ liệu nào của trung tâm.

## 4. Lưu vào DB như thế nào

Bảng mới `app.center_deletion_requests`:

| Cột | Ý nghĩa |
|---|---|
| `study_center_id`, `requested_by_user_id` | Trung tâm và người gửi. Khóa ngoại kiểu SET NULL để yêu cầu còn lại làm bằng chứng. |
| `center_name`, `contact_email` | Chụp lại lúc gửi, vì dữ liệu gốc bị xóa trắng khi hoàn tất. |
| `reason` | Lý do của trung tâm, không bắt buộc. |
| `status` | `pending`, `completed`, `rejected`, `cancelled`. |
| `due_at` | Hạn xử lý: thời điểm gửi cộng 30 ngày. |
| `identity_verified` | Admin đã xác nhận kiểm tra danh tính. |
| `processed_by_user_id`, `processed_at`, `admin_note` | Người xử lý, thời điểm, ghi chú hoặc lý do từ chối. |

Migration: `AddCenterProfileChangesAndDeletionRequests`. **Cần chạy migration trước khi dùng.**

`audit_logs`: `center.deletion_requested`, `center.deletion_cancelled`, `center.deleted` (kèm số yêu cầu liên hệ bị đóng, số người được thông báo, có quá hạn không), `center.deletion_rejected`.

`notifications`: `center_deletion_request` (cho Admin khi có yêu cầu, cho trung tâm khi bị từ chối), `lead_closed` (cho người có yêu cầu liên hệ bị đóng).

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/center/CenterDeletionRequestCard.tsx` | Cuối trang hồ sơ trung tâm. Giải thích hậu quả, hạn 30 ngày, không hoàn tác được. Gửi yêu cầu qua hộp thoại xác nhận; khi đang chờ thì hiện hạn xử lý và nút rút yêu cầu; khi bị từ chối thì hiện lý do. |
| `components/admin/centers/CenterDeletionRequestsView.tsx` | Bảng yêu cầu có lọc, đánh dấu "Quá hạn xử lý". "Thực hiện xóa" bắt buộc tick xác nhận đã kiểm tra danh tính; "Từ chối" bắt buộc nhập lý do. |
| `app/admin/center-deletion-requests/page.tsx`, `lib/admin-nav.ts` | Trang và mục điều hướng, chỉ Admin. |
| `lib/center-admin-api.ts` | `centerDeletionApi`, `adminCenterDeletionApi`. |

## 6. Test

`CenterProfileChangeAndDeletionTests.cs`: gửi yêu cầu (hạn 30 ngày, chụp tên và email, thông báo Admin), không gửi được yêu cầu thứ hai khi đang chờ, tài khoản không có trung tâm, rút yêu cầu rồi gửi lại, xóa bắt buộc xác nhận danh tính rồi gọi xóa, ghi nhật ký, thông báo người dùng liên quan và gửi email, không xử lý lại yêu cầu đã xong, từ chối bắt buộc lý do và không xóa gì, `ScrubCenter` và `ScrubUser` xóa trắng dữ liệu nhưng giữ dòng, phân quyền hai controller.

`ExecuteDeletionAsync` (phần chạm DB thật) chưa có test tự động vì project test không có DB. **Nên chạy thử trên DB thử nghiệm trước khi dùng thật, vì thao tác không hoàn tác được.**

## 7. Giới hạn và điểm cần nhóm xác nhận

- **Kiểm tra danh tính:** hệ thống chỉ ghi lại việc Admin xác nhận đã kiểm tra. Cách kiểm tra (gọi điện, đối chiếu giấy tờ) do Admin tự làm ngoài hệ thống.
- **Thời hạn 30 ngày:** tôi tự chọn vì Jira chỉ ghi "thời hạn đã công bố". Sửa ở hằng số `CenterDeletionService.ProcessingDeadline`.
- **Giữ dòng thay vì xóa hẳn:** xem mục 3. Nếu nhóm muốn xóa hẳn cả lịch sử yêu cầu liên hệ và đánh giá thì cần quyết định riêng.
- **Tin nhắn và đánh giá cũ:** tin nhắn trung tâm đã gửi trong các yêu cầu liên hệ và đánh giá của người dùng về trung tâm được giữ lại.
- **Ảnh, tài liệu trên dịch vụ lưu trữ ngoài (Cloudinary):** đường dẫn bị xóa khỏi DB nhưng tệp trên dịch vụ lưu trữ chưa bị xóa.
