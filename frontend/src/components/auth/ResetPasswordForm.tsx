"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { authApi, ApiError } from "@/lib/api";

export function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialEmail = searchParams.get("email") || "";

  const [email, setEmail] = useState(initialEmail);
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
    <div className="mx-auto w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="mb-6 text-center">
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-blue-100 text-blue-600">
          <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 11c0 3.517-1.009 6.799-2.753 9.571m-3.44-2.04l.054-.09A13.916 13.916 0 008 11a4 4 0 118 0c0 1.017-.07 2.019-.203 3m-2.118 6.844A21.88 21.88 0 0015.171 17m3.839 1.132c.645-2.026.99-4.17.99-6.39 0-4.97-4.03-9-9-9s-9 4.03-9 9c0 2.12.593 4.103 1.625 5.807" />
          </svg>
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Thiết lập mật khẩu mới</h1>
        <p className="mt-2 text-sm text-slate-600">
          Nhập mã OTP nhận được qua email cùng mật khẩu mới của bạn.
        </p>
      </div>

      {error && (
        <div
          role="alert"
          className="mb-6 rounded-lg border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-800"
        >
          {error}
        </div>
      )}

      {successMessage && (
        <div
          role="status"
          className="mb-6 rounded-lg border border-green-200 bg-green-50 p-4 text-sm font-medium text-green-800"
        >
          {successMessage}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Email */}
        <div>
          <label htmlFor="reset-email-input" className="block text-sm font-medium text-slate-700">
            Địa chỉ Email
          </label>
          <input
            id="reset-email-input"
            type="email"
            required
            maxLength={100}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="example@gmail.com"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Mã OTP */}
        <div>
          <div className="flex items-center justify-between">
            <label htmlFor="reset-otp-input" className="block text-sm font-medium text-slate-700">
              Mã xác thực OTP (6 chữ số)
            </label>
            <span className="text-xs font-medium text-amber-600">Hiệu lực trong 5 phút</span>
          </div>
          <input
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
            className="mt-1 block w-full text-center tracking-[0.5em] font-mono text-xl font-bold rounded-lg border border-slate-300 px-3 py-2.5 text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="mt-1.5 text-xs text-slate-500">
            Mã sẽ tự hủy sau 5 lần nhập sai. Tối đa 5 lần gửi mã mỗi giờ.
          </p>
        </div>

        {/* Mật khẩu mới */}
        <div>
          <label htmlFor="new-password" className="block text-sm font-medium text-slate-700">
            Mật khẩu mới
          </label>
          <input
            id="new-password"
            type="password"
            required
            maxLength={100}
            autoComplete="new-password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            placeholder="Tối thiểu 8 ký tự"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Xác nhận mật khẩu mới */}
        <div>
          <label htmlFor="confirm-new-password" className="block text-sm font-medium text-slate-700">
            Xác nhận mật khẩu mới
          </label>
          <input
            id="confirm-new-password"
            type="password"
            required
            maxLength={100}
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            placeholder="Nhập lại mật khẩu mới"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        <button
          type="submit"
          disabled={isSubmitting || otpCode.length !== 6}
          className="mt-2 w-full rounded-lg bg-blue-600 py-2.5 text-sm font-semibold text-white shadow hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:bg-blue-300"
        >
          {isSubmitting ? "Đang xử lý..." : "Cập nhật mật khẩu"}
        </button>
      </form>

      {/* Gửi lại OTP */}
      <div className="mt-6 flex flex-col items-center justify-center space-y-2 text-sm text-slate-600">
        <div>
          Chưa nhận được mã?{" "}
          <button
            type="button"
            onClick={handleResendOtp}
            disabled={cooldown > 0 || isResending}
            className="font-semibold text-blue-600 hover:text-blue-500 disabled:text-slate-400 disabled:cursor-not-allowed"
          >
            {cooldown > 0 ? `Gửi lại sau (${cooldown}s)` : isResending ? "Đang gửi..." : "Gửi lại mã OTP"}
          </button>
        </div>
        <div>
          <Link href="/login" className="text-xs text-slate-500 hover:text-slate-700">
            ← Quay lại đăng nhập
          </Link>
        </div>
      </div>
    </div>
  );
}
