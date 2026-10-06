"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { authApi, ApiError } from "@/lib/api";

export function ForgotPasswordForm() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email của bạn.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.forgotPassword({ email: email.trim() });
      setSuccessMessage(res.message || "Mã xác thực đặt lại mật khẩu đã được gửi đến email của bạn.");
      setTimeout(() => {
        router.push(`/reset-password?email=${encodeURIComponent(email.trim())}`);
      }, 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại sau.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="mx-auto w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="mb-6 text-center">
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-blue-100 text-blue-600">
          <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
          </svg>
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Quên mật khẩu?</h1>
        <p className="mt-2 text-sm text-slate-600">
          Nhập địa chỉ email đã đăng ký. Hệ thống sẽ gửi mã OTP gồm 6 chữ số để bạn thiết lập mật khẩu mới.
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
        <div>
          <label htmlFor="forgot-email-input" className="block text-sm font-medium text-slate-700">
            Địa chỉ Email
          </label>
          <input
            id="forgot-email-input"
            type="email"
            required
            maxLength={100}
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="example@gmail.com"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        <button
          type="submit"
          disabled={isSubmitting}
          className="mt-2 w-full rounded-lg bg-blue-600 py-2.5 text-sm font-semibold text-white shadow hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:bg-blue-300"
        >
          {isSubmitting ? "Đang gửi mã..." : "Gửi mã OTP xác nhận"}
        </button>
      </form>

      <div className="mt-6 text-center text-sm text-slate-600">
        Đã nhớ lại mật khẩu?{" "}
        <Link href="/login" className="font-semibold text-blue-600 hover:text-blue-500">
          Đăng nhập ngay
        </Link>
      </div>
    </div>
  );
}
