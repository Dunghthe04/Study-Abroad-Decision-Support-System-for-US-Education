"use client";

import { useState } from "react";
import { AlertTriangleIcon, ShieldAlertIcon } from "lucide-react";
import { Checkbox } from "@/components/ui/checkbox";
import {
  AppDialog,
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
import { Label } from "@/components/ui/label";

// [UC-111 1a, UC-75 1a] Hai hộp thoại phụ huynh rút đồng ý, dùng ở Tài khoản > Liên kết và ở Con của tôi

/** Phụ huynh rút lại sự đồng ý cho con (không xóa tài khoản). */
export function RevokeConsentDialog({
  open,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <AppDialog
      open={open}
      onOpenChange={(next) => !next && onCancel()}
      variant="action"
      icon={<ShieldAlertIcon aria-hidden />}
      iconTone="destructive"
      title="Rút lại sự đồng ý?"
      description="Hệ thống sẽ ngừng xử lý dữ liệu của con; con dưới 16 tuổi quay về chế độ hạn chế cho tới khi có đồng ý mới."
      cancelLabel="Không"
      confirmLabel="Xác nhận"
      confirmTone="destructive"
      onCancel={onCancel}
      onConfirm={onConfirm}
    />
  );
}

/** Phụ huynh rút đồng ý kèm xóa tài khoản con dưới 16; phải tick đã hiểu mới bấm được. */
export function RevokeAndDeleteChildDialog({
  open,
  childName,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  /** Giữ tên khi hộp thoại đang đóng dần */
  childName: string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const [understood, setUnderstood] = useState(false);
  const close = () => {
    setUnderstood(false);
    onCancel();
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && close()}>
      <DialogContent variant="form" size="md">
        <DialogHeader>
          <DialogIcon tone="destructive">
            <AlertTriangleIcon aria-hidden />
          </DialogIcon>
          <DialogTitle>Rút đồng ý và xóa tài khoản của {childName}?</DialogTitle>
          <DialogDescription>Việc này khác với chỉ rút đồng ý:</DialogDescription>
        </DialogHeader>

        <DialogBody className="space-y-4">
          <ul className="list-disc space-y-1 pl-5 text-sm text-ink-2">
            <li>Hệ thống ngừng xử lý dữ liệu và rút mọi đồng ý của phụ huynh cho con.</li>
            <li>Tài khoản của con bị khóa ngay và xóa vĩnh viễn sau 30 ngày. Con không tự khôi phục được khi chưa đủ 16 tuổi.</li>
            <li>Bạn nhận email có nút &quot;Hủy xóa và đồng ý lại cho con&quot; và &quot;Chỉ hủy xóa&quot;, dùng được tới ngày xóa vĩnh viễn.</li>
          </ul>
          <div className="flex items-start gap-2">
            <Checkbox
              id="parent-delete-child-understood"
              checked={understood}
              onCheckedChange={(checked) => setUnderstood(Boolean(checked))}
            />
            <Label htmlFor="parent-delete-child-understood" className="leading-snug text-ink">
              Tôi hiểu tài khoản và dữ liệu của con sẽ bị xóa vĩnh viễn nếu không hủy trong 30 ngày.
            </Label>
          </div>
        </DialogBody>

        <DialogFooter layout="pair">
          <DialogCancel onClick={close}>Không</DialogCancel>
          <DialogAction
            tone="destructive"
            disabled={!understood}
            onClick={() => {
              setUnderstood(false);
              onConfirm();
            }}
          >
            Rút đồng ý và xóa
          </DialogAction>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
