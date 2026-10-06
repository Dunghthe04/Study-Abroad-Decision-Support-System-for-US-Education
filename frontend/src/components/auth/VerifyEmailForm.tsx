"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";

export function VerifyEmailForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialEmail = searchParams.get("email") || "";

  const { verifyEmail } = useAuth();

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
      const user = await verifyEmail({ email: email.trim(), otpCode: cleanOtp });

      if (user.role === "center" && user.status === "pending") {
        setSuccessMessage("Xác thực email thành công! Tài khoản đại diện trung tâm của bạn đang chờ Quản trị viên duyệt hồ sơ.");
      } else {
        setSuccessMessage("Kích hoạt tài khoản thành công! Đang chuyển hướng...");
        setTimeout(() => {
          router.push("/account");
        }, 1500);
      }
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
    <div className="mx-auto w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="mb-6 text-center">
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-blue-100 text-blue-600">
          <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
          </svg>
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Xác thực Email</h1>
        <p className="mt-2 text-sm text-slate-600">
          Vui lòng nhập mã OTP gồm 6 chữ số vừa được gửi đến hòm thư của bạn để kích hoạt tài khoản.
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
          <label htmlFor="verify-email-input" className="block text-sm font-medium text-slate-700">
            Địa chỉ Email
          </label>
          <input
            id="verify-email-input"
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
            <label htmlFor="otp-input" className="block text-sm font-medium text-slate-700">
              Mã xác thực OTP (6 chữ số)
            </label>
            <span className="text-xs font-medium text-amber-600">Hiệu lực trong 5 phút</span>
          </div>
          <input
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
            className="mt-1 block w-full text-center tracking-[0.5em] font-mono text-xl font-bold rounded-lg border border-slate-300 px-3 py-2.5 text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="mt-1.5 text-xs text-slate-500">
            Mã sẽ tự hủy sau 5 lần nhập sai. Tối đa 5 lần gửi mã mỗi giờ.
          </p>
        </div>

        <button
          type="submit"
          disabled={isSubmitting || otpCode.length !== 6}
          className="mt-2 w-full rounded-lg bg-blue-600 py-2.5 text-sm font-semibold text-white shadow hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:bg-blue-300"
        >
          {isSubmitting ? "Đang xác thực..." : "Kích hoạt tài khoản"}
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
