"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { BellIcon, CheckCheckIcon } from "lucide-react";
import { notificationsApi } from "@/lib/api";
import { onRealtime } from "@/lib/realtime";
import type { NotificationDto } from "@/types/api";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

/** Khoảng thời gian làm mới số thông báo chưa đọc. */
const POLL_MS = 60_000;
/** Số thông báo mới nhất hiện trong bảng thả xuống. */
const PREVIEW_SIZE = 8;

/** Chỉ mở đường dẫn nội bộ (backend cũng chỉ lưu đường dẫn bắt đầu bằng "/"). */
function isInternalLink(link: string | null): link is string {
  return !!link && link.startsWith("/") && !link.startsWith("//");
}

function formatTime(iso: string) {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

/**
 * [UC-110] Chuông thông báo trên thanh đầu trang: hiện số chưa đọc, bấm mở bảng thông báo ngay dưới chuông (không chuyển trang).
 * Làm mới khi đổi trang, khi quay lại tab, khi có thông báo đẩy xuống và định kỳ mỗi phút.
 */
export function NotificationBell({ className }: { className?: string }) {
  const pathname = usePathname();
  const router = useRouter();
  const [count, setCount] = useState(0);
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const loadCount = useCallback(() => {
    notificationsApi
      .unreadCount()
      .then((r) => setCount(r?.count ?? 0))
      .catch(() => {
        // Lỗi mạng: giữ số cũ, lần sau thử lại
      });
  }, []);

  const loadItems = useCallback(() => {
    notificationsApi
      .list({ page: 1, pageSize: PREVIEW_SIZE })
      .then((r) => {
        setError(null);
        setItems(r.items);
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "Không tải được thông báo."));
  }, []);

  useEffect(() => {
    loadCount();
    const timer = window.setInterval(loadCount, POLL_MS);
    window.addEventListener("focus", loadCount);
    window.addEventListener("notifications:changed", loadCount);
    const offPush = onRealtime("notification", () => {
      loadCount();
      if (open) loadItems();
    });
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", loadCount);
      window.removeEventListener("notifications:changed", loadCount);
      offPush();
    };
  }, [pathname, loadCount, loadItems, open]);

  // Mỗi lần mở bảng thì tải danh sách mới
  useEffect(() => {
    if (open) loadItems();
  }, [open, loadItems]);

  const markAllRead = async () => {
    setBusy(true);
    try {
      await notificationsApi.markAllRead();
      setItems((prev) => prev?.map((n) => ({ ...n, isRead: true })) ?? prev);
      setCount(0);
      window.dispatchEvent(new Event("notifications:changed"));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không đánh dấu được.");
    } finally {
      setBusy(false);
    }
  };

  const openItem = async (n: NotificationDto) => {
    if (!n.isRead) {
      try {
        await notificationsApi.markRead(n.id);
        window.dispatchEvent(new Event("notifications:changed"));
      } catch {
        // Vẫn cho mở màn hình liên quan dù đánh dấu thất bại
      }
    }
    setOpen(false);
    if (isInternalLink(n.linkUrl)) router.push(n.linkUrl);
  };

  const label = count > 0 ? `Thông báo, ${count} chưa đọc` : "Thông báo";

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        aria-label={label}
        title={label}
        className={cn(
          "relative flex size-9 items-center justify-center rounded-full text-white outline-none transition-colors hover:bg-white/10 focus-visible:ring-3 focus-visible:ring-white/50 data-popup-open:bg-white/10",
          className,
        )}
      >
        <BellIcon className="size-5" />
        {count > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] leading-none font-semibold text-white">
            {count > 99 ? "99+" : count}
          </span>
        )}
      </PopoverTrigger>
      <PopoverContent align="end" sideOffset={8} className="w-[min(24rem,calc(100vw-1.5rem))] gap-0 p-0">
        <div className="flex items-center justify-between gap-2 border-b px-4 py-3">
          <h2 className="font-heading text-sm font-semibold text-ink">Thông báo</h2>
          <button
            type="button"
            onClick={() => void markAllRead()}
            disabled={busy || count === 0}
            className="inline-flex cursor-pointer items-center gap-1 text-xs font-medium text-brand hover:underline disabled:cursor-default disabled:text-ink-3 disabled:no-underline"
          >
            <CheckCheckIcon className="size-3.5" />
            Đánh dấu tất cả đã đọc
          </button>
        </div>

        <div className="max-h-96 overflow-y-auto">
          {error ? (
            <p role="alert" className="px-4 py-3 text-sm text-red-700">
              {error}
            </p>
          ) : items === null ? (
            <div className="space-y-2 p-3">
              {Array.from({ length: 3 }, (_, i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-8 text-sm text-ink-2">
              <BellIcon className="size-7 text-ink-3" />
              Chưa có thông báo nào.
            </div>
          ) : (
            <ul className="divide-y">
              {items.map((n) => (
                <li key={n.id}>
                  <button
                    type="button"
                    onClick={() => void openItem(n)}
                    className={cn(
                      "flex w-full cursor-pointer items-start gap-3 px-4 py-3 text-left transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                      !n.isRead && "bg-brand-wash/40",
                    )}
                  >
                    <span
                      aria-hidden
                      className={cn("mt-1.5 size-2 shrink-0 rounded-full", n.isRead ? "bg-transparent" : "bg-brand")}
                    />
                    <span className="min-w-0 flex-1">
                      <span className={cn("block text-sm text-ink", !n.isRead && "font-semibold")}>{n.title}</span>
                      {n.body && <span className="mt-0.5 line-clamp-2 block text-sm break-words text-ink-2">{n.body}</span>}
                      <span className="mt-1 block text-xs text-ink-3">
                        {formatTime(n.createdAt)}
                        {!n.isRead && <span className="sr-only"> · chưa đọc</span>}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="border-t px-4 py-2.5 text-center">
          <Link href="/notifications" onClick={() => setOpen(false)} className="text-sm font-medium text-brand hover:underline">
            Xem tất cả
          </Link>
        </div>
      </PopoverContent>
    </Popover>
  );
}
