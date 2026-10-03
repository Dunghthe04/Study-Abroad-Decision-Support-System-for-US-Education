"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import { USE_MOCK_AUTH, mockLogin } from "@/lib/mock-auth";
import type { AuthResponse, LoginRequest } from "@/types/api";

export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectUrl = searchParams.get("redirect") || "/";

  // Form states
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  // Validation states
  const [emailError, setEmailError] = useState("");
  const [passwordError, setPasswordError] = useState("");

  // Submission & API states
  const [isLoading, setIsLoading] = useState(false);
  const [apiError, setApiError] = useState<{ message: string; status?: number } | null>(null);
  const [loginSuccess, setLoginSuccess] = useState<AuthResponse | null>(null);

  // Client-side validation
  const validate = (): boolean => {
    let isValid = true;
    setEmailError("");
    setPasswordError("");
    setApiError(null);

    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setEmailError("Vui lòng nhập địa chỉ email.");
      isValid = false;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setEmailError("Địa chỉ email không đúng định dạng (vd: name@example.com).");
      isValid = false;
    }

    if (!password) {
      setPasswordError("Vui lòng nhập mật khẩu.");
      isValid = false;
    }

    return isValid;
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!validate()) return;

    setIsLoading(true);
    setApiError(null);

    const payload: LoginRequest = {
      email: email.trim(),
      password,
    };

    try {
      let result: AuthResponse;

      if (USE_MOCK_AUTH) {
        result = await mockLogin(payload);
      } else {
        // Gọi API thật khi backend đã sẵn sàng (credentails: include để nhận cookie)
        result = await apiFetch<AuthResponse>("/api/v1/auth/login", {
          method: "POST",
          body: JSON.stringify(payload),
          credentials: "include",
        });
      }

      setLoginSuccess(result);

      // Chuyển hướng sau 1s
      setTimeout(() => {
        router.push(redirectUrl);
        router.refresh();
      }, 1000);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setApiError({ message: err.message, status: err.status });
      } else {
        setApiError({
          message: "Không thể kết nối tới máy chủ. Vui lòng thử lại sau.",
        });
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="mb-6 text-center">
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Đăng nhập</h1>
        <p className="mt-1 text-sm text-slate-500">
          Truy cập hệ thống hỗ trợ ra quyết định du học Mỹ (USAS)
        </p>
      </div>

      {/* Thông báo lỗi từ API */}
      {apiError && (
        <div
          role="alert"
          className={`mb-5 flex items-start gap-3 rounded-lg border p-4 text-sm ${apiError.status === 423
              ? "border-amber-200 bg-amber-50 text-amber-900"
              : "border-red-200 bg-red-50 text-red-900"
            }`}
        >
          <span className="text-lg">
            {apiError.status === 423 ? "🔒" : "⚠️"}
          </span>
          <div className="flex-1">
            <p className="font-semibold">
              {apiError.status === 423
                ? "Tài khoản tạm khóa"
                : apiError.status === 401
                  ? "Đăng nhập thất bại"
                  : "Có lỗi xảy ra"}
            </p>
            <p className="mt-0.5">{apiError.message}</p>
          </div>
        </div>
      )}

      {/* Thông báo đăng nhập thành công */}
      {loginSuccess && (
        <div className="mb-5 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900">
          <p className="font-semibold">Đăng nhập thành công!</p>
          <p className="mt-0.5">
            Chào mừng <strong>{loginSuccess.fullName}</strong>. Đang chuyển hướng...
          </p>
        </div>
      )}

      <form onSubmit={handleSubmit} noValidate className="space-y-4">
        {/* Email */}
        <div>
          <label
            htmlFor="email"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-700"
          >
            Email
          </label>
          <div className="mt-1">
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                if (emailError) setEmailError("");
              }}
              disabled={isLoading || !!loginSuccess}
              placeholder="tenban@gmail.com"
              className={`w-full rounded-lg border px-3.5 py-2.5 text-sm outline-none transition ${emailError
                  ? "border-red-300 bg-red-50/30 focus:border-red-500 focus:ring-2 focus:ring-red-200"
                  : "border-slate-300 bg-white hover:border-slate-400 focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                }`}
            />
          </div>
          {emailError && (
            <p className="mt-1 text-xs text-red-600">{emailError}</p>
          )}
        </div>

        {/* Mật khẩu */}
        <div>
          <div className="flex items-center justify-between">
            <label
              htmlFor="password"
              className="block text-xs font-semibold uppercase tracking-wider text-slate-700"
            >
              Mật khẩu
            </label>
          </div>
          <div className="relative mt-1">
            <input
              id="password"
              name="password"
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                if (passwordError) setPasswordError("");
              }}
              disabled={isLoading || !!loginSuccess}
              placeholder="••••••••"
              className={`w-full rounded-lg border px-3.5 py-2.5 pr-12 text-sm outline-none transition ${passwordError
                  ? "border-red-300 bg-red-50/30 focus:border-red-500 focus:ring-2 focus:ring-red-200"
                  : "border-slate-300 bg-white hover:border-slate-400 focus:border-slate-900 focus:ring-2 focus:ring-slate-200"
                }`}
            />
            <button
              type="button"
              tabIndex={-1}
              onClick={() => setShowPassword(!showPassword)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-medium text-slate-500 hover:text-slate-800"
            >
              {showPassword ? "Ẩn" : "Hiện"}
            </button>
          </div>
          {passwordError && (
            <p className="mt-1 text-xs text-red-600">{passwordError}</p>
          )}
        </div>

        {/* Nút Submit */}
        <button
          type="submit"
          disabled={isLoading || !!loginSuccess}
          className="mt-2 flex w-full items-center justify-center rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white shadow transition hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isLoading ? (
            <span className="flex items-center gap-2">
              <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
              Đang xác thực...
            </span>
          ) : (
            "Đăng nhập"
          )}
        </button>
      </form>

      {/* Điều hướng đăng ký */}
      <p className="mt-6 text-center text-sm text-slate-600">
        Chưa có tài khoản?{" "}
        <Link
          href="/register"
          className="font-semibold text-slate-900 underline hover:text-slate-700"
        >
          Đăng ký ngay
        </Link>
      </p>

      {/* Hộp gợi ý test mock */}
      {USE_MOCK_AUTH && (
        <details className="mt-6 rounded-lg border border-dashed border-slate-200 bg-slate-50/70 p-3 text-xs text-slate-600">
          <summary className="cursor-pointer font-medium text-slate-700 hover:text-slate-900">
            🧪 Gợi ý kiểm thử Mock API (Task B1)
          </summary>
          <ul className="mt-2 list-disc space-y-1 pl-4 text-slate-600">
            <li>
              <strong>Đăng nhập thành công (200):</strong> Nhập email bất kỳ, mật khẩu bất kỳ.
            </li>
            <li>
              <strong>Sai mật khẩu (401):</strong> Nhập mật khẩu là <code>wrong</code>.
            </li>
            <li>
              <strong>Tài khoản bị khóa (423):</strong> Nhập email có chứa chữ <code>locked</code> hoặc <code>khoa</code>.
            </li>
          </ul>
        </details>
      )}
    </div>
  );
}
