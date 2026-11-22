"use client";

import { useMemo, useState } from "react";
import { HistoryIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { ApiError } from "@/lib/api";
import { chatHistoryApi, searchKey, timeAgo } from "@/lib/chat-history-api";
import { cn } from "@/lib/utils";
import type { ChatSessionSummary } from "@/types/chat-history";

const PAGE_SIZE = 20;

interface Props {
  /** Phiên đang mở trong khung chat, để tô đậm trong danh sách. */
  currentSessionId: string | null;
  /** Đang chờ câu trả lời: không cho đổi phiên (câu trả lời đang về có thể rơi nhầm sang phiên khác). */
  disabled?: boolean;
  onSelect: (sessionId: string) => void;
}

/**
 * Nút "Lịch sử" ở đầu khung chat: mở danh sách các cuộc trò chuyện cũ (mới trao đổi nhất trước), tìm theo tiêu đề, tải thêm khi cần.
 * Mỗi lần mở đều tải lại trang đầu để phiên vừa chat hiện lên trên cùng. Chỉ hiện phiên có tin đã lưu (backend lọc).
 */
export function ChatHistoryPopover({ currentSessionId, disabled = false, onSelect }: Props) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<ChatSessionSummary[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const load = async (cursor: string | null) => {
    setLoading(true);
    setError(null);
    try {
      const page = await chatHistoryApi.sessions(cursor, PAGE_SIZE);
      // Trang sau nối vào cuối, bỏ trùng theo mã (phòng khi có phiên mới chen vào giữa hai lần tải)
      setItems((current) => {
        const base = cursor ? current : [];
        const seen = new Set(base.map((s) => s.id));
        return [...base, ...page.items.filter((s) => !seen.has(s.id))];
      });
      setNextCursor(page.nextCursor);
    } catch (err) {
      setError(err instanceof ApiError ? err.message || "Không tải được lịch sử." : "Không tải được lịch sử.");
    } finally {
      setLoading(false);
    }
  };

  const onOpenChange = (next: boolean) => {
    setOpen(next);
    if (next) {
      setSearch("");
      void load(null);
    }
  };

  // Tìm trong các phiên đã tải, không phân biệt dấu và chữ hoa
  const shown = useMemo(() => {
    const key = searchKey(search.trim());
    return key ? items.filter((s) => searchKey(s.title).includes(key)) : items;
  }, [items, search]);

  const choose = (id: string) => {
    setOpen(false);
    if (id !== currentSessionId) onSelect(id);
  };

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger
        disabled={disabled}
        render={
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="Lịch sử trò chuyện"
            title={disabled ? "Đợi trợ lý trả lời xong rồi mở lịch sử" : "Lịch sử trò chuyện"}
            className="size-9 rounded-full border-line text-ink-2 hover:border-brand/40 hover:bg-brand-wash hover:text-brand dark:hover:bg-brand-wash"
          />
        }
      >
        <HistoryIcon className="size-4.5" />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] gap-2 p-2">
        <div className="relative">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Tìm cuộc trò chuyện..."
            aria-label="Tìm cuộc trò chuyện theo tiêu đề"
            className="h-9 pl-8"
          />
        </div>

        <div className="max-h-80 overflow-y-auto" aria-busy={loading}>
          {error ? <p className="px-2 py-3 text-sm text-risk">{error}</p> : null}
          {!error && items.length === 0 && loading ? <p className="px-2 py-3 text-sm text-ink-2">Đang tải...</p> : null}
          {!error && !loading && items.length === 0 ? (
            <p className="px-2 py-3 text-sm text-ink-2">Chưa có cuộc trò chuyện nào được lưu.</p>
          ) : null}
          {items.length > 0 && shown.length === 0 ? (
            <p className="px-2 py-3 text-sm text-ink-2">Không có cuộc trò chuyện nào khớp.</p>
          ) : null}

          <ul className="flex flex-col">
            {shown.map((session) => {
              const current = session.id === currentSessionId;
              return (
                <li key={session.id}>
                  <button
                    type="button"
                    onClick={() => choose(session.id)}
                    aria-current={current ? "true" : undefined}
                    className={cn(
                      "flex w-full cursor-pointer items-center gap-3 rounded-md px-2 py-2 text-left text-sm outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring",
                      current ? "bg-brand-wash font-semibold text-brand-ink" : "text-ink hover:bg-ground",
                    )}
                  >
                    <span className="min-w-0 flex-1 truncate" title={session.title}>{session.title}</span>
                    <span className="shrink-0 text-xs text-ink-3">{timeAgo(session.lastActivityAt)}</span>
                  </button>
                </li>
              );
            })}
          </ul>

          {/* Tìm chỉ trong các phiên đã tải: còn trang sau thì cho tải thêm để tìm tiếp */}
          {nextCursor && !error ? (
            <Button type="button" variant="ghost" size="sm" className="mt-1 w-full" disabled={loading} onClick={() => void load(nextCursor)}>
              {loading ? "Đang tải..." : "Xem thêm cuộc trò chuyện cũ hơn"}
            </Button>
          ) : null}
        </div>
      </PopoverContent>
    </Popover>
  );
}
