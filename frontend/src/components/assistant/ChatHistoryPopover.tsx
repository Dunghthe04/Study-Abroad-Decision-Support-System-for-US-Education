"use client";

import { useMemo, useState, type ReactNode } from "react";
import { ArchiveIcon, CheckIcon, HistoryIcon, PencilIcon, SearchIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { ApiError } from "@/lib/api";
import { chatHistoryApi, searchKey, timeAgo } from "@/lib/chat-history-api";
import { cn } from "@/lib/utils";
import type { ChatSessionSummary } from "@/types/chat-history";

const PAGE_SIZE = 20;
const MAX_TITLE = 100; // khớp ChatSessionTitle.MaxUserLength ở backend

const messageOf = (err: unknown, fallback: string) => (err instanceof ApiError && err.message ? err.message : fallback);

interface Props {
  /** Phiên đang mở trong khung chat, để tô đậm trong danh sách. */
  currentSessionId: string | null;
  /** Đang chờ câu trả lời: không cho đổi phiên (câu trả lời đang về có thể rơi nhầm sang phiên khác). */
  disabled?: boolean;
  onSelect: (sessionId: string) => void;
  /** Một phiên vừa bị xóa khỏi lịch sử (nếu là phiên đang mở thì khung chat cần chuyển sang cuộc trò chuyện mới). */
  onDeleted: (sessionId: string) => void;
}

/**
 * Nút "Lịch sử" ở đầu khung chat: mở danh sách các cuộc trò chuyện cũ (mới trao đổi nhất trước), tìm theo tiêu đề, tải thêm khi cần.
 * Rê chuột (hoặc chuyển tiêu điểm) vào một dòng thì hiện hai nút: đổi tên và xóa (xóa mềm, có hỏi lại).
 * Mỗi lần mở đều tải lại trang đầu để phiên vừa chat hiện lên trên cùng. Chỉ hiện phiên có tin đã lưu và chưa xóa (backend lọc).
 */
export function ChatHistoryPopover({ currentSessionId, disabled = false, onSelect, onDeleted }: Props) {
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
      setError(messageOf(err, "Không tải được lịch sử."));
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

  const renamed = (id: string, title: string) => setItems((current) => current.map((s) => (s.id === id ? { ...s, title } : s)));

  const deleted = (id: string) => {
    setItems((current) => current.filter((s) => s.id !== id));
    onDeleted(id);
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
            {shown.map((session) => (
              <SessionRow
                key={session.id}
                session={session}
                current={session.id === currentSessionId}
                onOpen={() => choose(session.id)}
                onRenamed={renamed}
                onDeleted={deleted}
              />
            ))}
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

// ---- một dòng: xem, đổi tên tại chỗ, hỏi lại trước khi xóa

interface RowProps {
  session: ChatSessionSummary;
  current: boolean;
  onOpen: () => void;
  onRenamed: (id: string, title: string) => void;
  onDeleted: (id: string) => void;
}

function SessionRow({ session, current, onOpen, onRenamed, onDeleted }: RowProps) {
  const [mode, setMode] = useState<"view" | "edit" | "confirm">("view");
  const [draft, setDraft] = useState(session.title);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEdit = () => {
    setDraft(session.title);
    setError(null);
    setMode("edit");
  };

  const cancel = () => {
    setError(null);
    setMode("view");
  };

  const saveName = async () => {
    const title = draft.trim();
    if (!title) {
      setError("Tên không được để trống.");
      return;
    }
    if (title === session.title) {
      cancel();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const saved = await chatHistoryApi.rename(session.id, title);
      onRenamed(session.id, saved.title);
      setMode("view");
    } catch (err) {
      setError(messageOf(err, "Không đổi được tên."));
    } finally {
      setBusy(false);
    }
  };

  const confirmDelete = async () => {
    setBusy(true);
    setError(null);
    try {
      await chatHistoryApi.remove(session.id);
      onDeleted(session.id);   // dòng biến khỏi danh sách nên không cần đặt lại trạng thái
    } catch (err) {
      setError(messageOf(err, "Không xóa được cuộc trò chuyện."));
      setBusy(false);
    }
  };

  if (mode === "edit") {
    return (
      <li className="px-1 py-1">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void saveName();
          }}
          className="flex items-center gap-1"
        >
          <Input
            autoFocus
            value={draft}
            maxLength={MAX_TITLE}
            disabled={busy}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Escape") {
                event.stopPropagation();   // chỉ thoát sửa tên, không đóng cả danh sách
                cancel();
              }
            }}
            aria-label="Tên mới của cuộc trò chuyện"
            className="h-8 min-w-0 flex-1"
          />
          <RowAction label="Lưu tên" type="submit" disabled={busy}>
            <CheckIcon className="size-4" />
          </RowAction>
          <RowAction label="Hủy đổi tên" onClick={cancel} disabled={busy}>
            <XIcon className="size-4" />
          </RowAction>
        </form>
        {error ? <p className="px-1 pt-1 text-xs text-risk">{error}</p> : null}
      </li>
    );
  }

  if (mode === "confirm") {
    return (
      <li className="rounded-md bg-risk-wash px-2 py-2 text-sm">
        <p className="truncate text-ink" title={session.title}>Xóa “{session.title}”?</p>
        <p className="mt-0.5 text-xs text-ink-2">Cuộc trò chuyện sẽ biến khỏi lịch sử và không hỏi tiếp được nữa.</p>
        <div className="mt-2 flex justify-end gap-1.5">
          <Button type="button" variant="ghost" size="sm" onClick={cancel} disabled={busy}>Hủy</Button>
          <Button type="button" variant="destructive" size="sm" onClick={() => void confirmDelete()} disabled={busy}>
            {busy ? "Đang xóa..." : "Xóa"}
          </Button>
        </div>
        {error ? <p className="pt-1 text-xs text-risk">{error}</p> : null}
      </li>
    );
  }

  return (
    <li
      className={cn(
        "group flex items-center rounded-md transition-colors",
        current ? "bg-brand-wash" : "hover:bg-ground focus-within:bg-ground",
      )}
    >
      <button
        type="button"
        onClick={onOpen}
        aria-current={current ? "true" : undefined}
        className={cn(
          "flex min-w-0 flex-1 cursor-pointer items-center gap-3 rounded-md px-2 py-2 text-left text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring",
          current ? "font-semibold text-brand-ink" : "text-ink",
        )}
      >
        <span className="min-w-0 flex-1 truncate" title={session.title}>{session.title}</span>
        {/* Thời gian nhường chỗ cho hai nút khi rê chuột hoặc chuyển tiêu điểm vào dòng */}
        <span className="shrink-0 text-xs text-ink-3 group-focus-within:hidden group-hover:hidden [@media(hover:none)]:hidden">
          {timeAgo(session.lastActivityAt)}
        </span>
      </button>
      {/* Màn cảm ứng không có rê chuột: luôn hiện hai nút */}
      <div className="hidden shrink-0 items-center gap-0.5 pr-1 group-focus-within:flex group-hover:flex [@media(hover:none)]:flex">
        <RowAction label="Đổi tên cuộc trò chuyện" onClick={startEdit}>
          <PencilIcon className="size-3.5" />
        </RowAction>
        <RowAction label="Xóa cuộc trò chuyện" onClick={() => { setError(null); setMode("confirm"); }}>
          <ArchiveIcon className="size-3.5" />
        </RowAction>
      </div>
    </li>
  );
}

function RowAction({
  label, onClick, type = "button", disabled, children,
}: {
  label: string;
  onClick?: () => void;
  type?: "button" | "submit";
  disabled?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className="inline-flex size-7 cursor-pointer items-center justify-center rounded-md text-ink-2 outline-none transition-colors hover:bg-surface hover:text-ink focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-default disabled:opacity-50"
    >
      {children}
    </button>
  );
}
