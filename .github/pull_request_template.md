## Task
<!-- Link Jira, ví dụ: USAS-12 -->

## Thay đổi
<!-- Làm gì, vì sao. Gạch đầu dòng ngắn gọn -->
-

## Ảnh chụp / cách test
<!-- Ảnh màn hình nếu đổi giao diện; các bước để reviewer tự thử -->

## Checklist
- [ ] PR nhắm vào `develop` (chỉ PR release mới nhắm vào `main`)
- [ ] Đã chạy lint + test ở local (xem README, mục Kiểm thử)
- [ ] Không commit `.env`, mật khẩu, dữ liệu khảo sát thô
- [ ] Đổi DTO backend → đã cập nhật `frontend/src/types/api.ts`
- [ ] Thêm biến môi trường → đã thêm vào `.env.example`
- [ ] Đổi schema → đã tạo migration mới (không sửa migration cũ)
