"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { authApi, ApiError } from "@/lib/api";

export function UnlockAccountForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialEmail = searchParams.get("email") || "";

  const [email, setEmail] = useState(initialEmail);
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
    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email.");
      return;
    }
    if (!cleanOtp || cleanOtp.length !== 6 || !/^\d{6}$/.test(cleanOtp)) {
      setError("Mã OTP phải gồm đúng 6 chữ số.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.unlockAccount({
        email: email.trim(),
        otpCode: cleanOtp,
      });

      setSuccessMessage(res.message || "Mở khóa tài khoản thành công! Bạn có thể đăng nhập ngay bây giờ.");
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
      setError("Vui lòng nhập địa chỉ email để gửi lại mã mở khóa.");
      return;
    }

    setIsResending(true);
    try {
      const res = await authApi.resendOtp({
        email: email.trim(),
        purpose: "unlock_account",
      });
      setSuccessMessage(res.message || "Mã OTP mở khóa mới đã được gửi tới email của bạn.");
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
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 text-amber-600">
          <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Mở khóa tài khoản</h1>
        <p className="mt-2 text-sm text-slate-600">
          Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá 5 lần. Hãy nhập mã OTP 6 số đã được gửi về email của bạn để mở khóa.
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
          <label htmlFor="unlock-email-input" className="block text-sm font-medium text-slate-700">
            Địa chỉ Email tài khoản
          </label>
          <input
            id="unlock-email-input"
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
            <label htmlFor="unlock-otp-input" className="block text-sm font-medium text-slate-700">
              Mã xác thực mở khóa (6 chữ số)
            </label>
            <span className="text-xs text-slate-500">Hiệu lực trong 10 phút</span>
          </div>
          <input
            id="unlock-otp-input"
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
        </div>

        <button
          type="submit"
          disabled={isSubmitting || otpCode.length !== 6}
          className="mt-2 w-full rounded-lg bg-amber-600 py-2.5 text-sm font-semibold text-white shadow hover:bg-amber-700 focus:outline-none focus:ring-2 focus:ring-amber-500 focus:ring-offset-2 disabled:bg-amber-300"
        >
          {isSubmitting ? "Đang xử lý mở khóa..." : "Mở khóa tài khoản"}
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
            className="font-semibold text-amber-600 hover:text-amber-500 disabled:text-slate-400 disabled:cursor-not-allowed"
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
