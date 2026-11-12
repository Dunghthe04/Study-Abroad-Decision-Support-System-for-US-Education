# Kế hoạch UC-44: Xem yêu cầu liên hệ đã gửi (FE-08 · View Sent Contact Requests)

Sprint v0.5 (20/11 – 26/11/2026) · Người làm: Việt · Ước tính 3.5 giờ · Actor: Student (từ 16 tuổi); Parent

> Việt đã duyệt đề xuất câu 3 và câu 8 (12/11). Đã xong phần 1 và 2.

## Mục tiêu

Trong trang tài khoản (`/account`) có thêm một thẻ **"Yêu cầu liên hệ đã gửi"**. Thẻ liệt kê các yêu cầu đã gửi. Mỗi yêu cầu hiện:
- trung tâm;
- ngày gửi;
- trạng thái;
- tóm tắt dữ liệu đã chia sẻ;
- số tin nhắn trung tâm đã gửi.

## Hiện trạng (kiểm tra ngày 12/11, cả `HEAD` và `origin/develop`)

- **Chưa có API danh sách.** Kế hoạch UC-42 có nhắc `GET /leads/my`, nhưng trong code không có. Hiện chỉ có `GET /api/v1/leads/{id}` để xem một lead.
- **Tin nhắn mới chỉ có phía trung tâm** (`/leads/received/{id}/messages`, UC-74). Học sinh và phụ huynh chưa có API tin nhắn.
- Có sẵn để dùng lại:
  - `LeadDto`: tên trung tâm, trạng thái, ngày gửi, nhóm dữ liệu, người bấm gửi;
  - `LEAD_SHARED_FIELD_INFO`: nhãn từng nhóm;
  - `leadApi`;
  - `IParentLinkRepository.GetActiveByParentAsync`: danh sách con đang liên kết;
  - `useAgeBlock(AGE_FEATURES.shareWithCenter)`.

## Đã chốt (Việt trả lời ngày 12/11)

| Câu | Chốt |
|---|---|
| Mã UC | **UC-44** |
| File của Đức (`LeadDtos`, `ILeadRepository`, `LeadRepository`, `LeadService`, `LeadsController`, test lead) | Sửa luôn, không cần hỏi lại |
| Phụ huynh thấy gì | **Cả hai loại**: lead phụ huynh bấm gửi, và lead do con **đang liên kết** tự gửi |
| Học sinh thấy lead phụ huynh gửi thay mình | **Có**. Lọc theo `Lead.UserId = học sinh`, nên tự có cả lead phụ huynh gửi |
| Chặn tuổi | Theo tài liệu "Quy định độ tuổi" (Phanh, 09/11). **Học sinh dưới 16 không xem được**. Phụ huynh xem bình thường. Dùng lại luật `share_with_center` (mốc 16), vì tài liệu dùng cùng mốc cho gửi và xem |
| Vị trí | **Một thẻ** trong `/account` (`AccountView.tsx`, file của Việt), không làm trang riêng |

## Đề xuất cho câu còn mở

### Câu 3: phần "liên kết tới tin nhắn"

Đúng, UC-44 là **xem yêu cầu đã gửi**. Câu "liên kết tới tin nhắn" trong mô tả FE-08 nghĩa là từ yêu cầu đã gửi phải đi được tới cuộc trao đổi với trung tâm. Hiện phía học sinh chưa có màn tin nhắn nào để dẫn tới.

**Đề xuất:**
- Mỗi yêu cầu hiện **số tin nhắn trung tâm đã gửi**, ví dụ "Trung tâm đã nhắn 2 tin". Chỉ cần đếm, không cần API mới.
- **Không làm** nút mở khung tin nhắn. Khi có chức năng nhắn tin phía học sinh thì thêm nút "Tin nhắn" vào đúng chỗ đó.
- Ghi chú lại để báo nhóm: chức năng nhắn tin phía học sinh và phụ huynh (đọc và trả lời) **chưa có ai làm**.

### Câu 8: tóm tắt dữ liệu đã chia sẻ

**Đề xuất: hiện tên nhóm đã chọn kèm mô tả ngắn, không hiện giá trị.**

Ví dụ: "Đã chia sẻ: Liên hệ (Họ tên, email) · Mục tiêu (Bậc học muốn học, ngành dự định)". Thêm 3 ô người gửi tự nhập lúc gửi: số điện thoại, bậc học, lời nhắn.

Lý do:
- Hệ thống **không lưu bản chụp hồ sơ lúc gửi**. Trung tâm đọc hồ sơ hiện tại. Nếu hiện GPA, IELTS… thì đó là số hôm nay, không phải số lúc gửi, dễ hiểu nhầm.
- Số điện thoại, bậc học, lời nhắn **được lưu trên lead**, nên hiện đúng như lúc gửi.
- Dùng lại `LEAD_SHARED_FIELD_INFO`, không cần thêm truy vấn hồ sơ. Vừa với 3.5 giờ.

## Các phần

1. **DTO và repository** (`LeadDtos.cs`, `ILeadRepository.cs`, `LeadRepository.cs`)
   - Thêm `SentLeadDto` gồm:
     - `Id`, `StudyCenterId`, `StudyCenterName`, `Status`, `CreatedAt`;
     - `SharedFields`, `StudyLevel`, `ContactPhone`, `Message`;
     - `StudentUserId`, `StudentName` (để phụ huynh biết lead của con nào);
     - `SubmittedByParent` (bool, để hiện "Phụ huynh gửi thay");
     - `CenterMessageCount`: số tin `visible` do phía trung tâm gửi (người gửi không phải học sinh của lead, cũng không phải phụ huynh đã bấm gửi).
   - Thêm `GetSentLeadsAsync(IReadOnlyCollection<Guid> studentIds, Guid? submitterId, int page, int pageSize)`:
     - lấy lead có `UserId` thuộc `studentIds` **hoặc** `SubmittedByUserId = submitterId`;
     - mới nhất trước, có phân trang;
     - trả `PagedResult<SentLeadDto>` hoặc kiểu tương tự kiểu đang dùng ở UC-73.
2. **Service** (`LeadService.GetSentLeadsAsync(userId, page, pageSize)`)
   - Lấy role và ngày sinh qua `IUserRepository` (đã thêm ở UC-42).
   - **Student**:
     - dưới 16 thì 403 `age_restricted` (`AgeFeatures.Check(ShareWithCenter, …)`);
     - từ 16 thì `studentIds = [userId]`.
   - **Parent**: `studentIds` = các con đang liên kết (`GetActiveByParentAsync`), `submitterId = userId`.
   - Role khác: 403 `role_not_allowed`.
   - Test trong `LeadServiceTests`:
     - học sinh 18 thấy lead của mình, cả lead phụ huynh gửi thay;
     - học sinh 15 bị chặn;
     - phụ huynh thấy lead mình gửi và lead con liên kết tự gửi;
     - phụ huynh không thấy lead của học sinh không liên kết;
     - liên kết đã hủy thì không thấy lead con tự gửi nữa.
3. **Controller** (`LeadsController`)
   - Thêm `GET /api/v1/leads/sent?page=&pageSize=` với `[Authorize(Roles = Student, Parent)]`.
   - Đổi `LeadRuleException` thành `{ code, message }` như POST.
   - Thêm test 200 và 403 trong `LeadsControllerTests`.
4. **Frontend kiểu và API**: thêm `SentLeadDto` vào `types/lead.ts`, thêm `leadApi.listSent(page)` vào `lib/lead-api.ts`.
5. **Thẻ trong `/account`**: component mới `components/account/SentLeadsCard.tsx`, gắn vào `AccountView.tsx` cho role student và parent.
   - Mỗi dòng gồm:
     - tên trung tâm (link `/centers/{id}`);
     - ngày gửi;
     - nhãn trạng thái (Mới gửi / Đã liên hệ / Đã đóng);
     - với phụ huynh: tên con, và "Phụ huynh gửi thay" hoặc "Con tự gửi";
     - nhóm đã chia sẻ, số điện thoại, bậc học, lời nhắn rút gọn;
     - "Trung tâm đã nhắn N tin".
   - Chưa có yêu cầu nào: câu "Bạn chưa gửi yêu cầu nào" và link sang `/centers`.
   - Học sinh dưới 16: câu báo theo `useAgeBlock`, không gọi API.
   - Có nút "Xem thêm" nếu còn trang sau.
6. **Kiểm tra tay** với tài khoản test UC-42. DB local đã có:
   - s18: 5 lead;
   - s16: 1 lead;
   - phụ huynh: 1 lead gửi thay s15.

   Kiểm tra:
   - s15 bị chặn;
   - s16 và s18 thấy đúng lead;
   - phụ huynh thấy lead gửi thay s15;
   - nhắn 1 tin từ tài khoản trung tâm, rồi kiểm tra số tin tăng.

## Ghi lại để báo nhóm

- Chưa có chức năng nhắn tin phía học sinh và phụ huynh (đọc và trả lời tin trung tâm).
- Học sinh dưới 16 có lead do phụ huynh gửi thay nhưng không tự xem được, đúng theo tài liệu tuổi. Chỉ phụ huynh xem.
