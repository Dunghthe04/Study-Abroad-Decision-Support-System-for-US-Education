"use client";

import React, { useState } from "react";
import { Trash2Icon } from "lucide-react";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
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
    <div className="flex flex-col gap-4 rounded-xl border border-risk/30 bg-surface p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
      <div className="min-w-0 space-y-1">
        <h2 className="font-heading text-lg font-semibold text-risk">Xóa tài khoản</h2>
        <p className="max-w-xl text-sm text-ink-2">
          Tài khoản bị khóa ngay và xóa hẳn sau {RETENTION_DAYS} ngày. Trong {RETENTION_DAYS} ngày đó, bạn đăng nhập lại là khôi phục được.
        </p>
      </div>

      <Button
        type="button"
        variant="destructive"
        size="lg"
        onClick={openDialog}
        className="w-full shrink-0 sm:w-auto"
      >
        <Trash2Icon data-icon="inline-start" aria-hidden />
        Xóa tài khoản
      </Button>

      <Dialog open={open} onOpenChange={handleOpenChange} disablePointerDismissal={isSubmitting}>
        <DialogContent variant="form" size="md">
          <DialogHeader>
            <DialogIcon tone="destructive">
              <Trash2Icon aria-hidden />
            </DialogIcon>
            <DialogTitle>Xóa tài khoản của bạn?</DialogTitle>
            <DialogDescription>Hãy đọc kỹ trước khi xác nhận.</DialogDescription>
          </DialogHeader>

          <DialogBody className="space-y-4">
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
              <li>
                Gói của bạn dừng khi tài khoản bị xóa hẳn sau 30 ngày. Nếu bạn là chủ gói Gia đình, các thành viên vẫn dùng được tới lúc đó hoặc tới khi gói hết
                hạn. Thông tin đơn hàng (mã đơn, số tiền, họ tên, email) được giữ 10 năm, chỉ dùng để đối soát và hoàn tiền.
              </li>
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
          </DialogBody>

          <DialogFooter layout="pair">
            <DialogCancel disabled={isSubmitting}>Hủy</DialogCancel>
            <DialogAction tone="destructive" onClick={handleDelete} disabled={!canConfirm || isSubmitting} loading={isSubmitting}>
              Xóa tài khoản
            </DialogAction>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
