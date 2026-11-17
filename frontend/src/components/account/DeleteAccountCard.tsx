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
import { Spinner } from "@/components/ui/spinner";

/** Chữ người dùng phải gõ đúng để xác nhận xóa */
export const DELETE_CONFIRM_WORD = "XÓA";

// [UC-80] Tự xóa tài khoản (chỉ học sinh, phụ huynh): khóa ngay, xóa hẳn sau 30 ngày,
// trong 30 ngày đăng nhập lại là khôi phục được.
export function DeleteAccountCard() {
  const [open, setOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

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
          Tài khoản bị khóa ngay và xóa hẳn sau 30 ngày. Trong 30 ngày đó, bạn đăng nhập lại là khôi phục được.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <Button type="button" variant="destructive" onClick={() => setOpen(true)}>
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
              Sau <strong>30 ngày</strong>, thông tin cá nhân, hồ sơ, bảng điểm và nội dung bài viết diễn đàn của bạn sẽ bị xóa hẳn, không lấy lại được.
            </li>
            <li>Trong 30 ngày đó, đăng nhập lại và chọn khôi phục là dùng tiếp như cũ.</li>
            <li>Gói đăng ký bị hủy; lịch sử giao dịch được giữ ẩn danh theo quy định kế toán.</li>
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
            <Button type="button" variant="destructive" onClick={handleDelete} disabled={!canConfirm || isSubmitting}>
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang xóa..." : "Xóa tài khoản"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
