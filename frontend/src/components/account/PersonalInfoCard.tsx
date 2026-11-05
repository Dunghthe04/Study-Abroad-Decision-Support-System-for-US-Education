"use client";

import React, { useState } from "react";
import { CheckIcon, HourglassIcon, LockIcon, MailIcon, TriangleAlertIcon, UserIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

const MAX_FULL_NAME_LENGTH = 100;
const MAX_PHONE_LENGTH = 20;

// Thông tin cá nhân: sửa được họ tên và số điện thoại. Email đăng nhập chỉ hiển thị, không đổi được.
export function PersonalInfoCard() {
  const { user, refreshUser } = useAuth();
  const [fullName, setFullName] = useState(user?.fullName ?? "");
  const [phone, setPhone] = useState(user?.phone ?? "");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!user) return null;

  const isDirty = fullName.trim() !== user.fullName || phone.trim() !== (user.phone ?? "");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    const trimmedName = fullName.trim();
    if (!trimmedName) {
      setError("Vui lòng nhập họ và tên.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.updateMe({ fullName: trimmedName, phone: phone.trim() || null });
      // Lấy lại bản đã chuẩn hóa từ server (ví dụ +84 912... → 0912...)
      setFullName(res.user.fullName);
      setPhone(res.user.phone ?? "");
      await refreshUser();
      setSuccessMessage(res.message || "Đã lưu thông tin cá nhân.");
    } catch (err) {
      setError(err instanceof ApiError ? err.message || "Không thể lưu thông tin." : "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <UserIcon className="size-5" aria-hidden />
          <h2>Thông tin cá nhân</h2>
        </CardTitle>
        <CardDescription>
          Họ tên và số điện thoại được dùng khi chuyên viên tư vấn liên hệ hỗ trợ hồ sơ du học.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-label">Trạng thái tài khoản:</span>
          {user.status === "pending" ? (
            <Badge variant="warn"><HourglassIcon aria-hidden />Chờ xét duyệt</Badge>
          ) : user.status === "locked" ? (
            <Badge variant="risk"><LockIcon aria-hidden />Khóa bởi Quản trị viên</Badge>
          ) : user.status === "temp_locked" ? (
            <Badge variant="warn"><TriangleAlertIcon aria-hidden />Tạm khóa (Sai 5 lần)</Badge>
          ) : user.status === "unverified" ? (
            <Badge variant="warn"><MailIcon aria-hidden />Chưa xác thực Email</Badge>
          ) : (
            <Badge variant="ok"><CheckIcon aria-hidden />Đang hoạt động</Badge>
          )}
        </div>

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
              <FieldLabel htmlFor="account-email">Email đăng nhập</FieldLabel>
              <Input id="account-email" type="email" value={user.email} disabled readOnly />
              <FieldDescription>Email đăng nhập không thể thay đổi.</FieldDescription>
            </Field>

            <Field>
              <FieldLabel htmlFor="account-full-name">Họ và tên</FieldLabel>
              <Input
                id="account-full-name"
                required
                maxLength={MAX_FULL_NAME_LENGTH}
                autoComplete="name"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
              />
            </Field>

            <Field>
              <FieldLabel htmlFor="account-phone">Số điện thoại</FieldLabel>
              <Input
                id="account-phone"
                type="tel"
                inputMode="tel"
                maxLength={MAX_PHONE_LENGTH}
                autoComplete="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="Ví dụ: 0912345678"
              />
              <FieldDescription>Không bắt buộc. Để trống nếu muốn xóa số điện thoại.</FieldDescription>
            </Field>

            <Button type="submit" disabled={isSubmitting || !isDirty} className="w-full sm:w-auto">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang lưu..." : "Lưu thay đổi"}
            </Button>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  );
}
