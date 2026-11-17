"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CheckCheckIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { ApiError } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import {
  announceNotificationsChanged,
  notificationsApi,
  safeNotificationLink,
  type NotificationDto,
} from "@/lib/notification-api";
import { useFetched } from "@/lib/use-fetched";

const PAGE_SIZE = 20;

type Filter = "all" | "unread";

const FILTER_OPTIONS: { id: Filter; label: string }[] = [
  { id: "all", label: "Tất cả" },
  { id: "unread", label: "Chưa đọc" },
];

/**
 * [UC-110] Danh sách thông báo của người đang đăng nhập: xem, đánh dấu đã đọc, mở tới màn hình liên quan.
 * Bấm vào một thông báo thì đánh dấu đã đọc rồi mở màn hình liên quan (nếu thông báo có đường dẫn).
 */
export function NotificationsView() {
  const router = useRouter();
  const [filter, setFilter] = useState<Filter>("all");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState(false);

  const fetched = useFetched(`notifications|${filter}|${page}|${version}`, () =>
    notificationsApi.list({ unreadOnly: filter === "unread", page, pageSize: PAGE_SIZE }),
  );
  const list = fetched.data;

  const changeFilter = (next: Filter) => {
    setFilter(next);
    setPage(1);
  };

  const reload = () => {
    setVersion((current) => current + 1);
    announceNotificationsChanged();
  };

  const errorText = (err: unknown) => (err instanceof ApiError ? err.message : "Không thể cập nhật thông báo. Vui lòng thử lại.");

  const open = async (item: NotificationDto) => {
    if (busy) return;
    const link = safeNotificationLink(item.linkUrl);
    if (!item.isRead) {
      setBusy(true);
      try {
        await notificationsApi.markRead(item.id);
        reload();
      } catch (err: unknown) {
        toast.add({ type: "error", title: "Không đánh dấu được đã đọc", description: errorText(err) });
      } finally {
        setBusy(false);
      }
    }
    if (link) router.push(link);
  };

  const markAllRead = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await notificationsApi.markAllRead();
      reload();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Không đánh dấu được đã đọc", description: errorText(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="text-h2">Thông báo</h1>
          <p className="text-sm text-ink-2">
            {list ? (list.unreadCount > 0 ? `Bạn có ${list.unreadCount} thông báo chưa đọc.` : "Bạn đã đọc hết thông báo.") : "Đang tải..."}
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void markAllRead()}
          disabled={busy || !list || list.unreadCount === 0}
          className="gap-1.5"
        >
          <CheckCheckIcon className="size-3.5" />
          Đánh dấu tất cả đã đọc
        </Button>
      </header>

      <Segmented label="Lọc thông báo" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {fetched.error ? (
        <Alert variant="destructive">
          <AlertDescription>{fetched.error}</AlertDescription>
        </Alert>
      ) : null}

      <ul data-loading={fetched.loading ? "true" : "false"} className="flex flex-col gap-2 data-[loading=true]:opacity-60">
        {list?.items.map((item) => {
          const link = safeNotificationLink(item.linkUrl);
          return (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => void open(item)}
                disabled={busy}
                className={`flex w-full flex-col gap-1 rounded-md border px-4 py-3 text-left transition-colors hover:bg-muted ${
                  item.isRead ? "border-line bg-transparent" : "border-brand bg-brand-wash"
                }`}
              >
                <span className="flex flex-wrap items-center gap-2">
                  <span className={`text-sm text-ink ${item.isRead ? "font-medium" : "font-semibold"}`}>{item.title}</span>
                  {item.isRead ? null : <Badge variant="brand">Mới</Badge>}
                </span>
                {item.body ? <span className="whitespace-pre-line break-words text-sm text-ink-2">{item.body}</span> : null}
                <span className="flex flex-wrap items-center gap-x-3 text-xs text-ink-3">
                  <span className="tabular">{formatDateTime(item.createdAt)}</span>
                  {link ? <span>Bấm để mở màn hình liên quan</span> : null}
                </span>
              </button>
            </li>
          );
        })}
        {list && list.items.length === 0 ? (
          <li className="rounded-md border border-line py-8 text-center text-sm text-ink-2">
            {filter === "unread" ? "Không có thông báo chưa đọc." : "Bạn chưa có thông báo nào."}
          </li>
        ) : null}
        {!list && !fetched.error ? (
          <li className="rounded-md border border-line py-8 text-center text-sm text-ink-2">Đang tải...</li>
        ) : null}
      </ul>

      {list && list.totalPages > 1 ? (
        <TablePagination
          page={list.page}
          totalPages={list.totalPages}
          totalCount={list.totalCount}
          itemLabel="thông báo"
          disabled={fetched.loading}
          onPageChange={setPage}
        />
      ) : null}
    </div>
  );
}
