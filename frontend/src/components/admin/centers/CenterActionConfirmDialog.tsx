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
import { Spinner } from "@/components/ui/spinner";
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

  return (
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          {action === "verify" ? (
            <>
              <ShieldCheckIcon className="w-5 h-5 text-emerald-600" />
              <span>Xác minh trung tâm tư vấn</span>
            </>
          ) : action === "unverify" ? (
            <>
              <ShieldAlertIcon className="w-5 h-5 text-rose-600" />
              <span>Hủy xác minh trung tâm tư vấn</span>
            </>
          ) : action === "hide" ? (
            <>
              <EyeOffIcon className="w-5 h-5 text-rose-600" />
              <span>Ẩn trung tâm tư vấn</span>
            </>
          ) : (
            <>
              <EyeIcon className="w-5 h-5 text-emerald-600" />
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

      {missingChecklist.length > 0 && (
        <div className="rounded-lg border border-amber-300 bg-amber-50/70 dark:bg-amber-950/30 p-3 text-xs text-amber-800 dark:text-amber-200 space-y-1">
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
          disabled={isSubmitting}
          className={
            action === "verify" || action === "show"
              ? "bg-emerald-600 hover:bg-emerald-700 text-white"
              : undefined
          }
        >
          {isSubmitting ? (
            <>
              <Spinner className="w-4 h-4 mr-1.5" />
              Đang xử lý...
            </>
          ) : action === "verify" ? (
            <>
              <CheckCircle2Icon className="w-4 h-4 mr-1.5" />
              Xác nhận xác minh
            </>
          ) : action === "unverify" ? (
            <>
              <ShieldAlertIcon className="w-4 h-4 mr-1.5" />
              Xác nhận hủy xác minh
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
