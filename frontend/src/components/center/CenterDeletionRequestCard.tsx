"use client";

import { useEffect, useState } from "react";
import { Trash2Icon } from "lucide-react";
import { centerPortalApi, ApiError } from "@/lib/api";
import type { CenterDeletionRequestDto } from "@/types/api";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogIcon,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";

const MAX_REASON_LENGTH = 1000;

const STATUS_LABELS: Record<string, string> = {
  pending: "Đang chờ Quản trị viên xử lý",
  approved: "Đã được duyệt, tài khoản sẽ bị xóa hẳn khi hết thời hạn chờ",
  completed: "Đã xử lý xong",
  rejected: "Đã bị từ chối",
};

function formatDate(value: string) {
  return new Date(value).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });
}

/** [UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu; Administrator xử lý ở UC-93. */
export function CenterDeletionRequestCard() {
  const [request, setRequest] = useState<CenterDeletionRequestDto | null>(null);
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    centerPortalApi
      .getDeletionRequest()
      .then((r) => {
        if (active) setRequest(r);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, []);

  const hasOpenRequest = request?.status === "pending" || request?.status === "approved";

  const submit = async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Vui lòng nhập lý do yêu cầu xóa tài khoản.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const created = await centerPortalApi.requestDeletion(trimmed);
      setRequest(created);
      setSuccess("Đã gửi yêu cầu xóa tài khoản. Quản trị viên sẽ kiểm tra danh tính và báo kết quả qua email.");
      setOpen(false);
      setReason("");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không gửi được yêu cầu. Vui lòng thử lại.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="rounded-xl border border-destructive/30 bg-surface p-6 space-y-4">
      <div>
        <h2 className="text-base font-semibold text-ink flex items-center gap-2">
          <Trash2Icon className="h-4 w-4 text-destructive" />
          Yêu cầu xóa tài khoản và dữ liệu
        </h2>
        <p className="text-sm text-ink-2 mt-1">
          Quản trị viên sẽ kiểm tra danh tính người yêu cầu rồi xóa tài khoản và dữ liệu trung tâm theo thời hạn đã công bố.
          Các yêu cầu liên hệ đang mở sẽ được đóng và người dùng liên quan nhận thông báo.
        </p>
      </div>

      {success && (
        <Alert>
          <AlertDescription>{success}</AlertDescription>
        </Alert>
      )}

      {request && (
        <p className="text-sm text-ink-2">
          Yêu cầu gần nhất ({formatDate(request.createdAt)}):{" "}
          <span className="font-medium text-ink">{STATUS_LABELS[request.status] ?? request.status}</span>
        </p>
      )}

      {hasOpenRequest && request?.dueAt && (
        <p className="text-sm text-ink-2">
          {request.extendedAt ? "Thời hạn xử lý đã được gia hạn đến" : "USAS sẽ xử lý chậm nhất vào"}{" "}
          <span className="font-medium text-ink">{formatDate(request.dueAt)}</span>.
        </p>
      )}

      <Button
        type="button"
        variant="destructive"
        disabled={hasOpenRequest}
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
      >
        {hasOpenRequest ? "Đã gửi yêu cầu xóa" : "Gửi yêu cầu xóa tài khoản"}
      </Button>

      <AlertDialog open={open} onOpenChange={(next) => !submitting && setOpen(next)}>
        <AlertDialogContent variant="form">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <Trash2Icon />
            </AlertDialogIcon>
            <AlertDialogTitle>Xác nhận yêu cầu xóa tài khoản trung tâm</AlertDialogTitle>
            <AlertDialogDescription>
              Sau khi được xử lý, tài khoản và dữ liệu trung tâm sẽ bị xóa và không khôi phục được. Vui lòng cho biết lý do.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogBody className="space-y-3">
            <Textarea
              rows={4}
              maxLength={MAX_REASON_LENGTH}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Lý do yêu cầu xóa (bắt buộc)"
              aria-label="Lý do yêu cầu xóa tài khoản"
            />
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </AlertDialogBody>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={submitting}>Hủy</AlertDialogCancel>
            <AlertDialogAction
              type="button"
              tone="destructive"
              onClick={() => void submit()}
              loading={submitting}
              disabled={!reason.trim()}
            >
              Gửi yêu cầu xóa
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
