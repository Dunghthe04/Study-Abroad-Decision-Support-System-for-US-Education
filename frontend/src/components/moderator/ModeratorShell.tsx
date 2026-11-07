"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { MODERATOR_NAV } from "@/lib/moderator-nav";
import { cn } from "@/lib/utils";
import { AdminUserMenu } from "@/components/admin/AdminUserMenu";
import { ArrowLeftRightIcon } from "lucide-react";

export function ModeratorShell({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const pathname = usePathname();

  const isActive = (href: string) => {
    if (href === "/moderator") {
      return pathname === "/moderator";
    }
    return pathname === href || pathname.startsWith(`${href}/`);
  };

  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden md:flex-row">
      <aside className="flex shrink-0 flex-col md:h-full md:w-60 md:pr-3">
        <Link
          href="/moderator"
          className="flex items-center gap-2.5 px-4 py-3 font-heading text-lg font-bold tracking-tight text-brand-ink sm:pl-6 md:py-5"
        >
          <span className="whitespace-nowrap">USAS Moderator</span>
        </Link>

        {user?.role === "admin" && (
          <div className="px-4 pb-2 sm:pl-6">
            <Link
              href="/admin"
              className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-brand font-medium"
            >
              <ArrowLeftRightIcon className="size-3" />
              <span>Chuyển sang Admin Portal</span>
            </Link>
          </div>
        )}

        <nav
          aria-label="Menu kiểm duyệt & vận hành"
          className="min-h-0 md:flex-1 md:overflow-y-auto"
        >
          <ul className="flex gap-1 overflow-x-auto pr-3 pb-2 md:flex-col md:overflow-visible md:pr-0 md:pb-0 [scrollbar-width:none]">
            {MODERATOR_NAV.map((item) => {
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

      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-6 md:overflow-hidden md:py-6">
        <div className="md:hidden">
          <AdminUserMenu />
        </div>
        {children}
      </div>
    </div>
  );
}
