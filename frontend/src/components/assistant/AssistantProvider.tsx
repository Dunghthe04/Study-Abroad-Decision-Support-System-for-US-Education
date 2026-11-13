"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { streamAdvisorChat } from "@/lib/advisor-stream";
import { PROFILE_SAVED_EVENT, onRecommendationsPage, showSimulation } from "@/lib/simulation-link";
import type { AssistantAction, ChatMessage, ChatWidget, Citation, QuickReply } from "@/types/api";
import type { SimulationOverrides } from "@/types/recommendation";

export type AssistantTurn = ChatMessage & {
  citations?: Citation[];
  disclaimer?: string | null;
  /** Nút trả lời nhanh của lượt trợ lý; chỉ hiện ở lượt cuối */
  quickReplies?: QuickReply[] | null;
  /** Widget kèm lượt trợ lý (vd bảng rà soát hồ sơ) */
  widget?: ChatWidget | null;
};

export type AssistantError = { kind: "unavailable" | "auth" | "other"; message: string };

/** Câu trả lời đang hiện dần: advisor trả từng ý khi xong, câu cuối cùng thay thế khi có đủ */
export type AssistantStreaming = {
  /** Số câu hỏi advisor sắp trả lời (0 nếu chưa biết) */
  answers: number;
  /** Số câu đã trả lời xong */
  done: number;
  /** Các câu đã trả lời, nối nhau theo thứ tự hỏi */
  text: string;
  /** Thẻ kết quả do code dựng, tới trước lời Gemini (goal-seeking) */
  widget?: ChatWidget;
};

export interface AssistantPageContext {
  /** Nhãn "Đang xem: …" trong khung trợ lý */
  label: string;
  suggestions: string[];
  /** Bậc học gửi kèm câu hỏi (STUDY_LEVELS), không có thì null */
  studyLevel?: string | null;
  /** Lời chào thay câu mặc định khi chưa có tin nhắn */
  greeting?: string;
  /** Giá trị giả định đang kéo trên trang (không có thì là hồ sơ thật): gửi kèm để chat giải thích đúng cái học sinh đang xem */
  simulation?: SimulationOverrides | null;
}

interface AssistantContextValue {
  open: boolean;
  wide: boolean;
  turns: AssistantTurn[];
  loading: boolean;
  /** Phần câu trả lời đã nhận khi đang chờ (null nếu chưa có gì); hết lượt thì bị câu cuối cùng thay */
  streaming: AssistantStreaming | null;
  error: AssistantError | null;
  page: AssistantPageContext;
  openPanel: (question?: string, options?: SendOptions) => void;
  closePanel: () => void;
  toggleWide: () => void;
  newChat: () => void;
  send: (text: string, options?: SendOptions) => void;
  /** Gửi một câu kèm hành động có cấu trúc (mã trường): hiện câu trong khung chat, backend chạy thẳng không qua SLM */
  sendAction: (text: string, action: AssistantAction) => void;
  retry: () => void;
  setPageContext: (ctx: AssistantPageContext | null) => void;
  setBeforeSend: (fn: BeforeSend | null) => void;
  setOnProfileChanged: (fn: ProfileChanged | null) => void;
}

/** fromForm: câu do form gửi ngay sau khi lưu hồ sơ chính thức, advisor bỏ bảng xác nhận đang treo và trả lời luôn */
export interface SendOptions {
  fromForm?: boolean;
}

/** Việc trang cần làm xong trước khi gửi tin nhắn chat (vd lưu nháp form đang chờ) */
export type BeforeSend = () => Promise<void>

/** Chat vừa ghi các ô này vào nháp hồ sơ (vd để form nạp lại); formStep: trang form của câu chat đang hỏi */
export type ProfileChanged = (keys: string[], formStep: number | null) => void

// Lịch sử chat lưu theo từng người dùng: khóa có thêm ":<userId>", để người đăng nhập sau trên cùng tab không thấy chat của người trước
const STORAGE_KEY = "usas_assistant_chat";
const SESSION_KEY = "usas_assistant_session";
const chatKey = (userId: string) => `${STORAGE_KEY}:${userId}`;
const sessionKey = (userId: string) => `${SESSION_KEY}:${userId}`;


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
      suggestions: ["Vì sao chia trường thành 3 nhóm?", "Điểm phù hợp được tính như thế nào?", ...GENERAL_SUGGESTIONS.slice(0, 1)],
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
  const router = useRouter();
  const { user, isLoading: authLoading } = useAuth();
  const userId = user?.id ?? null;
  const [open, setOpen] = useState(false);
  const [wide, setWide] = useState(false);
  const [turns, setTurns] = useState<AssistantTurn[]>([]);
  const [loading, setLoading] = useState(false);
  const [streaming, setStreaming] = useState<AssistantStreaming | null>(null);
  const [error, setError] = useState<AssistantError | null>(null);
  const [registered, setRegistered] = useState<AssistantPageContext | null>(null);

  const turnsRef = useRef<AssistantTurn[]>([]);
  const loadingRef = useRef(false);
  const lastActionRef = useRef<AssistantAction | null>(null); // hành động của lượt gần nhất, để "Thử lại" gửi lại đúng như cũ
  const lastFromFormRef = useRef(false); // lượt gần nhất do form gửi sau khi lưu, "Thử lại" gửi lại kèm cờ này
  const restoredRef = useRef(false);
  const sessionIdRef = useRef<string | null>(null);
  // Người dùng đang đăng nhập (null: chưa đăng nhập) — chưa đăng nhập thì không gửi chat, không đọc ghi lịch sử
  const userIdRef = useRef<string | null>(null);
  // Chủ của cuộc trò chuyện đang hiện, để biết khi nào đổi người dùng
  const ownerRef = useRef<string | null>(null);

  // Đổi người dùng (đăng xuất hoặc đăng nhập người khác): xóa cuộc trò chuyện đang hiện.
  // Đặt lại state ngay lúc render (cách React khuyên khi state phụ thuộc giá trị khác), ref và storage làm trong effect bên dưới
  const [shownUserId, setShownUserId] = useState<string | null>(null);
  if (!authLoading && shownUserId !== userId) {
    setShownUserId(userId);
    setTurns([]);
    setError(null);
    setStreaming(null);
  }

  // Dọn khóa cũ không có userId (cách lưu trước đây), để không ai đọc lại được
  useEffect(() => {
    try {
      sessionStorage.removeItem(STORAGE_KEY);
      sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // trình duyệt chặn storage → bỏ qua
    }
  }, []);

  useEffect(() => {
    if (authLoading) return;
    userIdRef.current = userId;
    const prev = ownerRef.current;
    if (prev === userId) return;
    ownerRef.current = userId;
    turnsRef.current = [];
    sessionIdRef.current = null;
    lastActionRef.current = null;
    lastFromFormRef.current = false;
    restoredRef.current = false; // lần mở sau khôi phục đúng lịch sử của người mới
    try {
      // Đăng xuất: xóa hẳn lịch sử của người vừa thoát trong tab này (máy dùng chung)
      if (prev && !userId) {
        sessionStorage.removeItem(chatKey(prev));
        sessionStorage.removeItem(sessionKey(prev));
      }
    } catch {
      // trình duyệt chặn storage → bỏ qua
    }
  }, [userId, authLoading]);
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
    const uid = userIdRef.current;
    if (!uid) return;
    try {
      sessionStorage.setItem(chatKey(uid), JSON.stringify(next));
      sessionIdRef.current = sessionStorage.getItem(sessionKey(uid));
    } catch {
      // trình duyệt chặn storage → chỉ giữ trong bộ nhớ
    }
  }, []);

  // Khôi phục cuộc trò chuyện của phiên (sau khi tải lại trang) ở lần mở đầu tiên
  // Chỉ khôi phục lịch sử của người đang đăng nhập; chưa đăng nhập thì để lần sau
  const restore = useCallback(() => {
    const uid = userIdRef.current;
    if (!uid || restoredRef.current) return;
    restoredRef.current = true;
    if (turnsRef.current.length > 0) return;
    try {
      sessionIdRef.current = sessionStorage.getItem(sessionKey(uid));
      const saved = JSON.parse(sessionStorage.getItem(chatKey(uid)) ?? "null");
      if (Array.isArray(saved) && saved.length > 0) {
        turnsRef.current = saved;
        setTurns(saved);
      }
    } catch {
      // bỏ qua dữ liệu hỏng
    }
  }, []);

  const request = useCallback(
    async (history: AssistantTurn[], action?: AssistantAction, fromForm = false) => {
      // Người gửi câu này: nếu đổi người dùng trong lúc chờ thì bỏ câu trả lời, không hiện cho người mới
      const owner = userIdRef.current;
      const stillOwner = () => userIdRef.current === owner;
      lastActionRef.current = action ?? null;
      lastFromFormRef.current = fromForm;
      loadingRef.current = true;
      setLoading(true);
      setError(null);
      try {
        // D1: lưu ngay nháp form đang chờ, để chat đọc đúng những gì học sinh vừa gõ.
        // Lưu lỗi vẫn gửi chat (form tự báo lỗi lưu của nó)
        await beforeSendRef.current?.().catch(() => undefined);
        // Advisor trả từng ý khi xong: hiện dần các câu đã có, rồi câu cuối cùng (res) thay thế tất cả
        const res = await streamAdvisorChat(
          {
            sessionId: sessionIdRef.current,
            messages: history.map(({ role, content }) => ({ role, content })),
            studyLevel: pageRef.current.studyLevel || null,
            simulation: pageRef.current.simulation ?? undefined,
            action: action ?? undefined,
            fromForm: fromForm || undefined,
          },
          (event) => {
            if (!stillOwner()) return;
            if (event.type === "progress") setStreaming({ answers: event.answers, done: 0, text: "" });
            else if (event.type === "card")
              setStreaming((cur) => ({ answers: cur?.answers ?? 0, done: cur?.done ?? 0, text: cur?.text ?? "", widget: event.widget }));
            else
              setStreaming((cur) => ({
                answers: cur?.answers ?? 0,
                done: (cur?.done ?? 0) + 1,
                text: cur?.text ? `${cur.text}\n\n${event.text}` : event.text,
              }));
          },
        );
        if (!stillOwner() || !owner) return;
        if (res.sessionId) {
          sessionIdRef.current = res.sessionId;
          try {
            sessionStorage.setItem(sessionKey(owner), res.sessionId);
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
        // What-if: đang đứng ở trang Gợi ý trường thì cập nhật thanh kéo và danh sách bên cạnh tại chỗ (không tải lại trang).
        // Ở trang khác thì không tự chuyển trang, học sinh bấm nút "Xem trên thanh kéo" trong tin nhắn
        if (res.widget?.type === "simulate" && onRecommendationsPage())
          showSimulation(res.widget.data, router.push);
        // Goal-seeking và giải thích trường hiện ngay trong khung chat (thẻ do code dựng), không mở khung riêng trên trang
        // D2: báo trang biết chat vừa đổi hồ sơ nháp, hoặc đang hỏi sang trang form khác (mục 7)
        if (res.profileChanged?.length || res.formStep != null)
          onProfileChangedRef.current?.(res.profileChanged ?? [], res.formStep ?? null);
        // Vừa lưu hồ sơ chính thức: trang Gợi ý trường (nếu đang mở) hiện nút cập nhật danh sách
        if (res.profileSaved) window.dispatchEvent(new Event(PROFILE_SAVED_EVENT));
      } catch (err) {
        if (stillOwner()) setError(toAssistantError(err));
      } finally {
        loadingRef.current = false;
        setLoading(false);
        setStreaming(null);
      }
    },
    [commitTurns, router]
  );

  const send = useCallback(
    (text: string, options?: SendOptions) => {
      const content = text.trim().slice(0, 1000);
      // Chưa đăng nhập thì không gửi (khung chat hiện lời mời đăng nhập)
      if (!content || loadingRef.current || !userIdRef.current) return;
      const history: AssistantTurn[] = [...turnsRef.current, { role: "user", content }];
      commitTurns(history);
      void request(history, undefined, options?.fromForm ?? false);
    },
    [commitTurns, request]
  );

  const sendAction = useCallback(
    (text: string, action: AssistantAction) => {
      const content = text.trim().slice(0, 1000);
      if (!content || loadingRef.current) return;
      // Bấm nút trên trang: mở khung chat (nếu đang đóng) rồi hiện câu hỏi và gửi hành động
      restore();
      setOpen(true);
      // Chưa đăng nhập: chỉ mở khung (thấy lời mời đăng nhập), không gửi
      if (!userIdRef.current) return;
      const history: AssistantTurn[] = [...turnsRef.current, { role: "user", content }];
      commitTurns(history);
      void request(history, action);
    },
    [commitTurns, request, restore]
  );

  const retry = useCallback(() => {
    const history = turnsRef.current;
    if (loadingRef.current || history.length === 0 || history[history.length - 1].role !== "user") return;
    void request(history, lastActionRef.current ?? undefined, lastFromFormRef.current);
  }, [request]);

  const openPanel = useCallback(
    (question?: string, options?: SendOptions) => {
      restore();
      setOpen(true);
      if (question) send(question, options);
    },
    [restore, send]
  );

  const newChat = useCallback(() => {
    // Cuộc trò chuyện mới: quên phiên cũ để backend tạo phiên mới (bắt đầu lại từ B0)
    sessionIdRef.current = null;
    const uid = userIdRef.current;
    try {
      if (uid) sessionStorage.removeItem(sessionKey(uid));
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
      streaming,
      error,
      page,
      openPanel,
      closePanel: () => setOpen(false),
      toggleWide: () => setWide((w) => !w),
      newChat,
      send,
      sendAction,
      retry,
      setPageContext: setRegistered,
      setBeforeSend,
      setOnProfileChanged,
    }),
    [open, wide, turns, loading, streaming, error, page, openPanel, newChat, send, sendAction, retry, setBeforeSend, setOnProfileChanged]
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
