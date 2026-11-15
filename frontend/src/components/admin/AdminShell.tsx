"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { adminNavForRole } from "@/lib/admin-nav";
import { usePendingCenterCount } from "@/hooks/use-pending-center-count";
import { cn } from "@/lib/utils";
import { AdminUserMenu } from "./AdminUserMenu";

export function AdminShell({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const pathname = usePathname();
  const items = adminNavForRole(user?.role);
  // Moderator thấy số hồ sơ trung tâm đang chờ duyệt ngay trên menu
  const { count: pendingCenters } = usePendingCenterCount(user?.role === "moderator");

  const isActive = (href: string) => {
    if (href === "/admin") {
      return pathname === "/admin";
    }
    return pathname === href || pathname.startsWith(`${href}/`);
  };

  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden md:flex-row">
      <aside className="flex shrink-0 flex-col md:h-full md:w-60 md:pr-3">
        <Link
          href="/admin"
          className="flex items-center gap-2.5 px-4 py-3 font-heading text-lg font-bold tracking-tight text-brand-ink sm:pl-6 md:py-5"
        >
          <span className="whitespace-nowrap">
            {user?.role === "moderator" ? "USAS Moderator System" : "USAS Admin System"}
          </span>
        </Link>

        <nav
          aria-label="Menu quản trị"
          className="min-h-0 md:flex-1 md:overflow-y-auto"
        >
          <ul className="flex gap-1 overflow-x-auto pr-3 pb-2 md:flex-col md:overflow-visible md:pr-0 md:pb-0 [scrollbar-width:none]">
            {items.map((item) => {
              const active = isActive(item.href);
              const Icon = item.icon;
              return (
                <li key={item.href} className="shrink-0">
                  <Link
                    href={item.href}
                    aria-current={active ? "page" : undefined}
                    className={cn(
                      "flex items-center gap-2.5 whitespace-nowrap rounded-l-none rounded-r-full py-2 pr-4 pl-4 text-sm font-medium transition-colors sm:pl-6",
                      active
                        ? "bg-brand-wash font-semibold text-brand"
                        : "text-muted-foreground hover:bg-muted/60 hover:text-foreground",
                    )}
                  >
                    <Icon className="size-4 shrink-0" />
                    {item.label}
                    {item.href === "/admin/centers" && pendingCenters > 0 && (
                      <span
                        className="ml-auto inline-flex min-w-5 items-center justify-center rounded-full bg-risk px-1.5 text-[11px] font-semibold leading-5 text-white"
                        aria-label={`${pendingCenters} hồ sơ trung tâm đang chờ duyệt`}
                        title={`${pendingCenters} hồ sơ trung tâm đang chờ duyệt`}
                      >
                        {pendingCenters > 99 ? "99+" : pendingCenters}
                      </span>
                    )}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>

        <div className="hidden px-2 py-3 md:block">
          <AdminUserMenu />
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-6 md:py-6">
        <div className="md:hidden">
          <AdminUserMenu />
        </div>
        {children}
      </div>
    </div>
  );
}
