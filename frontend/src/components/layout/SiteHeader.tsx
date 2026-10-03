"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";

const links = [
  { href: "/advisor", label: "Tư vấn AI" },
  { href: "/centers", label: "Trung tâm" },
];

const ROLE_LABELS: Record<string, string> = {
  student: "Học sinh",
  parent: "Phụ huynh",
  center: "Trung tâm",
};

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
        {/* Logo & Navigation */}
        <div className="flex items-center gap-8">
          <Link href="/" className="text-lg font-bold tracking-tight text-slate-900">
            USAS
          </Link>
          <ul className="flex gap-5 text-sm font-medium text-slate-600">
            {links.map((l) => (
              <li key={l.href}>
                <Link href={l.href} className="transition hover:text-slate-900">
                  {l.label}
                </Link>
              </li>
            ))}
          </ul>
        </div>

        {/* Auth Section */}
        <div className="flex items-center gap-3">
          {isLoading ? (
            <div className="h-8 w-24 animate-pulse rounded bg-slate-100" />
          ) : user ? (
            /* Đã đăng nhập: Hiện tên, role, link /account và nút Đăng xuất */
            <div className="flex items-center gap-3">
              <Link
                href="/account"
                className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 transition hover:bg-slate-100"
              >
                <span className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-900 text-xs font-semibold text-white">
                  {user.fullName.charAt(0).toUpperCase()}
                </span>
                <span className="max-w-[140px] truncate text-sm font-medium text-slate-800">
                  {user.fullName}
                </span>
                <span className="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-slate-700">
                  {ROLE_LABELS[user.role] ?? user.role}
                </span>
                {user.role === "center" && user.status === "PendingApproval" && (
                  <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-medium text-amber-800">
                    Chờ duyệt
                  </span>
                )}
              </Link>
              <button
                type="button"
                onClick={handleLogout}
                className="rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-50 hover:text-red-600"
              >
                Đăng xuất
              </button>
            </div>
          ) : (
            /* Chưa đăng nhập: Hiện nút Đăng nhập và Đăng ký */
            <div className="flex items-center gap-2">
              <Link
                href="/login"
                className="rounded-lg px-3 py-1.5 text-sm font-medium text-slate-600 transition hover:text-slate-900"
              >
                Đăng nhập
              </Link>
              <Link
                href="/register"
                className="rounded-lg bg-slate-900 px-3.5 py-1.5 text-sm font-medium text-white shadow-sm transition hover:bg-slate-800"
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
