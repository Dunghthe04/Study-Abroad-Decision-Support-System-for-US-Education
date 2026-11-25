"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { clearPendingEmail, usePendingEmail } from "@/lib/pending-email";
import { AUTH_RETURN_TO_KEY } from "@/components/billing/FamilyInviteForm";
import { NETWORK_ERROR, emailError, otpError } from "@/lib/auth-validation";

export function VerifyEmailForm() {
  const router = useRouter();

  const { verifyEmail } = useAuth();

  // Email do bước trước (đăng ký / quên mật khẩu / đăng nhập) để lại trong sessionStorage;
  // người dùng sửa ô email thì dùng giá trị đã nhập
  const pendingEmail = usePendingEmail();
  const [typedEmail, setEmail] = useState<string | null>(null);
  const email = typedEmail ?? pendingEmail;
  const [otpCode, setOtpCode] = useState("");
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
    const inputError = emailError(email) ?? otpError(cleanOtp);
    if (inputError) {
      setError(inputError);
      return;
    }

    setIsSubmitting(true);
    try {
      const user = await verifyEmail({ email: email.trim(), otpCode: cleanOtp });
      clearPendingEmail();

      if (user.role === "center" && user.status === "pending") {
        setSuccessMessage("Xác thực email thành công! Tài khoản đại diện trung tâm của bạn đang chờ Quản trị viên duyệt hồ sơ.");
      } else {
        setSuccessMessage("Kích hoạt tài khoản thành công! Đang chuyển hướng...");
        // Mở từ trang lời mời gói Gia đình: quay lại đúng trang đó (chỉ nhận đường dẫn nội bộ, như LoginForm)
        let returnTo = "/";
        try {
          const saved = sessionStorage.getItem(AUTH_RETURN_TO_KEY);
          sessionStorage.removeItem(AUTH_RETURN_TO_KEY);
          if (saved && /^\/(?![\/\\])/.test(saved)) returnTo = saved;
        } catch {
          // Trình duyệt chặn lưu trữ: về trang chủ
        }
        setTimeout(() => {
          router.push(returnTo);
        }, 1500);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Mã xác thực không hợp lệ hoặc đã hết hạn.");
      } else {
        setError(NETWORK_ERROR);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResendOtp = async () => {
    if (cooldown > 0 || isResending) return;
    setError(null);
    setSuccessMessage(null);

    const emailProblem = emailError(email);
    if (emailProblem) {
      setError(emailProblem);
      return;
    }

    setIsResending(true);
    try {
      const res = await authApi.resendOtp({
        email: email.trim(),
        purpose: "verify_email",
      });
      setSuccessMessage(res.message || "Mã OTP mới đã được gửi tới email của bạn.");
      setCooldown(60); // Đặt lại bộ đếm 60 giây
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
          Xác thực Email
        </h1>
        <p className="text-sm text-ink-2">
          Vui lòng nhập mã OTP gồm 6 chữ số vừa được gửi đến hòm thư của bạn để kích hoạt tài khoản.
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
            <FieldLabel htmlFor="verify-email-input">Địa chỉ Email</FieldLabel>
            <Input
              id="verify-email-input"
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
              <FieldLabel htmlFor="otp-input">Mã xác thực OTP (6 chữ số)</FieldLabel>
              <Badge variant="warn">Hiệu lực trong 5 phút</Badge>
            </div>
            <Input
              id="otp-input"
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

          <Button type="submit" size="xl" disabled={isSubmitting || otpCode.length !== 6} className="w-full" loading={isSubmitting}>
            {isSubmitting ? "Đang xác thực..." : "Kích hoạt tài khoản"}
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
            className="cursor-pointer rounded-sm font-semibold text-brand outline-none hover:underline focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default disabled:opacity-50 disabled:no-underline"
          >
            {cooldown > 0 ? `Gửi lại sau (${cooldown}s)` : isResending ? "Đang gửi..." : "Gửi lại mã"}
          </button>
        </p>
        <p>
          Đã có tài khoản?{" "}
          <Link href="/login" className="font-semibold text-brand hover:underline">
            Đăng nhập
          </Link>
        </p>
      </div>
    </div>
  );
}
