# UC-57 (USAS-2079): Gỡ đánh giá trung tâm

**Jira:** Tác nhân Moderator. Moderator gỡ một đánh giá trung tâm vi phạm (UC-45) kèm lý do; đánh giá bị gỡ không còn hiện và không tính vào điểm đánh giá của trung tâm. Thao tác ghi nhật ký.

## 1. Luồng chạy

```
Trang chi tiết trung tâm, tab Đánh giá (CenterReviewList, chỉ Moderator thấy nút "Gỡ đánh giá")
  └─ Hộp thoại xác nhận + ô lý do (bắt buộc)
       └─ studyCentersApi.removeReview(centerId, reviewId, reason)
            └─ POST /api/v1/study-centers/{centerId}/reviews/{reviewId}/remove  { reason }
                 └─ CenterReviewModerationController.Remove        [Authorize: moderator]
                      └─ CenterReviewModerationService.RemoveAsync
                           ├─ ICenterReviewModerationRepository.GetForUpdateAsync
                           ├─ SaveAsync            -> UPDATE app.center_reviews SET status = 'removed'
                           └─ IAuditLogRepository  -> INSERT audit_logs (center_review.remove)
  └─ onReviewRemoved -> tải lại danh sách và điểm trung bình
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Api/Controllers/CenterReviewModerationController.cs` | `Remove(centerId, reviewId, body)` | Chỉ role `moderator`. `ArgumentException` thành 400, `KeyNotFoundException` thành 404. Không có body thì lý do là null và bị service từ chối. |
| `Application/StudyCenters/CenterReviewModeration.cs` | `RemoveCenterReviewRequest(Reason)` | Lý do gỡ, bắt buộc, tối đa 500 ký tự. |
| | `CenterReviewRemovalDto(ReviewId, StudyCenterId, Status)` | Kết quả trả về. |
| | `CenterReviewModerationService.RemoveAsync` | Xem mục 4. |
| | `ICenterReviewModerationRepository` | `GetForUpdateAsync(centerId, reviewId)`: lấy đánh giá theo đúng trung tâm, mọi trạng thái. `SaveAsync`: lưu thay đổi. |
| `Infrastructure/.../CenterReviewModerationRepository.cs` | | Cài đặt hai hàm trên bằng EF Core. |

## 3. Lưu vào DB như thế nào

- `app.center_reviews`: chỉ đổi `status` thành `removed` và cập nhật `updated_at`. **Không xóa dòng**, số sao và bình luận gốc vẫn được giữ để tra cứu.
- `audit_logs`: một dòng `action = center_review.remove`, `actor_user_id` là Moderator, `entity_id` là mã đánh giá. `detail_json` gồm `studyCenterId`, `reviewerUserId`, `rating`, `hadComment`, `previousStatus`, `reason`.

Lý do gỡ chỉ nằm trong nhật ký, bảng `center_reviews` không có cột lý do.

Vì sao đánh giá bị gỡ không còn hiện và không tính điểm: cả `CenterReviewRepository.GetVisibleAsync` (UC-41) lẫn `StudyCenterRepository.ApplyLiveReviewStatsAsync` (điểm ở danh sách) đều chỉ lấy `status = 'visible'` và tính điểm trực tiếp từ các dòng đó, không dùng cột lưu sẵn.

## 4. Quy tắc trong `RemoveAsync`

1. Mã trung tâm hoặc mã đánh giá rỗng: 400.
2. Lý do trống hoặc chỉ có khoảng trắng: 400 "Vui lòng nhập lý do gỡ đánh giá."
3. Lý do dài hơn 500 ký tự: 400.
4. Không tìm thấy đánh giá, hoặc đánh giá thuộc trung tâm khác: 404.
5. Đánh giá đang `removed`: không đổi gì, không ghi nhật ký lần hai, vẫn trả kết quả.
6. Còn lại: đổi trạng thái và ghi nhật ký.

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/centers/CenterReviewList.tsx` | Khi người dùng là Moderator và có `centerId` thì mỗi đánh giá có nút "Gỡ đánh giá". Hộp thoại hiện lại nội dung đánh giá, ô lý do bắt buộc. Nút xác nhận bị khóa khi lý do trống. Gỡ xong báo toast và gọi `onReviewRemoved`. |
| `lib/api.ts` | `studyCentersApi.removeReview(centerId, reviewId, reason)`. |
| `StudyCenterDetailView.tsx`, `AdminCenterDetailView.tsx` | Truyền `centerId` và `onReviewRemoved` (tải lại đánh giá). |

## 6. Sửa trong đợt đối chiếu Jira

Jira ghi gỡ đánh giá "kèm lý do", nhưng code cũ cho phép bỏ trống. Nay lý do là bắt buộc ở service (400 nếu trống) và ở giao diện (khóa nút xác nhận).

## 7. Test

`CenterReviewModerationTests.cs`: gỡ đánh giá đang hiển thị (đổi trạng thái, giữ nội dung gốc, ghi nhật ký kèm lý do), gỡ lại lần hai không đổi gì, đánh giá của trung tâm khác trả 404, đánh giá không tồn tại, mã rỗng, lý do quá dài, **lý do trống bị từ chối và không đổi gì**, controller chỉ cho Moderator.

## 8. Giới hạn

- Chưa có chức năng khôi phục đánh giá đã gỡ, và Moderator chưa có màn hình xem lại các đánh giá đã gỡ.
- Người viết đánh giá không nhận thông báo khi đánh giá bị gỡ (Jira không yêu cầu).
