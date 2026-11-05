"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { KeyRoundIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

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
    if (newPassword.length < 8) {
      setError("Mật khẩu mới phải có ít nhất 8 ký tự.");
      return;
    }
    if (newPassword.length > 100) {
      setError("Mật khẩu mới không được vượt quá 100 ký tự.");
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
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <KeyRoundIcon aria-hidden />
          Đổi mật khẩu
        </CardTitle>
        <CardDescription>
          Sau khi đổi, các thiết bị khác đang đăng nhập sẽ bị đăng xuất. Nhập sai mật khẩu hiện tại 5 lần liên tiếp sẽ tạm khóa tài khoản 15 phút.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
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

        <form onSubmit={handleSubmit} noValidate>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="current-password">Mật khẩu hiện tại</FieldLabel>
              <Input
                id="current-password"
                type="password"
                required
                maxLength={100}
                autoComplete="current-password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
              />
            </Field>

            <Field>
              <FieldLabel htmlFor="change-new-password">Mật khẩu mới</FieldLabel>
              <Input
                id="change-new-password"
                type="password"
                required
                maxLength={100}
                autoComplete="new-password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="Tối thiểu 8 ký tự"
              />
              <FieldDescription>Từ 8 đến 100 ký tự, khác mật khẩu hiện tại.</FieldDescription>
            </Field>

            <Field>
              <FieldLabel htmlFor="change-confirm-password">Xác nhận mật khẩu mới</FieldLabel>
              <Input
                id="change-confirm-password"
                type="password"
                required
                maxLength={100}
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Nhập lại mật khẩu mới"
              />
            </Field>

            <Button type="submit" disabled={isSubmitting} className="w-full sm:w-auto">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang xử lý..." : "Đổi mật khẩu"}
            </Button>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  );
}
