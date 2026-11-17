"use client";

import { useState } from "react";
import { Trash2Icon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api";
import { centerDeletionApi } from "@/lib/center-admin-api";
import { useFetched } from "@/lib/use-fetched";

const formatDate = (value: string) => new Date(value).toLocaleDateString("vi-VN");

/**
 * [UC-92] Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu từ trang hồ sơ. Administrator xử lý (UC-93) trong thời hạn đã công bố
 * và báo kết quả qua email. Trung tâm rút lại được khi yêu cầu chưa được xử lý.
 */
export function CenterDeletionRequestCard() {
  const [version, setVersion] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const fetched = useFetched(`center-deletion-request|${version}`, () => centerDeletionApi.getMine());
  const latest = fetched.data;
  const pending = latest?.status === "pending" ? latest : null;

  const errorText = (err: unknown) => (err instanceof ApiError ? err.message : "Không thể xử lý yêu cầu. Vui lòng thử lại.");

  const submit = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await centerDeletionApi.create(reason);
      toast.add({ type: "success", title: "Đã gửi yêu cầu xóa", description: "Quản trị viên sẽ xử lý và báo kết quả qua email." });
      setDialogOpen(false);
      setReason("");
      setVersion((v) => v + 1);
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Gửi yêu cầu thất bại", description: errorText(err) });
    } finally {
      setBusy(false);
    }
  };

  const cancel = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await centerDeletionApi.cancel();
      toast.add({ type: "success", title: "Đã rút yêu cầu xóa", description: "Tài khoản và dữ liệu của trung tâm được giữ nguyên." });
      setVersion((v) => v + 1);
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Rút yêu cầu thất bại", description: errorText(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="center-deletion-title" className="flex flex-col gap-3 rounded-md border border-line p-4 sm:p-6">
      <h2 id="center-deletion-title" className="text-base font-semibold text-ink">
        Xóa tài khoản và dữ liệu trung tâm
      </h2>
      <p className="text-sm text-ink-2">
        Khi yêu cầu được thực hiện, trung tâm bị gỡ khỏi danh bạ, thông tin hồ sơ và tài liệu xác minh bị xóa, các yêu cầu liên hệ đang
        mở được đóng và tài khoản không đăng nhập được nữa. Quản trị viên kiểm tra danh tính người yêu cầu trước khi xóa, xử lý trong
        vòng 30 ngày và báo kết quả qua email. Việc xóa không hoàn tác được.
      </p>

      {fetched.error ? (
        <Alert variant="destructive">
          <AlertDescription>{fetched.error}</AlertDescription>
        </Alert>
      ) : null}

      {pending ? (
        <Alert>
          <AlertDescription>
            Yêu cầu xóa gửi ngày {formatDate(pending.createdAt)} đang chờ xử lý, hạn xử lý {formatDate(pending.dueAt)}.
          </AlertDescription>
        </Alert>
      ) : null}

      {latest && latest.status === "rejected" ? (
        <Alert>
          <AlertDescription>
            Yêu cầu xóa gần nhất bị từ chối{latest.adminNote ? `. Lý do: ${latest.adminNote}` : "."}
          </AlertDescription>
        </Alert>
      ) : null}

      <div>
        {pending ? (
          <Button type="button" variant="outline" disabled={busy} onClick={() => void cancel()} className="gap-1.5">
            {busy ? <Spinner className="size-3.5" /> : null}
            Rút yêu cầu xóa
          </Button>
        ) : (
          <Button
            type="button"
            variant="destructive"
            disabled={busy || fetched.loading}
            onClick={() => setDialogOpen(true)}
            className="gap-1.5"
          >
            <Trash2Icon className="size-3.5" />
            Yêu cầu xóa tài khoản
          </Button>
        )}
      </div>

      <AlertDialog open={dialogOpen} onOpenChange={(open) => !busy && setDialogOpen(open)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gửi yêu cầu xóa tài khoản và dữ liệu?</AlertDialogTitle>
            <AlertDialogDescription>
              Yêu cầu được chuyển cho quản trị viên. Bạn rút lại được cho tới khi yêu cầu được xử lý. Sau khi xóa, dữ liệu của trung tâm
              không khôi phục được.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={1000}
            rows={3}
            disabled={busy}
            placeholder="Lý do (không bắt buộc, tối đa 1000 ký tự)"
            aria-label="Lý do yêu cầu xóa"
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void submit()} disabled={busy} className="gap-1.5">
              {busy ? <Spinner className="size-3.5" /> : null}
              Gửi yêu cầu xóa
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </section>
  );
}
