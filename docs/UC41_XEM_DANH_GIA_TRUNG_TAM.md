# UC-41 (USAS-2068): Xem đánh giá trung tâm

Hiển thị danh sách đánh giá và xếp hạng sao của một trung tâm tư vấn trên trang chi tiết công khai.

> **Trạng thái:** đã được implement ở commit `11ef8ea` (BE + FE). Đợt này bổ sung unit test cho phần che tên và tài liệu review này.
> **Phạm vi:** chỉ **đọc**. Tạo đánh giá là UC-45, gỡ đánh giá là UC-57.

## 1. Luồng chạy

```
Trang /centers/{id}  (StudyCenterDetailView)
  └─ hook useCenterReviews(centerId)
       └─ studyCentersApi.getReviews(id)  ->  GET /api/v1/study-centers/{id}/reviews
            └─ StudyCentersController.GetReviews
                 ├─ service.GetByIdAsync(id)           (kiểm tra trung tâm tồn tại + quyền xem)
                 └─ ICenterReviewRepository.GetVisibleAsync(id, page, pageSize)
                      └─ bảng app.center_reviews JOIN app.users
```

## 2. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Domain/Entities/CenterReview.cs` | `CenterReview` | Entity một đánh giá: `StudyCenterId`, `UserId`, `LeadId?`, `Rating` (1-5), `Comment?`, `IsVerified`, `Status` (`visible` / `hidden` / `removed`). Kế thừa `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`). |
| `Application/StudyCenters/ICenterReviewRepository.cs` | `CenterReviewDto` | 1 đánh giá công khai: `Id`, `ReviewerName` (đã che), `ReviewerRole` (`student`/`parent`), `IsVerified`, `CreatedAt`, `Rating`, `Comment`. |
| | `CenterReviewsDto` | Gói trả về: `TotalCount`, `AverageRating` (null nếu chưa có), `Items`. |
| | `ICenterReviewRepository.GetVisibleAsync` | Hợp đồng lấy một trang đánh giá đang hiển thị của một trung tâm, kèm tổng số và điểm trung bình toàn bộ. |
| `Infrastructure/.../Repositories/CenterReviewRepository.cs` | `GetVisibleAsync(centerId, page, pageSize)` | Xem mục 4. |
| | `MaskName(fullName)` | Che tên người đánh giá: giữ từ đầu, các từ sau chỉ lấy chữ cái đầu. `"Nguyễn Văn An"` thành `"Nguyễn V. A."`. Tên trống thành `"Người dùng ẩn danh"`. |
| `Api/Controllers/StudyCentersController.cs` | `GetReviews(id)` | Endpoint `GET /{id}/reviews?page=1&pageSize=20`, **không cần đăng nhập**. Trả 404 nếu trung tâm không tồn tại, hoặc chưa xác minh / ngừng hoạt động mà người gọi không phải Moderator. |
| `Infrastructure/.../Repositories/StudyCenterRepository.cs` | `ApplyLiveReviewStatsAsync` | Gán `ReviewCount` và `AverageRating` cho danh sách / chi tiết trung tâm bằng cách tính từ `center_reviews` (chỉ `visible`), không dùng cột lưu sẵn. Nhờ đó số sao ở danh sách luôn khớp với trang chi tiết. |

## 3. Lưu vào DB như thế nào

Bảng `app.center_reviews` (cấu hình ở `CenterReviewConfiguration.cs`):

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | uuid, PK | |
| `study_center_id` | uuid, FK `study_centers` | `ON DELETE CASCADE`, có index |
| `user_id` | uuid, FK `users` | `ON DELETE CASCADE`, có index |
| `lead_id` | uuid, null, FK `leads` | `ON DELETE SET NULL`. Gắn với yêu cầu liên hệ nếu có, dùng để đánh dấu "đã xác thực" |
| `rating` | integer | CHECK `rating >= 1 AND rating <= 5` |
| `comment` | varchar(2000), null | |
| `is_verified` | boolean | Người đánh giá đã thực sự liên hệ trung tâm |
| `status` | varchar(32), mặc định `visible` | `visible` / `hidden` / `removed`. UC-57 sẽ đổi giá trị này |
| `created_at`, `updated_at` | timestamptz | |

UC-41 **không ghi** gì vào DB, chỉ đọc.

## 4. Chi tiết `GetVisibleAsync(centerId, page, pageSize)`

1. Chuẩn hóa tham số bằng `CenterReviewPaging.Normalize`: `page >= 1`, `pageSize` trong khoảng 1..50 (mặc định 20).
2. Lọc `StudyCenterId == centerId && Status == "visible"` (đánh giá `hidden` / `removed` không bao giờ lọt ra ngoài).
3. **Một** truy vấn `GroupBy` lấy cả `Count` và `Average`, nên hai số liệu luôn cùng một thời điểm. Nếu không có đánh giá thì trả `CenterReviewsDto(0, null, [])`.
4. Điểm trung bình làm tròn bằng `CenterRatingMath.Round` (2 chữ số, nửa thì làm tròn lên).
5. JOIN `users` lấy tên và vai trò, sắp xếp `CreatedAt` giảm dần rồi `Id` giảm dần (tie-breaker để phân trang không lặp hoặc sót), `Skip((page-1)*pageSize).Take(pageSize)`.
6. Map sang DTO: che tên bằng `MaskName`. Vai trò chỉ nhận `parent` hoặc `student`, mọi role khác coi là `student`.

Thiết kế đáng lưu ý:
- `TotalCount` và `AverageRating` tính trên **toàn bộ** đánh giá hiển thị, `Items` chỉ là một trang. FE dùng `items.length < totalCount` để biết còn trang tiếp.
- Email và id người dùng không bao giờ được trả ra.
- Điểm trung bình luôn đi qua `CenterRatingMath.Round`, cả ở trang chi tiết lẫn `StudyCenterRepository.ApplyLiveReviewStatsAsync` (danh sách), nên hai nơi cho cùng một số. Trước đây một nơi làm tròn banker's 1 chữ số, nơi kia làm tròn 2 chữ số rồi FE `toFixed(1)`, nên cùng trung tâm có thể hiện 4,2 và 4,3.

## 5. Frontend

| File | Chức năng |
|---|---|
| `types/api.ts` | `CenterReviewDto`, `CenterReviewsDto` khớp với DTO backend. |
| `lib/api.ts` | `studyCentersApi.getReviews(id, page = 1, pageSize = 20)` gọi endpoint ở trên. |
| `lib/rating.ts` | `formatRating(value)`: định dạng điểm 1 chữ số thập phân, nửa thì làm tròn lên (4,25 thành 4,3). Dùng ở danh sách công khai, danh sách quản trị, hai trang chi tiết. |
| `hooks/use-center-reviews.ts` | Hook tải đánh giá có phân trang. State gắn với `centerId` nên đổi trung tâm thì dữ liệu cũ biến mất và trở về `loading`. `loadMore()` tải trang kế, bỏ trùng theo `id`, ngừng đề nghị "xem thêm" nếu trang mới không có bản ghi nào mới. Trả về `reviews`, `loading`, `error`, `loadingMore`, `loadMoreError`, `hasMore`, `loadMore`, `total`, `average`. |
| `components/centers/CenterReviewList.tsx` | Vẽ danh sách: tên đã che, vai trò, huy hiệu "Đã xác thực", ngày, nội dung. `StarRating` luôn vẽ đủ 5 ngôi (sao đầy theo điểm, phần còn lại là sao rỗng màu xám). Có các trạng thái: đang tải, lỗi, rỗng, có dữ liệu, và nút "Xem thêm (n)" kèm thông báo lỗi khi tải thêm thất bại. |
| `components/centers/StudyCenterDetailView.tsx`, `components/admin/centers/AdminCenterDetailView.tsx` | Dùng hook và truyền các prop phân trang cho `CenterReviewList`. |

## 6. Test

- `CenterReviewRepositoryTests.cs`: 8 test cho `MaskName` (giữ họ, rút gọn các từ còn lại, bỏ khoảng trắng thừa, tên trống thành "Người dùng ẩn danh", không lộ tên riêng).
- `CenterReviewRulesTests.cs`: `CenterReviewPaging.Normalize` (page nhỏ hơn 1, pageSize ngoài khoảng) và `CenterRatingMath.Round` (4,125 thành 4,13 chứ không phải 4,12 như banker's).

Chưa test trực tiếp `GetVisibleAsync` vì project test chưa có provider DB trong bộ nhớ (InMemory / SQLite), cần test tích hợp với PostgreSQL. Frontend chưa có test runner, được kiểm bằng `tsc` và `eslint`.

## 7. Giới hạn hiện tại và việc cho UC sau

- Chưa lọc theo số sao.
- Chỉ role Moderator được xem đánh giá của trung tâm chưa xác minh. Admin không còn quản lý trung tâm nên không cần xử lý.
- Moderator gọi endpoint này vẫn chỉ thấy đánh giá `visible`. UC-57 cần endpoint riêng cho moderator thấy cả `hidden` / `removed`.
- Chưa có cơ chế chặn một người đánh giá nhiều lần. Đây là việc của UC-45.
- Chưa có index kết hợp `(study_center_id, status, created_at)`, hiện chỉ có index đơn trên `study_center_id`.
