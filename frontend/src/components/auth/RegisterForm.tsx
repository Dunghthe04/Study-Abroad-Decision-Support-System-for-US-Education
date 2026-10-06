"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";

export function RegisterForm() {
  const router = useRouter();
  const { register, login } = useAuth();

  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [role, setRole] = useState<"student" | "parent" | "center">("student");
  const [parentAcknowledged, setParentAcknowledged] = useState(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Hàm kiểm tra và chuẩn hóa số điện thoại Việt Nam
  const validateVietnamesePhone = (raw: string): { valid: boolean; normalized?: string } => {
    const cleaned = raw.trim().replace(/[\s.\-()]/g, "");
    let normalized = cleaned;
    if (normalized.startsWith("+84")) {
      normalized = "0" + normalized.slice(3);
    } else if (normalized.startsWith("84") && normalized.length === 11) {
      normalized = "0" + normalized.slice(2);
    }
    // Định dạng di động 10 số Việt Nam: 03x, 05x, 07x, 08x, 09x
    const regex = /^0(3|5|7|8|9)\d{8}$/;
    if (regex.test(normalized)) {
      return { valid: true, normalized };
    }
    return { valid: false };
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // 1. Kiểm tra tính hợp lệ cơ bản ở phía client
    if (!fullName.trim()) {
      setError(role === "center" ? "Vui lòng nhập tên trung tâm hoặc người đại diện." : "Vui lòng nhập họ và tên.");
      return;
    }
    if (fullName.trim().length > 100) {
      setError("Họ và tên không được vượt quá 100 ký tự.");
      return;
    }
    if (!email.trim()) {
      setError("Vui lòng nhập email.");
      return;
    }
    if (email.trim().length > 100) {
      setError("Email không được vượt quá 100 ký tự.");
      return;
    }
    if (password.length < 8) {
      setError("Mật khẩu phải có tối thiểu 8 ký tự.");
      return;
    }
    if (password.length > 100) {
      setError("Mật khẩu không được vượt quá 100 ký tự.");
      return;
    }
    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }
    if (role === "student" && !parentAcknowledged) {
      setError("Học sinh cần xác nhận phụ huynh đã biết về việc tạo tài khoản này.");
      return;
    }

    // 2. Validate số điện thoại Việt Nam nếu có nhập
    let normalizedPhone: string | null = null;
    if (phone.trim()) {
      if (phone.trim().length > 20) {
        setError("Số điện thoại không được vượt quá 20 ký tự.");
        return;
      }
      const phoneValidation = validateVietnamesePhone(phone);
      if (!phoneValidation.valid) {
        setError("Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");
        return;
      }
      normalizedPhone = phoneValidation.normalized!;
    }

    setIsSubmitting(true);
    try {
      // 3. Gọi API đăng ký
      await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: normalizedPhone,
        role,
        parentAcknowledged: role === "student" ? true : undefined,
        password,
      });

      // 4. Đăng ký thành công -> Tự động đăng nhập
      await login({
        email: email.trim(),
        password,
      });

      router.push("/account");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Đăng ký không thành công. Vui lòng thử lại.");
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
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Đăng ký tài khoản</h1>
        <p className="mt-2 text-sm text-slate-600">
          Tạo tài khoản để nhận tư vấn du học Mỹ và quản lý hồ sơ cá nhân
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

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Lựa chọn vai trò: Học sinh, Phụ huynh, Trung tâm */}
        <div>
          <label className="block text-sm font-semibold text-slate-700">Bạn là:</label>
          <div className="mt-2 grid grid-cols-3 gap-2">
            <button
              type="button"
              onClick={() => setRole("student")}
              className={`flex items-center justify-center rounded-lg border py-2.5 text-xs sm:text-sm font-medium transition-colors ${
                role === "student"
                  ? "border-blue-600 bg-blue-50 text-blue-700 font-semibold shadow-sm"
                  : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50"
              }`}
            >
              🎓 Học sinh
            </button>
            <button
              type="button"
              onClick={() => setRole("parent")}
              className={`flex items-center justify-center rounded-lg border py-2.5 text-xs sm:text-sm font-medium transition-colors ${
                role === "parent"
                  ? "border-blue-600 bg-blue-50 text-blue-700 font-semibold shadow-sm"
                  : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50"
              }`}
            >
              👨‍👩‍👧 Phụ huynh
            </button>
            <button
              type="button"
              onClick={() => setRole("center")}
              className={`flex items-center justify-center rounded-lg border py-2.5 text-xs sm:text-sm font-medium transition-colors ${
                role === "center"
                  ? "border-blue-600 bg-blue-50 text-blue-700 font-semibold shadow-sm"
                  : "border-slate-200 bg-white text-slate-700 hover:bg-slate-50"
              }`}
            >
              🏢 Trung tâm
            </button>
          </div>
        </div>

        {/* Thông báo riêng cho tài khoản trung tâm */}
        {role === "center" && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
            ℹ️ Tài khoản Trung tâm du học sẽ ở trạng thái chờ duyệt (Pending) bởi Ban quản trị trước khi có thể công khai hồ sơ.
          </div>
        )}

        {/* Họ và tên */}
        <div>
          <label htmlFor="fullName" className="block text-sm font-medium text-slate-700">
            {role === "center" ? "Tên trung tâm / Đại diện" : "Họ và tên"} <span className="text-red-500">*</span>
          </label>
          <input
            id="fullName"
            type="text"
            required
            maxLength={100}
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder={role === "center" ? "Trung tâm Du học Á Âu" : "Nguyễn Văn A"}
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Email đăng nhập */}
        <div>
          <label htmlFor="email" className="block text-sm font-medium text-slate-700">
            Email <span className="text-red-500">*</span>
          </label>
          <input
            id="email"
            type="email"
            required
            maxLength={100}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="example@gmail.com"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Số điện thoại Việt Nam */}
        <div>
          <label htmlFor="phone" className="block text-sm font-medium text-slate-700">
            Số điện thoại liên hệ (Việt Nam)
          </label>
          <input
            id="phone"
            type="tel"
            maxLength={20}
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="0912 345 678 hoặc +84912345678"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="mt-1 text-xs text-slate-500">
            Chỉ chấp nhận số điện thoại di động Việt Nam (10 chữ số, đầu số 03, 05, 07, 08, 09).
          </p>
        </div>

        {/* Mật khẩu */}
        <div>
          <label htmlFor="password" className="block text-sm font-medium text-slate-700">
            Mật khẩu <span className="text-red-500">*</span>
          </label>
          <input
            id="password"
            type="password"
            required
            minLength={8}
            maxLength={100}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Tối thiểu 8 ký tự, tối đa 100"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Xác nhận mật khẩu */}
        <div>
          <label htmlFor="confirmPassword" className="block text-sm font-medium text-slate-700">
            Xác nhận mật khẩu <span className="text-red-500">*</span>
          </label>
          <input
            id="confirmPassword"
            type="password"
            required
            maxLength={100}
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            placeholder="Nhập lại mật khẩu"
            className="mt-1 block w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {/* Checkbox dành riêng cho vai học sinh */}
        {role === "student" && (
          <div className="flex items-start gap-2 pt-1">
            <input
              id="parentAcknowledged"
              type="checkbox"
              checked={parentAcknowledged}
              onChange={(e) => setParentAcknowledged(e.target.checked)}
              className="mt-1 h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
            />
            <label htmlFor="parentAcknowledged" className="text-xs text-slate-600">
              Tôi xác nhận phụ huynh đã biết về việc tôi đăng ký tài khoản tư vấn du học trên hệ thống.
            </label>
          </div>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="mt-2 w-full rounded-lg bg-blue-600 py-2.5 text-sm font-semibold text-white shadow hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:bg-blue-300"
        >
          {isSubmitting ? "Đang xử lý..." : "Tạo tài khoản"}
        </button>
      </form>

      <div className="mt-6 text-center text-sm text-slate-600">
        Đã có tài khoản?{" "}
        <Link href="/login" className="font-semibold text-blue-600 hover:text-blue-500">
          Đăng nhập ngay
        </Link>
      </div>
    </div>
  );
}
