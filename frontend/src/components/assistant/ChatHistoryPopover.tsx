"use client";

import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import Image from "next/image";
import { ArchiveIcon, CheckIcon, HomeIcon, MenuIcon, Minimize2Icon, PencilIcon, PlusIcon, SearchIcon, XIcon } from "lucide-react";
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
  onSelect: (sessionId: string, title?: string) => void;
  /** Một phiên vừa bị đổi tên trong lịch sử. */
  onRenamed?: (sessionId: string, title: string) => void;
  /** Một phiên vừa bị xóa khỏi lịch sử (nếu là phiên đang mở thì khung chat cần chuyển sang cuộc trò chuyện mới). */
  onDeleted: (sessionId: string) => void;
}

function useHistoryList(
  currentSessionId: string | null,
  onSelect: (sessionId: string, title?: string) => void,
  onRenamed: ((sessionId: string, title: string) => void) | undefined,
  onDeleted: (sessionId: string) => void,
  afterSelect?: () => void,
) {
  const [items, setItems] = useState<ChatSessionSummary[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const load = useCallback(async (cursor: string | null) => {
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
  }, []);

  // Tìm trong các phiên đã tải, không phân biệt dấu và chữ hoa
  const shown = useMemo(() => {
    const key = searchKey(search.trim());
    return key ? items.filter((s) => searchKey(s.title).includes(key)) : items;
  }, [items, search]);

  const choose = (id: string) => {
    afterSelect?.();
    const title = items.find((s) => s.id === id)?.title;
    if (id !== currentSessionId) onSelect(id, title);
  };

  const renamed = (id: string, title: string) => {
    setItems((current) => current.map((s) => (s.id === id ? { ...s, title } : s)));
    onRenamed?.(id, title);
  };

  const deleted = (id: string) => {
    setItems((current) => current.filter((s) => s.id !== id));
    onDeleted(id);
  };

  return { items, shown, nextCursor, loading, error, search, setSearch, load, choose, renamed, deleted };
}

function HistoryListBody({
  state,
  currentSessionId,
  listClassName,
  hideSearch = false,
  sectionLabel,
}: {
  state: ReturnType<typeof useHistoryList>;
  currentSessionId: string | null;
  listClassName?: string;
  hideSearch?: boolean;
  sectionLabel?: string;
}) {
  return (
    <>
      {!hideSearch && (
        <div className="relative">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            value={state.search}
            onChange={(event) => state.setSearch(event.target.value)}
            placeholder="Tìm cuộc trò chuyện..."
            aria-label="Tìm cuộc trò chuyện theo tiêu đề"
            className="h-9 bg-surface pl-8"
          />
        </div>
      )}

      <div className={cn("overflow-y-auto", listClassName)} aria-busy={state.loading}>
        {sectionLabel && (
          <p className="px-2 pb-1.5 text-xs font-medium text-ink-3">{sectionLabel}</p>
        )}
        {state.error ? <p className="px-2 py-3 text-sm text-risk">{state.error}</p> : null}
        {!state.error && state.items.length === 0 && state.loading ? (
          <p className="px-2 py-3 text-sm text-ink-2">Đang tải...</p>
        ) : null}
        {!state.error && !state.loading && state.items.length === 0 ? (
          <p className="px-2 py-3 text-sm text-ink-2">Chưa có cuộc trò chuyện nào được lưu.</p>
        ) : null}
        {state.items.length > 0 && state.shown.length === 0 ? (
          <p className="px-2 py-3 text-sm text-ink-2">Không có cuộc trò chuyện nào khớp.</p>
        ) : null}

        <ul className="flex flex-col">
          {state.shown.map((session) => (
            <SessionRow
              key={session.id}
              session={session}
              current={session.id === currentSessionId}
              onOpen={() => state.choose(session.id)}
              onRenamed={state.renamed}
              onDeleted={state.deleted}
            />
          ))}
        </ul>

        {/* Tìm chỉ trong các phiên đã tải: còn trang sau thì cho tải thêm để tìm tiếp */}
        {state.nextCursor && !state.error ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="mt-1 w-full"
            disabled={state.loading}
            onClick={() => void state.load(state.nextCursor)}
          >
            {state.loading ? "Đang tải..." : "Xem thêm cuộc trò chuyện cũ hơn"}
          </Button>
        ) : null}
      </div>
    </>
  );
}

/**
 * Nút "Lịch sử" ở đầu khung chat: mở danh sách các cuộc trò chuyện cũ (mới trao đổi nhất trước), tìm theo tiêu đề, tải thêm khi cần.
 * Rê chuột (hoặc chuyển tiêu điểm) vào một dòng thì hiện hai nút: đổi tên và xóa (xóa mềm, có hỏi lại).
 * Mỗi lần mở đều tải lại trang đầu để phiên vừa chat hiện lên trên cùng. Chỉ hiện phiên có tin đã lưu và chưa xóa (backend lọc).
 */
export function ChatHistoryPopover({ currentSessionId, disabled = false, onSelect, onRenamed, onDeleted }: Props) {
  const [open, setOpen] = useState(false);
  const state = useHistoryList(currentSessionId, onSelect, onRenamed, onDeleted, () => setOpen(false));

  const onOpenChange = (next: boolean) => {
    setOpen(next);
    if (next) {
      state.setSearch("");
      void state.load(null);
    }
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
            className="size-9 rounded-full border border-line bg-[var(--surface-2)] text-ink-2 hover:border-brand/40 hover:bg-brand-wash hover:text-brand dark:hover:bg-brand-wash"
          />
        }
      >
        <MenuIcon className="size-4.5" />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-80 max-w-[calc(100vw-2rem)] gap-2 p-2">
        <HistoryListBody state={state} currentSessionId={currentSessionId} listClassName="max-h-80" />
      </PopoverContent>
    </Popover>
  );
}

/** Thanh bên lịch sử khi khung chat mở Toàn màn hình (có thể ẩn/hiện từ nút ≡). */
export function ChatHistorySidebar({
  open,
  currentSessionId,
  disabled = false,
  onToggle,
  onNewChat,
  onShrink,
  onGoHome,
  onSelect,
  onRenamed,
  onDeleted,
}: Props & {
  open: boolean;
  onToggle: () => void;
  onNewChat: () => void;
  onShrink: () => void;
  onGoHome: () => void;
}) {
  const state = useHistoryList(currentSessionId, onSelect, onRenamed, onDeleted);
  const loadHistory = state.load;

  useEffect(() => {
    if (open) void loadHistory(null);
  }, [open, currentSessionId, loadHistory]);

  return (
    <div
      aria-label="Lịch sử trò chuyện"
      aria-hidden={!open}
      inert={!open || undefined}
      className={cn(
        "hidden shrink-0 flex-col overflow-hidden border-r border-line bg-[var(--surface-2)] transition-[width,opacity,transform] duration-250 ease-out motion-reduce:transition-none min-[900px]:flex",
        open ? "w-72 translate-x-0 opacity-100" : "w-0 -translate-x-4 border-r-0 opacity-0 pointer-events-none",
      )}
    >
      <div className="flex w-72 min-h-0 flex-1 flex-col gap-3 p-3">
        {/* Hàng đầu: Logo + Advisor bên trái, nút thu gọn thanh bên bên phải */}
        <div className="flex items-center justify-between gap-2 px-1">
          <div className="flex items-center gap-2.5">
            <Image src="/icons/USDSS.png" alt="" width={28} height={28} className="size-7 shrink-0 object-contain" />
            <span className="font-heading text-xl font-semibold text-ink">Advisor</span>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={onToggle}
            aria-label="Ẩn thanh lịch sử"
            title="Ẩn thanh lịch sử"
            className="size-9 shrink-0 rounded-full border border-line bg-surface text-ink-2 hover:border-brand/40 hover:bg-brand-wash hover:text-brand"
          >
            <MenuIcon className="size-4.5" />
          </Button>
        </div>

        {/* Ô tìm kiếm đoạn chat */}
        <div className="relative">
          <SearchIcon aria-hidden className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
          <Input
            value={state.search}
            onChange={(event) => state.setSearch(event.target.value)}
            placeholder="Tìm kiếm đoạn chat"
            aria-label="Tìm kiếm đoạn chat"
            className="h-9 rounded-full border-line bg-surface pl-9 text-sm"
          />
        </div>

        {/* Cuộc trò chuyện mới: không nền, cỡ chữ lớn hơn */}
        <button
          type="button"
          disabled={disabled}
          onClick={onNewChat}
          className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-2 text-left text-[15px] font-medium text-ink transition-colors hover:bg-ground disabled:cursor-default disabled:opacity-50"
        >
          <PlusIcon className="size-5 shrink-0 text-ink-2" />
          <span>Cuộc trò chuyện mới</span>
        </button>

        <HistoryListBody
          state={state}
          currentSessionId={currentSessionId}
          listClassName="min-h-0 flex-1"
          hideSearch
          sectionLabel="Gần đây"
        />

        {/* Chân thanh bên: Thu nhỏ & Về màn hình chính */}
        <div className="mt-auto flex flex-col gap-0.5 border-t border-line/70 pt-2">
          <button
            type="button"
            onClick={onShrink}
            className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-2 text-left text-sm text-ink transition-colors hover:bg-ground"
          >
            <Minimize2Icon className="size-4 shrink-0 text-ink-2" />
            <span>Thu nhỏ</span>
          </button>
          <button
            type="button"
            onClick={onGoHome}
            className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2 py-2 text-left text-sm text-ink transition-colors hover:bg-ground"
          >
            <HomeIcon className="size-4 shrink-0 text-ink-2" />
            <span>Về màn hình chính</span>
          </button>
        </div>
      </div>
    </div>
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
          current ? "text-brand-ink" : "text-ink",
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
