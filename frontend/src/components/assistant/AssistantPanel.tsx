"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ArrowUpIcon,
  Maximize2Icon,
  MessageCircleIcon,
  Minimize2Icon,
  PlusIcon,
  RotateCwIcon,
  XIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { useAssistant, type AssistantTurn } from "./AssistantProvider";

// Trang đăng nhập / đăng ký không hiện trợ lý
const HIDDEN_PREFIXES = ["/login", "/register", "/forgot-password", "/reset-password", "/verify-email"];

export function useAssistantVisible() {
  const pathname = usePathname();
  return !HIDDEN_PREFIXES.some((p) => pathname.startsWith(p));
}

/** Khung "Trợ lý USAS" cố định bên phải; màn hẹp thì phủ kín */
export function AssistantPanel() {
  const { open, wide, turns, loading, error, page, closePanel, toggleWide, newChat, send, retry } = useAssistant();
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
  }, [turns, loading, error, open]);

  // Mở khung thì đặt con trỏ vào ô nhập
  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Textarea tự cao theo nội dung, tối đa 120px
  useEffect(() => {
    const el = inputRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 120)}px`;
  }, [input]);

  if (!visible || !open) return null;

  const submit = () => {
    if (!input.trim() || loading) return;
    send(input);
    setInput("");
  };

  return (
    <aside
      aria-label="Trợ lý USAS"
      className="fixed inset-0 z-50 flex flex-col bg-surface min-[900px]:left-auto min-[900px]:w-(--assistant-w) min-[900px]:border-l min-[900px]:border-line min-[900px]:shadow-[-8px_0_24px_-12px_rgba(15,23,42,0.18)]"
    >
      {/* Header */}
      <div className="flex items-center gap-2 border-b border-line px-4 py-3">
        <div className="min-w-0 flex-1">
          <div className="font-heading text-base font-semibold text-foreground">Trợ lý USAS</div>
          <div className="mt-1 inline-flex max-w-full items-center gap-1.5 rounded-full bg-ground px-2.5 py-0.5 text-xs text-muted-foreground">
            <span className="size-1.5 shrink-0 rounded-full bg-ok" />
            <span className="truncate">Đang xem: {page.label}</span>
          </div>
        </div>
        <IconButton label="Cuộc trò chuyện mới" onClick={newChat}>
          <PlusIcon className="size-4.5" />
        </IconButton>
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

      {/* Tin nhắn */}
      <div ref={listRef} className="flex flex-1 flex-col gap-4 overflow-y-auto p-4 text-[14.5px] leading-relaxed">
        {turns.length === 0 && page.greeting && <p className="text-foreground">{page.greeting}</p>}
        {turns.length === 0 && !page.greeting && (
          <p className="text-foreground">
            Chào bạn, mình thấy bạn đang ở <b className="font-semibold">{page.label}</b>. Mình giải thích, kiểm tra
            những gì bạn đang điền, và trả lời câu hỏi về du học Mỹ.
          </p>
        )}

        {turns.map((t, i) => (
          <Turn key={i} turn={t} />
        ))}

        {loading && (
          <div className="flex items-center gap-1 py-1" role="status" aria-label="Đang trả lời">
            {[0, 1, 2].map((d) => (
              <span
                key={d}
                className="size-1.5 animate-pulse rounded-full bg-muted-foreground"
                style={{ animationDelay: `${d * 200}ms` }}
              />
            ))}
          </div>
        )}

        {error && (
          <div className="rounded-lg border border-warn/30 bg-warn-wash px-3 py-2.5 text-[13px] text-warn">
            <p>{error.message}</p>
            <div className="mt-1.5 flex gap-3 font-medium">
              {error.kind === "auth" ? (
                <Link href={`/login?redirect=${encodeURIComponent(pathname)}`} className="underline">
                  Đăng nhập để hỏi trợ lý
                </Link>
              ) : (
                <button type="button" onClick={retry} className="inline-flex cursor-pointer items-center gap-1 underline">
                  <RotateCwIcon className="size-3.5" />
                  Thử lại
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Chip gợi ý + ô nhập */}
      <div className="border-t border-line px-4 pt-3 pb-4">
        {ageBlock ? (
          // [Q11] Chat gửi dữ liệu cá nhân cho AI: học sinh dưới 16 cần phụ huynh đồng ý
          <p className="text-sm text-muted-foreground">
            {ageBlock.message}{" "}
            <Link href="/account" className="font-medium text-brand underline">
              Mời phụ huynh xác nhận
            </Link>
          </p>
        ) : (
          <>
          {page.suggestions.length > 0 && (
            <div className="-mx-4 mb-3 flex gap-2 overflow-x-auto px-4 pb-0.5 [scrollbar-width:none]">
              {page.suggestions.map((s) => (
                <button
                  key={s}
                  type="button"
                  disabled={loading}
                  onClick={() => send(s)}
                  className="h-8 shrink-0 cursor-pointer rounded-full border border-line bg-surface px-3 text-[13px] whitespace-nowrap text-foreground transition-colors hover:border-brand/50 hover:bg-brand-wash/50 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {s}
                </button>
              ))}
            </div>
          )}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              submit();
            }}
            className="flex items-end gap-2"
          >
            <textarea
              ref={inputRef}
              rows={1}
              value={input}
              maxLength={1000}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                  e.preventDefault();
                  submit();
                }
              }}
              placeholder="Hỏi về trang này, hoặc bất cứ điều gì…"
              aria-label="Câu hỏi cho trợ lý"
              className="min-h-[42px] flex-1 resize-none rounded-xl border border-line bg-surface px-3.5 py-2.5 text-sm outline-none transition-colors placeholder:text-muted-foreground focus:border-brand focus:ring-2 focus:ring-brand/20"
            />
            <button
              type="submit"
              aria-label="Gửi"
              disabled={loading || !input.trim()}
              className="flex size-[42px] shrink-0 cursor-pointer items-center justify-center rounded-xl bg-brand text-white transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ArrowUpIcon className="size-4.5" />
            </button>
          </form>
          </>
        )}
      </div>
    </aside>
  );
}

function Turn({ turn }: { turn: AssistantTurn }) {
  if (turn.role === "user") {
    return (
      <div className="max-w-[90%] self-end rounded-[16px_4px_16px_16px] bg-[#E7EDF1] px-[15px] py-2.5 whitespace-pre-wrap text-foreground">
        {turn.content}
      </div>
    );
  }
  return (
    <div className="w-full text-foreground">
      <p className="whitespace-pre-wrap">{turn.content}</p>
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
        "inline-flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-muted hover:text-foreground",
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
      className="fixed right-5 bottom-5 z-30 inline-flex min-h-[50px] cursor-pointer items-center gap-2 rounded-full bg-brand px-5 text-sm font-semibold text-white shadow-[0_8px_24px_-6px_rgba(14,95,122,0.5)] transition-transform hover:-translate-y-0.5 print:hidden"
    >
      <MessageCircleIcon className="size-5" />
      Hỏi trợ lý
    </button>
  );
}
