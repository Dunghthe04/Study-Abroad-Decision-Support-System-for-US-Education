"use client";

import { useState } from "react";
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
import { ApiError, centerLegalHoldApi } from "@/lib/api";
import type { CenterLegalHoldDto } from "@/types/api";

const MAX_REASON = 500;

export type LegalHoldAction = "set" | "release";

const TEXT: Record<LegalHoldAction, { title: string; description: string; placeholder: string; confirm: string; done: string }> = {
  set: {
    title: "Giữ pháp lý dữ liệu trung tâm?",
    description:
      "Dùng khi trung tâm bị nghi lừa đảo, có khiếu nại hoặc tranh chấp, hay cơ quan có thẩm quyền yêu cầu giữ dữ liệu. Trong thời gian giữ, yêu cầu xóa của trung tâm không thể duyệt và dữ liệu không bị xóa hẳn, kể cả khi đã tới hạn. Trung tâm không được báo về việc này.",
    placeholder: "Lý do giữ (bắt buộc), ví dụ: số công văn, mã khiếu nại, cơ quan yêu cầu",
    confirm: "Bật giữ pháp lý",
    done: "Đã bật giữ pháp lý",
  },
  release: {
    title: "Gỡ giữ pháp lý?",
    description:
      "Gỡ khi việc điều tra hoặc tranh chấp đã kết thúc. Nếu trung tâm có yêu cầu xóa đã duyệt và đã quá hạn, dữ liệu sẽ bị xóa hẳn ở lần chạy kế tiếp của hệ thống (trong vòng một giờ).",
    placeholder: "Lý do gỡ (bắt buộc), ví dụ: kết luận điều tra, khiếu nại đã giải quyết",
    confirm: "Gỡ giữ pháp lý",
    done: "Đã gỡ giữ pháp lý",
  },
};

interface CenterLegalHoldDialogProps {
  target: { action: LegalHoldAction; centerId: string; centerName: string } | null;
  onClose: () => void;
  onDone: (hold: CenterLegalHoldDto) => void;
}

/** [UC-93] Hộp thoại bật/gỡ giữ pháp lý, lý do bắt buộc và được ghi vào nhật ký. */
export function CenterLegalHoldDialog({ target, onClose, onDone }: CenterLegalHoldDialogProps) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const text = target ? TEXT[target.action] : null;

  const close = () => {
    if (busy) return;
    setReason("");
    onClose();
  };

  const confirm = async () => {
    if (!target || busy) return;
    const trimmed = reason.trim();
    if (!trimmed) return;
    setBusy(true);
    try {
      const hold =
        target.action === "set"
          ? await centerLegalHoldApi.set(target.centerId, trimmed)
          : await centerLegalHoldApi.release(target.centerId, trimmed);
      toast.add({ type: "success", title: TEXT[target.action].done, description: target.centerName });
      setReason("");
      onDone(hold);
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Thao tác thất bại",
        description: err instanceof ApiError ? err.message : "Không thể cập nhật giữ pháp lý. Vui lòng thử lại.",
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <AlertDialog open={target !== null} onOpenChange={(isOpen) => !isOpen && close()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{text?.title}</AlertDialogTitle>
          <AlertDialogDescription>{text?.description}</AlertDialogDescription>
        </AlertDialogHeader>
        {target ? (
          <p className="rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">{target.centerName}</p>
        ) : null}
        <Textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          maxLength={MAX_REASON}
          rows={3}
          disabled={busy}
          placeholder={text?.placeholder}
          aria-label={target?.action === "set" ? "Lý do giữ pháp lý" : "Lý do gỡ giữ pháp lý"}
          aria-required
          className="resize-y text-sm"
        />
        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
          <Button
            type="button"
            variant={target?.action === "set" ? "destructive" : "default"}
            onClick={() => void confirm()}
            disabled={busy || !reason.trim()}
            className="gap-1.5"
          >
            {busy ? <Spinner className="size-3.5" /> : null}
            {text?.confirm}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
