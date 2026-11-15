# Kế hoạch: Nhắn tin với trung tâm trong yêu cầu liên hệ (FE-08 · Message Center in Contact Request)

Phạm vi: **Optional**, không nằm trong sprint nào · Mã UC: **chưa rõ** · Người làm: **chưa giao** · Actor: Student (từ 16 tuổi); Parent

> Ngày 12/11 Việt đã duyệt các đề xuất câu 2–10. Câu 1 (mã UC, người làm) chờ nhóm. Việt cho phép sửa file lead của Đức cho UC này. Đã xong phần 1 (lỗi lead đã hoàn tất: `lead_closed`, 400) và phần 2 (lỗi trả `{ code, message }`: `not_found` 404, `invalid_content` 400; đã thử bằng curl trên API 5080) và phần 3 (kiểu tin nhắn import lại từ `types/api.ts` của Đức vì đã khớp backend; `types/lead.ts` chỉ thêm 3 mã lỗi) và phần 4 (`SentLeadMessagesDialog` mở từ nút "Tin nhắn" trên thẻ UC-44; tsc, eslint sạch) và phần 5 (kiểm tra tay ngày 12/11, kết quả ở cuối file).

## Mục tiêu

Học sinh và phụ huynh **gửi và đọc tin nhắn** với trung tâm ngay trong một yêu cầu liên hệ của mình. Tin có số điện thoại hoặc quảng cáo (link ngoài, hotline…) thì **bị gắn cờ**.

Chỗ mở: nút **"Tin nhắn"** trên mỗi yêu cầu trong thẻ "Yêu cầu liên hệ đã gửi" (UC-44, `SentLeadsCard`). Đây đúng là chỗ kế hoạch UC-44 đã để dành.

## Hiện trạng (kiểm tra ngày 12/11)

Phía **trung tâm** đã có đủ (UC-74 của Đức). Phía **học sinh và phụ huynh chưa có gì**.

Có sẵn để dùng lại, không cần viết lại:

| Thứ | Ở đâu | Dùng thế nào |
|---|---|---|
| Bảng `LeadMessage` | `Domain/Entities/LeadMessage.cs` | Đã có `SenderUserId`, `IsFlagged`, `FlagReason`, `Status` (`visible` / `removed`). Comment trong entity đã nhắc UC-44 |
| DTO tin nhắn | `LeadDtos.cs`: `LeadMessageDto`, `LeadMessagesThreadDto`, `SendLeadMessageRequest`, `SendLeadMessageResultDto` | Trả về y như phía trung tâm |
| Đọc và lưu tin | `ILeadRepository.GetLeadMessagesAsync`, `AddLeadMessageAsync` | Gọi lại, không thêm truy vấn mới |
| Lọc nội dung | `LeadMessageContentFilter.Inspect` | Trả `IsFlagged`, `FlagReason` (SĐT Việt Nam, hotline, SĐT quốc tế, link ngoài…) |
| Hiện tên người gửi | `LeadMessageMapper` | Trung tâm → tên trung tâm; kiểm duyệt viên / quản trị viên → nhãn vai trò; tin bị gỡ → "Tin nhắn đã bị gỡ do vi phạm quy tắc." |
| Kiểm tra quyền xem lead | Luật của UC-44 `GetSentLeadsAsync` | Học sinh từ 16 tuổi sở hữu `lead.UserId`; phụ huynh là người bấm gửi hoặc đang liên kết với con |
| Cảnh báo sớm ở frontend | `frontend/src/lib/lead-message-filter.ts` (Đức) | Báo trước khi gửi nếu có SĐT / link. `MAX_LEAD_MESSAGE_LENGTH = 4000` |

Cách phía trung tâm đang làm (`LeadService.SendLeadMessageForCenterUserAsync`), để làm giống:
1. Kiểm tra quyền với lead (chống IDOR).
2. Cắt khoảng trắng; rỗng hoặc dài hơn 4000 ký tự thì lỗi.
3. `LeadMessageContentFilter.Inspect` để gắn cờ. **Tin vẫn được lưu và hiện**, chỉ đánh dấu.
4. Nếu lead đang `new` thì tự đổi sang `contacted`.
5. Ghi audit log `lead_message.send` hoặc `lead_message.send_flagged`.

## Câu hỏi và chốt

Câu 2–10: Việt duyệt theo đề xuất (12/11). Câu 1 còn mở.

| # | Câu hỏi | Chốt |
|---|---|---|
| 1 | Mã UC là gì? Ai làm? | Hỏi nhóm / thầy |
| 2 | Học sinh hoặc phụ huynh nhắn thì **có đổi trạng thái lead** không? | **Không đổi.** Trạng thái là việc của trung tâm. `new` → `contacted` chỉ khi trung tâm nhắn |
| 3 | Học sinh nhắn trước khi trung tâm nhắn tin đầu tiên được không? | **Được.** Lời nhắn lúc gửi lead cũng là tin đầu tiên của người gửi |
| 4 | Phụ huynh được nhắn trong lead **con tự gửi** không? Học sinh được nhắn trong lead **phụ huynh gửi thay** không? | **Cả hai được** đọc và nhắn, theo đúng quyền xem của UC-44. Tin hiện tên thật người gửi (phụ huynh hay học sinh) cho trung tâm |
| 5 | Lead `closed` thì còn nhắn được không? | **Chỉ đọc, không gửi.** Hiện câu "Yêu cầu đã hoàn tất, không gửi thêm tin được". Lưu ý: phía trung tâm (UC-74) hiện **không chặn** lead `closed`. Nếu chặn phía học sinh thì nên báo Đức chặn cả phía trung tâm cho giống |
| 6 | Tin bị gắn cờ thì **hiện thế nào cho người gửi**? | Vẫn hiện, kèm nhãn nhỏ "Đã gắn cờ: có số điện thoại". Kiểm duyệt viên xử lý sau (UC-58) |
| 7 | Học sinh dưới 16 có lead phụ huynh gửi thay | **Không xem, không nhắn** (giống UC-44). Chỉ phụ huynh nhắn |
| 8 | Tin của người gửi đang hiện thành "ẩn danh" khi không chia sẻ nhóm Liên hệ. Phía học sinh thì sao? | Phía học sinh hiện **"Bạn"** cho tin của mình, tên thật cho tin phụ huynh / con. Phía trung tâm giữ nguyên luật ẩn danh của Đức |
| 9 | Có cần **đếm tin chưa đọc** không? | **Không làm** ở bản này (cần thêm cột "đã đọc" → migration). Giữ "Trung tâm đã nhắn N tin" của UC-44 |
| 10 | Có cần tự làm mới (polling / realtime) không? | **Không.** Mở khung thì tải; gửi xong thì thêm tin vào cuối; có nút "Tải lại" |

## Các phần

Phần 1 và 2 sửa file của Đức (`LeadService`, `ILeadService`, `LeadsController`, test lead). Ở UC-44 Việt đã được phép sửa các file này. **Cần hỏi lại khi làm UC này**, vì đây là UC khác.

1. **Service** (`ILeadService`, `LeadService`)
   - Tách phần kiểm tra quyền của UC-44 thành hàm riêng `GetAuthorizedSentLeadAsync(userId, leadId)`:
     - không thấy lead hoặc không có quyền → 404 (không lộ lead của người khác);
     - học sinh dưới 16 → 403 `age_restricted`;
     - role khác → 403 `role_not_allowed`.
   - `GetSentLeadMessagesAsync(userId, leadId)`: gọi `GetLeadMessagesAsync`, trả `LeadMessagesThreadDto`.
   - `SendSentLeadMessageAsync(userId, leadId, content)`: các bước 2, 3, 5 như phía trung tâm. Bước 4 theo câu 2. Lead `closed` theo câu 5.
   - Test trong `LeadServiceTests`:
     - học sinh 18 đọc và gửi được trong lead của mình;
     - tin có SĐT thì `IsFlagged = true`;
     - học sinh 15 bị chặn;
     - phụ huynh nhắn trong lead mình gửi thay và lead con liên kết tự gửi;
     - người không liên quan (học sinh khác, phụ huynh không liên kết, liên kết đã hủy) → 404;
     - nội dung rỗng / quá 4000 ký tự → lỗi;
     - lead `closed` → không gửi được (nếu chốt câu 5).
2. **Controller** (`LeadsController`)
   - `GET /api/v1/leads/sent/{id}/messages`, `POST /api/v1/leads/sent/{id}/messages`, `[Authorize(Roles = Student, Parent)]`.
   - Lỗi trả `{ code, message }` như `GET /leads/sent`.
   - Test 200 / 201 / 403 / 404 trong `LeadsControllerTests`.
3. **Frontend kiểu và API** (file của Việt: `types/lead.ts`, `lib/lead-api.ts`)
   - Thêm kiểu `LeadMessageDto`, `LeadMessagesThreadDto`, `SendLeadMessageResultDto` vào `types/lead.ts`. Không sửa `types/api.ts` của Đức. Nếu Đức đã có kiểu này ở file khác thì import lại, không khai báo trùng.
   - `leadApi.listSentMessages(leadId)`, `leadApi.sendSentMessage(leadId, content)`.
4. **Khung tin nhắn** (component mới `components/account/SentLeadMessagesDialog.tsx`)
   - Mở từ nút "Tin nhắn" trên mỗi `SentLeadItem` trong `SentLeadsCard`.
   - Danh sách tin theo thời gian: tin của trung tâm bên trái, tin của mình bên phải; tin bị gỡ hiện câu thay thế; tin bị gắn cờ có nhãn nhỏ.
   - Ô nhập + nút Gửi; đếm ký tự trên 4000; dùng `lead-message-filter.ts` để cảnh báo trước khi gửi ("Tin có số điện thoại sẽ bị gắn cờ").
   - Gửi xong thì cập nhật số tin trên thẻ UC-44.
5. **Kiểm tra tay** với tài khoản test UC-42:
   - s18 mở tin nhắn lead Demo Center Hà Nội, thấy tin trung tâm đã nhắn ở phần 6 UC-44, trả lời 1 tin;
   - đăng nhập tài khoản trung tâm, thấy tin trả lời;
   - s18 gửi tin có số điện thoại → bị gắn cờ, hai bên đều thấy nhãn;
   - phụ huynh nhắn trong lead gửi thay s15;
   - s15 bị chặn; s16 không mở được lead của s18 (404).

## Ước tính

Khoảng 5–6 giờ: service và test 2 giờ, controller và test 1 giờ, frontend 2–3 giờ, kiểm tra tay 0.5 giờ.

## Kết quả kiểm tra tay (phần 5, ngày 12/11)

Frontend localhost:3000, API 5080, tài khoản test UC-42.

| # | Bước | Kết quả |
|---|---|---|
| 1 | s18 mở "Tin nhắn" lead Demo Center Hà Nội | Đạt. Thấy tin trung tâm (bên trái) và tin của mình hiện "Bạn" (bên phải); tin có SĐT có nhãn "Đã gắn cờ: Chứa số điện thoại liên hệ" |
| 2 | s18 gõ tin có link | Đạt. Hiện cảnh báo vàng trước khi gửi (không gửi tin này) |
| 3 | s18 gửi tin thường bằng Ctrl+Enter | Đạt. Tin thêm vào cuối khung, ô nhập được xóa |
| 4 | Trung tâm đọc luồng tin (curl `/leads/received/{id}/messages`) | Đạt. Thấy tin mới của s18, tin có SĐT `isFlagged = true` |
| 5 | Phụ huynh mở và gửi tin trong lead gửi thay s15 | Đạt. Gửi được; thẻ vẫn "Mới gửi", "Trung tâm chưa nhắn tin" (câu 2) |
| 6 | s15 mở tab "Yêu cầu liên hệ đã gửi" | Đạt. Hiện thông báo dưới 16 tuổi, không có nút "Tin nhắn" |
| 7 | s16 mở lead của s18 | Đạt (curl ở phần 2): 404 |
| 8 | Lead `closed` | Chưa thử tay: DB chưa có lead đã hoàn tất. Đã có unit test |
