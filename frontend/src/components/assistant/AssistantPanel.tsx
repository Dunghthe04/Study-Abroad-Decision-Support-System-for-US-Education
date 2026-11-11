"use client";

import { useEffect, useRef, useState } from "react";
import { Button, buttonVariants } from "@/components/ui/button";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  ArrowUpIcon,
  ChevronRightIcon,
  FileTextIcon,
  FlaskConicalIcon,
  GraduationCapIcon,
  Maximize2Icon,
  MessageCircleIcon,
  Minimize2Icon,
  PlusIcon,
  RotateCwIcon,
  SlidersHorizontalIcon,
  TriangleAlertIcon,
  Undo2Icon,
  XIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { describeSimulation, resetSimulation, showSimulation } from "@/lib/simulation-link";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { useAssistant, type AssistantTurn } from "./AssistantProvider";
import { BudgetWidget } from "./BudgetWidget";
import { ChatText } from "./ChatText";
import { MajorWidget } from "./MajorWidget";
import { ReviewWidget } from "./ReviewWidget";
import { ScholarshipWidget } from "./ScholarshipWidget";
import { SchoolWidget } from "./SchoolWidget";
import { SelectWidget } from "./SelectWidget";

// Trang đăng nhập / đăng ký không hiện trợ lý
const HIDDEN_PREFIXES = ["/login", "/register", "/forgot-password", "/reset-password", "/verify-email"];

export function useAssistantVisible() {
  const pathname = usePathname();
  return !HIDDEN_PREFIXES.some((p) => pathname.startsWith(p));
}

/** Khung "Trợ lý USAS" cố định bên phải; màn hẹp thì phủ kín */
export function AssistantPanel() {
  const { open, wide, turns, loading, streaming, error, page, closePanel, toggleWide, newChat, send, retry } =
    useAssistant();
  const visible = useAssistantVisible();
  const pathname = usePathname();
  const [input, setInput] = useState("");
  const ageBlock = useAgeBlock(AGE_FEATURES.processStudentData);
  const listRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  // Cuộn xuống tin mới nhất
  useEffect(() => {
    const el = listRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [turns, loading, streaming, error, open]);

  // Mở khung thì đặt con trỏ vào ô nhập
  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Textarea tự cao theo nội dung, tối đa 160px
  useEffect(() => {
    const el = inputRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`;
  }, [input]);

  if (!visible || !open) return null;

  const submit = () => {
    if (!input.trim() || loading) return;
    send(input);
    setInput("");
  };

  const empty = turns.length === 0 && !loading && !error;

  return (
    <aside
      aria-label="USAS Advisor"
      className={cn(
        "fixed inset-0 z-50 flex flex-col overflow-hidden bg-surface",
        // Màn rộng: khung nổi, cách mép trên, dưới và phải; mép trái sát nội dung trang
        "min-[900px]:inset-y-3 min-[900px]:right-3 min-[900px]:left-auto min-[900px]:w-(--assistant-w) min-[900px]:rounded-2xl min-[900px]:border min-[900px]:border-line",
        "motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-right-4 motion-safe:duration-300",
      )}
    >
      <div ref={listRef} className="flex min-h-0 flex-1 flex-col overflow-y-auto">
        {/* Đầu khung dính trên cùng, nền mờ dần xuống dưới để tin nhắn trượt qua bên dưới */}
        <div className="sticky top-0 z-10 bg-[linear-gradient(to_bottom,var(--surface)_70%,transparent)] px-3 pt-3 pb-6">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={newChat}
              aria-label="Cuộc trò chuyện mới"
              title="Cuộc trò chuyện mới"
              className="inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-full border border-line text-ink-2 transition-colors hover:border-brand/40 hover:bg-brand-wash hover:text-brand active:scale-95"
            >
              <PlusIcon className="size-4.5" />
            </button>
            <h2 className="min-w-0 flex-1 truncate font-heading text-base font-semibold text-ink">USAS Advisor</h2>
            <div className="flex shrink-0 items-center rounded-lg border border-line p-0.5">
              <IconButton
                label={wide ? "Thu hẹp khung" : "Mở rộng khung"}
                onClick={toggleWide}
                className="hidden min-[900px]:inline-flex"
              >
                {wide ? <Minimize2Icon className="size-4" /> : <Maximize2Icon className="size-4" />}
              </IconButton>
              <IconButton label="Đóng trợ lý" onClick={closePanel}>
                <XIcon className="size-4.5" />
              </IconButton>
            </div>
          </div>

          {/* Trang gợi ý trường: chat đang nói về hồ sơ thật hay về giả định học sinh đang kéo (undefined: trang khác) */}
          {page.simulation === null && (
            <div className="mt-2.5 flex items-center gap-2 rounded-lg bg-brand-wash px-3 py-2 text-xs font-medium text-brand">
              <span className="size-1.5 shrink-0 rounded-full bg-brand" />
              Đang xem theo hồ sơ thật của bạn
            </div>
          )}
          {page.simulation && (
            <div className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-dashed border-ink-3 bg-surface px-3 py-2.5 text-xs text-ink">
              <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5">
                <span className="inline-flex items-center gap-1 font-semibold">
                  <FlaskConicalIcon className="size-3.5" />
                  Giả định, chưa phải kết quả thật
                </span>
                {describeSimulation(page.simulation)
                  .split(" · ")
                  .filter(Boolean)
                  .map((part) => (
                    <span key={part} className="rounded-md bg-ground px-2 py-0.5 font-medium">
                      {part}
                    </span>
                  ))}
              </div>
              <button
                type="button"
                onClick={resetSimulation}
                className="inline-flex shrink-0 cursor-pointer items-center gap-1 rounded-md border border-line px-2.5 py-1 font-semibold transition-colors hover:bg-ground"
              >
                <Undo2Icon className="size-3.5" />
                Về hồ sơ thật
              </button>
            </div>
          )}
        </div>

        {empty ? (
          /* Chưa hỏi gì: lời chào ở giữa khung và các câu hỏi gợi ý xếp dọc */
          <div className="flex flex-1 flex-col items-center justify-center px-6 pb-10 text-center">
            <p className="font-heading text-xl font-semibold text-ink">USAS Advisor</p>
            <p className="mt-1.5 max-w-[34ch] text-sm leading-relaxed text-ink-2">
              {page.greeting ?? "Tôi có thể giúp gì cho bạn?"}
            </p>
            {!ageBlock && page.suggestions.length > 0 && (
              <div className="mt-6 flex w-full max-w-xs flex-col gap-2">
                {page.suggestions.map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => send(s)}
                    className="min-h-10 cursor-pointer rounded-lg border border-line bg-surface px-3.5 py-2 text-sm text-ink transition-colors hover:border-brand/40 hover:bg-brand-wash active:translate-y-px"
                  >
                    {s}
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          <div className="flex flex-col gap-5 px-4 pb-4 text-[14.5px] leading-relaxed">
            {turns.map((t, i) => (
              <Turn
                key={i}
                turn={t}
                // D3: nút trả lời nhanh chỉ bấm được ở lượt cuối, khi không chờ trả lời và không bị chặn tuổi
                onReply={i === turns.length - 1 && !loading && !ageBlock ? send : undefined}
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
              <div role="alert" className="flex items-start gap-3 px-1">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-risk-wash text-risk">
                  <TriangleAlertIcon className="size-4.5" />
                </span>
                <div className="min-w-0 space-y-1.5">
                  <p className="text-sm font-semibold text-ink">
                    {error.kind === "auth" ? "Cần đăng nhập để hỏi tiếp" : "Đã có lỗi xảy ra"}
                  </p>
                  <p className="text-xs text-ink-2">{error.message}</p>
                  {error.kind === "auth" ? (
                    <Link
                      href={`/login?redirect=${encodeURIComponent(pathname)}`}
                      className={buttonVariants({ variant: "outline", size: "sm" })}
                    >
                      Đăng nhập
                    </Link>
                  ) : (
                    <Button variant="outline" size="sm" onClick={retry}>
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
      <div className="px-3 pt-1 pb-3">
        {ageBlock ? (
          // [Q11] Chat gửi dữ liệu cá nhân cho AI: học sinh dưới 16 cần phụ huynh đồng ý
          <p className="rounded-xl border border-line px-3.5 py-3 text-sm text-ink-2">
            {ageBlock.message}{" "}
            <Link href="/account#parent-links" className="font-medium text-brand underline">
              Mời phụ huynh xác nhận
            </Link>
          </p>
        ) : (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              submit();
            }}
            className="flex flex-col gap-2 rounded-2xl border border-line bg-surface px-3.5 pt-3 pb-2.5 transition-colors focus-within:border-brand focus-within:ring-3 focus-within:ring-ring/20"
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
              placeholder="Hỏi USAS Advisor"
              aria-label="Câu hỏi cho trợ lý"
              className="min-h-12 w-full resize-none bg-transparent text-sm text-ink outline-none placeholder:text-ink-3"
            />
            <button
              type="submit"
              aria-label="Gửi"
              disabled={loading || !input.trim()}
              className="flex size-9 shrink-0 cursor-pointer items-center justify-center self-end rounded-lg bg-brand text-brand-foreground transition-[opacity,transform] hover:opacity-90 active:scale-95 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ArrowUpIcon className="size-4.5" />
            </button>
          </form>
        )}
      </div>
    </aside>
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

  return (
    <div className="flex items-center gap-1 py-1" role="status" aria-label="Đang trả lời">
      {[0, 1, 2].map((d) => (
        <span
          key={d}
          className="size-1.5 animate-pulse rounded-full bg-muted-foreground"
          style={{ animationDelay: `${d * 200}ms` }}
        />
      ))}
      {progress ? (
        <span className="ml-2 text-xs text-muted-foreground">{progress}</span>
      ) : (
        slow && (
          <span className="ml-2 text-xs text-muted-foreground">
            {recommending
              ? "Mình đang tính toán các trường phù hợp với bạn, có thể mất khoảng một phút..."
              : "Mình đang xử lý, bạn chờ thêm một chút nhé..."}
          </span>
        )
      )}
    </div>
  );
}

function Turn({ turn, onReply }: { turn: AssistantTurn; onReply?: (value: string) => void }) {
  const router = useRouter();
  const { sendAction } = useAssistant();
  if (turn.role === "user") {
    return (
      <div className="rounded-xl bg-brand-wash px-4 py-3 whitespace-pre-wrap text-ink">{turn.content}</div>
    );
  }
  // Mọi câu trả lời là chữ do AI viết đầy đủ (không còn thẻ số liệu đi kèm); các nút và widget chọn bên dưới vẫn giữ
  return (
    <div className="w-full px-1 text-foreground">
      {turn.content && <ChatText text={turn.content} />}
      {turn.citations && turn.citations.length > 0 && (
        <ol className="mt-2 list-decimal space-y-0.5 pl-5 text-xs text-muted-foreground">
          {turn.citations.map((c, j) => (
            <li key={j}>
              {c.url ? (
                <a href={c.url} target="_blank" rel="noreferrer" className="underline hover:text-brand">
                  {c.title}
                </a>
              ) : (
                c.title
              )}
            </li>
          ))}
        </ol>
      )}
      {turn.disclaimer && <p className="mt-2 text-xs text-muted-foreground italic">{turn.disclaimer}</p>}
      {turn.widget?.type === "review" && <ReviewWidget data={turn.widget.data} />}
      {/* E: chỉ chọn được ở lượt cuối, giống nút trả lời nhanh */}
      {turn.widget?.type === "major" && <MajorWidget onPick={onReply} />}
      {turn.widget?.type === "select" && <SelectWidget data={turn.widget.data} onPick={onReply} />}
      {turn.widget?.type === "school" && <SchoolWidget data={turn.widget.data} onPick={onReply} />}
      {turn.widget?.type === "budget" && <BudgetWidget data={turn.widget.data} onPick={onReply} />}
      {turn.widget?.type === "scholarship" && <ScholarshipWidget data={turn.widget.data} onPick={onReply} />}
      {/* Mục 1: học sinh chọn điền bằng form. from=chat: điền xong form thì quay về chat trả lời câu đã hỏi */}
      {turn.widget?.type === "form" && (
        <Link
          href="/analysis?from=chat"
          className="mt-2.5 inline-flex h-8 items-center gap-1.5 rounded-full bg-brand px-3.5 text-[13px] font-semibold text-white transition-colors hover:bg-brand-ink"
        >
          <FileTextIcon className="size-4" />
          Mở form hồ sơ
        </Link>
      )}
      {/* Chat vừa chạy gợi ý trường: mở trang Gợi ý trường xem đủ danh sách (đọc lượt vừa lưu, không chạy lại) */}
      {turn.widget?.type === "recommend" && (
        // Mỗi lần bấm thêm ts khác nhau để trang tải lại kết quả mới nhất, kể cả khi đang đứng sẵn ở trang này
        <button
          type="button"
          onClick={() => router.push(`/recommendations?ts=${Date.now()}`)}
          className="mt-2.5 inline-flex h-8 items-center gap-1.5 rounded-full bg-brand px-3.5 text-[13px] font-semibold text-white transition-colors hover:bg-brand-ink"
        >
          <GraduationCapIcon className="size-4" />
          Xem đầy đủ danh sách trường
        </button>
      )}
      {/* What-if: mở trang Gợi ý trường với các giá trị vừa thử điền sẵn vào thanh kéo */}
      {turn.widget?.type === "simulate" && (
        <button
          type="button"
          onClick={() => showSimulation(turn.widget?.data, router.push)}
          className="mt-2.5 inline-flex h-8 items-center gap-1.5 rounded-full bg-brand px-3.5 text-[13px] font-semibold text-white transition-colors hover:bg-brand-ink"
        >
          <SlidersHorizontalIcon className="size-4" />
          Xem trên thanh kéo
        </button>
      )}
      {onReply && turn.quickReplies && turn.quickReplies.length > 0 && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          {turn.quickReplies.map((r) => (
            <button
              key={r.value}
              type="button"
              onClick={() => (r.href ? router.push(r.href) : r.action ? sendAction(r.value, r.action) : onReply(r.value))}
              className="group inline-flex min-h-9 cursor-pointer items-center gap-1 rounded-lg border border-line bg-surface px-3 py-1.5 text-left text-[13px] font-medium text-ink transition-colors hover:border-brand/40 hover:bg-brand-wash hover:text-brand active:translate-y-px"
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
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className={cn(
        "inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-md text-ink-2 transition-colors hover:bg-ground hover:text-ink active:scale-95",
        className
      )}
    >
      {children}
    </button>
  );
}

/** Nút nổi "Hỏi trợ lý" ở góc dưới phải, chỉ hiện khi khung đang đóng */
export function AssistantLauncher() {
  const { open, openPanel } = useAssistant();
  const visible = useAssistantVisible();
  if (!visible || open) return null;
  return (
    <button
      type="button"
      onClick={() => openPanel()}
      className="fixed right-5 bottom-5 z-30 inline-flex min-h-[50px] cursor-pointer items-center gap-2 rounded-full bg-brand px-5 text-sm font-semibold text-white transition-transform hover:-translate-y-0.5 print:hidden"
    >
      <MessageCircleIcon className="size-5" />
      Hỏi trợ lý
    </button>
  );
}
