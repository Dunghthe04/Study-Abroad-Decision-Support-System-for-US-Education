"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { ApiError, apiFetch } from "@/lib/api";
import type { AdvisorChatResponse, ChatMessage, Citation } from "@/types/api";

export type AssistantTurn = ChatMessage & { citations?: Citation[]; disclaimer?: string | null };

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
}

const STORAGE_KEY = "usas_assistant_chat";

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
        const res = await apiFetch<AdvisorChatResponse>("/api/v1/advisor/chat", {
          method: "POST",
          body: JSON.stringify({
            messages: history.map(({ role, content }) => ({ role, content })),
            studyLevel: pageRef.current.studyLevel || null,
          }),
        });
        commitTurns([
          ...history,
          { role: "assistant", content: res.answer, citations: res.citations, disclaimer: res.disclaimer },
        ]);
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
    }),
    [open, wide, turns, loading, error, page, openPanel, newChat, send, retry]
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
