"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { BellIcon, CheckCheckIcon } from "lucide-react";
import { notificationsApi } from "@/lib/api";
import { onRealtime } from "@/lib/realtime";
import type { NotificationDto, PagedResult } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 20;

/** Chỉ mở đường dẫn nội bộ (backend cũng chỉ lưu đường dẫn bắt đầu bằng "/"). */
function isInternalLink(link: string | null): link is string {
  return !!link && link.startsWith("/") && !link.startsWith("//");
}

function formatTime(iso: string) {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
}

/** Báo chuông trên thanh đầu trang tải lại số chưa đọc. */
function announceChanged() {
  window.dispatchEvent(new Event("notifications:changed"));
}

/** [UC-110] Danh sách thông báo của chính người dùng: lọc chưa đọc, đánh dấu đã đọc, bấm để mở màn hình liên quan. */
export function NotificationsView() {
  const router = useRouter();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<NotificationDto> | null>(null);
  const [unreadCount, setUnreadCount] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Tăng để tải lại danh sách sau khi đánh dấu đã đọc
  const [reloadKey, setReloadKey] = useState(0);
  const load = useCallback(() => setReloadKey((k) => k + 1), []);

  // Thông báo mới tự hiện, không cần tải lại trang: ngay khi server đẩy xuống, và khi quay lại tab
  // (phòng khi chưa có kết nối realtime). Không nghe "notifications:changed" vì chính trang này phát sự kiện đó sau khi đánh dấu đã đọc.
  useEffect(() => {
    const offPush = onRealtime("notification", load);
    const onVisible = () => {
      if (document.visibilityState === "visible") load();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      offPush();
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [load]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      notificationsApi.list({ page, pageSize: PAGE_SIZE, unreadOnly }),
      notificationsApi.unreadCount().catch(() => null),
    ])
      .then(([result, countRes]) => {
        if (cancelled) return;
        setError(null);
        setData(result);
        if (countRes) setUnreadCount(countRes.count);
      })
      .catch((e: unknown) => {
        if (!cancelled)
          setError(
            e instanceof Error ? e.message : "Không tải được thông báo.",
          );
      });
    return () => {
      cancelled = true;
    };
  }, [page, unreadOnly, reloadKey]);

  const markAllRead = async () => {
    setBusy(true);
    try {
      await notificationsApi.markAllRead();
      announceChanged();
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không đánh dấu được.");
    } finally {
      setBusy(false);
    }
  };

  const open = async (n: NotificationDto) => {
    if (!n.isRead) {
      try {
        await notificationsApi.markRead(n.id);
        announceChanged();
      } catch {
        // Vẫn cho mở màn hình liên quan dù đánh dấu thất bại
      }
    }
    if (isInternalLink(n.linkUrl)) router.push(n.linkUrl);
    else void load();
  };

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <section className="mx-auto max-w-3xl space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-heading text-2xl font-bold text-ink">Thông báo</h1>
        <div className="flex items-center gap-2">
          <div
            role="tablist"
            aria-label="Lọc thông báo"
            className="flex rounded-lg border bg-surface p-0.5 text-sm"
          >
            {[
              { value: false, label: "Tất cả" },
              { value: true, label: "Chưa đọc" },
            ].map((t) => (
              <button
                key={t.label}
                type="button"
                role="tab"
                aria-selected={unreadOnly === t.value}
                onClick={() => {
                  setUnreadOnly(t.value);
                  setPage(1);
                }}
                className={cn(
                  "cursor-pointer rounded-md px-3 py-1.5 transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default",
                  unreadOnly === t.value
                    ? "bg-brand text-white"
                    : "text-ink-2 hover:bg-muted",
                )}
              >
                {t.label}
              </button>
            ))}
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={markAllRead}
            disabled={busy || unreadCount === 0}
          >
            <CheckCheckIcon />
            Đánh dấu tất cả đã đọc
          </Button>
        </div>
      </header>

      {error && (
        <p
          role="alert"
          className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
        >
          {error}
        </p>
      )}

      {!data && !error ? (
        <div className="space-y-2">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border bg-surface py-12 text-center text-ink-2">
          <BellIcon className="size-8 text-ink-3" />
          <p>
            {unreadOnly
              ? "Bạn đã đọc hết thông báo."
              : "Chưa có thông báo nào."}
          </p>
        </div>
      ) : (
        <ul className="divide-y rounded-xl border bg-surface">
          {items.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                onClick={() => void open(n)}
                className={cn(
                  "flex w-full cursor-pointer items-start gap-3 px-4 py-3 text-left transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default",
                  !n.isRead && "bg-brand-wash/40",
                )}
              >
                <span
                  aria-hidden
                  className={cn(
                    "mt-1.5 size-2 shrink-0 rounded-full",
                    n.isRead ? "bg-transparent" : "bg-brand",
                  )}
                />
                <span className="min-w-0 flex-1">
                  <span
                    className={cn(
                      "block text-sm text-ink",
                      !n.isRead && "font-semibold",
                    )}
                  >
                    {n.title}
                  </span>
                  {n.body && (
                    <span className="mt-0.5 block text-sm break-words text-ink-2">
                      {n.body}
                    </span>
                  )}
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

      {totalPages > 1 && (
        <nav
          aria-label="Phân trang thông báo"
          className="flex items-center justify-center gap-2 text-sm"
        >
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Trước
          </Button>
          <span className="text-ink-2">
            Trang {page}/{totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Sau
          </Button>
        </nav>
      )}
    </section>
  );
}
