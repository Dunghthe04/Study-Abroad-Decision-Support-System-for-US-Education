"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { MessageCircleIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { getLeadRuleError, leadInboxApi } from "@/lib/lead-api";
import { onRealtime } from "@/lib/realtime";
import type { LeadConversationDto, LeadInboxDto } from "@/types/lead";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { INBOX_CHANGED_EVENT, useChatDock, type ChatSide } from "./ChatDock";

/** Làm mới số tin chưa đọc định kỳ, phòng khi không có realtime. */
const POLL_MS = 60_000;

/** Số chưa đọc trên huy hiệu: quá 99 thì "99+". */
export function formatUnread(count: number) {
  return count > 99 ? "99+" : String(count);
}

/** Hôm nay thì giờ:phút, còn lại ngày/tháng (giống danh sách Messenger). */
export function formatConversationTime(iso: string | null, now = new Date()) {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toDateString() === now.toDateString()
    ? date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
}

/** Dòng xem trước tin cuối: "Bạn: ..." khi mình gửi, "[Ảnh]" khi tin chỉ có ảnh. */
export function conversationPreview(c: Pick<LeadConversationDto, "lastMessage" | "lastMessageHasImage" | "lastMessageFromMe">) {
  const text = c.lastMessage?.trim() || (c.lastMessageHasImage ? "[Ảnh]" : "");
  if (!text) return "Chưa có tin nhắn";
  return c.lastMessageFromMe ? `Bạn: ${text}` : text;
}

/**
 * [Hộp thư tin nhắn] Biểu tượng tin nhắn trên thanh đầu trang (học sinh, phụ huynh, trung tâm).
 * Huy hiệu là tổng tin chưa đọc; bấm mở danh sách cuộc trò chuyện, mỗi cuộc có số chưa đọc riêng;
 * bấm một cuộc thì mở khung chat nổi ở góc dưới.
 * Làm mới khi đổi trang, quay lại tab, có thông báo đẩy xuống, mở / đóng khung chat và định kỳ mỗi phút.
 */
export function MessagesInbox({ className }: { className?: string }) {
  const { user } = useAuth();
  const pathname = usePathname();
  const { openChat } = useChatDock();
  const side: ChatSide = user?.role === "center" ? "center" : "sent";
  const [inbox, setInbox] = useState<LeadInboxDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Học sinh dưới 16 tuổi không được liên hệ trung tâm: ẩn hẳn biểu tượng
  const [hidden, setHidden] = useState(false);
  const [open, setOpen] = useState(false);

  const load = useCallback(() => {
    (side === "center" ? leadInboxApi.received() : leadInboxApi.sent())
      .then((r) => {
        setInbox(r);
        setError(null);
        setHidden(false);
      })
      .catch((e: unknown) => {
        if (getLeadRuleError(e)?.code === "age_restricted") {
          setHidden(true);
          return;
        }
        setError(e instanceof Error ? e.message : "Không tải được tin nhắn.");
      });
  }, [side]);

  useEffect(() => {
    load();
    const timer = window.setInterval(load, POLL_MS);
    window.addEventListener("focus", load);
    window.addEventListener("notifications:changed", load);
    window.addEventListener(INBOX_CHANGED_EVENT, load);
    const offPush = onRealtime("notification", load);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", load);
      window.removeEventListener("notifications:changed", load);
      window.removeEventListener(INBOX_CHANGED_EVENT, load);
      offPush();
    };
  }, [pathname, load]);

  // Mỗi lần mở danh sách thì tải mới
  useEffect(() => {
    if (open) load();
  }, [open, load]);

  if (hidden) return null;

  const total = inbox?.totalUnread ?? 0;
  const label = total > 0 ? `Tin nhắn, ${total} chưa đọc` : "Tin nhắn";
  const showStudent = user?.role === "parent";

  const openConversation = (c: LeadConversationDto) => {
    setOpen(false);
    openChat({
      leadId: c.leadId,
      title: c.title,
      subtitle: side === "sent" && showStudent && c.studentName ? `Học sinh: ${c.studentName}` : null,
      side,
      leadStatus: c.leadStatus,
    });
  };

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
        <MessageCircleIcon className="size-5" />
        {total > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] leading-none font-semibold text-white">
            {formatUnread(total)}
          </span>
        )}
      </PopoverTrigger>
      <PopoverContent align="end" sideOffset={8} className="w-[min(24rem,calc(100vw-1.5rem))] gap-0 p-0">
        <div className="border-b px-4 py-3">
          <h2 className="font-heading text-sm font-semibold text-ink">Tin nhắn</h2>
        </div>
        <div className="max-h-96 overflow-y-auto">
          {error && !inbox ? (
            <p role="alert" className="px-4 py-3 text-sm text-red-700">
              {error}
            </p>
          ) : !inbox ? (
            <div className="space-y-2 p-3">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-14 w-full" />
              ))}
            </div>
          ) : inbox.items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 px-6 py-8 text-center text-sm text-ink-2">
              <MessageCircleIcon className="size-7 text-ink-3" />
              {side === "center"
                ? "Chưa có cuộc trò chuyện nào. Chấp nhận yêu cầu liên hệ để nhắn tin với học sinh / phụ huynh."
                : "Chưa có cuộc trò chuyện nào. Trung tâm chấp nhận yêu cầu liên hệ thì hai bên nhắn tin được."}
            </div>
          ) : (
            <ul className="divide-y">
              {inbox.items.map((c) => {
                const unread = c.unreadCount > 0;
                return (
                  <li key={c.leadId}>
                    <button
                      type="button"
                      onClick={() => openConversation(c)}
                      className={cn(
                        "flex w-full cursor-pointer items-start gap-3 px-4 py-3 text-left outline-none transition-colors hover:bg-ground focus-visible:bg-ground",
                        unread && "bg-brand-wash/40",
                      )}
                    >
                      <span
                        aria-hidden
                        className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand-wash text-sm font-semibold text-brand-ink"
                      >
                        {c.title.trim().charAt(0).toUpperCase() || "?"}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="flex items-baseline justify-between gap-2">
                          <span className={cn("truncate text-sm text-ink", unread && "font-semibold")}>{c.title}</span>
                          <span className="shrink-0 text-xs text-ink-3">{formatConversationTime(c.lastMessageAt)}</span>
                        </span>
                        {showStudent && c.studentName && (
                          <span className="block truncate text-xs text-ink-3">Học sinh: {c.studentName}</span>
                        )}
                        <span className="mt-0.5 flex items-center gap-2">
                          <span className={cn("min-w-0 flex-1 truncate text-sm", unread ? "font-medium text-ink" : "text-ink-2")}>
                            {conversationPreview(c)}
                          </span>
                          {unread && (
                            <span className="flex h-4.5 min-w-4.5 shrink-0 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] leading-none font-semibold text-white">
                              {formatUnread(c.unreadCount)}
                              <span className="sr-only"> tin chưa đọc</span>
                            </span>
                          )}
                        </span>
                        {!c.canMessage && <span className="mt-0.5 block text-xs text-ink-3">Chỉ xem, không nhắn tiếp được</span>}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
        <div className="border-t px-4 py-2.5 text-center">
          <Link
            href={side === "center" ? "/center/leads" : "/account#sent-leads"}
            onClick={() => setOpen(false)}
            className="text-sm font-medium text-brand hover:underline"
          >
            {side === "center" ? "Xem tất cả yêu cầu liên hệ" : "Xem tất cả yêu cầu đã gửi"}
          </Link>
        </div>
      </PopoverContent>
    </Popover>
  );
}
