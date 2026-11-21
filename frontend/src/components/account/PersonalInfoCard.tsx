"use client";

import React, { useState } from "react";
import {
  CheckIcon,
  HourglassIcon,
  LockIcon,
  MailIcon,
  RotateCcwIcon,
  TriangleAlertIcon,
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { formatUserRole } from "@/lib/user-role-labels";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const MAX_FULL_NAME_LENGTH = 100;
const MAX_PHONE_LENGTH = 20;

function formatDob(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}

// Thông tin cá nhân: sửa được họ tên và số điện thoại. Email, vai trò và ngày sinh chỉ đọc.
export function PersonalInfoCard() {
  const { user, refreshUser } = useAuth();
  const [fullName, setFullName] = useState(user?.fullName ?? "");
  const [phone, setPhone] = useState(user?.phone ?? "");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!user) return null;

  const isDirty = fullName.trim() !== user.fullName || phone.trim() !== (user.phone ?? "");

  const handleReset = () => {
    setFullName(user.fullName);
    setPhone(user.phone ?? "");
    setError(null);
    setSuccessMessage(null);
  };

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
      setError(
        err instanceof ApiError
          ? err.message || "Không thể lưu thông tin."
          : "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const statusBadge =
    user.status === "pending" ? (
      <Badge variant="warn"><HourglassIcon aria-hidden />Chờ xét duyệt</Badge>
    ) : user.status === "locked" ? (
      <Badge variant="risk"><LockIcon aria-hidden />Khóa bởi Quản trị viên</Badge>
    ) : user.status === "temp_locked" ? (
      <Badge variant="warn"><TriangleAlertIcon aria-hidden />Tạm khóa (Sai 5 lần)</Badge>
    ) : user.status === "unverified" ? (
      <Badge variant="warn"><MailIcon aria-hidden />Chưa xác thực Email</Badge>
    ) : (
      <Badge variant="ok"><CheckIcon aria-hidden />Đang hoạt động</Badge>
    );

  return (
    <div>
      <AccountSectionHeader
        title="Thông tin cá nhân"
        description="Họ tên và số điện thoại được dùng khi chuyên viên tư vấn liên hệ hỗ trợ hồ sơ du học."
        badge={statusBadge}
      />

      <div className="space-y-6">
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

        {/* Khối định danh chỉ đọc */}
        <div className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
          <div className="flex flex-col gap-2 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-8 sm:px-6">
            <div className="space-y-0.5 sm:w-56 sm:shrink-0">
              <div className="text-sm font-medium text-ink">Email đăng nhập</div>
              <p className="text-xs text-ink-3">Cố định theo tài khoản.</p>
            </div>
            <div className="flex min-w-0 flex-1 items-center gap-2 text-sm font-medium text-ink">
              <span className="truncate">{user.email}</span>
              <LockIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
            </div>
          </div>

          <div className="flex flex-col gap-2 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-8 sm:px-6">
            <div className="space-y-0.5 sm:w-56 sm:shrink-0">
              <div className="text-sm font-medium text-ink">Vai trò</div>
              <p className="text-xs text-ink-3">Phân quyền hiện tại trên USDSS.</p>
            </div>
            <div className="flex-1">
              <Badge variant="neutral">{formatUserRole(user.role)}</Badge>
            </div>
          </div>

          {user.dateOfBirth && (
            <div className="flex flex-col gap-2 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-8 sm:px-6">
              <div className="space-y-0.5 sm:w-56 sm:shrink-0">
                <div className="text-sm font-medium text-ink">Ngày sinh</div>
                <p className="text-xs text-ink-3">Chỉ Quản trị viên điều chỉnh được.</p>
              </div>
              <div className="flex-1 text-sm font-medium text-ink tabular-nums">
                {formatDob(user.dateOfBirth)}
              </div>
            </div>
          )}
        </div>

        {/* Khối thông tin liên hệ chỉnh sửa được */}
        <form
          onSubmit={handleSubmit}
          noValidate
          className="overflow-hidden rounded-xl border border-line bg-surface"
        >
          <div className="divide-y divide-line">
            <div className="flex flex-col gap-3 px-5 py-5 sm:flex-row sm:items-start sm:gap-8 sm:px-6">
              <div className="space-y-1 sm:w-56 sm:shrink-0">
                <Label htmlFor="account-full-name" className="text-sm font-medium text-ink">
                  Họ và tên
                </Label>
                <p className="text-xs leading-relaxed text-ink-2">
                  Tên hiển thị trên hồ sơ và trong các yêu cầu gửi tới trung tâm.
                </p>
              </div>
              <div className="min-w-0 flex-1">
                <Input
                  id="account-full-name"
                  required
                  maxLength={MAX_FULL_NAME_LENGTH}
                  autoComplete="name"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className="h-10 max-w-md"
                />
              </div>
            </div>

            <div className="flex flex-col gap-3 px-5 py-5 sm:flex-row sm:items-start sm:gap-8 sm:px-6">
              <div className="space-y-1 sm:w-56 sm:shrink-0">
                <Label htmlFor="account-phone" className="text-sm font-medium text-ink">
                  Số điện thoại
                </Label>
                <p className="text-xs leading-relaxed text-ink-2">
                  {user.role === "center"
                    ? "Trung tâm đổi số điện thoại tại mục Hồ sơ trung tâm (cần xác thực)."
                    : "Không bắt buộc. Để trống nếu muốn xóa số điện thoại."}
                </p>
              </div>
              <div className="min-w-0 flex-1">
                <Input
                  id="account-phone"
                  type="tel"
                  inputMode="tel"
                  maxLength={MAX_PHONE_LENGTH}
                  autoComplete="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="Ví dụ: 0912345678"
                  disabled={user.role === "center"}
                  readOnly={user.role === "center"}
                  className="h-10 max-w-md"
                />
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line bg-ground/60 px-5 py-3.5 sm:px-6">
            <span className="text-xs text-ink-3">
              {isDirty ? "Bạn có thay đổi chưa lưu." : "Thông tin hiện tại đã được đồng bộ."}
            </span>
            <div className="flex w-full flex-wrap items-center justify-end gap-2 sm:w-auto">
              {isDirty && (
                <Button
                  type="button"
                  variant="ghost"
                  size="lg"
                  disabled={isSubmitting}
                  onClick={handleReset}
                >
                  <RotateCcwIcon data-icon="inline-start" aria-hidden />
                  Hoàn tác
                </Button>
              )}
              <Button
                type="submit"
                size="lg"
                disabled={isSubmitting || !isDirty}
                loading={isSubmitting}
                className="w-full sm:w-auto"
              >
                <CheckIcon data-icon="inline-start" aria-hidden />
                {isSubmitting ? "Đang lưu..." : "Lưu thay đổi"}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
