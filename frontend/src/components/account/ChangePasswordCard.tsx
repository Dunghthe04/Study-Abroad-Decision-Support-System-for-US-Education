"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { KeyRoundIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { PASSWORD_RULE_HINT, passwordRuleError } from "@/lib/password-rules";

// [UC-06] Đổi mật khẩu khi đã đăng nhập: bắt buộc nhập mật khẩu hiện tại.
// Thành công thì phiên này giữ nguyên, các thiết bị khác bị đăng xuất.
export function ChangePasswordCard() {
  const { logout } = useAuth();
  const router = useRouter();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    if (!currentPassword) {
      setError("Vui lòng nhập mật khẩu hiện tại.");
      return;
    }
    // [UC-01] Cùng luật mật khẩu với đăng ký (khớp backend)
    const passwordError = passwordRuleError(newPassword, "Mật khẩu mới");
    if (passwordError) {
      setError(passwordError);
      return;
    }
    if (newPassword === currentPassword) {
      setError("Mật khẩu mới phải khác mật khẩu hiện tại.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.changePassword({ currentPassword, newPassword });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setSuccessMessage(res.message || "Đổi mật khẩu thành công.");
    } catch (err) {
      if (err instanceof ApiError && err.status === 423) {
        // Tài khoản bị tạm khóa, mọi phiên đã bị thu hồi: báo rồi đưa về trang đăng nhập
        setError(err.message);
        setTimeout(async () => {
          await logout();
          router.push("/login");
        }, 3000);
      } else if (err instanceof ApiError) {
        setError(err.message || "Không thể đổi mật khẩu. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="overflow-hidden rounded-xl border border-line bg-surface"
    >
      <div className="grid grid-cols-1 gap-6 p-5 sm:p-6 lg:grid-cols-[240px_minmax(0,1fr)] lg:gap-8">
        <div className="space-y-1.5">
          <h2 className="font-heading text-lg font-semibold text-ink">Đổi mật khẩu</h2>
          <p className="text-xs leading-relaxed text-ink-2">
            Sau khi đổi, các thiết bị khác đang đăng nhập sẽ bị đăng xuất. Nhập sai mật khẩu hiện tại 5 lần liên tiếp sẽ khóa tài khoản (vui lòng liên hệ để được mở khóa).
          </p>
        </div>

        <div className="min-w-0 space-y-4">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {successMessage && (
            <Alert variant="success" role="status">
              <AlertDescription>{successMessage}</AlertDescription>
            </Alert>
          )}

          <FieldGroup className="max-w-md">
            <Field>
              <FieldLabel htmlFor="current-password">Mật khẩu hiện tại</FieldLabel>
              <PasswordInput
                id="current-password"
                required
                maxLength={100}
                autoComplete="current-password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                className="h-10"
              />
            </Field>

            <Field>
              <FieldLabel htmlFor="change-new-password">Mật khẩu mới</FieldLabel>
              <PasswordInput
                id="change-new-password"
                required
                maxLength={100}
                autoComplete="new-password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder={PASSWORD_RULE_HINT}
                className="h-10"
              />
              <FieldDescription>Từ 8 đến 100 ký tự, khác mật khẩu hiện tại.</FieldDescription>
            </Field>

            <Field>
              <FieldLabel htmlFor="change-confirm-password">Xác nhận mật khẩu mới</FieldLabel>
              <PasswordInput
                id="change-confirm-password"
                required
                maxLength={100}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Nhập lại mật khẩu mới"
                className="h-10"
              />
            </Field>
          </FieldGroup>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-end gap-3 border-t border-line bg-ground/60 px-5 py-3.5 sm:px-6">
        <Button
          type="submit"
          size="lg"
          disabled={isSubmitting}
          loading={isSubmitting}
          className="w-full sm:w-auto"
        >
          <KeyRoundIcon data-icon="inline-start" aria-hidden />
          {isSubmitting ? "Đang xử lý..." : "Đổi mật khẩu"}
        </Button>
      </div>
    </form>
  );
}
