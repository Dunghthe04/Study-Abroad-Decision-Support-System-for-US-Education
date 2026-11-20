# UC-45 (USAS-2070): Đánh giá trung tâm

**Jira:** Tác nhân Học sinh, Phụ huynh. Sau khi gửi yêu cầu liên hệ, người dùng chấm trung tâm từ 1 đến 5 và viết bình luận. Quy tắc: một lần đánh giá cho mỗi yêu cầu. Đánh giá vi phạm bị Moderator gỡ (UC-57).

## 1. Luồng chạy

```
Trang /centers/{id}, tab Đánh giá
  └─ CenterReviewForm
       ├─ GET  /api/v1/study-centers/{id}/reviews/eligibility   (có được đánh giá không)
       └─ POST /api/v1/study-centers/{id}/reviews  { rating, comment }
            └─ CenterReviewsController.Create           [Authorize: student, parent]
                 └─ CenterReviewService.CreateAsync
                      ├─ EvaluateAsync                  (trung tâm, vai trò, yêu cầu liên hệ chưa đánh giá)
                      ├─ ICenterReviewSubmissionRepository.TryAddAsync   -> INSERT app.center_reviews
                      └─ IAuditLogRepository.AddAsync   -> INSERT audit_logs (center_review.create)
  └─ onCreated -> useCenterReviews.refresh()            (tải lại danh sách và điểm trung bình)
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Api/Controllers/CenterReviewsController.cs` | `GetEligibility` | `GET .../reviews/eligibility`. Trả `CenterReviewEligibilityDto(CanReview, HasReviewed, Reason)`. |
| | `Create` | `POST .../reviews`. Trả 201 kèm `CenterReviewDto` (tên đã che). |
| | `RunAsync` | Lấy user id từ token. `ArgumentException` thành 400, `KeyNotFoundException` thành 404. |
| `Application/StudyCenters/CenterReviewSubmission.cs` | `CreateCenterReviewRequest(Rating, Comment?)` | Dữ liệu gửi lên. |
| | `CenterReviewService.CreateAsync` | Kiểm tra số sao 1..5, cắt khoảng trắng bình luận (rỗng thành null, tối đa 2000 ký tự), gọi `EvaluateAsync`, tạo `CenterReview`, lưu, ghi nhật ký. |
| | `CenterReviewService.GetEligibilityAsync` | Chỉ trả kết quả của `EvaluateAsync`, không ghi gì. |
| | `EvaluateAsync` (private) | Xem mục 4. |
| | `ICenterReviewSubmissionRepository` | `FindLatestLeadAsync`, `FindLatestUnreviewedLeadAsync`, `TryAddAsync`, `GetPublicDtoAsync`. |
| `Infrastructure/.../CenterReviewSubmissionRepository.cs` | `FindLatestUnreviewedLeadAsync` | Yêu cầu liên hệ mới nhất của người này tới trung tâm (`UserId` hoặc `SubmittedByUserId`) mà chưa có đánh giá nào, kể cả đánh giá đã gỡ. |
| | `TryAddAsync` | `INSERT`. Nếu DB báo vi phạm index duy nhất (mã 23505) thì trả `false` thay vì ném lỗi. |
| | `GetPublicDtoAsync` | Đọc lại đánh giá vừa tạo, JOIN `users`, che tên bằng `MaskName`. |

## 3. Lưu vào DB như thế nào

Một dòng mới trong `app.center_reviews`:

| Cột | Giá trị |
|---|---|
| `study_center_id` | Trung tâm được đánh giá |
| `user_id` | Người gửi đánh giá |
| `lead_id` | Yêu cầu liên hệ được dùng để đánh giá |
| `rating` | 1..5 (DB có CHECK) |
| `comment` | Bình luận đã cắt khoảng trắng, hoặc null |
| `is_verified` | `true`, vì người đánh giá đã thật sự liên hệ trung tâm |
| `status` | `visible` |

Ràng buộc "một lần đánh giá cho mỗi yêu cầu" có hai lớp:
1. **Tầng service:** `FindLatestUnreviewedLeadAsync` chỉ trả yêu cầu chưa có đánh giá.
2. **Tầng DB:** index duy nhất `ix_center_reviews_lead_id` trên `lead_id` (lọc `lead_id IS NOT NULL`), tạo ở migration `20261116165220_AddCenterReviewUniqueLead`.

Một dòng trong `audit_logs`: `action = center_review.create`, `entity_type = center_review`, `detail_json` gồm `studyCenterId`, `leadId`, `rating`, `hasComment`. Nội dung bình luận không ghi vào nhật ký.

## 4. Quy tắc trong `EvaluateAsync`

1. Trung tâm không tồn tại, ngừng hoạt động hoặc chưa xác minh: 404.
2. Tài khoản không phải học sinh / phụ huynh: không được đánh giá.
3. Có yêu cầu liên hệ chưa được đánh giá: được đánh giá, gắn với yêu cầu đó.
4. Có yêu cầu liên hệ nhưng đều đã đánh giá: từ chối, `HasReviewed = true`, lý do "Yêu cầu liên hệ này đã được đánh giá."
5. Chưa gửi yêu cầu nào: từ chối, lý do "Bạn cần gửi yêu cầu liên hệ tới trung tâm trước khi đánh giá."

Hệ quả:
- Gửi yêu cầu liên hệ mới tới cùng trung tâm thì được đánh giá thêm một lần.
- Phụ huynh gửi yêu cầu thay học sinh: ai đánh giá trước thì yêu cầu đó coi như đã dùng.
- Đánh giá đã bị Moderator gỡ vẫn chiếm lượt của yêu cầu đó, không đánh giá lại được.

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/centers/CenterReviewForm.tsx` | Gọi eligibility khi mở. Đã đánh giá hoặc không đủ điều kiện thì hiện lý do thay cho form. Form gồm chọn số sao và ô bình luận (giới hạn độ dài), gọi `createReview`, sau đó gọi `onCreated`. |
| `lib/api.ts` | `studyCentersApi.getReviewEligibility(id)`, `studyCentersApi.createReview(id, rating, comment)`. |
| `components/centers/StudyCenterDetailView.tsx` | Đặt form trong tab Đánh giá, truyền `onCreated={refreshReviews}`. |

## 6. Sửa trong đợt đối chiếu Jira

- **Gửi trùng cùng lúc:** trước đây hai lượt gửi đồng thời cho cùng một yêu cầu làm lượt sau vi phạm index duy nhất và trả lỗi 500. Nay `TryAddAsync` bắt lỗi này, service trả 400 với thông báo "Yêu cầu liên hệ này đã được đánh giá." và không ghi nhật ký cho lượt bị từ chối.
- **Chú thích:** controller ghi "Mỗi người một đánh giá cho mỗi trung tâm", sửa thành "Mỗi yêu cầu liên hệ một đánh giá" cho đúng code và Jira.

## 7. Test

`CenterReviewSubmissionTests.cs`: lưu đánh giá gắn yêu cầu và ghi nhật ký, số sao ngoài khoảng, bình luận quá dài, chưa gửi yêu cầu, đánh giá lần hai cho cùng yêu cầu, phụ huynh và học sinh cùng một yêu cầu, yêu cầu mới thì được đánh giá thêm, đánh giá đã gỡ vẫn chiếm lượt, yêu cầu tới trung tâm khác, sai vai trò, trung tâm chưa xác minh, và các test controller. Thêm mới: lượt gửi trùng bị DB từ chối được báo là "đã được đánh giá" và không ghi nhật ký.

Phần bắt lỗi `PostgresException` trong repository chưa có test tự động vì project test không có DB thật.
