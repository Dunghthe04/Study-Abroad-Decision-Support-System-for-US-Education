"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { BellIcon } from "lucide-react";
import { notificationsApi } from "@/lib/api";
import { cn } from "@/lib/utils";

/** Khoảng thời gian làm mới số thông báo chưa đọc. */
const POLL_MS = 60_000;

/**
 * [UC-110] Chuông thông báo trên thanh đầu trang: hiện số chưa đọc, bấm mở trang /notifications.
 * Làm mới khi đổi trang, khi quay lại tab và định kỳ mỗi phút.
 */
export function NotificationBell({ className }: { className?: string }) {
  const pathname = usePathname();
  const [count, setCount] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const load = () => {
      notificationsApi
        .unreadCount()
        .then((r) => {
          if (!cancelled) setCount(r?.count ?? 0);
        })
        .catch(() => {
          // Lỗi mạng: giữ số cũ, lần sau thử lại
        });
    };
    load();
    const timer = window.setInterval(load, POLL_MS);
    const onFocus = () => load();
    window.addEventListener("focus", onFocus);
    window.addEventListener("notifications:changed", onFocus);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      window.removeEventListener("focus", onFocus);
      window.removeEventListener("notifications:changed", onFocus);
    };
  }, [pathname]);

  const label = count > 0 ? `Thông báo, ${count} chưa đọc` : "Thông báo";

  return (
    <Link
      href="/notifications"
      aria-label={label}
      title={label}
      className={cn(
        "relative flex size-9 items-center justify-center rounded-full text-white outline-none transition-colors hover:bg-white/10 focus-visible:ring-3 focus-visible:ring-white/50",
        className,
      )}
    >
      <BellIcon className="size-5" />
      {count > 0 && (
        <span className="absolute -top-0.5 -right-0.5 flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] leading-none font-semibold text-white">
          {count > 99 ? "99+" : count}
        </span>
      )}
    </Link>
  );
}
