"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";

export function AccountView() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  if (!user) return null;

  const handleLogout = async () => {
    setIsLoggingOut(true);
    await logout();
    router.push("/login");
  };

  const roleLabel =
    user.role === "student"
      ? "🎓 Học sinh"
      : user.role === "parent"
        ? "👨‍👩‍👧 Phụ huynh"
        : user.role === "center"
          ? "🏢 Trung tâm du học"
          : "🛡️ Quản trị viên";

  return (
    <div className="mx-auto w-full max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
      <div className="flex items-center justify-between border-b border-slate-100 pb-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Hồ sơ tài khoản</h1>
          <p className="mt-1 text-sm text-slate-500">
            Thông tin cá nhân được bảo vệ và quản lý theo phiên làm việc an toàn
          </p>
        </div>
        <button
          onClick={handleLogout}
          disabled={isLoggingOut}
          className="rounded-lg border border-red-200 bg-white px-4 py-2 text-sm font-medium text-red-600 shadow-sm transition hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-500 disabled:opacity-50"
        >
          {isLoggingOut ? "Đang đăng xuất..." : "Đăng xuất"}
        </button>
      </div>

      <div className="mt-6 space-y-5">
        <div className="flex items-center justify-between rounded-xl bg-slate-50 p-4">
          <div>
            <div className="text-xs font-medium text-slate-500">Vai trò trong hệ thống</div>
            <div className="mt-1 text-base font-semibold text-slate-900">{roleLabel}</div>
          </div>
          {user.status === "pending" ? (
            <span className="inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800">
              ⏳ Chờ xét duyệt
            </span>
          ) : user.status === "locked" ? (
            <span className="inline-flex items-center rounded-full bg-red-100 px-3 py-1 text-xs font-semibold text-red-800">
              🔒 Khóa bởi Quản trị viên
            </span>
          ) : user.status === "temp_locked" ? (
            <span className="inline-flex items-center rounded-full bg-orange-100 px-3 py-1 text-xs font-semibold text-orange-800">
              ⚠️ Tạm khóa (Sai 5 lần)
            </span>
          ) : user.status === "unverified" ? (
            <span className="inline-flex items-center rounded-full bg-yellow-100 px-3 py-1 text-xs font-semibold text-yellow-800">
              ✉️ Chưa xác thực Email
            </span>
          ) : (
            <span className="inline-flex items-center rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
              ✓ Đang hoạt động
            </span>
          )}
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="rounded-xl border border-slate-200 p-4">
            <span className="block text-xs font-medium text-slate-500">Họ và tên</span>
            <span className="mt-1 block text-sm font-semibold text-slate-900">{user.fullName}</span>
          </div>

          <div className="rounded-xl border border-slate-200 p-4">
            <span className="block text-xs font-medium text-slate-500">Email đăng nhập</span>
            <span className="mt-1 block text-sm font-semibold text-slate-900">{user.email}</span>
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 p-4">
          <span className="block text-xs font-medium text-slate-500">Số điện thoại liên hệ</span>
          <span className="mt-1 block text-sm font-semibold text-slate-900">
            {user.phone ? user.phone : <span className="italic text-slate-400">Chưa cung cấp</span>}
          </span>
          <p className="mt-2 text-xs text-slate-500">
            Số điện thoại được sử dụng để chuyên viên tư vấn liên hệ hỗ trợ hồ sơ du học.
          </p>
        </div>

        {/* [USAS-363] Thẻ liên kết Hồ sơ học thuật & Bảng điểm (Học sinh / Phụ huynh) */}
        {(user.role === "student" || user.role === "parent") && (
          <div className="rounded-xl border border-blue-200 bg-gradient-to-r from-blue-50/80 to-indigo-50/80 p-5 shadow-sm">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-base font-bold text-slate-900">📑 Hồ sơ học thuật & Bảng điểm</span>
                  {/* <span className="rounded-full bg-blue-100 px-2 py-0.5 text-[10px] font-bold text-blue-700">
                    B1-01 & B1-02
                  </span> */}
                </div>
                <p className="mt-1 text-xs text-slate-600">
                  Nhập bảng điểm 3 năm gần nhất theo thang điểm của trường, cùng chứng chỉ IELTS/SAT để AI phân tích năng lực học thuật.
                </p>
              </div>
              <Link
                href="/profile/academic"
                className="inline-flex items-center justify-center rounded-lg bg-blue-600 px-4 py-2 text-xs font-bold text-white shadow-sm transition hover:bg-blue-700 whitespace-nowrap"
              >
                Cập nhật bảng điểm & chứng chỉ →
              </Link>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
