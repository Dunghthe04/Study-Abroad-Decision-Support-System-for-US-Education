"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  ArrowUpIcon,
  FileTextIcon,
  FlaskConicalIcon,
  GraduationCapIcon,
  Maximize2Icon,
  MessageCircleIcon,
  Minimize2Icon,
  PlusIcon,
  RotateCwIcon,
  SlidersHorizontalIcon,
  Undo2Icon,
  XIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { describeSimulation, resetSimulation, showSimulation } from "@/lib/simulation-link";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { useAssistant, type AssistantTurn } from "./AssistantProvider";
import type {
  ExplainBasis,
  ExplainCardData,
  SchoolCompareData,
  SchoolGoalResult,
  SchoolInfoData,
  SchoolInfoField,
  SchoolSearchData,
} from "@/types/recommendation";
import { BudgetWidget } from "./BudgetWidget";
import { ExplainCard } from "./ExplainCard";
import { SchoolCompareCard } from "./SchoolCompareCard";
import { SchoolSearchCard } from "./SchoolSearchCard";
import { SchoolInfoCard } from "./SchoolInfoCard";
import { GoalSeekCard } from "./GoalSeekCard";
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
          {/* Trang gợi ý trường đã có dải trạng thái bên dưới (hồ sơ thật hay giả định), không hiện thêm nhãn "Đang xem" nữa cho khỏi rối */}
          {page.simulation === undefined ? (
            <div className="mt-1 inline-flex max-w-full items-center gap-1.5 rounded-full bg-ground px-2.5 py-0.5 text-xs text-muted-foreground">
              <span className="size-1.5 shrink-0 rounded-full bg-ok" />
              <span className="truncate">Đang xem: {page.label}</span>
            </div>
          ) : (
            <div className="mt-0.5 text-xs text-muted-foreground">{page.label}</div>
          )}
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

      {/* Trang gợi ý trường: chat đang nói về hồ sơ thật hay về giả định học sinh đang kéo (undefined: trang khác, không có khái niệm này) */}
      {page.simulation === null && (
        <div className="flex items-center gap-2 border-b border-line bg-brand/5 px-4 py-2 text-xs font-medium text-brand">
          <span className="size-1.5 shrink-0 rounded-full bg-brand" />
          Đang xem theo hồ sơ thật của bạn
        </div>
      )}
      {page.simulation && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-warn/20 bg-warn-wash px-4 py-2.5 text-xs text-warn">
          <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5">
            <span className="inline-flex items-center gap-1 font-semibold">
              <FlaskConicalIcon className="size-3.5" />
              Đang thử giả định
            </span>
            {describeSimulation(page.simulation)
              .split(" · ")
              .filter(Boolean)
              .map((part) => (
                <span key={part} className="rounded-full bg-surface/70 px-2 py-0.5 font-medium">
                  {part}
                </span>
              ))}
          </div>
          <button
            type="button"
            onClick={resetSimulation}
            className="inline-flex shrink-0 cursor-pointer items-center gap-1 rounded-full border border-warn/30 bg-surface px-2.5 py-1 font-semibold transition-colors hover:bg-warn/10"
          >
            <Undo2Icon className="size-3.5" />
            Về hồ sơ thật
          </button>
        </div>
      )}

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
          <Turn
            key={i}
            turn={t}
            // D3: nút trả lời nhanh chỉ bấm được ở lượt cuối, khi không chờ trả lời và không bị chặn tuổi
            onReply={i === turns.length - 1 && !loading && !ageBlock ? send : undefined}
          />
        ))}

        {/* Advisor trả từng ý khi xong: hiện dần các câu đã có, câu cuối cùng sẽ thay thế khi xong cả lượt */}
        {loading && (streaming?.text || streaming?.widget) && (
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
      <div className="max-w-[90%] self-end rounded-[16px_4px_16px_16px] bg-[#E7EDF1] px-[15px] py-2.5 whitespace-pre-wrap text-foreground">
        {turn.content}
      </div>
    );
  }
  // Goal-seeking: thẻ kết quả (code dựng, có ngay) nằm TRÊN lời Gemini (tới sau), nên khi lời tới thẻ không bị đẩy xuống
  const goalCard = turn.widget?.type === "goal_seek" ? goalResultOf(turn.widget.data) : null;
  // Giải thích trường: thẻ do code dựng cũng nằm trên lời tư vấn của AI, cùng lý do như trên
  const explainCard = turn.widget?.type === "explain" ? explainCardOf(turn.widget.data) : null;
  // Thông tin một trường: thẻ số liệu thật nằm trên câu trả lời bằng chữ
  const infoCard = turn.widget?.type === "school_info" ? schoolInfoOf(turn.widget.data) : null;
  // So sánh từ hai trường: thẻ gồm bảng số liệu và khối "AI nhận xét" tách riêng
  const compareCard = turn.widget?.type === "school_compare" ? schoolCompareOf(turn.widget.data) : null;
  // Tìm trường theo điều kiện: 5 trường đầu và nút "Xem thêm"
  const searchCard = turn.widget?.type === "school_search" ? schoolSearchOf(turn.widget.data) : null;
  const hasCard = Boolean(goalCard || explainCard || infoCard || compareCard || searchCard);
  // Thẻ đã nói đủ số liệu: bỏ đoạn chữ trùng, chỉ hiện thẻ cho gọn; các đoạn khác của lượt (nếu có) vẫn hiện
  const covered = infoCard?.coveredText ?? compareCard?.coveredText ?? searchCard?.coveredText ?? null;
  const content = covered ? turn.content.replace(covered, "").trim() : turn.content;
  return (
    <div className="w-full text-foreground">
      {goalCard && <GoalSeekCard result={goalCard} />}
      {infoCard && <SchoolInfoCard info={infoCard.info} fields={infoCard.fields} remember={Boolean(onReply)} />}
      {compareCard && <SchoolCompareCard data={compareCard} remember={Boolean(onReply)} />}
      {searchCard && <SchoolSearchCard data={searchCard} remember={Boolean(onReply)} />}
      {explainCard && (
        <ExplainCard code={String(turn.widget?.data?.code ?? "")} card={explainCard} basis={explainBasisOf(turn.widget?.data)} />
      )}
      {content && <p className={cn("whitespace-pre-wrap", hasCard && "mt-2.5")}>{content}</p>}
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
              className="min-h-8 cursor-pointer rounded-full border border-brand/40 bg-brand-wash/40 px-3 py-1 text-[13px] text-foreground transition-colors hover:border-brand hover:bg-brand-wash"
            >
              {r.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// Kết quả goal-seeking trong widget (backend gửi kèm ở data.result). Không có hoặc thiếu vị trí thì không dựng thẻ
function explainBasisOf(data: Record<string, unknown> | null | undefined): ExplainBasis {
  const basis = data?.basis as ExplainBasis | undefined;
  return basis && (basis.kind === "real" || basis.kind === "simulated") ? basis : { kind: "real", label: null };
}

// Thẻ tìm trường trong widget. Thiếu hoặc sai khuôn thì không dựng thẻ, câu trả lời bằng chữ vẫn hiện
function schoolSearchOf(data: Record<string, unknown> | null | undefined): SchoolSearchData | null {
  const items = data?.items;
  if (!Array.isArray(items) || items.length === 0 || typeof data?.total !== "number") return null;
  if (!items.every((i) => i && typeof i.code === "string" && typeof i.name === "string")) return null;
  return {
    total: data.total,
    summary: typeof data.summary === "string" ? data.summary : "",
    sortLabel: typeof data.sortLabel === "string" ? data.sortLabel : "",
    visible: typeof data.visible === "number" && data.visible > 0 ? data.visible : 5,
    items: items as SchoolSearchData["items"],
    coveredText: typeof data.coveredText === "string" && data.coveredText.length > 0 ? data.coveredText : null,
  };
}

// Thẻ so sánh trong widget. Thiếu hoặc sai khuôn thì không dựng thẻ, câu trả lời bằng chữ vẫn hiện
function schoolCompareOf(data: Record<string, unknown> | null | undefined): SchoolCompareData | null {
  const schools = data?.schools;
  const rows = data?.rows;
  if (!Array.isArray(schools) || schools.length < 2 || !Array.isArray(rows) || rows.length === 0) return null;
  const valid = rows.every((r) => r && typeof r.label === "string" && Array.isArray(r.values) && r.values.length === schools.length);
  if (!valid) return null;
  return {
    schools: schools as SchoolCompareData["schools"],
    rows: rows as SchoolCompareData["rows"],
    aiNote: typeof data?.aiNote === "string" && data.aiNote.length > 0 ? data.aiNote : null,
    verify: data?.verify === true,
    coveredText: typeof data?.coveredText === "string" && data.coveredText.length > 0 ? data.coveredText : null,
  };
}

// Thẻ thông tin trường trong widget (backend gửi data.info và data.fields). Thiếu hoặc sai khuôn thì không dựng thẻ, câu trả lời bằng chữ vẫn hiện
function schoolInfoOf(
  data: Record<string, unknown> | null | undefined,
): { info: SchoolInfoData; fields: SchoolInfoField[]; coveredText: string | null } | null {
  const info = data?.info as SchoolInfoData | undefined;
  if (!info || typeof info.code !== "string" || typeof info.name !== "string" || !info.cost || !info.aid) return null;
  const fields = Array.isArray(data?.fields) ? (data.fields as unknown[]).filter((f): f is SchoolInfoField => typeof f === "string") : [];
  const coveredText = typeof data?.coveredText === "string" && data.coveredText.length > 0 ? data.coveredText : null;
  return { info, fields, coveredText };
}

function explainCardOf(data: Record<string, unknown> | null | undefined): ExplainCardData | null {
  const card = data?.card as ExplainCardData | undefined;
  return card && typeof card === "object" && Array.isArray(card.why) ? card : null;
}

function goalResultOf(data: Record<string, unknown> | null | undefined): SchoolGoalResult | null {
  const result = data?.result as SchoolGoalResult | undefined;
  return result && typeof result === "object" && result.position ? result : null;
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
