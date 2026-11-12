"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { Spinner } from "@/components/ui/spinner";
import { clearPendingEmail, usePendingEmail } from "@/lib/pending-email";

export function ResetPasswordForm() {
  const router = useRouter();

  // Email do bước trước (đăng ký / quên mật khẩu / đăng nhập) để lại trong sessionStorage;
  // người dùng sửa ô email thì dùng giá trị đã nhập
  const pendingEmail = usePendingEmail();
  const [typedEmail, setEmail] = useState<string | null>(null);
  const email = typedEmail ?? pendingEmail;
  const [otpCode, setOtpCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Bộ đếm gửi lại OTP (cooldown 60s)
  const [cooldown, setCooldown] = useState(60);
  const [isResending, setIsResending] = useState(false);

  useEffect(() => {
    if (cooldown > 0) {
      const timer = setTimeout(() => setCooldown(cooldown - 1), 1000);
      return () => clearTimeout(timer);
    }
  }, [cooldown]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    const cleanOtp = otpCode.trim();
    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email.");
      return;
    }
    if (!cleanOtp || cleanOtp.length !== 6 || !/^\d{6}$/.test(cleanOtp)) {
      setError("Mã OTP phải gồm đúng 6 chữ số.");
      return;
    }
    if (newPassword.length < 8) {
      setError("Mật khẩu mới phải có tối thiểu 8 ký tự.");
      return;
    }
    if (newPassword.length > 100) {
      setError("Mật khẩu không được vượt quá 100 ký tự.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.resetPassword({
        email: email.trim(),
        otpCode: cleanOtp,
        newPassword,
      });
      clearPendingEmail();

      setSuccessMessage(res.message || "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập bằng mật khẩu mới.");
      setTimeout(() => {
        router.push("/login");
      }, 2000);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Mã xác thực không hợp lệ hoặc đã hết hạn.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResendOtp = async () => {
    if (cooldown > 0 || isResending) return;
    setError(null);
    setSuccessMessage(null);

    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email để gửi lại mã.");
      return;
    }

    setIsResending(true);
    try {
      const res = await authApi.resendOtp({
        email: email.trim(),
        purpose: "reset_password",
      });
      setSuccessMessage(res.message || "Mã OTP mới đã được gửi tới email của bạn.");
      setCooldown(60);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể gửi lại mã lúc này. Vui lòng thử lại sau.");
      } else {
        setError("Có lỗi kết nối máy chủ khi gửi lại mã OTP.");
      }
    } finally {
      setIsResending(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Thiết lập mật khẩu mới
        </h1>
        <p className="text-sm text-ink-2">
          Nhập mã OTP nhận được qua email cùng mật khẩu mới của bạn.
        </p>
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

      <form onSubmit={handleSubmit}>
        <FieldGroup>
          {/* Email */}
          <Field>
            <FieldLabel htmlFor="reset-email-input">Địa chỉ Email</FieldLabel>
            <Input
              id="reset-email-input"
              type="email"
              required
              maxLength={100}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="example@gmail.com"
            />
          </Field>

          {/* Mã OTP */}
          <Field>
            <div className="flex items-center justify-between">
              <FieldLabel htmlFor="reset-otp-input">Mã xác thực OTP (6 chữ số)</FieldLabel>
              <Badge variant="warn">Hiệu lực trong 5 phút</Badge>
            </div>
            <Input
              id="reset-otp-input"
              type="text"
              required
              maxLength={6}
              pattern="[0-9]*"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={otpCode}
              onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
              placeholder="123456"
              className="text-center font-mono tracking-widest"
            />
            <FieldDescription>
              Mã sẽ tự hủy sau 5 lần nhập sai. Tối đa 5 lần gửi mã mỗi giờ.
            </FieldDescription>
          </Field>

          {/* Mật khẩu mới */}
          <Field>
            <FieldLabel htmlFor="new-password">Mật khẩu mới</FieldLabel>
            <PasswordInput
              id="new-password"
              required
              maxLength={100}
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="Tối thiểu 8 ký tự"
            />
          </Field>

          {/* Xác nhận mật khẩu mới */}
          <Field>
            <FieldLabel htmlFor="confirm-new-password">Xác nhận mật khẩu mới</FieldLabel>
            <PasswordInput
              id="confirm-new-password"
              required
              maxLength={100}
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="Nhập lại mật khẩu mới"
            />
          </Field>

          <Button type="submit" size="xl" disabled={isSubmitting || otpCode.length !== 6} className="w-full">
            {isSubmitting && <Spinner data-icon="inline-start" />}
            {isSubmitting ? "Đang xử lý..." : "Cập nhật mật khẩu"}
          </Button>
        </FieldGroup>
      </form>

      {/* Gửi lại OTP và Đăng nhập */}
      <div className="space-y-2 pt-2 text-center text-sm text-ink-2">
        <p>
          Chưa nhận được mã?{" "}
          <button
            type="button"
            onClick={handleResendOtp}
            disabled={cooldown > 0 || isResending}
            className="font-semibold text-brand hover:underline disabled:opacity-50 disabled:no-underline disabled:cursor-not-allowed"
          >
            {cooldown > 0 ? `Gửi lại sau (${cooldown}s)` : isResending ? "Đang gửi..." : "Gửi lại mã"}
          </button>
        </p>
        <p>
          Đã nhớ mật khẩu?{" "}
          <Link href="/login" className="font-semibold text-brand hover:underline">
            Đăng nhập
          </Link>
        </p>
      </div>
    </div>
  );
}
