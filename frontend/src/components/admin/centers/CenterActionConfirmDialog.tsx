"use client";

import { useState } from "react";
import {
  AlertTriangleIcon,
  EyeIcon,
  EyeOffIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getCenterLegalMissingChecklist } from "@/lib/center-legal";
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
import { Textarea } from "@/components/ui/textarea";

export type CenterConfirmAction = "verify" | "unverify" | "hide" | "show";

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
      disablePointerDismissal={isSubmitting}
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
  const [note, setNote] = useState(center.verificationNote || "");

  const isVerificationAction = action === "verify" || action === "unverify";
  const missingChecklist =
    action === "verify" ? getCenterLegalMissingChecklist(center) : [];
  const isPositive = action === "verify" || action === "show";

  return (
    <DialogContent variant={isVerificationAction ? "form" : "action"}>
      <DialogHeader>
        <DialogIcon tone={isPositive ? "success" : "destructive"}>
          {action === "verify" ? (
            <ShieldCheckIcon />
          ) : action === "unverify" ? (
            <ShieldAlertIcon />
          ) : action === "hide" ? (
            <EyeOffIcon />
          ) : (
            <EyeIcon />
          )}
        </DialogIcon>
        <DialogTitle>
          {action === "verify"
            ? "Xác minh trung tâm tư vấn"
            : action === "unverify"
              ? "Hủy xác minh trung tâm tư vấn"
              : action === "hide"
                ? "Ẩn trung tâm tư vấn"
                : "Hiện lại trung tâm tư vấn"}
        </DialogTitle>
        <DialogDescription>
          {action === "verify" ? (
            <>
              Bạn có chắc chắn muốn xác minh trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Sau khi xác minh, trung tâm sẽ được gắn huy hiệu &quot;Đã xác minh&quot; và đủ điều kiện xuất hiện trong danh bạ đề xuất cho học sinh, phụ huynh.
            </>
          ) : action === "unverify" ? (
            <>
              Bạn có chắc chắn muốn hủy trạng thái xác minh của trung tâm{" "}
              <strong className="text-foreground">{center.name}</strong>?
              <br />
              Trung tâm sẽ chuyển về trạng thái Chưa xác minh (&quot;unverified&quot;) và không còn hiển thị huy hiệu xác minh trên toàn hệ thống.
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

      {(missingChecklist.length > 0 || isVerificationAction) && (
        <DialogBody className="space-y-4">
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
            <Field>
              <FieldLabel htmlFor="verify-center-note">
                {action === "verify"
                  ? "Ghi chú xác minh / đối chiếu hồ sơ (tùy chọn)"
                  : "Lý do hủy xác minh / ghi chú pháp lý (tùy chọn)"}
              </FieldLabel>
              <Textarea
                id="verify-center-note"
                rows={2}
                placeholder={
                  action === "verify"
                    ? "Ví dụ: Đã đối chiếu GCN ĐKDN, giấy phép Sở GD&ĐT bản gốc..."
                    : "Ví dụ: Chưa bổ sung giấy phép đào tạo hoặc giấy tờ hết hiệu lực..."
                }
                value={note}
                onChange={(e) => setNote(e.target.value)}
                disabled={isSubmitting}
                maxLength={1000}
              />
            </Field>
          )}
        </DialogBody>
      )}

      <DialogFooter layout="pair">
        <DialogCancel onClick={onClose} disabled={isSubmitting}>
          Hủy bỏ
        </DialogCancel>
        <DialogAction
          tone={isPositive ? "ok" : "destructive"}
          onClick={() => onConfirm(isVerificationAction ? note.trim() : undefined)}
          loading={isSubmitting}
        >
          {action === "verify"
            ? "Xác nhận xác minh"
            : action === "unverify"
              ? "Xác nhận hủy xác minh"
              : action === "hide"
                ? "Xác nhận ẩn"
                : "Xác nhận hiện"}
        </DialogAction>
      </DialogFooter>
    </DialogContent>
  );
}
