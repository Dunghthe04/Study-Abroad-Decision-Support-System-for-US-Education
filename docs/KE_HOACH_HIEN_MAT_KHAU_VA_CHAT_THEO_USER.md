# Kế hoạch: Nút hiện mật khẩu, khóa chat khi chưa đăng nhập, lịch sử chat theo từng người dùng

Ngày 12/11 · Người làm: Việt · Chỉ sửa frontend, **không sửa backend**

> Ngày 12/11 Việt duyệt các đề xuất A–D. Đã làm xong phần 1–5. tsc và eslint sạch. Kết quả kiểm tra tay ở cuối file.

## Hiện trạng (kiểm tra ngày 12/11)

### 1. Ô mật khẩu

Có 8 ô `type="password"` trong 4 form. Chưa ô nào có nút hiện mật khẩu.

| Form | File | Số ô | Người tạo / sửa gần nhất |
|---|---|---|---|
| Đăng nhập | `components/auth/LoginForm.tsx` | 1 | Việt tạo; Dương thiết kế lại 11/11; Đức sửa chuyển trang |
| Đăng ký | `components/auth/RegisterForm.tsx` | 2 (mật khẩu, nhập lại) | Việt tạo; Dương thiết kế lại 11/11 |
| Quên mật khẩu → đặt mật khẩu mới | `components/auth/ResetPasswordForm.tsx` | 2 | Việt tạo; Dương thiết kế lại 11/11 |
| Đổi mật khẩu | `components/account/ChangePasswordCard.tsx` | 3 (hiện tại, mới, nhập lại) | Việt tạo (UC-06) |

`ForgotPasswordForm.tsx` chỉ có ô email nên không cần nút này.

Đức đã có mẫu nút con mắt trong `admin/centers/CreateCenterAccountButton.tsx`:
- `showPassword` đổi `type` giữa `"text"` và `"password"`;
- nút nằm tuyệt đối bên phải ô, dùng icon `EyeIcon` / `EyeOffIcon`, có `tabIndex={-1}`.

### 2. Chat khi chưa đăng nhập

- **Backend đã chặn sẵn.** `AdvisorController` có `[Authorize]`, chưa đăng nhập thì trả 401.
- **Frontend chưa chặn.** Người chưa đăng nhập vẫn mở được khung, gõ và bấm gửi. Chỉ sau khi gửi mới hiện lỗi "Cần đăng nhập để hỏi tiếp".
- Khung chat có sẵn chỗ chặn dưới 16 tuổi (`ageBlock` trong `AssistantPanel.tsx`). Chỗ chặn này thay ô nhập bằng một dòng thông báo. Có thể làm giống như vậy.

### 3. Lịch sử chat

- **Backend đã đúng.** `ChatService` tìm phiên bằng `sessions.FindAsync(sessionId, userId)`, nên không đọc được phiên của người khác.
- **Frontend sai.**
  - `AssistantProvider.tsx` lưu tin nhắn trong `sessionStorage` với khóa chung `usas_assistant_chat` và `usas_assistant_session`. Khóa này không gắn với người dùng.
  - Lỗi xảy ra như sau: A chat rồi đăng xuất, sau đó B đăng nhập trên cùng tab. B mở khung chat và **thấy toàn bộ tin nhắn của A**.
  - Khi B gửi tin, `sessionId` của A bị gửi lên. Backend không thấy phiên này (không phải của B) nên tạo phiên mới. Dữ liệu không lộ ở backend, nhưng **màn hình vẫn lộ**.
  - Đăng xuất (`AuthContext.logout`) không xóa lịch sử chat.

`AssistantProvider.tsx` và `AssistantPanel.tsx` do Việt tạo; Dũng sửa nhiều phần widget.

## Cách làm

### Phần 1. Component `PasswordInput` (file mới của Việt)

- File mới: `components/auth/PasswordInput.tsx`.
- Bọc `Input` có sẵn. Nhận đủ props của `Input` (id, value, onChange, autoComplete, placeholder…), nên khi thay chỉ cần đổi `Input type="password"` thành `PasswordInput`.
- Nút con mắt theo mẫu của Đức:
  - nằm bên phải trong ô; ô thêm `pr-9` để chữ không bị nút che;
  - `type="button"`, để bấm nút không gửi form;
  - `aria-label` là "Hiện mật khẩu" hoặc "Ẩn mật khẩu", và có `aria-pressed`.
- Mỗi ô có nút riêng. Hiện ô này không làm hiện ô "nhập lại".
- **Câu hỏi A:** Nút có nhận Tab không?
  - Đức để `tabIndex={-1}`, tức là bấm Tab sẽ bỏ qua nút.
  - Đề xuất: **để nút nhận Tab**, để người dùng bàn phím vẫn bấm được.

### Phần 2. Thay vào 4 form

- Thay 8 ô `type="password"` trong 4 file ở bảng trên.
- Chỉ đổi tên component, giữ nguyên các props khác và giao diện của Dương.
- **Câu hỏi B:** Dương vừa thiết kế lại `LoginForm`, `RegisterForm` và `ResetPasswordForm` ngày 11/11. Việt có muốn báo hoặc hỏi Dương trước khi sửa không? Mỗi file chỉ đổi 1–2 dòng.

### Phần 3. Khóa chat khi chưa đăng nhập (`AssistantPanel.tsx`)

- Lấy `user` và `isLoading` từ `useAuth()`.
- Khi đã tải xong mà chưa đăng nhập (`!isLoading && !user`):
  - ô nhập được thay bằng hộp thông báo giống `ageBlock`: "Bạn cần đăng nhập để hỏi trợ lý." kèm nút **Đăng nhập** (`/login?redirect=<trang hiện tại>`);
  - ẩn chip gợi ý và nút trả lời nhanh, vì bấm vào sẽ gửi tin;
  - lời chào vẫn hiện.
- Trong `AssistantProvider`, `send`, `sendAction` và `openPanel(question)` không gửi gì khi chưa đăng nhập. Một số nút trên trang gọi thẳng `sendAction`, nên việc chặn ô nhập chưa đủ.
- Khi đang tải trạng thái đăng nhập (`isLoading`), ô nhập bị khóa. Như vậy không bị nháy thông báo đăng nhập.
- **Câu hỏi C:** Chưa đăng nhập thì có hiện nút mở trợ lý không?
  - Đề xuất: **vẫn hiện**. Mở ra thì thấy lời mời đăng nhập, nên người dùng biết có tính năng này.
  - Cách khác: ẩn hẳn nút.

### Phần 4. Lịch sử chat riêng từng người (`AssistantProvider.tsx`)

- Khóa `sessionStorage` gắn với id người dùng: `usas_assistant_chat:<userId>` và `usas_assistant_session:<userId>`.
- Theo dõi `user?.id`. Khi id đổi (đăng xuất, hoặc đăng nhập người khác):
  - xóa tin nhắn đang hiện, `sessionId`, lỗi và câu trả lời đang chạy dở;
  - đặt lại cờ `restoredRef`, để lần mở sau khôi phục đúng lịch sử của người mới.
- Chưa đăng nhập thì không đọc và không ghi storage.
- Xóa 2 khóa cũ không có userId một lần khi tải, để dọn dữ liệu đã lưu theo cách cũ.
- **Câu hỏi D:** Đăng xuất thì có **xóa hẳn** lịch sử chat của người đó trong tab không?
  - Đề xuất: **xóa** (gọi lúc user thành null), vì máy dùng chung là trường hợp hay gặp.
  - Nếu không xóa: cùng người đăng nhập lại trong tab đó sẽ thấy lại lịch sử, còn người khác thì không thấy.
- Không thuộc kế hoạch này: tải lại các cuộc chat cũ từ database khi đăng nhập ở máy khác. Hiện chưa có API danh sách phiên cho học sinh; nếu cần thì làm thành UC riêng.

### Phần 5. Kiểm tra

- `tsc` và `eslint` sạch.
- Thử tay trên localhost:3000:
  - con mắt ở cả 8 ô; bấm nút không gửi form;
  - chưa đăng nhập: khung chat hiện lời mời đăng nhập, không gửi được;
  - s18 chat 1 câu, đăng xuất, đăng nhập parent trên cùng tab: không thấy tin của s18;
  - đăng nhập lại s18: theo câu D.

## Ước tính

Khoảng 2–2.5 giờ: phần 1–2 mất 1 giờ, phần 3 mất 0.5 giờ, phần 4 mất 0.5–1 giờ, phần 5 mất 0.5 giờ.

## Kết quả kiểm tra tay (ngày 12/11)

Frontend localhost:3000, API 5080, tài khoản test UC-42.

| # | Bước | Kết quả |
|---|---|---|
| 1 | Bấm con mắt ở ô mật khẩu trang đăng nhập | Đạt. Ô đổi sang chữ thường, nhãn nút đổi thành "Ẩn mật khẩu", nút là `type="button"` nên không gửi form |
| 2 | Con mắt ở 2 ô trang đăng ký, 2 ô trang đặt mật khẩu mới, 3 ô "Bảo mật tài khoản" | Đạt cả 7 ô |
| 3 | Chưa đăng nhập, mở USAS Advisor | Đạt. Không có ô nhập, không có câu gợi ý; hiện "Bạn cần đăng nhập để hỏi trợ lý. Đăng nhập" (`/login?redirect=/`) |
| 4 | Khóa cũ `usas_assistant_chat` còn trong tab | Đạt. Bị xóa khi tải trang |
| 5 | s18 hỏi 1 câu | Đạt. Lưu vào khóa `usas_assistant_chat:<id s18>` và `usas_assistant_session:<id s18>` |
| 6 | s18 đăng xuất | Đạt. Hai khóa của s18 bị xóa (câu D) |
| 7 | Phụ huynh đăng nhập trên cùng tab, mở trợ lý | Đạt. Khung trống, không thấy tin của s18; có ô nhập và câu gợi ý |
