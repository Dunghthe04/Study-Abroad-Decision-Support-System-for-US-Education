"use client";

import Link from "next/link";
import { ChevronRightIcon, ShieldAlertIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { usePendingCenterCount } from "@/hooks/use-pending-center-count";
import { adminNavSectionsForRole } from "@/lib/admin-nav";
import { cn } from "@/lib/utils";

export function AdminOverview() {
  const { user } = useAuth();
  const isModerator = user?.role === "moderator";
  const { count: pendingCenters } = usePendingCenterCount(isModerator);

  const sections = adminNavSectionsForRole(user?.role).filter(
    (section) => section.id !== "overview",
  );

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
      <header className="flex flex-col gap-3 border-b border-border pb-5 sm:flex-row sm:items-end sm:justify-between">
        <div className="space-y-1">
          <h1 className="text-h1 text-ink">
            {isModerator ? "Khu vực Kiểm duyệt & Vận hành" : "Trang quản trị"}
          </h1>
          <p className="text-body-s text-muted-foreground">
            Xin chào <strong className="font-semibold text-ink">{user?.fullName}</strong>.{" "}
            {isModerator
              ? "Chọn một phân hệ kiểm duyệt hoặc dữ liệu bên dưới để bắt đầu làm việc."
              : "Chọn một khu vực quản trị bên dưới để bắt đầu."}
          </p>
        </div>

        {isModerator && pendingCenters > 0 && (
          <Link
            href="/admin/centers"
            className="inline-flex items-center gap-2 self-start rounded-lg border border-warn/30 bg-warn-wash px-3.5 py-2 text-xs font-medium text-warn transition-colors hover:bg-warn-wash/80 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none sm:self-auto"
          >
            <ShieldAlertIcon className="size-4 shrink-0" />
            <span>{pendingCenters} hồ sơ trung tâm đang chờ duyệt</span>
            <ChevronRightIcon className="size-3.5 shrink-0" />
          </Link>
        )}
      </header>

      <div className="grid items-start gap-6 lg:grid-cols-2">
        {sections.map((section) => (
          <section
            key={section.id}
            aria-labelledby={`admin-section-${section.id}`}
            className="overflow-hidden rounded-xl border border-border bg-card"
          >
            {section.title && (
              <div className="flex items-center justify-between border-b border-border bg-muted/50 px-4 py-2.5">
                <h2
                  id={`admin-section-${section.id}`}
                  className="font-sans text-sm font-semibold text-ink"
                >
                  {section.title}
                </h2>
                <span className="text-xs text-muted-foreground tabular-nums">
                  {section.items.length} mục
                </span>
              </div>
            )}

            <ul className="divide-y divide-border">
              {section.items.map((item) => {
                const Icon = item.icon;
                const showPending = item.href === "/admin/centers" && pendingCenters > 0;
                return (
                  <li key={item.href}>
                    <Link
                      href={item.href}
                      className={cn(
                        "group flex items-start gap-3.5 px-4 py-3.5 transition-colors",
                        "hover:bg-muted/50 focus-visible:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset focus-visible:outline-none",
                      )}
                    >
                      <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-lg bg-brand-wash text-brand transition-colors group-hover:bg-brand group-hover:text-white">
                        <Icon className="size-[18px]" />
                      </span>
                      <div className="min-w-0 flex-1 space-y-0.5">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-semibold text-ink group-hover:text-brand">
                            {item.label}
                          </span>
                          {showPending && (
                            <span className="inline-flex min-w-5 items-center justify-center rounded-full bg-risk px-1.5 text-[11px] font-semibold leading-5 text-white tabular-nums">
                              {pendingCenters > 99 ? "99+" : pendingCenters}
                            </span>
                          )}
                        </div>
                        <p className="line-clamp-2 text-xs leading-relaxed text-muted-foreground">
                          {item.description}
                        </p>
                      </div>
                      <ChevronRightIcon className="mt-1 size-4 shrink-0 text-muted-foreground/60 transition-transform group-hover:translate-x-0.5 group-hover:text-brand" />
                    </Link>
                  </li>
                );
              })}
            </ul>
          </section>
        ))}
      </div>
    </div>
  );
}
