# UC-92 (USAS-2120): Sửa hồ sơ trung tâm

**Jira:** Tác nhân Trung tâm. Trung tâm tự cập nhật hồ sơ hiển thị: tên hiển thị, mô tả, khu vực phục vụ, địa chỉ và chi nhánh, website, fanpage, dịch vụ, bậc học, mức phí công bố, logo. Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu từ trang này; Administrator xử lý ở UC-93.

Quy tắc phân loại theo mức ảnh hưởng: mô tả, dịch vụ và logo lưu ngay; website và fanpage lưu ngay nhưng được đánh dấu để Moderator kiểm tra (UC-114); tên pháp lý, mã số doanh nghiệp và tài liệu đưa hồ sơ về Chờ duyệt và tạm ẩn dấu xác minh; email và số điện thoại đổi qua UC-91.

## 1. Đối chiếu Jira và code

| Yêu cầu Jira | Trạng thái |
|---|---|
| Sửa tên hiển thị, mô tả, địa chỉ, khu vực, dịch vụ, bậc học, mức phí, logo | Có sẵn |
| Trường fanpage | **Bổ sung** (`study_centers.fanpage_url`) |
| Website, fanpage lưu ngay và đánh dấu để Moderator kiểm tra | **Bổ sung** (bảng `center_profile_changes`) |
| Gửi yêu cầu xóa tài khoản và dữ liệu từ trang này | **Bổ sung** (xem `UC93_XOA_TAI_KHOAN_TRUNG_TAM.md`) |
| Thông tin pháp lý | Có sẵn, cách làm khác Jira, xem mục 6 |
| Email, số điện thoại đổi qua UC-91 | Có sẵn, xem `UC91_XAC_THUC_EMAIL_SDT_TRUNG_TAM.md` |

## 2. Luồng chạy

```
Trang /center/profile (CenterProfileView)
  ├─ GET /api/v1/center/profile
  └─ PUT /api/v1/center/profile  { description, website, fanpageUrl, services, ... }
       └─ CenterPortalController.UpdateProfile          [Authorize: center]
            └─ StudyCenterService.UpdateCenterProfileAsync
                 ├─ kiểm tra thông tin pháp lý (khóa khi đã xác minh hoặc đang chờ duyệt)
                 ├─ kiểm tra website / fanpage là đường dẫn http(s)
                 ├─ IStudyCenterRepository.UpdateAsync            -> UPDATE app.study_centers
                 ├─ nhật ký center.profile_updated
                 └─ ICenterProfileChangeService.RecordAsync       -> INSERT / UPDATE app.center_profile_changes
```

## 3. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Application/StudyCenters/StudyCenterService.cs` | `UpdateCenterProfileAsync` | Lưu các trường hồ sơ. Ghi lại website và fanpage trước khi sửa, sau khi lưu thì gọi `RecordAsync` cho từng trường. |
| | `IsValidPublicUrlOrEmpty` | Chấp nhận chuỗi rỗng (xóa giá trị) hoặc đường dẫn tuyệt đối `http` / `https`, tối đa 512 ký tự. Từ chối `javascript:`, `ftp:`, chuỗi không có giao thức. |
| `Application/StudyCenters/CenterProfileChanges.cs` | `CenterProfileChangeService.RecordAsync` | Giá trị không đổi thì không làm gì. Chưa có thay đổi chờ kiểm tra thì tạo mới với giá trị cũ và mới. Đã có thì giữ giá trị cũ ban đầu, chỉ cập nhật giá trị mới. Trung tâm đổi lại đúng giá trị cũ thì chuyển sang `withdrawn`. |
| `Application/StudyCenters/CenterVerificationDtos.cs` | `UpdateCenterProfileRequest` | Thêm `FanpageUrl`. Với `Website` và `FanpageUrl`: null là giữ nguyên, chuỗi rỗng là xóa. |
| `Application/StudyCenters/StudyCenterDtos.cs` | `StudyCenterDto` | Thêm `FanpageUrl`. |

## 4. Lưu vào DB như thế nào

- `app.study_centers`: thêm cột `fanpage_url` (varchar 512). Các trường hồ sơ được ghi đè trực tiếp.
- `app.center_profile_changes` (bảng mới): mỗi dòng là một thay đổi website hoặc fanpage chờ kiểm tra.

| Cột | Ý nghĩa |
|---|---|
| `study_center_id` | Trung tâm. Xóa trung tâm thì xóa theo. |
| `field` | `website` hoặc `fanpage`. |
| `old_value`, `new_value` | Giá trị trước và sau khi sửa. |
| `status` | `pending`, `passed`, `failed`, `withdrawn`. |
| `changed_by_user_id` | Tài khoản trung tâm đã sửa. |
| `reviewed_by_user_id`, `reviewed_at`, `review_reason` | Do UC-114 ghi. |

Migration: `AddCenterProfileChangesAndDeletionRequests`. **Cần chạy migration trước khi dùng.**

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/center/CenterProfileView.tsx` | Tách ô "Website / Fanpage" thành hai ô Website và Fanpage. Ghi chú rằng hai trường này được kiểm duyệt viên kiểm tra sau khi lưu. Để trống ô và lưu thì xóa giá trị (trước đây để trống không xóa được website). Cuối trang có khối yêu cầu xóa tài khoản. |
| `components/center/CenterDeletionRequestCard.tsx` | Gửi, xem trạng thái và rút yêu cầu xóa. |
| `types/api.ts` | Thêm `fanpageUrl`. |

## 6. Khác với Jira

- **Thông tin pháp lý:** Jira ghi sửa tên pháp lý, mã số doanh nghiệp, tài liệu thì hồ sơ về Chờ duyệt và tạm ẩn dấu xác minh. Code hiện tại (có từ trước, theo đặc tả 5.1) **chặn** việc sửa các trường này khi trung tâm đã xác minh hoặc đang chờ duyệt, và yêu cầu trung tâm nộp lại hồ sơ xác minh (UC-71). Kết quả cuối giống nhau (thay đổi pháp lý phải qua duyệt), nhưng dấu xác minh không tự động bị ẩn. Tôi giữ nguyên cách này, cần nhóm xác nhận.
- **Chi nhánh:** Jira nhắc "địa chỉ và chi nhánh". Hồ sơ hiện chỉ có một địa chỉ, chưa có danh sách chi nhánh.

## 7. Test

`CenterProfileChangeAndDeletionTests.cs`: đổi website tạo thay đổi chờ kiểm tra với giá trị cũ và mới, giá trị giống nhau (kể cả khác hoa thường, rỗng và null) không tạo gì, đổi tiếp trước khi kiểm tra thì giữ giá trị gốc, đổi lại giá trị gốc thì rút, và kiểm tra đường dẫn hợp lệ.
