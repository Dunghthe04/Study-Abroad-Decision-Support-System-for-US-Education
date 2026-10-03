"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";

const ROLE_LABELS: Record<string, { title: string; icon: string; desc: string }> = {
  student: {
    title: "Học sinh / Sinh viên",
    icon: "🎓",
    desc: "Tìm kiếm thông tin học bổng, lộ trình du học Mỹ",
  },
  parent: {
    title: "Phụ huynh",
    icon: "👨‍👩‍👧",
    desc: "Định hướng và đồng hành cùng lộ trình du học của con",
  },
  center: {
    title: "Trung tâm tư vấn",
    icon: "🏢",
    desc: "Cung cấp các dịch vụ du học Mỹ trên hệ thống",
  },
};

export function AccountView() {
  const { user, logout } = useAuth();
  const router = useRouter();

  if (!user) return null;

  const roleInfo = ROLE_LABELS[user.role] ?? {
    title: user.role,
    icon: "👤",
    desc: "Người dùng hệ thống",
  };

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center justify-between border-b border-slate-200 pb-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">
            Hồ sơ tài khoản
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Thông tin chi tiết tài khoản của bạn trên nền tảng USAS
          </p>
        </div>
        <button
          type="button"
          onClick={handleLogout}
          className="rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-sm transition hover:border-red-200 hover:bg-red-50 hover:text-red-600"
        >
          Đăng xuất
        </button>
      </div>

      {/* Thông báo trạng thái nếu là Trung tâm Chờ duyệt */}
      {user.role === "center" && user.status === "PendingApproval" && (
        <div className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          <span className="text-xl">⏳</span>
          <div>
            <p className="font-semibold">Tài khoản đang chờ Admin phê duyệt</p>
            <p className="mt-1 text-xs text-amber-800">
              Hồ sơ trung tâm của bạn đang trong danh sách xem xét. Sau khi Admin kiểm tra và duyệt, bạn sẽ có quyền đăng tải thông tin khảo sát và dịch vụ.
            </p>
          </div>
        </div>
      )}

      {/* Thẻ thông tin cá nhân */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-4 border-b border-slate-100 pb-6">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-900 text-2xl font-bold text-white shadow-sm">
            {user.fullName.charAt(0).toUpperCase()}
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold text-slate-900">{user.fullName}</h2>
              <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                {roleInfo.icon} {roleInfo.title}
              </span>
            </div>
            <p className="mt-0.5 text-sm text-slate-500">{user.email}</p>
          </div>
        </div>

        <div className="grid gap-4 py-6 sm:grid-cols-2">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Mã người dùng (User ID)
            </p>
            <p className="mt-1 font-mono text-xs text-slate-700">{user.id}</p>
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Trạng thái tài khoản
            </p>
            <p className="mt-1 flex items-center gap-1.5 text-sm font-semibold">
              {user.status === "PendingApproval" ? (
                <span className="inline-flex items-center gap-1.5 text-amber-700">
                  <span className="h-2 w-2 rounded-full bg-amber-500" />
                  Chờ duyệt (PendingApproval)
                </span>
              ) : (
                <span className="inline-flex items-center gap-1.5 text-emerald-700">
                  <span className="h-2 w-2 rounded-full bg-emerald-500" />
                  Đang hoạt động (Active)
                </span>
              )}
            </p>
          </div>

          <div className="sm:col-span-2">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Mô tả vai trò
            </p>
            <p className="mt-1 text-sm text-slate-600">{roleInfo.desc}</p>
          </div>
        </div>

        {/* Các liên kết nhanh */}
        <div className="border-t border-slate-100 pt-5">
          <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">
            Truy cập nhanh
          </p>
          <div className="mt-3 flex flex-wrap gap-2.5">
            <Link
              href="/advisor"
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-3.5 py-2 text-xs font-medium text-slate-800 transition hover:border-slate-300 hover:bg-slate-100"
            >
              🤖 Tư vấn AI (RAG)
            </Link>
            <Link
              href="/centers"
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-3.5 py-2 text-xs font-medium text-slate-800 transition hover:border-slate-300 hover:bg-slate-100"
            >
              🏢 Danh sách trung tâm
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
