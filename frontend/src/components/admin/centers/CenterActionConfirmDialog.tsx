"use client";

import { useState } from "react";
import {
  AlertTriangleIcon,
  CheckCircle2Icon,
  EyeIcon,
  EyeOffIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getCenterLegalMissingChecklist } from "@/lib/center-legal";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Textarea } from "@/components/ui/textarea";

export type CenterConfirmAction = "verify" | "revoke" | "hide" | "show";

interface CenterActionConfirmDialogProps {
  center: StudyCenter | null;
  action: CenterConfirmAction | null;
  isSubmitting: boolean;
  onClose: () => void;
  onConfirm: (verificationNote?: string) => void;
}

export function CenterActionConfirmDialog({
  center,
  action,
  isSubmitting,
  onClose,
  onConfirm,
}: CenterActionConfirmDialogProps) {
  return (
    <Dialog
      open={action !== null && center !== null}
      onOpenChange={(open) => {
        if (!open && !isSubmitting) onClose();
      }}
    >
      {center && action && (
        <CenterActionConfirmContent
          key={`${center.id}:${action}`}
          center={center}
          action={action}
          isSubmitting={isSubmitting}
          onClose={onClose}
          onConfirm={onConfirm}
        />
      )}
    </Dialog>
  );
}

interface CenterActionConfirmContentProps {
  center: StudyCenter;
  action: CenterConfirmAction;
  isSubmitting: boolean;
  onClose: () => void;
  onConfirm: (verificationNote?: string) => void;
}

function CenterActionConfirmContent({
  center,
  action,
  isSubmitting,
  onClose,
  onConfirm,
}: CenterActionConfirmContentProps) {
  const [note, setNote] = useState(action === "verify" ? center.verificationNote || "" : "");

  const isVerificationAction = action === "verify" || action === "revoke";
  const missingChecklist =
    action === "verify" ? getCenterLegalMissingChecklist(center) : [];

  return (
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          {action === "verify" ? (
            <>
              <ShieldCheckIcon className="w-5 h-5 text-ok" />
            </>
          ) : action === "revoke" ? (
            <>
              <ShieldAlertIcon className="w-5 h-5 text-risk" />
              <span>Thu hồi dấu xác minh</span>
            </>
          ) : action === "hide" ? (
            <>
              <EyeOffIcon className="w-5 h-5 text-risk" />
              <span>Ẩn trung tâm tư vấn</span>
            </>
          ) : (
            <>
              <EyeIcon className="w-5 h-5 text-ok" />
              <span>Hiện lại trung tâm tư vấn</span>
            </>
          )}
        </DialogTitle>
        <DialogDescription className="pt-2 text-sm leading-relaxed">
          {action === "verify" ? (
            <>
              Bạn có chắc chắn muốn xác minh trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Sau khi xác minh, trung tâm sẽ được gắn huy hiệu &quot;Đã xác minh&quot; và đủ điều kiện xuất hiện trong danh bạ đề xuất cho học sinh, phụ huynh.
            </>
          ) : action === "revoke" ? (
            <>
              Bạn có chắc chắn muốn thu hồi dấu xác minh của trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Trung tâm chuyển sang Tạm ngừng: không còn hiện công khai, không nhận yêu cầu liên hệ mới và nhận email kèm lý do. Các yêu cầu và tin nhắn đã có vẫn được giữ.
            </>
          ) : action === "hide" ? (
            <>
              Bạn có chắc chắn muốn ẩn trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Trung tâm sẽ bị ẩn khỏi danh bạ công khai và thuật toán gợi ý của AI sẽ không đề xuất trung tâm này cho học sinh.
            </>
          ) : (
            <>
              Bạn có chắc chắn muốn kích hoạt và hiển thị lại trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Trung tâm sẽ xuất hiện trở lại trên danh bạ công khai và đủ điều kiện để AI gợi ý.
            </>
          )}
        </DialogDescription>
      </DialogHeader>

      {missingChecklist.length > 0 && (
        <div className="rounded-lg border border-warn/30 bg-warn-wash p-3 text-xs text-warn space-y-1">
          <div className="font-semibold flex items-center gap-1.5">
            <AlertTriangleIcon className="w-4 h-4 shrink-0" />
            Lưu ý hồ sơ pháp lý còn thiếu ({missingChecklist.length}):
          </div>
          <div>{missingChecklist.join(", ")}.</div>
        </div>
      )}

      {isVerificationAction && (
        <Field className="pt-1">
          <FieldLabel htmlFor="verify-center-note" className="text-xs">
            {action === "verify"
              ? "Ghi chú xác minh / đối chiếu hồ sơ (tùy chọn)"
              : "Lý do thu hồi dấu xác minh (bắt buộc)"}
          </FieldLabel>
          <Textarea
            id="verify-center-note"
            rows={2}
            placeholder={
              action === "verify"
                ? "Ví dụ: Đã đối chiếu GCN ĐKDN, giấy phép Sở GD&ĐT bản gốc..."
                : "Ví dụ: Giấy chứng nhận đăng ký doanh nghiệp đã hết hiệu lực..."
            }
            value={note}
            onChange={(e) => setNote(e.target.value)}
            disabled={isSubmitting}
            maxLength={1000}
            className="text-xs"
          />
        </Field>
      )}

      <DialogFooter className="mt-4 gap-2 sm:gap-0">
        <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
          Hủy bỏ
        </Button>
        <Button
          variant={action === "verify" || action === "show" ? "default" : "destructive"}
          onClick={() => onConfirm(isVerificationAction ? note.trim() : undefined)}
          loading={isSubmitting}
          disabled={action === "revoke" && note.trim().length === 0}
          className={
            action === "verify" || action === "show"
              ? "bg-ok hover:bg-ok/90 text-white"
              : undefined
          }
        >
          {action === "verify" ? (
            <>
              <CheckCircle2Icon className="w-4 h-4 mr-1.5" />
              Xác nhận xác minh
            </>
          ) : action === "revoke" ? (
            <>
              <ShieldAlertIcon className="w-4 h-4 mr-1.5" />
              Xác nhận thu hồi
            </>
          ) : action === "hide" ? (
            <>
              <EyeOffIcon className="w-4 h-4 mr-1.5" />
              Xác nhận ẩn
            </>
          ) : (
            <>
              <EyeIcon className="w-4 h-4 mr-1.5" />
              Xác nhận hiện
            </>
          )}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
