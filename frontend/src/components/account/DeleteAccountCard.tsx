"use client";

import React, { useState } from "react";
import { Trash2Icon } from "lucide-react";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";

/** Chữ người dùng phải gõ đúng để xác nhận xóa */
export const DELETE_CONFIRM_WORD = "XÓA";

/** Khớp AccountDeletionOptions.RetentionPeriod ở backend */
const RETENTION_DAYS = 30;
/** Khớp chat_log_keep_days ở advisor/app/core/config.py */
const CHAT_LOG_KEEP_DAYS = 90;

function purgeDateLabel(): string {
  const date = new Date();
  date.setDate(date.getDate() + RETENTION_DAYS);
  return date.toLocaleDateString("vi-VN");
}

// [UC-80] Tự xóa tài khoản (chỉ học sinh, phụ huynh): khóa ngay, xóa hẳn sau 30 ngày,
// trong 30 ngày đăng nhập lại là khôi phục được.
export function DeleteAccountCard() {
  const [open, setOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Tính lúc mở hộp thoại (không tính trong render)
  const [purgeDate, setPurgeDate] = useState("");

  const openDialog = () => {
    setPurgeDate(purgeDateLabel());
    setOpen(true);
  };

  // So khớp sau khi chuẩn hóa Unicode: bàn phím tiếng Việt có thể gõ "Ó" thành 2 ký tự (O + dấu)
  const canConfirm = confirmText.trim().normalize("NFC") === DELETE_CONFIRM_WORD.normalize("NFC");

  const handleOpenChange = (next: boolean) => {
    if (isSubmitting) return;
    setOpen(next);
    if (!next) {
      setConfirmText("");
      setError(null);
    }
  };

  const handleDelete = async () => {
    if (!canConfirm) return;
    setError(null);
    setIsSubmitting(true);
    try {
      await authApi.deleteMe();
      // Server đã xóa cookie phiên. Tải lại hẳn về trang chủ: nếu xóa user trong context trước,
      // ProtectedRoute sẽ đẩy sang /login trước khi kịp chuyển về "/". Tải lại hẳn cũng xóa sạch dữ liệu phía trình duyệt của tài khoản cũ
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- cố ý tải lại toàn trang
      window.location.assign("/");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể xóa tài khoản. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
      setIsSubmitting(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-destructive">
          <Trash2Icon aria-hidden />
          Xóa tài khoản
        </CardTitle>
        <CardDescription>
          Tài khoản bị khóa ngay và xóa hẳn sau {RETENTION_DAYS} ngày. Trong {RETENTION_DAYS} ngày đó, bạn đăng nhập lại là khôi phục được.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <Button type="button" variant="destructive" onClick={openDialog}>
          Xóa tài khoản
        </Button>
      </CardContent>

      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Xóa tài khoản của bạn?</DialogTitle>
            <DialogDescription>Hãy đọc kỹ trước khi xác nhận.</DialogDescription>
          </DialogHeader>

          <ul className="list-disc space-y-1 pl-5 text-sm">
            <li>Tài khoản bị khóa ngay, bạn sẽ bị đăng xuất khỏi mọi thiết bị.</li>
            <li>
              Ngày <strong>{purgeDate}</strong> (sau {RETENTION_DAYS} ngày), mọi dữ liệu của bạn bị xóa hẳn, không lấy lại được: thông tin cá nhân, hồ sơ,
              bảng điểm, nội dung bài viết diễn đàn, kể cả dữ liệu đã chia sẻ cho trung tâm tư vấn và tin nhắn với trung tâm.
            </li>
            <li>Trước ngày đó, đăng nhập lại và chọn khôi phục là dùng tiếp như cũ.</li>
            <li>
              Liên kết với phụ huynh và các chia sẻ hồ sơ cho trung tâm bị hủy ngay; trung tâm thôi xem được dữ liệu của bạn. Nếu khôi phục, các liên
              kết và chia sẻ này không tự nối lại.
            </li>
            <li>Gói đăng ký bị hủy; lịch sử giao dịch được giữ ẩn danh theo quy định kế toán.</li>
            <li>
              Nhật ký trò chuyện với trợ lý tư vấn đã được che thông tin cá nhân và tự xóa sau {CHAT_LOG_KEEP_DAYS} ngày.
            </li>
          </ul>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Field>
            <FieldLabel htmlFor="delete-account-confirm">
              Gõ <strong>{DELETE_CONFIRM_WORD}</strong> để xác nhận
            </FieldLabel>
            <Input
              id="delete-account-confirm"
              autoComplete="off"
              value={confirmText}
              onChange={(e) => setConfirmText(e.target.value)}
              disabled={isSubmitting}
            />
          </Field>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)} disabled={isSubmitting}>
              Hủy
            </Button>
            <Button type="button" variant="destructive" onClick={handleDelete} disabled={!canConfirm || isSubmitting} loading={isSubmitting}>
              {isSubmitting ? "Đang xóa..." : "Xóa tài khoản"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
