"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutDashboardIcon, MessageSquareIcon, ShieldCheckIcon, Building2Icon } from "lucide-react";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/center", label: "Tổng quan", icon: LayoutDashboardIcon, exact: true },
  { href: "/center/leads", label: "Yêu cầu liên hệ", icon: MessageSquareIcon },
  { href: "/center/verification", label: "Xác minh pháp lý", icon: ShieldCheckIcon },
  { href: "/center/profile", label: "Hồ sơ công bố", icon: Building2Icon },
];

export function CenterNav() {
  const pathname = usePathname();

  return (
    <div className="border-b border-line bg-surface">
      <div className="container mx-auto flex items-center justify-between px-4 sm:px-6">
        <div className="flex items-center gap-1 overflow-x-auto py-2">
          {NAV_ITEMS.map((item) => {
            const isActive = item.exact
              ? pathname === item.href
              : pathname.startsWith(item.href);
            const Icon = item.icon;

            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors whitespace-nowrap",
                  isActive
                    ? "bg-brand/10 text-brand"
                    : "text-ink-2 hover:bg-surface-2 hover:text-ink"
                )}
              >
                <Icon className="h-4 w-4" />
                <span>{item.label}</span>
              </Link>
            );
          })}
        </div>
      </div>
    </div>
  );
}

