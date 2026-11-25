"use client";

import { useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore } from "react";
import { Button, buttonVariants } from "@/components/ui/button";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  ArrowUpIcon,
  ChevronRightIcon,
  FlaskConicalIcon,
  LockIcon,
  Maximize2Icon,
  MenuIcon,
  Minimize2Icon,
  PlusIcon,
  RotateCwIcon,
  TriangleAlertIcon,
  Undo2Icon,
  UsersIcon,
  XIcon,
} from "lucide-react";
import { AnimatedShinyText } from "@/components/ui/animated-shiny-text";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import { describeSimulation, resetSimulation, showSimulation } from "@/lib/simulation-link";
import { sourceDateText } from "@/lib/source-date";
import { citationLinks } from "@/lib/citation-links";
import { ChatImages } from "@/components/assistant/ChatImages";
import { useAuth } from "@/contexts/AuthContext";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { isAuthRoute } from "@/lib/layout-routes";
import { useAssistant, type AssistantPanelMode, type AssistantTurn } from "./AssistantProvider";
import { BudgetWidget } from "./BudgetWidget";
import { ChatHistoryPopover, ChatHistorySidebar } from "./ChatHistoryPopover";
import { CenterCardsWidget } from "./CenterCardsWidget";
import { ChatText } from "./ChatText";
import { FitCompareCard } from "./FitCompareCard";
import { MajorWidget } from "./MajorWidget";
import { CurrentValues, ReviewWidget } from "./ReviewWidget";
import { ScholarshipWidget } from "./ScholarshipWidget";
import { SchoolSearchCard } from "./SchoolSearchCard";
import { SchoolWidget } from "./SchoolWidget";
import { SelectWidget } from "./SelectWidget";
import { GoalCompareTable } from "./GoalCompareTable";
import { PremiumLockedCard } from "@/components/billing/PremiumLockedCard";
import { formatVnDate } from "@/lib/billing-api";
import type { QuickReply } from "@/types/api";
import type { SubscriptionSummaryDto } from "@/types/billing";
import type { FitCompareData, SchoolGoalResult, SchoolSearchData } from "@/types/recommendation";

// Trang đăng nhập / đăng ký không hiện trợ lý
export function useAssistantVisible() {
  const pathname = usePathname();
  return !isAuthRoute(pathname);
}

/** Khung "Trợ lý USDSS" cố định bên phải; màn hẹp thì phủ kín */
export function AssistantPanel() {
  const {
    open, panelMode, setPanelMode, turns, loading, streaming, error, page, closePanel, newChat, send, retry, stop, subscription,
    currentSessionId, currentSessionTitle, setCurrentSessionTitle, openSession, openingSession, resumed, hasOlder, loadingOlder, loadOlder, debugOverrides,
  } = useAssistant();
  const visible = useAssistantVisible();
  const pathname = usePathname();
  const router = useRouter();
  const [input, setInput] = useState("");
  const [composerExpanded, setComposerExpanded] = useState(false);
  const [fullSidebarOpen, setFullSidebarOpen] = useState(true);
  const lastHeightRef = useRef(48);
  const rawAgeBlock = useAgeBlock(AGE_FEATURES.processStudentData);
  const { user, isLoading: authLoading } = useAuth();
  const composerMode = debugOverrides?.composerMode ?? "auto";
  // Chưa đăng nhập thì không chat được: thay ô nhập bằng lời mời đăng nhập (đang tải trạng thái thì chưa hiện, tránh nháy)
  const needLogin = composerMode === "needLogin" ? true : composerMode === "auto" ? !authLoading && !user : false;
  const ageBlock =
    composerMode === "ageBlock"
      ? { message: "Tài khoản dưới 16 tuổi cần phụ huynh xác nhận trước khi dùng trợ lý AI." }
      : composerMode === "auto"
        ? rawAgeBlock
        : null;
  // Được gửi tin: đã đăng nhập và không bị chặn tuổi
  const canSend = composerMode === "normal" ? true : composerMode === "auto" ? !!user && !ageBlock : false;
  const listRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  // Vị trí đang đọc trước khi chèn tin cũ lên trên (cuộn lên đầu phiên mở lại), để giữ nguyên chỗ thay vì nhảy xuống cuối
  const anchorRef = useRef<{ height: number; top: number } | null>(null);

  // Cuộn xuống tin mới nhất; vừa chèn tin cũ lên trên thì giữ nguyên chỗ đang đọc
  useLayoutEffect(() => {
    const el = listRef.current;
    if (!el) return;
    const anchor = anchorRef.current;
    if (anchor) {
      el.scrollTop = el.scrollHeight - anchor.height + anchor.top;
      anchorRef.current = null;
      return;
    }
    el.scrollTop = el.scrollHeight;
  }, [turns, loading, streaming, error, open]);

  // Tải tin cũ hơn: ghi chỗ đang đọc rồi tải; tải lỗi hoặc không có gì mới thì bỏ chỗ đã ghi (sau khi khung đã vẽ lại)
  const loadEarlier = () => {
    const el = listRef.current;
    if (!el || !hasOlder || loadingOlder) return;
    anchorRef.current = { height: el.scrollHeight, top: el.scrollTop };
    void loadOlder().finally(() => requestAnimationFrame(() => {
      anchorRef.current = null;
    }));
  };

  // Cuộn gần tới đầu khung thì tự tải tin cũ hơn
  const onListScroll = () => {
    const el = listRef.current;
    if (el && el.scrollTop < 120) loadEarlier();
  };

  // Mở khung thì đặt con trỏ vào ô nhập
  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Textarea tự cao/thu gọn mượt theo nội dung và nút mở rộng: tắt transition lúc đo scrollHeight, reflow ở chiều cao cũ rồi gán chiều cao mới
  useLayoutEffect(() => {
    const el = inputRef.current;
    if (!el || !open) return;
    const prev = lastHeightRef.current;
    el.style.transition = "none";
    el.style.height = "auto";
    const measured = el.scrollHeight;
    const next = composerExpanded ? Math.min(Math.max(measured, 220), 320) : Math.min(Math.max(measured, 48), 160);
    el.style.height = `${prev}px`;
    void el.offsetHeight;
    el.style.transition = "";
    el.style.height = `${next}px`;
    lastHeightRef.current = next;
  }, [input, composerExpanded, open, loading]);

  if (!visible) return null;

  const submit = () => {
    if (!input.trim() || loading || openingSession || !canSend) return;
    send(input);
    setInput("");
    setComposerExpanded(false);
  };

  const docked = panelMode === "compact" || panelMode === "half";
  const floating = docked && pathname.startsWith("/analysis");
  const fullScreen = panelMode === "full";
  const windowMode = panelMode === "window";
  const empty = turns.length === 0 && !loading && !error && !openingSession;
  const chatTitle =
    currentSessionTitle?.trim() ||
    turns.find((t) => t.role === "user")?.content.trim() ||
    "Cuộc trò chuyện mới";

  // Đang ở Toàn màn hình mà bấm hành động chuyển trang / xem trên trang thì tự thu về khung nhỏ để thấy nội dung trang
  const shrinkIfFull = () => {
    if (fullScreen) setPanelMode("compact");
  };

  const onPanelClickCapture = (e: React.MouseEvent<HTMLElement>) => {
    if (!fullScreen) return;
    const anchor = (e.target as HTMLElement | null)?.closest("a[href]");
    if (!anchor) return;
    const href = anchor.getAttribute("href") ?? "";
    if (href.startsWith("/") && anchor.getAttribute("target") !== "_blank") {
      setPanelMode("compact");
    }
  };

  return (
    <aside
      aria-label={chatTitle}
      aria-hidden={!open}
      inert={!open || undefined}
      onClickCapture={onPanelClickCapture}
      className={cn(
        "fixed inset-0 z-50 flex overflow-hidden bg-surface motion-reduce:transition-none",
        windowMode
          ? "transition-none"
          : "transition-[transform,opacity,width,height,top,right,bottom,left,border-radius] duration-250 ease-out",
        fullScreen ? "flex-row" : "flex-col",
        // Màn rộng: tùy chế độ Nhỏ / Nửa màn hình / Toàn màn hình / Cửa sổ
        docked && "min-[900px]:left-auto min-[900px]:w-(--assistant-w) min-[900px]:border-l min-[900px]:border-line",
        windowMode &&
          "min-[900px]:top-auto min-[900px]:right-5 min-[900px]:bottom-5 min-[900px]:left-auto min-[900px]:h-[min(640px,calc(100dvh-2.5rem))] min-[900px]:w-[420px] min-[900px]:rounded-2xl min-[900px]:border min-[900px]:border-line min-[900px]:shadow-2xl",
        // Trang phân tích: khung chat bo góc, nổi trên nền xanh đậm như bảng câu hỏi
        floating &&
          "min-[900px]:top-3 min-[900px]:right-3 min-[900px]:bottom-3 min-[900px]:w-[calc(var(--assistant-w)-0.75rem)] min-[900px]:rounded-2xl min-[900px]:border-0 min-[900px]:shadow-lg",
        // Hiệu ứng mở/đóng cho thanh bên phải và toàn màn hình; chế độ Cửa sổ hiện/ẩn trực tiếp
        open
          ? "translate-x-0 scale-100 opacity-100"
          : cn(
              "pointer-events-none opacity-0",
              windowMode ? "hidden" : fullScreen ? "scale-[0.985]" : "translate-x-full",
            ),
      )}
    >
      {fullScreen && canSend && (
        <ChatHistorySidebar
          open={fullSidebarOpen && open}
          currentSessionId={currentSessionId}
          disabled={loading || openingSession}
          onToggle={() => setFullSidebarOpen(false)}
          onNewChat={newChat}
          onShrink={() => setPanelMode("compact")}
          onGoHome={() => {
            setPanelMode("compact");
            closePanel();
            router.push("/");
          }}
          onSelect={(id, title) => void openSession(id, title)}
          onRenamed={(id, title) => {
            if (id === currentSessionId) setCurrentSessionTitle(title);
          }}
          onDeleted={(id) => {
            if (id === currentSessionId) newChat();
          }}
        />
      )}
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
      <div ref={listRef} onScroll={onListScroll} className="flex min-h-0 flex-1 flex-col overflow-y-auto">
        {/* Đầu khung dính trên cùng, nền mờ dần xuống dưới để tin nhắn trượt qua bên dưới */}
        <div className="sticky top-0 z-10 bg-[linear-gradient(to_bottom,var(--surface)_70%,transparent)] px-3 pt-3 pb-6">
          <div className="grid grid-cols-[4.75rem_minmax(0,1fr)_4.75rem] items-center gap-2">
            {/* Lịch sử: toàn màn hình khi thanh bên đang ẩn thì hiện nút mở lại; chế độ khác mở popover */}
            <div className="justify-self-start">
              {canSend ? (
                fullScreen ? (
                  <>
                    {!fullSidebarOpen && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        onClick={() => setFullSidebarOpen(true)}
                        aria-label="Hiện thanh lịch sử"
                        title="Hiện thanh lịch sử"
                        className="hidden size-9 rounded-full border border-line bg-[var(--surface-2)] text-ink-2 hover:border-brand/40 hover:bg-brand-wash hover:text-brand min-[900px]:inline-flex"
                      >
                        <MenuIcon className="size-4.5" />
                      </Button>
                    )}
                    <div className="min-[900px]:hidden">
                      <ChatHistoryPopover
                        currentSessionId={currentSessionId}
                        disabled={loading || openingSession}
                        onSelect={(id, title) => void openSession(id, title)}
                        onRenamed={(id, title) => {
                          if (id === currentSessionId) setCurrentSessionTitle(title);
                        }}
                        onDeleted={(id) => {
                          if (id === currentSessionId) newChat();
                        }}
                      />
                    </div>
                  </>
                ) : (
                  <ChatHistoryPopover
                    currentSessionId={currentSessionId}
                    disabled={loading || openingSession}
                    onSelect={(id, title) => void openSession(id, title)}
                    onRenamed={(id, title) => {
                      if (id === currentSessionId) setCurrentSessionTitle(title);
                    }}
                    // Xóa đúng phiên đang mở thì khung chat chuyển sang cuộc trò chuyện mới (phiên đó không hỏi tiếp được nữa)
                    onDeleted={(id) => {
                      if (id === currentSessionId) newChat();
                    }}
                  />
                )
              ) : null}
            </div>
            <h2
              title={chatTitle}
              className="min-w-0 truncate text-center font-heading text-sm font-semibold text-ink"
            >
              {chatTitle}
            </h2>
            <div className="flex shrink-0 items-center justify-self-end rounded-full border border-line bg-[var(--surface-2)] p-0.5">
              <PanelModeMenu mode={panelMode} onSelect={setPanelMode} />
              <IconButton label="Đóng trợ lý" onClick={closePanel} className="size-8 rounded-full">
                <XIcon className="size-4" />
              </IconButton>
            </div>
          </div>
        </div>

        {openingSession || loadingOlder ? (
          <div
            role="status"
            aria-label={openingSession ? "Đang mở cuộc trò chuyện" : "Đang tải tin cũ hơn"}
            className="flex flex-1 items-center justify-center px-6 pb-10"
          >
            <span className="size-6 rounded-full border-2 border-[var(--surface-3)] border-t-brand animate-spin motion-reduce:animate-none" />
          </div>
        ) : empty ? (
          /* Chưa hỏi gì: lời chào ở giữa khung và các câu hỏi gợi ý xếp dọc */
          <div className="flex flex-1 flex-col items-center justify-center px-6 pb-10 text-center">
            <p className="font-heading text-xl font-semibold text-ink">USDSS Advisor</p>
            <p className="mt-1.5 max-w-[34ch] text-sm leading-relaxed text-ink-2">
              {page.greeting ?? "Tôi có thể giúp gì cho bạn?"}
            </p>
            {canSend && page.suggestions.length > 0 && (
              <div className="mt-6 flex w-full min-w-0 max-w-xs flex-col gap-2">
                {page.suggestions.map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => send(s)}
                    className="min-h-10 w-full min-w-0 cursor-pointer whitespace-normal break-words rounded-lg border border-line bg-surface px-3.5 py-2 text-sm leading-snug text-ink outline-none transition-[color,background-color,border-color,transform,scale] duration-[var(--cds-dur-fast)] hover:border-brand/40 hover:bg-brand-wash focus-visible:ring-3 focus-visible:ring-ring/50 active:scale-[0.975] motion-reduce:transition-none motion-reduce:active:scale-100"
                  >
                    {s}
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          <div className={cn("flex flex-col gap-7 px-4 pb-6 text-[14.5px] leading-relaxed", fullScreen && "mx-auto w-full max-w-3xl")}>
            {resumed && hasOlder && (
              <div className="flex justify-center">
                <Button type="button" variant="ghost" size="sm" onClick={loadEarlier}>
                  Tải tin cũ hơn
                </Button>
              </div>
            )}

            {turns.map((t, i) => (
              <Turn
                key={i}
                turn={t}
                className={i > 0 && t.role === "user" ? "mt-5" : undefined}
                // D3: nút trả lời nhanh chỉ bấm được ở lượt cuối, khi không chờ trả lời, đã đăng nhập và không bị chặn tuổi
                onReply={i === turns.length - 1 && !loading && canSend ? send : undefined}
              />
            ))}

            {/* Advisor trả từng ý khi xong: hiện dần các câu đã có, câu cuối cùng sẽ thay thế khi xong cả lượt */}
            {loading && streaming?.text && (
              <Turn turn={{ role: "assistant", content: streaming.text, widget: streaming.widget }} />
            )}

            {loading && (
              <TypingIndicator
                recommending={
                  turns.at(-2)?.role === "assistant" &&
                  turns.at(-2)?.content.includes("đề xuất lộ trình và danh sách trường")
                }
                progress={
                  streaming && streaming.answers >= 2
                    ? `Đã trả lời ${streaming.done}/${streaming.answers} câu hỏi...`
                    : undefined
                }
              />
            )}

            {error && (
              <div role="alert" className="flex items-start gap-3 rounded-xl border border-risk/15 bg-risk-wash py-3.5 pr-4 pl-5">
                <TriangleAlertIcon className="mt-0.5 size-4.5 shrink-0 text-risk" aria-hidden />
                <div className="min-w-0 space-y-2.5">
                  <div>
                    <p className="text-sm font-semibold text-ink">
                      {error.kind === "auth" ? "Cần đăng nhập để hỏi tiếp" : "Đã có lỗi xảy ra"}
                    </p>
                    <p className="text-xs leading-relaxed text-ink-2">{error.message}</p>
                  </div>
                  {error.kind === "auth" ? (
                    <Link
                      href={`/login?redirect=${encodeURIComponent(pathname)}`}
                      className={cn(buttonVariants({ variant: "outline", size: "sm" }), "rounded-lg bg-surface hover:bg-ground")}
                    >
                      Đăng nhập
                    </Link>
                  ) : (
                    <Button variant="outline" size="sm" onClick={retry} className="rounded-lg bg-surface hover:bg-ground">
                      <RotateCwIcon />
                      Thử lại
                    </Button>
                  )}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Ô nhập */}
      <div className={cn("px-3 pt-1 pb-2.5", fullScreen && "mx-auto w-full max-w-3xl")}>
        {needLogin ? (
          <div className="flex items-center justify-between gap-3 rounded-2xl border border-[var(--control-border)]/70 bg-[var(--surface-2)] px-4 py-3.5">
            <div className="min-w-0 space-y-1">
              <LockIcon className="size-4 text-ink-2" />
              <p className="text-sm font-medium text-ink">Vui lòng đăng nhập để hỏi</p>
            </div>
            <Link
              href={`/login?redirect=${encodeURIComponent(pathname)}`}
              className={cn(buttonVariants({ size: "sm" }), "shrink-0 rounded-lg bg-brand text-brand-foreground hover:bg-brand-ink")}
            >
              Đăng nhập
            </Link>
          </div>
        ) : ageBlock ? (
          // [Q11] Chat gửi dữ liệu cá nhân cho AI: học sinh dưới 16 cần phụ huynh đồng ý
          <div className="flex items-center justify-between gap-3 rounded-2xl border border-[var(--control-border)]/70 bg-[var(--surface-2)] px-4 py-3.5">
            <div className="min-w-0 space-y-1">
              <UsersIcon className="size-4 text-ink-2" />
              <p className="text-sm leading-snug font-medium text-ink">
                Hãy liên hệ với phụ huynh để xác nhận trước khi dùng nhé
              </p>
            </div>
            <Link
              href="/account#parent-links"
              className={cn(buttonVariants({ size: "sm" }), "shrink-0 rounded-lg bg-brand text-brand-foreground hover:bg-brand-ink")}
            >
              Xác nhận
            </Link>
          </div>
        ) : loading ? (
          <div className="flex items-center justify-between gap-3 rounded-2xl border border-[var(--control-border)]/70 bg-[var(--surface-2)] px-4 py-3">
            <AnimatedShinyText className="text-sm font-medium">USDSS đang trả lời...</AnimatedShinyText>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={stop}
              className="shrink-0 rounded-lg bg-brand-wash text-brand hover:bg-brand hover:text-white"
            >
              Dừng
            </Button>
          </div>
        ) : (
          <div
            className={cn(
              page.simulation !== undefined && "overflow-hidden rounded-2xl bg-brand p-0.5",
            )}
          >
            {/* Dải ngữ cảnh hồ sơ thật / giả định gắn liền trên đầu ô nhập */}
            {page.simulation === null && (
              <div className="flex items-center gap-2 px-3.5 py-2 text-xs font-medium text-white">
                <span className="size-1.5 shrink-0 rounded-full bg-white/80" />
                Đang xem hồ sơ thật của bạn
              </div>
            )}
            {page.simulation && (
              <div className="flex flex-wrap items-center justify-between gap-1.5 px-3.5 py-2 text-xs text-white">
                <span className="inline-flex items-center gap-1.5 font-semibold">
                  <FlaskConicalIcon className="size-3.5" />
                  Giả định
                </span>
                <div className="flex flex-wrap items-center gap-1">
                  {describeSimulation(page.simulation)
                    .split(" · ")
                    .filter(Boolean)
                    .map((part) => (
                      <span key={part} className="rounded-md bg-white/18 px-2 py-0.5 text-[11px] font-medium text-white">
                        {part}
                      </span>
                    ))}
                  <button
                    type="button"
                    onClick={resetSimulation}
                    aria-label="Về hồ sơ thật"
                    title="Về hồ sơ thật"
                    className="inline-flex size-5 cursor-pointer items-center justify-center rounded-md text-white/80 transition-colors hover:bg-white/20 hover:text-white"
                  >
                    <Undo2Icon className="size-3.5" />
                  </button>
                </div>
              </div>
            )}
            <form
              onSubmit={(e) => {
                e.preventDefault();
                submit();
              }}
              className={cn(
                "flex flex-col gap-2 bg-[var(--surface-2)] px-3.5 pt-3 pb-2.5 transition-colors focus-within:ring-3 focus-within:ring-ring/20",
                page.simulation !== undefined
                  ? "rounded-[14px]"
                  : "rounded-2xl border border-[var(--control-border)]/70 focus-within:border-brand",
              )}
            >
              <textarea
                ref={inputRef}
                rows={2}
                value={input}
                maxLength={1000}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                    e.preventDefault();
                    submit();
                  }
                }}
                placeholder="Hỏi USDSS Advisor"
                aria-label="Câu hỏi cho trợ lý"
                className="h-12 w-full resize-none bg-transparent text-sm text-ink outline-none transition-[height] duration-200 ease-out placeholder:text-ink-3 motion-reduce:transition-none"
              />
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5">
                  {subscription && (subscription.lowQuota || subscription.expiringSoon) && (
                    <QuotaAlertBadge subscription={subscription} onNavigate={shrinkIfFull} />
                  )}
                </div>
                <div className="flex items-center gap-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() => setComposerExpanded((v) => !v)}
                    aria-label={composerExpanded ? "Thu gọn ô nhập" : "Mở rộng ô nhập"}
                    title={composerExpanded ? "Thu gọn ô nhập" : "Mở rộng ô nhập"}
                    className={cn(
                      "size-8 rounded-lg text-ink-2 hover:bg-ground hover:text-ink",
                      composerExpanded && "bg-brand-wash text-brand hover:bg-brand-wash hover:text-brand",
                    )}
                  >
                    {composerExpanded ? <Minimize2Icon className="size-4" /> : <Maximize2Icon className="size-4" />}
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={newChat}
                    aria-label="Cuộc trò chuyện mới"
                    title="Cuộc trò chuyện mới"
                    className="size-8 rounded-lg text-ink-2 hover:bg-ground hover:text-ink"
                  >
                    <PlusIcon className="size-4.5" />
                  </Button>
                  <Button
                    type="submit"
                    size="icon"
                    aria-label="Gửi"
                    disabled={loading || openingSession || !canSend || !input.trim()}
                    className="size-9 rounded-lg bg-brand text-brand-foreground hover:bg-brand hover:opacity-90 [&:disabled:not([aria-busy])]:opacity-40"
                  >
                    <ArrowUpIcon className="size-4.5" />
                  </Button>
                </div>
              </div>
            </form>
          </div>
        )}
        <p className="mt-1.5 text-center text-[11px] text-ink-3">USDSS Advisor là AI và có thể mắc lỗi</p>
      </div>
      </div>
    </aside>
  );
}

function PanelModeIcon({ kind }: { kind: AssistantPanelMode }) {
  return (
    <svg aria-hidden viewBox="0 0 22 16" className="h-4 w-5.5 shrink-0" fill="none">
      <rect x="0.75" y="0.75" width="20.5" height="14.5" rx="3.25" stroke="currentColor" strokeWidth="1.5" />
      {kind === "compact" && <rect x="13.5" y="2.25" width="6.25" height="11.5" rx="1.75" fill="currentColor" opacity="0.28" />}
      {kind === "half" && <rect x="10.75" y="2.25" width="9" height="11.5" rx="1.75" fill="currentColor" opacity="0.28" />}
      {kind === "full" && <rect x="2.25" y="2.25" width="17.5" height="11.5" rx="1.75" fill="currentColor" opacity="0.28" />}
      {kind === "window" && <rect x="12.5" y="6" width="7.25" height="7.75" rx="1.75" fill="currentColor" opacity="0.28" />}
    </svg>
  );
}

const DOCKED_MODES: { mode: AssistantPanelMode; label: string }[] = [
  { mode: "compact", label: "Nhỏ" },
  { mode: "half", label: "Nửa màn hình" },
  { mode: "full", label: "Toàn màn hình" },
];

function PanelModeMenu({
  mode,
  onSelect,
}: {
  mode: AssistantPanelMode;
  onSelect: (mode: AssistantPanelMode) => void;
}) {
  const [menuOpen, setMenuOpen] = useState(false);

  const pick = (next: AssistantPanelMode) => {
    onSelect(next);
    setMenuOpen(false);
  };

  return (
    <Popover open={menuOpen} onOpenChange={setMenuOpen}>
      <PopoverTrigger
        type="button"
        aria-label="Chế độ hiển thị khung chat"
        title="Chế độ hiển thị"
        className="hidden size-8 cursor-pointer items-center justify-center rounded-full text-ink-2 transition-colors hover:bg-ground hover:text-ink focus-visible:ring-3 focus-visible:ring-ring/50 min-[900px]:inline-flex"
      >
        <PanelModeIcon kind={mode} />
      </PopoverTrigger>
      <PopoverContent align="end" sideOffset={8} className="w-48 gap-0.5 rounded-xl border border-line p-1.5">
        {DOCKED_MODES.map((item) => (
          <button
            key={item.mode}
            type="button"
            onClick={() => pick(item.mode)}
            className={cn(
              "flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm transition-colors",
              mode === item.mode ? "bg-brand-wash font-semibold text-brand" : "text-ink hover:bg-ground",
            )}
          >
            <PanelModeIcon kind={item.mode} />
            <span>{item.label}</span>
          </button>
        ))}
        <div className="my-1 border-t border-line" />
        <button
          type="button"
          onClick={() => pick("window")}
          className={cn(
            "flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm transition-colors",
            mode === "window" ? "bg-brand-wash font-semibold text-brand" : "text-ink hover:bg-ground",
          )}
        >
          <PanelModeIcon kind="window" />
          <span>Cửa sổ</span>
        </button>
      </PopoverContent>
    </Popover>
  );
}

function QuotaAlertBadge({ subscription, onNavigate }: { subscription: SubscriptionSummaryDto; onNavigate?: () => void }) {
  const [now] = useState(() => Date.now());
  const activePool =
    subscription.chargingPool === "personal"
      ? subscription.personal
      : subscription.chargingPool === "family"
        ? subscription.family
        : null;
  const usedPct = activePool?.usedPercent ?? subscription.free.usedPercent ?? 80;
  const remainingPct = Math.max(0, Math.min(100, Math.round(100 - usedPct)));
  const isLowQuota = subscription.lowQuota;
  const endsAt = activePool?.endsAt;
  const daysLeft = endsAt ? Math.max(0, Math.ceil((new Date(endsAt).getTime() - now) / 86_400_000)) : null;
  const pillLabel = isLowQuota
    ? remainingPct > 0
      ? `Còn ${remainingPct}% lượt hỏi`
      : "Hết lượt hỏi"
    : daysLeft != null && daysLeft > 0
      ? `Sắp hết hạn · còn ${daysLeft} ngày`
      : "Gói sắp hết hạn";

  return (
    <Popover>
      <PopoverTrigger
        type="button"
        className="inline-flex h-8 items-center gap-1.5 rounded-lg bg-warn-wash px-2.5 text-xs font-semibold text-warn transition-colors hover:bg-warn/15 focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        <TriangleAlertIcon className="size-3.5 shrink-0 text-warn" />
        {pillLabel}
      </PopoverTrigger>
      <PopoverContent side="top" align="start" sideOffset={8} className="w-72 rounded-xl border border-line p-3.5">
        <div className="space-y-1">
          <p className="text-sm font-semibold text-ink">
            {isLowQuota
              ? remainingPct > 0
                ? `Chỉ còn ${remainingPct}% lượt hỏi`
                : "Đã dùng hết lượt hỏi trong kỳ"
              : "Gói dịch vụ sắp hết hạn"}
          </p>
          <p className="text-xs leading-relaxed text-ink-2">
            {isLowQuota
              ? "Nâng cấp hoặc mua thêm gói để tiếp tục tra cứu trường và tư vấn hồ sơ không bị gián đoạn."
              : `Gói hiện tại của bạn sắp kết thúc${endsAt ? ` vào ngày ${formatVnDate(endsAt)}` : ""}. Gia hạn sớm để giữ nguyên quyền lợi trợ lý AI.`}
          </p>
        </div>
        <Link
          href="/plans"
          onClick={onNavigate}
          className={cn(
            buttonVariants({ size: "sm" }),
            "mt-1 w-full justify-center rounded-lg bg-brand text-brand-foreground hover:bg-brand-ink",
          )}
        >
          {isLowQuota ? "Xem gói dịch vụ" : "Gia hạn ngay"}
        </Link>
      </PopoverContent>
    </Popover>
  );
}

// Lượt chat có chạy gợi ý trường mất khoảng một phút: chờ quá 8 giây thì báo thêm để học sinh không tưởng bị treo.
// Giao diện không biết trước lượt nào chạy gợi ý, nên dựa vào thời gian chờ; lượt thường xong trước mốc này
const SLOW_REPLY_MS = 8000;

function TypingIndicator({ recommending, progress }: { recommending?: boolean; progress?: string }) {
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), SLOW_REPLY_MS);
    return () => clearTimeout(timer);
  }, []);

  const text = progress
    ? progress
    : slow
      ? recommending
        ? "Mình đang tính toán các trường phù hợp với bạn, có thể mất khoảng một phút..."
        : "Mình đang xử lý, bạn chờ thêm một chút nhé..."
      : "USDSS đang suy nghĩ...";

  return (
    <div className="py-1" role="status" aria-label="Đang trả lời">
      <AnimatedShinyText className="text-xs font-medium">{text}</AnimatedShinyText>
    </div>
  );
}

// Link mở form từ chat; data.goals là mã việc advisor gửi (danh_gia, lo_trinh, goi_y_truong)
function formLink(data: Record<string, unknown> | null | undefined): string {
  const goals = Array.isArray(data?.goals) ? data.goals.filter((g) => typeof g === "string") : [];
  return goals.length > 0 ? `/analysis?from=chat&goal=${goals.join(",")}` : "/analysis?from=chat";
}

// Nút "Trả lời trong chat" của advisor (CHAT_BUTTON trong replies.py) mở phần phỏng vấn hồ sơ trong chat, chỉ dành cho Premium.
// Đang dùng lượt miễn phí thì thay bằng link sang form hồ sơ (form không tính lượt), bỏ nút "Điền bằng form" trùng ý.
const INTERVIEW_REPLY = "Trả lời trong chat";
const FORM_REPLY = "Điền bằng form";
function withoutInterview(replies: QuickReply[] | null | undefined): QuickReply[] | null | undefined {
  if (!replies?.some((r) => r.value === INTERVIEW_REPLY)) return replies;
  return replies
    .filter((r) => r.value !== FORM_REPLY)
    .map((r) => (r.value === INTERVIEW_REPLY ? { label: FORM_REPLY, value: FORM_REPLY, href: "/analysis?from=chat" } : r));
}

function Turn({ turn, onReply, className }: { turn: AssistantTurn; onReply?: (value: string) => void; className?: string }) {
  const router = useRouter();
  const { sendAction, subscription, panelMode, setPanelMode } = useAssistant();
  const shrinkIfFull = () => {
    if (panelMode === "full") setPanelMode("compact");
  };
  // Xét chargingPool chứ không xét tier: Premium đã dùng hết các gói cũng bị khóa phỏng vấn trong chat (bảng gating)
  const quickReplies = subscription?.chargingPool === "free" ? withoutInterview(turn.quickReplies) : turn.quickReplies;
  if (turn.role === "user") {
    return (
      <div className={cn("rounded-xl bg-brand-wash px-4 py-3 whitespace-pre-wrap text-ink", className)}>{turn.content}</div>
    );
  }
  // Câu trả lời là chữ do AI viết; riêng danh sách tìm trường có thẻ trường (5 trường đầu, nút chi tiết). Các nút và widget chọn bên dưới vẫn giữ
  const searchData =
    turn.widget?.type === "school_search" && Array.isArray(turn.widget.data?.items)
      ? (turn.widget.data as unknown as SchoolSearchData)
      : null;
  // Thẻ trường đã nói đủ đoạn chữ này (tổng số, cách xếp, danh sách): bỏ đoạn đó, các đoạn khác của lượt vẫn hiện
  // UC-154: bảng so sánh theo độ phù hợp nói đủ cả số liệu lẫn nhận xét, nên cũng bỏ đoạn chữ trùng
  const fitData =
    turn.widget?.type === "fit_compare" && Array.isArray(turn.widget.data?.schools)
      ? (turn.widget.data as unknown as FitCompareData)
      : null;
  const coveredText = searchData?.coveredText ?? fitData?.coveredText;
  const content = coveredText && turn.content ? turn.content.replace(coveredText, "").trim() : turn.content;
  const goalData = turn.widget?.type === "goal_seek" ? turn.widget.data : null;
  const goalResult = (goalData?.result as SchoolGoalResult | undefined) ?? null;
  const goalBasisLabel = typeof goalData?.basisLabel === "string" ? goalData.basisLabel : null;
  return (
    <div className="w-full px-1 text-foreground">
      {/* Cần gì để vào trường X: bảng "bạn so với trường" đứng trước, lời giải thích nằm bên dưới */}
      {goalResult && <GoalCompareTable result={goalResult} basisLabel={goalBasisLabel} />}
      {/* Có thẻ trung tâm thì danh sách chữ (các dòng bắt đầu bằng "- ") đã nằm trong thẻ: chỉ hiện phần mở đầu */}
      {content && <ChatText text={turn.widget?.type === "centers" ? content.split("\n- ")[0] : content} />}
      {/* Lượt bị khóa theo gói: câu hỏi có ý cần Premium (phần được phép vẫn trả lời ở trên), hoặc hết lượt miễn phí tháng này */}
      {turn.locked && (
        <PremiumLockedCard
          className="mt-2.5"
          title={
            turn.locked.reason === "quota_exhausted"
              ? "Bạn đã dùng hết lượt hỏi miễn phí của tháng này"
              : "Câu hỏi này cần tài khoản USDSS Premium"
          }
          note={
            turn.locked.reason === "quota_exhausted" && turn.locked.resetsAt
              ? `Lượt hỏi miễn phí được làm mới ngày ${formatVnDate(turn.locked.resetsAt)}.`
              : null
          }
        />
      )}
      {turn.widget?.type === "centers" && <CenterCardsWidget data={turn.widget.data} onPick={onReply} />}
      {searchData && <SchoolSearchCard data={searchData} />}
      {fitData && <FitCompareCard data={fitData} />}
      <ChatImages media={turn.media} />
      {turn.citations && turn.citations.length > 0 && (
        <ol className="mt-2 list-decimal space-y-0.5 pl-5 text-xs text-muted-foreground">
          {turn.citations.map((c, j) => {
            // UC-37: một liên kết thì tên nguồn là liên kết; nhiều liên kết thì hiển thị đủ, mỗi liên kết một nhãn tên miền
            const links = citationLinks(c.url);
            const only = links.length === 1 ? links[0] : null;
            return (
              <li key={j}>
                {only ? (
                  <a href={only.href} target="_blank" rel="noopener noreferrer" className="underline hover:text-brand">
                    {c.title}
                  </a>
                ) : (
                  c.title
                )}
                {links.length > 1 && (
                  <span>
                    {" ("}
                    {links.map((link, k) => (
                      <span key={link.href}>
                        {k > 0 ? ", " : ""}
                        <a href={link.href} target="_blank" rel="noopener noreferrer" className="underline hover:text-brand">
                          {link.label}
                        </a>
                      </span>
                    ))}
                    {")"}
                  </span>
                )}
                <span className="text-ink-3"> · {sourceDateText(c.retrievedAt)}</span>
              </li>
            );
          })}
        </ol>
      )}
      {turn.disclaimer && <p className="mt-2 text-xs text-muted-foreground italic">{turn.disclaimer}</p>}
      {/* Đang sửa hồ sơ: giá trị cũ của mục đang hỏi lại */}
      {turn.widget?.data?.current != null && <CurrentValues current={turn.widget.data.current} />}
      {turn.widget?.type === "review" && <ReviewWidget data={turn.widget.data} />}
      {/* E: chỉ chọn được ở lượt cuối, giống nút trả lời nhanh */}
      {turn.widget?.type === "major" && <MajorWidget onPick={onReply} />}
      {turn.widget?.type === "select" && <SelectWidget data={turn.widget.data} onPick={onReply} />}
      {turn.widget?.type === "school" && (
        <SchoolWidget
          data={turn.widget.data}
          onPick={
            onReply
              ? (question, schoolCode) =>
                  turn.widget?.data?.kind === "explain"
                    ? sendAction(question, { type: "explain", schoolCode })
                    : onReply(question)
              : undefined
          }
        />
      )}
      {turn.widget?.type === "budget" && <BudgetWidget data={turn.widget.data} onPick={onReply} />}
      {turn.widget?.type === "scholarship" && <ScholarshipWidget data={turn.widget.data} onPick={onReply} />}
      {/* Mục 1: học sinh chọn điền bằng form. from=chat: điền xong form thì quay về chat trả lời câu đã hỏi.
          goal=: việc học sinh đã hỏi trong chat, form tích sẵn và khóa các ô nhiệm vụ này */}
      {turn.widget?.type === "form" && (
        <Link
          href={formLink(turn.widget.data)}
          className="mt-2.5 inline-flex h-8 items-center rounded-lg bg-brand px-3.5 text-[13px] font-semibold text-white transition-colors hover:bg-brand-ink"
        >
          Mở form hồ sơ
        </Link>
      )}
      {/* Chat vừa chạy gợi ý trường: mở trang Gợi ý trường xem đủ danh sách (đọc lượt vừa lưu, không chạy lại) */}
      {turn.widget?.type === "recommend" && (
        // Mỗi lần bấm thêm ts khác nhau để trang tải lại kết quả mới nhất, kể cả khi đang đứng sẵn ở trang này
        <Button
          type="button"
          onClick={() => {
            shrinkIfFull();
            router.push(`/recommendations?ts=${Date.now()}`);
          }}
          className="mt-2.5 rounded-lg bg-brand px-3.5 text-[13px] font-semibold text-white hover:bg-brand-ink"
        >
          Xem đầy đủ danh sách trường
        </Button>
      )}
      {/* What-if: mở trang Gợi ý trường với các giá trị vừa thử điền sẵn vào thanh kéo */}
      {turn.widget?.type === "simulate" && (
        <Button
          type="button"
          onClick={() => {
            shrinkIfFull();
            showSimulation(turn.widget?.data, router.push);
          }}
          className="mt-2.5 rounded-lg bg-brand px-3.5 text-[13px] font-semibold text-white hover:bg-brand-ink"
        >
          Xem trên thanh kéo
        </Button>
      )}
      {onReply && quickReplies && quickReplies.length > 0 && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          {quickReplies.map((r) => (
            <button
              key={r.value}
              type="button"
              onClick={() => {
                if (r.href) {
                  shrinkIfFull();
                  router.push(r.href);
                } else if (r.action) {
                  sendAction(r.value, r.action);
                } else {
                  onReply(r.value);
                }
              }}
              className="group inline-flex min-h-9 cursor-pointer items-center gap-1 rounded-lg border border-line bg-surface px-3 py-1.5 text-left text-[13px] font-medium text-ink outline-none transition-[color,background-color,border-color,transform,scale] duration-[var(--cds-dur-fast)] hover:border-brand/40 hover:bg-brand-wash hover:text-brand focus-visible:ring-3 focus-visible:ring-ring/50 active:scale-[0.975] motion-reduce:transition-none motion-reduce:active:scale-100"
            >
              {r.label}
              <ChevronRightIcon className="size-3.5 shrink-0 opacity-60 transition-transform group-hover:translate-x-0.5 group-hover:opacity-100" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function IconButton({
  label,
  onClick,
  className,
  children,
}: {
  label: string;
  onClick: () => void;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={cn("text-ink-2 hover:bg-ground hover:text-ink dark:hover:bg-ground", className)}
    >
      {children}
    </Button>
  );
}

// Thẻ chào nhỏ hiện khi mới vào trang; bấm X thì thu về nút tròn, lựa chọn nhớ trong phiên (sessionStorage)
const PEEK_KEY = "usas.advisor.peek-dismissed";
const peekListeners = new Set<() => void>();
function readPeekDismissed() {
  try {
    return sessionStorage.getItem(PEEK_KEY) === "1";
  } catch {
    return false;
  }
}
function subscribePeek(fn: () => void) {
  peekListeners.add(fn);
  return () => peekListeners.delete(fn);
}
export function setPeekDismissed(dismissed: boolean) {
  try {
    if (dismissed) sessionStorage.setItem(PEEK_KEY, "1");
    else sessionStorage.removeItem(PEEK_KEY);
  } catch {
    // ignore
  }
  peekListeners.forEach((fn) => fn());
}

const LAUNCHER_DOTS = [
  { radius: 28, count: 28, dotR: 1.6, opacity: 0.8, offset: 0 },
  { radius: 24, count: 24, dotR: 1.15, opacity: 0.48, offset: Math.PI / 24 },
  { radius: 20, count: 20, dotR: 0.8, opacity: 0.22, offset: Math.PI / 20 },
].flatMap((ring, ringIdx) =>
  Array.from({ length: ring.count }, (_, i) => {
    const angle = ring.offset + (i * 2 * Math.PI) / ring.count;
    return {
      key: `${ringIdx}-${i}`,
      cx: (28 + ring.radius * Math.cos(angle)).toFixed(2),
      cy: (28 + ring.radius * Math.sin(angle)).toFixed(2),
      r: ring.dotR,
      opacity: ring.opacity,
    };
  }),
);

/**
 * Lối vào trợ lý ở góc dưới phải khi khung lớn đang đóng.
 * Lần đầu vào: thẻ nhỏ "USDSS Advisor · Tôi có thể giúp gì cho bạn?" có sẵn ô hỏi; gửi câu thì mở khung lớn và hỏi luôn.
 * Bấm X: thẻ thu thành nút tròn chỉ có icon, bấm nút để mở lại khung lớn.
 */
export function AssistantLauncher() {
  const { open, openPanel } = useAssistant();
  const visible = useAssistantVisible();
  // Server và lần render đầu coi như đã thu gọn để HTML khớp nhau, sau đó đọc sessionStorage
  const dismissed = useSyncExternalStore(subscribePeek, readPeekDismissed, () => true);
  const [text, setText] = useState("");

  if (!visible || open) return null;

  const dismiss = () => {
    try {
      sessionStorage.setItem(PEEK_KEY, "1");
    } catch {
      // trình duyệt chặn lưu trữ: thẻ sẽ hiện lại ở lần tải sau
    }
    peekListeners.forEach((fn) => fn());
  };

  if (dismissed) {
    return (
      <button
        type="button"
        onClick={() => openPanel()}
        aria-label="Mở USDSS Advisor"
        title="USDSS Advisor"
        className="group fixed right-5 bottom-5 z-30 flex size-14 cursor-pointer items-center justify-center overflow-hidden rounded-full border border-brand/30 bg-white outline-none transition-[border-color,transform] hover:border-brand/55 focus-visible:ring-3 focus-visible:ring-ring/50 active:scale-[0.98] motion-safe:animate-in motion-safe:fade-in motion-safe:zoom-in-95 motion-safe:duration-200 print:hidden"
      >
        <svg
          aria-hidden
          viewBox="0 0 56 56"
          className="pointer-events-none absolute inset-0 size-full text-brand opacity-85 transition-opacity group-hover:opacity-100"
        >
          {LAUNCHER_DOTS.map((d) => (
            <circle key={d.key} cx={d.cx} cy={d.cy} r={d.r} fill="currentColor" fillOpacity={d.opacity} />
          ))}
        </svg>
        <Image
          src="/icons/USDSS.png"
          alt=""
          width={40}
          height={40}
          className="relative z-10 size-10 object-contain transition-transform duration-200 group-hover:scale-105"
        />
      </button>
    );
  }

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    const q = text.trim();
    openPanel(q || undefined);
    setText("");
  };

  return (
    <div
      role="dialog"
      aria-labelledby="advisor-peek-title"
      className="fixed right-5 bottom-5 z-30 w-[min(22rem,calc(100vw-2.5rem))] rounded-2xl border border-line bg-surface p-4 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-2 motion-safe:duration-300 print:hidden"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p id="advisor-peek-title" className="font-heading text-base font-semibold text-ink">
            USDSS Advisor
          </p>
          <p className="mt-1 text-sm text-ink-2">Tôi có thể giúp gì cho bạn?</p>
        </div>
        <IconButton label="Thu gọn" onClick={dismiss} className="-mt-1 -mr-1">
          <XIcon className="size-4.5" />
        </IconButton>
      </div>
      <form
        onSubmit={submit}
        className="mt-3 flex items-center gap-2 rounded-xl border border-line py-1.5 pr-1.5 pl-3.5 transition-colors focus-within:border-brand focus-within:ring-3 focus-within:ring-ring/20"
      >
        <label htmlFor="advisor-peek-input" className="sr-only">
          Câu hỏi cho USDSS Advisor
        </label>
        <input
          id="advisor-peek-input"
          value={text}
          maxLength={1000}
          onChange={(e) => setText(e.target.value)}
          placeholder="Aa"
          className="min-w-0 flex-1 bg-transparent text-sm text-ink outline-none placeholder:text-ink-3"
        />
        <Button
          type="submit"
          size="icon"
          aria-label="Gửi"
          className="bg-brand text-brand-foreground hover:bg-brand hover:opacity-90"
        >
          <ArrowUpIcon className="size-4" />
        </Button>
      </form>
    </div>
  );
}
