"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { MinusIcon, XIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { SentLeadThread, type SentLeadThreadChange } from "@/components/account/SentLeadMessagesDialog";
import { LeadConversationPanel } from "@/components/center/leads/LeadConversationPanel";
import { cn } from "@/lib/utils";

/**
 * [Hộp thư tin nhắn] Khung chat nổi kiểu Messenger ở góc dưới màn hình.
 * sent: học sinh / phụ huynh nhắn với trung tâm (yêu cầu đã gửi); center: trung tâm nhắn với học sinh / phụ huynh.
 */
export type ChatSide = "sent" | "center";

export interface ChatWindowInfo {
  leadId: string;
  title: string;
  subtitle?: string | null;
  side: ChatSide;
  leadStatus: string;
}

interface ChatWindowState extends ChatWindowInfo {
  minimized: boolean;
}

/** Số khung mở cùng lúc; mở thêm thì đóng khung cũ nhất. */
export const MAX_CHAT_WINDOWS = 3;
/** Hộp thư cần tải lại số chưa đọc (vừa mở / đóng một khung). */
export const INBOX_CHANGED_EVENT = "lead-inbox:changed";
/** Khung nổi phía người gửi vừa tải / gửi tin: thẻ UC-44 cập nhật trạng thái và số tin của trung tâm. */
export const SENT_THREAD_CHANGED_EVENT = "sent-lead-thread:changed";
export type SentThreadChangedDetail = SentLeadThreadChange & { leadId: string };

/** Chờ phòng realtime đánh dấu đã xem xong rồi mới tải lại số chưa đọc. */
const READ_SETTLE_MS = 1_500;

const notifyInboxChanged = () => window.dispatchEvent(new Event(INBOX_CHANGED_EVENT));

/** Thêm hoặc mở lại một khung; quá số khung thì bỏ khung cũ nhất. */
export function upsertChatWindow(windows: ChatWindowState[], info: ChatWindowInfo, max = MAX_CHAT_WINDOWS): ChatWindowState[] {
  if (windows.some((w) => w.leadId === info.leadId)) {
    return windows.map((w) => (w.leadId === info.leadId ? { ...w, ...info, minimized: false } : w));
  }
  return [...windows, { ...info, minimized: false }].slice(-max);
}

interface ChatDockContextValue {
  windows: ChatWindowState[];
  openChat: (info: ChatWindowInfo) => void;
  closeChat: (leadId: string) => void;
  toggleMinimize: (leadId: string) => void;
  setLeadStatus: (leadId: string, status: string) => void;
}

const ChatDockContext = createContext<ChatDockContextValue | null>(null);

export function useChatDock() {
  const ctx = useContext(ChatDockContext);
  if (!ctx) throw new Error("useChatDock phải nằm trong ChatDockProvider");
  return ctx;
}

export function ChatDockProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [windows, setWindows] = useState<ChatWindowState[]>([]);
  const timers = useRef<number[]>([]);

  // Đổi tài khoản / đăng xuất thì đóng hết khung của người trước
  const userId = user?.id ?? null;
  const [ownerId, setOwnerId] = useState(userId);
  if (ownerId !== userId) {
    setOwnerId(userId);
    setWindows([]);
  }

  useEffect(() => {
    const pending = timers.current;
    return () => pending.forEach((t) => window.clearTimeout(t));
  }, []);

  const refreshInboxSoon = useCallback(() => {
    timers.current.push(window.setTimeout(notifyInboxChanged, READ_SETTLE_MS));
  }, []);

  const openChat = useCallback(
    (info: ChatWindowInfo) => {
      setWindows((prev) => upsertChatWindow(prev, info));
      refreshInboxSoon();
    },
    [refreshInboxSoon],
  );

  const closeChat = useCallback((leadId: string) => {
    setWindows((prev) => prev.filter((w) => w.leadId !== leadId));
    notifyInboxChanged();
  }, []);

  const toggleMinimize = useCallback(
    (leadId: string) => {
      setWindows((prev) => prev.map((w) => (w.leadId === leadId ? { ...w, minimized: !w.minimized } : w)));
      refreshInboxSoon();
    },
    [refreshInboxSoon],
  );

  const setLeadStatus = useCallback((leadId: string, status: string) => {
    setWindows((prev) =>
      prev.some((w) => w.leadId === leadId && w.leadStatus !== status)
        ? prev.map((w) => (w.leadId === leadId ? { ...w, leadStatus: status } : w))
        : prev,
    );
  }, []);

  const value = useMemo(
    () => ({ windows, openChat, closeChat, toggleMinimize, setLeadStatus }),
    [windows, openChat, closeChat, toggleMinimize, setLeadStatus],
  );

  return <ChatDockContext.Provider value={value}>{children}</ChatDockContext.Provider>;
}

/**
 * Dãy khung chat nổi, đặt bên trái nút trợ lý AI. Màn hẹp chỉ hiện khung mở sau cùng.
 * @param besideLauncher nút trợ lý nổi đang hiện ở góc phải dưới: chừa chỗ cho nút.
 * @param pushed khung trợ lý đang ghim bên phải (≥900px): dời dãy khung sang trái khung trợ lý.
 */
export function ChatDock({ besideLauncher, pushed }: { besideLauncher: boolean; pushed: boolean }) {
  const { windows } = useChatDock();
  if (windows.length === 0) return null;

  return (
    <div
      className={cn(
        "pointer-events-none fixed bottom-0 z-30 flex items-end gap-2 print:hidden max-sm:right-2 max-sm:left-2",
        besideLauncher ? "sm:right-24" : "sm:right-4",
        pushed && "min-[900px]:right-[calc(var(--assistant-w)+1rem)]",
      )}
    >
      {windows.map((w, i) => (
        <ChatWindow key={w.leadId} window={w} isLast={i === windows.length - 1} />
      ))}
    </div>
  );
}

function ChatWindow({ window: w, isLast }: { window: ChatWindowState; isLast: boolean }) {
  const { closeChat, toggleMinimize, setLeadStatus } = useChatDock();
  const leadId = w.leadId;

  const onSentThreadChange = useCallback(
    (change: SentLeadThreadChange) => {
      setLeadStatus(leadId, change.status);
      window.dispatchEvent(
        new CustomEvent<SentThreadChangedDetail>(SENT_THREAD_CHANGED_EVENT, { detail: { leadId, ...change } }),
      );
    },
    [leadId, setLeadStatus],
  );

  return (
    <section
      aria-label={`Tin nhắn với ${w.title}`}
      className={cn(
        "pointer-events-auto flex w-80 flex-col overflow-hidden rounded-t-xl border border-b-0 border-line bg-surface shadow-xl max-sm:w-full",
        !isLast && "max-lg:hidden",
      )}
    >
      <div className="flex items-center gap-1 bg-brand py-1.5 pr-1.5 pl-3 text-white">
        <button
          type="button"
          onClick={() => toggleMinimize(leadId)}
          aria-expanded={!w.minimized}
          className="min-w-0 flex-1 cursor-pointer py-0.5 text-left outline-none focus-visible:underline"
        >
          <span className="block truncate text-sm font-semibold">{w.title}</span>
          {w.subtitle && <span className="block truncate text-[11px] text-white/75">{w.subtitle}</span>}
        </button>
        <button
          type="button"
          onClick={() => toggleMinimize(leadId)}
          aria-label={w.minimized ? "Mở rộng khung chat" : "Thu nhỏ khung chat"}
          title={w.minimized ? "Mở rộng" : "Thu nhỏ"}
          className="flex size-7 cursor-pointer items-center justify-center rounded-full outline-none hover:bg-white/15 focus-visible:ring-2 focus-visible:ring-white/60"
        >
          <MinusIcon className="size-4" aria-hidden />
        </button>
        <button
          type="button"
          onClick={() => closeChat(leadId)}
          aria-label="Đóng khung chat"
          title="Đóng"
          className="flex size-7 cursor-pointer items-center justify-center rounded-full outline-none hover:bg-white/15 focus-visible:ring-2 focus-visible:ring-white/60"
        >
          <XIcon className="size-4" aria-hidden />
        </button>
      </div>
      {/* Thu nhỏ thì gỡ phần thân: rời phòng chat, tin mới lại đi qua thông báo và số chưa đọc */}
      {!w.minimized && (
        <div className="h-[28rem] max-h-[calc(100dvh-7rem)] p-2.5">
          {w.side === "sent" ? (
            <SentLeadThread leadId={leadId} initialStatus={w.leadStatus} onThreadChange={onSentThreadChange} compact />
          ) : (
            <LeadConversationPanel
              leadId={leadId}
              leadStatus={w.leadStatus}
              onLeadStatusChange={(status) => setLeadStatus(leadId, status)}
              compact
            />
          )}
        </div>
      )}
    </section>
  );
}
