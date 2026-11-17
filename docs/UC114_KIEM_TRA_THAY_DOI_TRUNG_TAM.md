# UC-114 (USAS-2138): Kiểm tra thay đổi thông tin trung tâm

**Jira:** Tác nhân Moderator. Moderator xem danh sách thay đổi mà trung tâm tự sửa và được đánh dấu cần kiểm tra ở UC-92 (website, fanpage), đối chiếu giá trị cũ và mới rồi chọn Đạt hoặc Không đạt (kèm lý do bắt buộc).

Quy tắc: Không đạt thì trung tâm nhận thông báo để sửa lại; nếu vi phạm, Moderator thu hồi dấu xác minh (UC-59) hoặc ẩn trung tâm (UC-72). Thay đổi tên pháp lý, mã số doanh nghiệp và tài liệu không qua UC này mà đưa hồ sơ về Chờ duyệt (UC-59). Mọi thao tác ghi nhật ký.

## 1. Luồng chạy

```
Trang /admin/center-changes (CenterProfileChangesView, chỉ Moderator)
  ├─ GET  /api/v1/admin/center-profile-changes?status=pending|passed|failed|withdrawn|all
  └─ POST /api/v1/admin/center-profile-changes/{id}/review  { passed, reason }
       └─ AdminCenterProfileChangesController        [Authorize: moderator]
            └─ CenterProfileChangeService.ReviewAsync
                 ├─ ICenterProfileChangeRepository.SaveAsync  -> UPDATE app.center_profile_changes
                 ├─ IAuditLogRepository                       -> center.profile_change_passed / _failed
                 └─ INotificationService.NotifyAsync          -> thông báo cho tài khoản trung tâm
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Api/Controllers/AdminCenterProfileChangesController.cs` | `List`, `Review` | Chỉ role `moderator`. `ArgumentException` thành 400, `KeyNotFoundException` thành 404. |
| `Application/StudyCenters/CenterProfileChanges.cs` | `CenterProfileChangeService.GetAsync` | Danh sách theo trạng thái, mặc định `pending`. Bộ lọc lạ thì 400. |
| | `ReviewAsync` | Xem mục 4. |
| | `SameValue`, `FieldLabel` | So sánh giá trị (bỏ khoảng trắng, không phân biệt hoa thường) và nhãn trường. |
| `Infrastructure/.../CenterProfileChangeRepository.cs` | `SearchAsync` | JOIN `study_centers` để lấy tên. Hàng đợi `pending` xếp chờ lâu nhất lên đầu, các danh sách khác mới nhất lên đầu. |
| | `GetCenterInfoAsync` | Tên trung tâm và tài khoản chủ trung tâm để gửi thông báo. |

## 3. Lưu vào DB như thế nào

Trên dòng `app.center_profile_changes` được kiểm tra: `status` thành `passed` hoặc `failed`, ghi `reviewed_by_user_id`, `reviewed_at`, `review_reason`.

**Giá trị trên hồ sơ trung tâm không bị đổi lại.** Chọn Không đạt chỉ ghi kết quả và báo cho trung tâm sửa; website hoặc fanpage mới vẫn hiển thị cho tới khi trung tâm sửa. Nếu nội dung vi phạm, Moderator dùng chức năng có sẵn để thu hồi dấu xác minh hoặc ẩn trung tâm.

`audit_logs`: `center.profile_change_passed` hoặc `center.profile_change_failed`, `detail_json` gồm trung tâm, trường, giá trị cũ, giá trị mới, lý do.

`notifications`: một thông báo loại `center_profile_change` cho tài khoản trung tâm, mở tới `/center/profile`.

## 4. Quy tắc trong `ReviewAsync`

1. Chọn Không đạt mà lý do trống: 400.
2. Lý do dài hơn 500 ký tự: 400.
3. Không tìm thấy thay đổi: 404.
4. Thay đổi không còn ở `pending` (đã kiểm tra, hoặc trung tâm đã đổi lại): 400 "Thay đổi này đã được xử lý."
5. Còn lại: ghi kết quả, ghi nhật ký, gửi thông báo. Lỗi khi gửi thông báo không làm hỏng kết quả đã ghi.

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/admin/centers/CenterProfileChangesView.tsx` | Bảng: thời điểm sửa, trung tâm (mở trang quản trị trung tâm), trường, giá trị cũ, giá trị mới, kết quả. Nút Đạt ghi ngay; nút Không đạt mở hộp thoại bắt buộc nhập lý do. Giá trị chỉ thành liên kết khi là đường dẫn `http(s)`, mở tab mới với `rel="noopener noreferrer nofollow"`. |
| `app/admin/center-changes/page.tsx`, `lib/admin-nav.ts` | Trang và mục điều hướng "Thay đổi của trung tâm", chỉ Moderator. |
| `lib/center-admin-api.ts` | `centerProfileChangesApi.list`, `review`. |

## 6. Test

`CenterProfileChangeAndDeletionTests.cs`: Không đạt bắt buộc lý do, sau đó ghi kết quả, gửi thông báo cho chủ trung tâm và ghi nhật ký; Đạt không cần lý do; không kiểm tra lại thay đổi đã xử lý; thay đổi không tồn tại trả 404; bộ lọc mặc định và bộ lọc lạ; controller chỉ cho Moderator.

## 7. Giới hạn

- Thu hồi dấu xác minh và ẩn trung tâm không nằm trong màn hình này. Moderator bấm tên trung tâm để sang trang quản trị trung tâm rồi thao tác ở đó.
- Chưa có số đếm thay đổi chờ kiểm tra trên thanh điều hướng.
