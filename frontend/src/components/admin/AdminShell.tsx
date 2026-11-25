"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { adminNavSectionsForRole } from "@/lib/admin-nav";
import { usePendingCenterCount } from "@/hooks/use-pending-center-count";
import { cn } from "@/lib/utils";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { AdminUserMenu } from "./AdminUserMenu";

export function AdminShell({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const pathname = usePathname();
  const sections = adminNavSectionsForRole(user?.role);
  // Moderator thấy số hồ sơ trung tâm đang chờ duyệt ngay trên menu
  const { count: pendingCenters } = usePendingCenterCount(user?.role === "moderator");
  const brandLabel =
    user?.role === "moderator" ? "USDSS Moderator System" : "USDSS Admin System";

  const isActive = (href: string) => {
    if (href === "/admin") {
      return pathname === "/admin";
    }
    return pathname === href || pathname.startsWith(`${href}/`);
  };

  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden md:flex-row">
      <aside className="flex shrink-0 flex-col md:h-full md:w-64 md:pr-3">
        <Link
          href="/admin"
          aria-label={brandLabel}
          title={brandLabel}
          className="flex w-fit items-center gap-1 px-4 py-3 focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none sm:pl-6 md:py-2.5"
        >
          <Image
            src="/icons/USDSS.png"
            alt="USDSS"
            width={44}
            height={44}
            priority
            className="-ml-1.5 size-11 shrink-0 object-contain"
          />
          <span className="font-heading text-xl font-semibold tracking-tight text-brand-ink">
            Console
          </span>
        </Link>

        <nav
          aria-label="Menu quản trị"
          className="min-h-0 overflow-x-auto md:flex-1 md:overflow-x-visible md:overflow-y-auto [scrollbar-width:none]"
        >
          <div className="flex gap-1 pr-3 pb-2 md:flex-col md:gap-0 md:pr-0 md:pb-0">
            {sections.map((section) => (
              <div key={section.id} className="flex shrink-0 gap-1 md:flex-col md:gap-1">
                {section.title && (
                  <div className="hidden px-4 pt-3 pb-0.5 text-[13px] leading-4 font-medium text-muted-foreground/60 sm:pl-6 md:block">
                    {section.title}
                  </div>
                )}
                <ul className="flex gap-1 md:flex-col md:gap-1">
                  {section.items.map((item) => {
                    const active = isActive(item.href);
                    const Icon = active ? item.filledIcon : item.icon;
                    return (
                      <li key={item.href} className="shrink-0">
                        <Link
                          href={item.href}
                          aria-current={active ? "page" : undefined}
                          className={cn(
                            "flex items-center gap-2.5 whitespace-nowrap rounded-l-none rounded-r-full py-1.5 pr-4 pl-4 text-sm font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none sm:pl-6",
                            active
                              ? "bg-brand-wash font-semibold text-brand"
                              : "text-muted-foreground hover:bg-foreground/[0.06] hover:text-foreground",
                          )}
                        >
                          <Icon className="size-[18px] shrink-0" />
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
              </div>
            ))}
          </div>
        </nav>

        <div className="hidden items-center justify-between gap-2 px-2 py-2.5 md:flex">
          <div className="min-w-0 flex-1">
            <AdminUserMenu />
          </div>
          <NotificationBell />
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-6 md:py-6">
        <div className="flex items-center justify-between gap-2 md:hidden">
          <div className="min-w-0 flex-1">
            <AdminUserMenu />
          </div>
          <NotificationBell />
        </div>
        {children}
      </div>
    </div>
  );
}
