# UC-91 (USAS-2119): Xác thực email và số điện thoại trung tâm

**Jira:** Tác nhân Trung tâm.

Luồng chính: (1) Nhập mã 6 chữ số gửi qua email; đúng thì tài khoản được kích hoạt và trung tâm ở trạng thái Chưa xác minh. (2) Chọn xác thực số điện thoại; hệ thống gửi OTP qua SMS. (3) Nhập OTP; đúng thì số điện thoại được ghi là đã xác thực.

Luồng thay thế: mã sai thì cho nhập lại; sai quá số lần cho phép thì mã bị vô hiệu và phải xin mã mới. Mã hết hạn thì cho gửi lại, có giới hạn số lần và khoảng cách giữa hai lần gửi.

Quy tắc: phải xác thực cả email và số điện thoại trước khi nộp hồ sơ xác minh (UC-71). Đổi email hoặc số điện thoại sau này cũng đi qua UC này. Việc xác thực không tự động cấp dấu xác minh.

## 1. Đối chiếu Jira và code

| Yêu cầu Jira | Trạng thái |
|---|---|
| Mã 6 chữ số qua email khi đăng ký, đúng thì kích hoạt tài khoản, trung tâm Chưa xác minh | Có sẵn |
| Đổi email qua OTP gửi tới email mới | Có sẵn |
| Mã sai nhập lại, sai quá số lần thì vô hiệu, hết hạn, giới hạn gửi lại | Có sẵn cho email, **dùng lại cho số điện thoại** |
| Gửi OTP qua SMS để xác thực số điện thoại | **Bổ sung luồng**, nhưng chưa có nhà cung cấp SMS thật (mục 6) |
| Nhập OTP, đúng thì số điện thoại được ghi là đã xác thực | **Bổ sung** |
| Đổi số điện thoại cũng đi qua UC này | **Bổ sung**: đổi số thì trạng thái xác thực bị xóa, phải xác thực lại |
| Phải xác thực cả email và số điện thoại trước khi nộp hồ sơ | Email có sẵn. Số điện thoại **bổ sung có điều kiện** (mục 6) |
| Xác thực không tự động cấp dấu xác minh | Đúng |

## 2. Luồng chạy (phần số điện thoại)

```
Trang /center/profile, thẻ Thông tin liên lạc (PhoneVerificationRow)
  ├─ POST /api/v1/center/contact/phone/verification/request
  │    └─ AuthService.RequestCenterPhoneVerificationAsync
  │         ├─ kiểm tra giới hạn gửi (60 giây, 5 lần mỗi giờ)
  │         ├─ INSERT otp_tokens (purpose = verify_phone, mã gắn với số điện thoại)
  │         └─ ISmsSender.SendOtpAsync
  └─ POST /api/v1/center/contact/phone/verification/confirm  { code }
       └─ AuthService.ConfirmCenterPhoneVerificationAsync
            ├─ TryConsumeOtpAsync (đếm lần sai, hết hạn, dùng một lần)
            └─ UPDATE study_centers SET phone_verified_at = now
```

## 3. Backend

| File | Thành phần | Chức năng |
|---|---|---|
| `Application/Auth/ISmsSender.cs` | `ISmsSender` | Kênh gửi OTP qua SMS. `CanDeliver` cho biết tin có tới được người nhận không. |
| `Infrastructure/Auth/LoggingSmsSender.cs` | `LoggingSmsSender` | Bộ gửi tạm khi chưa có nhà cung cấp SMS. Ở môi trường phát triển ghi mã ra log; ngoài môi trường phát triển `CanDeliver = false`. |
| `Application/Auth/AuthService.cs` | `RequestCenterPhoneVerificationAsync` | Từ chối nếu trung tâm chưa có số, số đã xác thực, hệ thống chưa gửi được SMS, hoặc gửi quá nhanh / quá nhiều. Vô hiệu mã cũ, tạo mã mới 6 chữ số, lưu băm của mã gắn với số điện thoại, gửi SMS, ghi nhật ký. |
| | `ConfirmCenterPhoneVerificationAsync` | Đối chiếu mã. Đúng thì ghi `PhoneVerifiedAt`. Sai thì được nhập lại; sai đủ 5 lần hoặc quá 5 phút thì mã vô hiệu. |
| | `ChangeCenterPhoneAsync` | Sửa: đổi số thì xóa `PhoneVerifiedAt` và vô hiệu các mã đang chờ, vì số mới chưa được xác thực. |
| `Application/StudyCenters/StudyCenterService.cs` | `SubmitVerificationAsync` | Thêm điều kiện số điện thoại đã xác thực trước khi nộp hồ sơ, chỉ áp dụng khi `ISmsSender.CanDeliver` là true. |
| `Api/Controllers/CenterPortalController.cs` | `RequestPhoneVerification`, `ConfirmPhoneVerification` | Hai endpoint mới, role `center`. |
| `Domain/Constants/OtpPurposes.cs` | `VerifyPhone` | Mục đích OTP mới `verify_phone`. |

Các giới hạn dùng chung với OTP email, khai báo trong `AuthService`: mã hiệu lực 5 phút (`OtpExpiryMinutes`), tối đa 5 lần nhập sai (`MaxOtpAttempts`), hai lần gửi cách nhau ít nhất 60 giây (`MinSecondsBetweenOtpRequests`), tối đa 5 lần gửi mỗi giờ (`MaxOtpRequestsPerHour`).

## 4. Lưu vào DB như thế nào

- `otp_tokens`: mỗi lần gửi mã thêm một dòng `purpose = verify_phone`, lưu **băm** của chuỗi "mã|số điện thoại" (không lưu mã gốc), `expires_at`, `attempts`. Vì mã gắn với số điện thoại, mã gửi cho số cũ không dùng được sau khi đổi số.
- `study_centers.phone_verified_at`: ghi thời điểm xác thực thành công; bị xóa về null khi đổi số điện thoại. Cột này đã có sẵn, không cần migration.
- `audit_logs`: `center.phone_verification_requested`, `center.phone_verified`, và `center.phone_changed` (có sẵn).

## 5. Frontend

| File | Chức năng |
|---|---|
| `components/center/PhoneVerificationRow.tsx` | Dưới dòng số điện thoại. Số đã xác thực thì hiện huy hiệu. Chưa xác thực thì có nút "Xác thực số điện thoại", sau đó là ô nhập mã 6 chữ số, nút xác nhận và nút gửi lại có đếm ngược 60 giây. Hiện thông báo lỗi từ backend (mã sai, hết hạn, hệ thống chưa gửi được SMS). |
| `components/center/CenterContactCard.tsx` | Nhận thêm `phoneVerified`, gắn khối trên. |
| `components/center/CenterProfileView.tsx` | Truyền `phoneVerified` từ `center.phoneVerifiedAt`. |
| `lib/api.ts` | `centerPortalApi.requestPhoneVerification`, `confirmPhoneVerification`. |

## 6. Điểm quan trọng: chưa có nhà cung cấp SMS

Hệ thống chưa tích hợp dịch vụ gửi SMS, và code trước đây ghi rõ "không còn xác thực SMS; email mới là kênh xác thực". Điều này ngược với Jira. Bản này dựng đủ luồng theo Jira nhưng giữ an toàn cho hành vi hiện tại:

- **Môi trường phát triển:** `LoggingSmsSender` ghi mã OTP ra log của backend, dùng để thử luồng. Quy tắc "phải xác thực số điện thoại trước khi nộp hồ sơ" được áp dụng.
- **Môi trường chạy thật khi chưa có nhà cung cấp SMS:** `CanDeliver = false`. Bấm xác thực sẽ nhận thông báo "Hệ thống chưa gửi được SMS", và quy tắc bắt buộc số điện thoại **không** được áp dụng, nên trung tâm vẫn nộp hồ sơ được như trước. Nếu áp quy tắc ở đây thì không trung tâm nào nộp được hồ sơ.
- **Khi có nhà cung cấp SMS:** viết một lớp cài đặt `ISmsSender` gọi dịch vụ đó, `CanDeliver = true`, rồi đăng ký thay cho `LoggingSmsSender` trong `Infrastructure/DependencyInjection.cs`. Luồng và quy tắc tự hoạt động đầy đủ, không cần sửa chỗ khác.

**Cần nhóm quyết định:** có tích hợp nhà cung cấp SMS (tốn phí theo tin nhắn) để làm đúng Jira, hay giữ quyết định cũ là chỉ xác thực email và sửa lại mô tả Jira.

## 7. Test

`CenterRegistrationAndVerificationTests.cs` thêm: gửi OTP qua SMS rồi nhập đúng thì số được ghi đã xác thực (và không cấp dấu xác minh), mã sai được nhập lại nhưng sai đủ số lần thì mã vô hiệu, mã hết hạn bị từ chối, gửi lại ngay bị chặn, không khả dụng khi chưa gửi được SMS / chưa có số / đã xác thực, đổi số thì xóa trạng thái xác thực và mã cũ không dùng được, quy tắc nộp hồ sơ chỉ áp dụng khi gửi được SMS.

Toàn bộ 2415 test backend pass. Frontend kiểm bằng `tsc` và `eslint`, chưa chạy thử trên trình duyệt.

## 8. Giới hạn

- Số điện thoại của các trung tâm đã đăng ký trước đây đều ở trạng thái chưa xác thực.
- Khi gộp nhánh này với nhánh UC-110, constructor của `StudyCenterService` sẽ có conflict nhỏ vì cả hai nhánh cùng thêm tham số tùy chọn ở cuối. Giữ cả hai tham số là xong.
