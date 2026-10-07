"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";

const links = [
  { href: "/advisor", label: "Tư vấn AI" },
  { href: "/profile/financial", label: "Tài chính & Ngoại khóa" },
  { href: "/centers", label: "Trung tâm" },
];

export function SiteHeader() {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  return (
    <header className="border-b border-slate-200 bg-white">
      <nav className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <div className="flex items-center gap-8">
          <Link href="/" className="text-lg font-bold tracking-tight text-blue-600">
            USAS
          </Link>
          <ul className="flex gap-6 text-sm font-medium text-slate-600">
            {links.map((l) => (
              <li key={l.href}>
                <Link href={l.href} className="transition hover:text-slate-900">
                  {l.label}
                </Link>
              </li>
            ))}
            {user && (user.role === "student" || user.role === "parent") && (
              <li>
                <Link
                  href="/profile/academic"
                  className="font-medium text-blue-600 transition hover:text-blue-700 flex items-center gap-1"
                >
                  <span>📑</span> Hồ sơ học thuật
                </Link>
              </li>
            )}
          </ul>
        </div>

        {/* Trạng thái xác thực của người dùng (Khách vs Đã đăng nhập) */}
        <div className="flex items-center gap-3">
          {isLoading ? (
            <div className="h-8 w-20 animate-pulse rounded bg-slate-100" />
          ) : user ? (
            <div className="flex items-center gap-4">
              <Link
                href="/account"
                className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-700 transition hover:bg-slate-50"
              >
                <span>👤 {user.fullName}</span>
                <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] font-semibold text-blue-700">
                  {user.role === "student" ? "Học sinh" : user.role === "parent" ? "Phụ huynh" : user.role}
                </span>
              </Link>
              <button
                onClick={handleLogout}
                className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-100 hover:text-red-600"
              >
                Đăng xuất
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-2 text-sm font-medium">
              <Link
                href="/login"
                className="rounded-lg px-3 py-1.5 text-slate-700 transition hover:bg-slate-100 hover:text-slate-900"
              >
                Đăng nhập
              </Link>
              <Link
                href="/register"
                className="rounded-lg bg-blue-600 px-3.5 py-1.5 text-white shadow-sm transition hover:bg-blue-700"
              >
                Đăng ký
              </Link>
            </div>
          )}
        </div>
      </nav>
    </header>
  );
}
