"use client";

import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { invokeRealtime, isRealtimeConnected, onRealtime, subscribeRealtimeState } from "@/lib/realtime";
import type { LeadMessageDto } from "@/types/api";

/** Gửi "đang nhập" tối đa mỗi ngần này ms khi người dùng gõ liên tục. */
const TYPING_SEND_INTERVAL_MS = 2_500;
/** Ngừng gõ ngần này ms thì báo "ngừng nhập". */
const TYPING_IDLE_MS = 3_000;
/** Phòng khi không nhận được tín hiệu "ngừng nhập" (mất mạng...): tự tắt sau ngần này ms. */
const PEER_TYPING_TIMEOUT_MS = 5_000;
/** Khi chưa có kết nối realtime, khung chat tự tải lại luồng tin theo chu kỳ này (như trước đây chỉ có nút Tải lại). */
const FALLBACK_POLL_MS = 20_000;

interface JoinLeadResult {
  side: "student" | "center";
  peerOnline: boolean;
}

interface MessagesReadPayload {
  leadId: string;
  messageIds: string[];
  readAt: string;
}

interface TypingPayload {
  leadId: string;
  isTyping: boolean;
}

interface PresencePayload {
  leadId: string;
  online: boolean;
}

export interface UseLeadChatOptions {
  leadId: string;
  /** false thì không vào phòng (ví dụ khung đang đóng). */
  enabled?: boolean;
  /** Tin mới từ phía đối diện hoặc từ chính mình ở tab khác. Người gọi tự loại tin trùng. */
  onNewMessage: (message: LeadMessageDto) => void;
  /** Phía đối diện vừa xem các tin này. */
  onMessagesRead: (messageIds: string[], readAt: string) => void;
  /** Cần tải lại cả luồng tin: sau khi nối lại, quay lại tab, hoặc khi chưa có realtime. */
  onResync: () => void;
}

const subscribeVisibility = (callback: () => void) => {
  document.addEventListener("visibilitychange", callback);
  return () => document.removeEventListener("visibilitychange", callback);
};
const getVisible = () => document.visibilityState === "visible";

/**
 * [Chat realtime] Gắn một khung chat yêu cầu liên hệ vào kết nối realtime dùng chung: vào phòng của lead, nhận tin mới,
 * đang nhập, đã xem, online. Chỉ vào phòng khi tab đang hiện: tab ẩn coi như không xem, nên tin mới sẽ đi qua thông báo.
 * Quay lại tab thì tải lại luồng tin để không sót tin trong lúc vắng.
 */
export function useLeadChat(options: UseLeadChatOptions) {
  const { leadId, enabled = true } = options;

  // Giữ callback mới nhất mà không làm các effect bên dưới chạy lại mỗi lần cha render
  const callbacks = useRef(options);
  useEffect(() => {
    callbacks.current = options;
  });

  const connected = useSyncExternalStore(subscribeRealtimeState, isRealtimeConnected, () => false);
  const visible = useSyncExternalStore(subscribeVisibility, getVisible, () => true);
  const active = enabled && visible;

  const [peerOnline, setPeerOnline] = useState(false);
  const [peerTyping, setPeerTyping] = useState(false);

  const joinedSide = useRef<JoinLeadResult["side"] | null>(null);
  const lastTypingSentAt = useRef(0);
  const idleTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const peerTypingTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const wasActive = useRef(false);

  const markRead = useCallback(() => {
    if (joinedSide.current && getVisible()) void invokeRealtime("MarkRead", leadId).catch(() => undefined);
  }, [leadId]);

  useEffect(() => {
    if (!active) return;

    const join = async () => {
      try {
        const result = await invokeRealtime<JoinLeadResult>("JoinLead", leadId);
        joinedSide.current = result.side;
        setPeerOnline(result.peerOnline);
        markRead();
      } catch {
        // Không vào được phòng (mất kết nối, không có quyền): chat vẫn chạy bằng REST + tải lại định kỳ
        joinedSide.current = null;
      }
    };

    // Tab vừa hiện lại sau khi ẩn: có thể đã lỡ tin, tải lại luồng tin
    if (wasActive.current) callbacks.current.onResync();
    wasActive.current = true;

    if (isRealtimeConnected()) void join();

    const offConnected = onRealtime("connected", () => void join());
    const offReconnected = onRealtime("reconnected", () => {
      void join();
      callbacks.current.onResync();
    });
    const offDisconnected = onRealtime("disconnected", () => {
      joinedSide.current = null;
      setPeerOnline(false);
      setPeerTyping(false);
    });

    const offNewMessage = onRealtime<LeadMessageDto>("newMessage", (message) => {
      if (message.leadId !== leadId) return;
      setPeerTyping(false);
      callbacks.current.onNewMessage(message);
      // Tin của phía đối diện đang hiện trước mắt người dùng: đánh dấu đã xem ngay
      const side = joinedSide.current;
      const fromPeer = side === "center" ? !message.isFromCenter : message.isFromCenter;
      if (side && fromPeer) markRead();
    });
    const offRead = onRealtime<MessagesReadPayload>("messagesRead", (payload) => {
      if (payload.leadId === leadId) callbacks.current.onMessagesRead(payload.messageIds, payload.readAt);
    });
    const offTyping = onRealtime<TypingPayload>("typing", (payload) => {
      if (payload.leadId !== leadId) return;
      setPeerTyping(payload.isTyping);
      if (peerTypingTimer.current) clearTimeout(peerTypingTimer.current);
      if (payload.isTyping) peerTypingTimer.current = setTimeout(() => setPeerTyping(false), PEER_TYPING_TIMEOUT_MS);
    });
    const offPresence = onRealtime<PresencePayload>("presence", (payload) => {
      if (payload.leadId === leadId) setPeerOnline(payload.online);
    });

    return () => {
      offConnected();
      offReconnected();
      offDisconnected();
      offNewMessage();
      offRead();
      offTyping();
      offPresence();
      if (peerTypingTimer.current) clearTimeout(peerTypingTimer.current);
      if (joinedSide.current) void invokeRealtime("LeaveLead", leadId).catch(() => undefined);
      joinedSide.current = null;
    };
  }, [active, leadId, markRead]);

  // Tab ẩn hoặc khung đóng thì lần sau vào lại phải tải lại
  useEffect(() => {
    if (!enabled) wasActive.current = false;
  }, [enabled]);

  // Chưa có realtime: tải lại luồng tin theo chu kỳ để vẫn thấy tin mới
  useEffect(() => {
    if (!active || connected) return;
    const timer = setInterval(() => callbacks.current.onResync(), FALLBACK_POLL_MS);
    return () => clearInterval(timer);
  }, [active, connected]);

  const sendTyping = useCallback(
    (isTyping: boolean) => {
      if (joinedSide.current) void invokeRealtime("Typing", leadId, isTyping).catch(() => undefined);
    },
    [leadId],
  );

  /** Gọi mỗi lần người dùng gõ (true) hoặc xóa hết / gửi xong (false). Tự giới hạn tần suất và tự báo ngừng nhập. */
  const notifyTyping = useCallback(
    (isTyping: boolean) => {
      if (idleTimer.current) clearTimeout(idleTimer.current);

      if (!isTyping) {
        if (lastTypingSentAt.current !== 0) {
          lastTypingSentAt.current = 0;
          sendTyping(false);
        }
        return;
      }

      const now = Date.now();
      if (now - lastTypingSentAt.current >= TYPING_SEND_INTERVAL_MS) {
        lastTypingSentAt.current = now;
        sendTyping(true);
      }
      idleTimer.current = setTimeout(() => {
        lastTypingSentAt.current = 0;
        sendTyping(false);
      }, TYPING_IDLE_MS);
    },
    [sendTyping],
  );

  // Rời khung thì báo ngừng nhập và dọn hẹn giờ
  useEffect(
    () => () => {
      if (idleTimer.current) clearTimeout(idleTimer.current);
      if (lastTypingSentAt.current !== 0) sendTyping(false);
    },
    [sendTyping],
  );

  return { connected, peerOnline, peerTyping, notifyTyping };
}
