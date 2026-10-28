"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import type { AdvisorChatResponse, ChatMessage, ChatWidget, Citation, QuickReply } from "@/types/api";

export type AssistantTurn = ChatMessage & {
  citations?: Citation[];
  disclaimer?: string | null;
  /** Nút trả lời nhanh của lượt trợ lý; chỉ hiện ở lượt cuối */
  quickReplies?: QuickReply[] | null;
  /** Widget kèm lượt trợ lý (vd bảng rà soát hồ sơ) */
  widget?: ChatWidget | null;
};

export type AssistantError = { kind: "unavailable" | "auth" | "other"; message: string };

export interface AssistantPageContext {
  /** Nhãn "Đang xem: …" trong khung trợ lý */
  label: string;
  suggestions: string[];
  /** Bậc học gửi kèm câu hỏi (STUDY_LEVELS), không có thì null */
  studyLevel?: string | null;
  /** Lời chào thay câu mặc định khi chưa có tin nhắn */
  greeting?: string;
}

interface AssistantContextValue {
  open: boolean;
  wide: boolean;
  turns: AssistantTurn[];
  loading: boolean;
  error: AssistantError | null;
  page: AssistantPageContext;
  openPanel: (question?: string) => void;
  closePanel: () => void;
  toggleWide: () => void;
  newChat: () => void;
  send: (text: string) => void;
  retry: () => void;
  setPageContext: (ctx: AssistantPageContext | null) => void;
  setBeforeSend: (fn: BeforeSend | null) => void;
  setOnProfileChanged: (fn: ProfileChanged | null) => void;
}

/** Việc trang cần làm xong trước khi gửi tin nhắn chat (vd lưu nháp form đang chờ) */
export type BeforeSend = () => Promise<void>

/** Chat vừa ghi các ô này vào nháp hồ sơ (vd để form nạp lại) */
export type ProfileChanged = (keys: string[]) => void

const STORAGE_KEY = "usas_assistant_chat";
const SESSION_KEY = "usas_assistant_session";


const GENERAL_SUGGESTIONS = [
  "I-20 là gì?",
  "Chi phí du học Mỹ một năm bao nhiêu?",
  "Cần IELTS bao nhiêu để du học Mỹ?",
];

// Nhãn mặc định khi trang không tự đăng ký ngữ cảnh
function defaultContext(pathname: string): AssistantPageContext {
  if (pathname === "/") return { label: "Trang chủ", suggestions: GENERAL_SUGGESTIONS };
  if (pathname.startsWith("/methodology")) {
    return {
      label: "Giới thiệu phương pháp",
      suggestions: ["Vì sao chia trường thành 4 nhóm?", "AHP và SAW là gì?", ...GENERAL_SUGGESTIONS.slice(0, 1)],
    };
  }
  if (pathname.startsWith("/recommendations")) return { label: "Trang đề xuất", suggestions: GENERAL_SUGGESTIONS };
  if (pathname.startsWith("/analysis")) return { label: "Phân tích tư vấn", suggestions: GENERAL_SUGGESTIONS };
  if (pathname.startsWith("/centers")) return { label: "Trung tâm tư vấn", suggestions: GENERAL_SUGGESTIONS };
  return { label: "Trang này", suggestions: GENERAL_SUGGESTIONS };
}

function toAssistantError(err: unknown): AssistantError {
  if (err instanceof ApiError) {
    if (err.status === 401) return { kind: "auth", message: "Bạn cần đăng nhập để hỏi trợ lý." };
    if (err.status === 503 || err.status === 502 || err.status === 504) {
      return { kind: "unavailable", message: "Trợ lý đang tạm ngưng, bạn thử lại sau." };
    }
    return { kind: "other", message: err.message || "Có lỗi xảy ra, bạn thử lại sau." };
  }
  return { kind: "unavailable", message: "Không kết nối được tới trợ lý, bạn thử lại sau." };
}

const AssistantContext = createContext<AssistantContextValue | null>(null);

export function AssistantProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [wide, setWide] = useState(false);
  const [turns, setTurns] = useState<AssistantTurn[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<AssistantError | null>(null);
  const [registered, setRegistered] = useState<AssistantPageContext | null>(null);

  const turnsRef = useRef<AssistantTurn[]>([]);
  const loadingRef = useRef(false);
  const restoredRef = useRef(false);
  const sessionIdRef = useRef<string | null>(null);
  const beforeSendRef = useRef<BeforeSend | null>(null);
  const setBeforeSend = useCallback((fn: BeforeSend | null) => {
    beforeSendRef.current = fn;
  }, []);
  const onProfileChangedRef = useRef<ProfileChanged | null>(null);
  const setOnProfileChanged = useCallback((fn: ProfileChanged | null) => {
    onProfileChangedRef.current = fn;
  }, []);

  const page = registered ?? defaultContext(pathname);
  const pageRef = useRef(page);
  useEffect(() => {
    pageRef.current = page;
  });

  const commitTurns = useCallback((next: AssistantTurn[]) => {
    turnsRef.current = next;
    setTurns(next);
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      sessionIdRef.current = sessionStorage.getItem(SESSION_KEY);
    } catch {
      // trình duyệt chặn storage → chỉ giữ trong bộ nhớ
    }
  }, []);

  // Khôi phục cuộc trò chuyện của phiên (sau khi tải lại trang) ở lần mở đầu tiên
  const restore = useCallback(() => {
    if (restoredRef.current) return;
    restoredRef.current = true;
    if (turnsRef.current.length > 0) return;
    try {
      const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? "null");
      if (Array.isArray(saved) && saved.length > 0) {
        turnsRef.current = saved;
        setTurns(saved);
      }
    } catch {
      // bỏ qua dữ liệu hỏng
    }
  }, []);

  const request = useCallback(
    async (history: AssistantTurn[]) => {
      loadingRef.current = true;
      setLoading(true);
      setError(null);
      try {
        // D1: lưu ngay nháp form đang chờ, để chat đọc đúng những gì học sinh vừa gõ.
        // Lưu lỗi vẫn gửi chat (form tự báo lỗi lưu của nó)
        await beforeSendRef.current?.().catch(() => undefined);
        const res = await apiFetch<AdvisorChatResponse>("/api/v1/advisor/chat", {
          method: "POST",
          body: JSON.stringify({
            sessionId: sessionIdRef.current,
            messages: history.map(({ role, content }) => ({ role, content })),
            studyLevel: pageRef.current.studyLevel || null,
          }),
        });
        if (res.sessionId) {
          sessionIdRef.current = res.sessionId;
          try {
            sessionStorage.setItem(SESSION_KEY, res.sessionId);
          } catch {
            // trình duyệt chặn storage → chỉ giữ trong bộ nhớ
          }
        }
        commitTurns([
          ...history,
          {
            role: "assistant",
            content: res.answer,
            citations: res.citations,
            disclaimer: res.disclaimer,
            quickReplies: res.quickReplies,
            widget: res.widget,
          },
        ]);
        // D2: báo trang biết chat vừa đổi hồ sơ nháp
        if (res.profileChanged?.length) onProfileChangedRef.current?.(res.profileChanged);
      } catch (err) {
        setError(toAssistantError(err));
      } finally {
        loadingRef.current = false;
        setLoading(false);
      }
    },
    [commitTurns]
  );

  const send = useCallback(
    (text: string) => {
      const content = text.trim().slice(0, 1000);
      if (!content || loadingRef.current) return;
      const history: AssistantTurn[] = [...turnsRef.current, { role: "user", content }];
      commitTurns(history);
      void request(history);
    },
    [commitTurns, request]
  );

  const retry = useCallback(() => {
    const history = turnsRef.current;
    if (loadingRef.current || history.length === 0 || history[history.length - 1].role !== "user") return;
    void request(history);
  }, [request]);

  const openPanel = useCallback(
    (question?: string) => {
      restore();
      setOpen(true);
      if (question) send(question);
    },
    [restore, send]
  );

  const newChat = useCallback(() => {
    // Cuộc trò chuyện mới: quên phiên cũ để backend tạo phiên mới (bắt đầu lại từ B0)
    sessionIdRef.current = null;
    try {
      sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // trình duyệt chặn storage → bỏ qua
    }
    commitTurns([]);
    setError(null);
  }, [commitTurns]);

  const value = useMemo<AssistantContextValue>(
    () => ({
      open,
      wide,
      turns,
      loading,
      error,
      page,
      openPanel,
      closePanel: () => setOpen(false),
      toggleWide: () => setWide((w) => !w),
      newChat,
      send,
      retry,
      setPageContext: setRegistered,
      setBeforeSend,
      setOnProfileChanged,
    }),
    [open, wide, turns, loading, error, page, openPanel, newChat, send, retry, setBeforeSend, setOnProfileChanged]
  );

  return <AssistantContext.Provider value={value}>{children}</AssistantContext.Provider>;
}

export function useAssistant() {
  const ctx = useContext(AssistantContext);
  if (!ctx) throw new Error("useAssistant phải nằm trong AssistantProvider");
  return ctx;
}

/** Trang đăng ký nhãn "Đang xem" và chip gợi ý của mình; rời trang thì trả về mặc định */
export function useAssistantPageContext(ctx: AssistantPageContext) {
  const { setPageContext } = useAssistant();
  const key = JSON.stringify(ctx);
  useEffect(() => {
    setPageContext(JSON.parse(key) as AssistantPageContext);
    return () => setPageContext(null);
  }, [key, setPageContext]);
}

/** Trang đăng ký việc phải xong trước mỗi tin nhắn chat; rời trang thì gỡ */
export function useAssistantBeforeSend(fn: BeforeSend) {
  const { setBeforeSend } = useAssistant();
  useEffect(() => {
    setBeforeSend(fn);
    return () => setBeforeSend(null);
  }, [fn, setBeforeSend]);
}

/** Trang đăng ký việc cần làm khi chat vừa đổi hồ sơ nháp; rời trang thì gỡ */
export function useAssistantProfileChanged(fn: ProfileChanged) {
  const { setOnProfileChanged } = useAssistant();
  useEffect(() => {
    setOnProfileChanged(fn);
    return () => setOnProfileChanged(null);
  }, [fn, setOnProfileChanged]);
}
