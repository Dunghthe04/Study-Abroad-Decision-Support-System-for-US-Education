"use client";

// 1.5b: nút "Thêm con" (con chưa có tài khoản) và nút "Nhập hồ sơ cho con" ở trang Con của tôi.
// Thêm con xong thì vào nhập hồ sơ cho con đó luôn. Vào hay thoát làm thay đều tải lại cả trang (AuthContext).

import { useState, type FormEvent } from "react";
import { Plus, UserPlusIcon, UserRoundPen } from "lucide-react";
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
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { parentChildrenApi } from "@/lib/parent-children-api";

const errorText = (err: unknown) =>
  err instanceof ApiError ? err.message : "Vui lòng thử lại.";

/** Nút vào nhập hồ sơ thay một con managed. */
export function ActAsChildButton({
  childId,
  childName,
  className,
}: {
  childId: string;
  childName: string;
  className?: string;
}) {
  const { startActing } = useAuth();
  const [starting, setStarting] = useState(false);

  const start = async () => {
    setStarting(true);
    try {
      await startActing(childId); // thành công thì trang tải lại sang /profile/academic
    } catch (err) {
      setStarting(false);
      toast.add({
        type: "error",
        title: `Chưa mở được hồ sơ của ${childName}`,
        description: errorText(err),
      });
    }
  };

  return (
    <Button
      type="button"
      className={className}
      onClick={start}
      loading={starting}
    >
      <UserRoundPen data-icon="inline-start" />
      Nhập hồ sơ cho con
    </Button>
  );
}

/** Nút + hộp thoại thêm con chưa có tài khoản (tên, ngày sinh). Backend kiểm tra tên, ngày sinh và giới hạn 5 con.
 *  defaultOpen, next: trang /parent/children/add (nút "Thêm con" trong chat, 1.5e) mở sẵn hộp, thêm xong mở lại chat. */
export function AddManagedChildButton({
  variant = "outline",
  defaultOpen = false,
  next,
}: {
  variant?: "outline" | "default";
  defaultOpen?: boolean;
  next?: string;
}) {
  const { startActing } = useAuth();
  const [open, setOpen] = useState(defaultOpen);
  const [fullName, setFullName] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const onOpenChange = (next: boolean) => {
    if (submitting) return;
    setOpen(next);
    if (!next) {
      setFullName("");
      setDateOfBirth("");
      setError(null);
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!fullName.trim() || !dateOfBirth) {
      setError("Nhập tên và ngày sinh của con.");
      return;
    }
    setSubmitting(true);
    setError(null);
    let child: { studentUserId: string; fullName: string };
    try {
      child = await parentChildrenApi.addManagedChild({
        fullName: fullName.trim(),
        dateOfBirth,
      });
    } catch (err) {
      setSubmitting(false);
      setError(errorText(err));
      return;
    }
    try {
      await startActing(child.studentUserId, next); // trang tải lại sang /profile/academic hoặc next
    } catch (err) {
      // Con đã được thêm, chỉ chưa vào được: tải lại để danh sách có con mới, phụ huynh bấm nút trên thẻ con
      toast.add({
        type: "error",
        title: `Đã thêm ${child.fullName} nhưng chưa mở được hồ sơ`,
        description: errorText(err),
      });
      window.location.reload();
    }
  };

  return (
    <>
      <Button
        type="button"
        variant={variant}
        onClick={() => onOpenChange(true)}
      >
        <Plus data-icon="inline-start" />
        Thêm con
      </Button>
      <Dialog open={open} onOpenChange={onOpenChange} disablePointerDismissal={submitting}>
        <DialogContent variant="form" size="md">
          <form onSubmit={submit} noValidate className="flex min-h-0 flex-1 flex-col gap-6">
            <DialogHeader>
              <DialogIcon tone="brand">
                <UserPlusIcon aria-hidden />
              </DialogIcon>
              <DialogTitle>Thêm con chưa có tài khoản</DialogTitle>
              <DialogDescription>
                Bạn nhập hồ sơ thay con. Sau này con tự tạo tài khoản và liên
                kết với bạn thì có thể nhận lại hồ sơ này.
              </DialogDescription>
            </DialogHeader>

            <DialogBody className="space-y-4">
              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Field>
                <FieldLabel htmlFor="managed-child-name">Tên của con</FieldLabel>
                <Input
                  id="managed-child-name"
                  autoComplete="off"
                  maxLength={128}
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  disabled={submitting}
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="managed-child-dob">Ngày sinh</FieldLabel>
                <Input
                  id="managed-child-dob"
                  type="date"
                  value={dateOfBirth}
                  onChange={(e) => setDateOfBirth(e.target.value)}
                  disabled={submitting}
                />
              </Field>
            </DialogBody>

            <DialogFooter layout="pair">
              <DialogCancel disabled={submitting}>Hủy</DialogCancel>
              <DialogAction type="submit" tone="primary" disabled={submitting} loading={submitting}>
                Thêm và nhập hồ sơ
              </DialogAction>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
