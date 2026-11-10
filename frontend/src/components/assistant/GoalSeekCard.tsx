"use client";

// Thẻ "Cần gì để vào trường này" trong khung chat. Dựng từ kết quả code đã tính (không có AI), nên hiện đúng và ngay.
// Luôn mở đầu bằng vị trí của trường với hồ sơ thật, rồi từng mức đích: "Sẽ thành Vừa sức nếu...", "Sẽ thành An toàn hơn nếu...".

import {
  DIFFICULTY_LABELS,
  FACTOR_LABELS,
  GOAL_REASON_TEXT,
  formatUsd,
  goalValue,
  groupLabel,
  groupVariant,
  formatPct,
} from "@/lib/recommendation-labels";
import { MessageSquareTextIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAssistant } from "./AssistantProvider";
import { BasisBadge } from "./BasisBadge";
import type {
  GoalAlternative,
  GoalBudget,
  GoalEnglish,
  GoalPlan,
  GoalPosition,
  GoalSchoolStats,
  GoalTargetResult,
  SchoolGoalResult,
} from "@/types/recommendation";

const FILTER_TEXT: Record<string, string> = {
  MAJOR: "ngành bạn chọn",
  STATE: "bang bạn chọn",
  CONTROL: "loại trường (công lập hoặc tư thục) bạn chọn",
};

// "IELTS 7,5, TOEFL 100 hoặc Duolingo 130"
function englishNeed(english: GoalEnglish | null | undefined): string | null {
  if (!english) return null;
  const parts = [
    english.minIelts != null ? `IELTS ${String(english.minIelts).replace(".", ",")}` : null,
    english.minToefl != null ? `TOEFL ${english.minToefl}` : null,
    english.minDuolingo != null ? `Duolingo ${english.minDuolingo}` : null,
  ].filter(Boolean) as string[];
  if (parts.length === 0) return null;
  return parts.length === 1 ? parts[0] : `${parts.slice(0, -1).join(", ")} hoặc ${parts[parts.length - 1]}`;
}

function budgetText(budget: GoalBudget): string {
  const cost = budget.costUsd != null ? formatUsd(budget.costUsd) : null;
  const min = budget.minBudgetUsd != null ? formatUsd(budget.minBudgetUsd) : null;
  switch (budget.status) {
    case "ok":
      return `Chi phí khoảng ${cost} mỗi năm, nằm trong ngân sách của bạn.`;
    case "need_aid":
      return `Chi phí khoảng ${cost} mỗi năm, vượt ngân sách khoảng ${formatUsd(budget.shortfallUsd)}. Cần ngân sách từ ${min} mỗi năm hoặc học bổng lớn.`;
    case "over_budget":
      return `Chi phí khoảng ${cost} mỗi năm, vượt ngân sách và trường không có học bổng phù hợp. Cần ngân sách từ ${min} mỗi năm.`;
    case "no_budget":
      return `Chi phí khoảng ${cost} mỗi năm. Hồ sơ của bạn chưa đặt trần ngân sách.`;
    default:
      return "Trường chưa có số liệu chi phí.";
  }
}

// Câu mở đầu: trường đang ở đâu với hồ sơ thật
function positionText(name: string, position: GoalPosition, english: GoalEnglish | null | undefined, budget: GoalBudget): string {
  if (position.status === "in_list") {
    if (position.needAid) return `${name} vượt ngân sách của bạn, chỉ đi được nếu có học bổng lớn.`;
    if (position.group === "NA") return `${name} chưa đủ số liệu học thuật để xếp nhóm với hồ sơ của bạn.`;
    return `${name} đang ở nhóm ${groupLabel(position.group)} với hồ sơ của bạn.`;
  }
  switch (position.excludeReason) {
    case "ENGLISH":
    case "ENGLISH_NO_CERT": {
      const need = englishNeed(english);
      const head =
        position.excludeReason === "ENGLISH_NO_CERT"
          ? `${name} chưa nằm trong danh sách vì bạn chưa có chứng chỉ tiếng Anh.`
          : `${name} chưa nằm trong danh sách vì điểm tiếng Anh của bạn chưa đủ.`;
      return need ? `${head} Cần đạt ${need} (một bài là đủ).` : head;
    }
    case "BUDGET":
      return `${name} chưa nằm trong danh sách vì vượt ngân sách và trường không có học bổng phù hợp.${
        budget.minBudgetUsd != null ? ` Cần ngân sách từ ${formatUsd(budget.minBudgetUsd)} mỗi năm.` : ""
      }`;
    default:
      return `${name} chưa nằm trong danh sách vì không khớp ${FILTER_TEXT[position.excludeReason ?? ""] ?? "bộ lọc"}. Muốn xét trường này, bạn cần đổi trong hồ sơ.`;
  }
}

function planText(plan: GoalPlan): string {
  return plan.changes
    .map((c) => `${FACTOR_LABELS[c.factor] ?? c.factor} ${goalValue(c.factor, c.from)} → ${goalValue(c.factor, c.to)}`)
    .join(" và ");
}

const difficulty = (plan: GoalPlan) => DIFFICULTY_LABELS[plan.difficulty] ?? plan.difficulty;

const pct = formatPct;

// Lý do xếp nhóm, nói rõ con số và mốc khi có số liệu tuyển sinh ("nhận 24% hồ sơ, dưới mốc 50%") thay vì "ít hơn hẳn"
function reasonLine(code: string, stats: GoalSchoolStats | null | undefined): string {
  const rate = stats?.acceptRate ?? null;
  if (stats && rate != null) {
    if (code === "LOW_ACCEPT_RATE")
      return `Trường chỉ nhận ${pct(rate)} hồ sơ, dưới mốc ${pct(stats.alwaysReachRate)}, nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm.`;
    if (code === "CAPPED_SELECTIVE")
      return `Trường nhận ${pct(rate)} hồ sơ, dưới mốc ${pct(stats.safetyMinRate)}, nên chỉ tối đa là Vừa sức.`;
    if (code === "INTL_DOWNGRADE" && stats.intlAcceptRate != null && stats.intlRatio != null)
      return `Trường nhận du học sinh ${pct(stats.intlAcceptRate)} trong khi tỷ lệ nhận chung là ${pct(rate)}, chỉ bằng ${pct(stats.intlRatio)} mức chung và dưới mốc ${pct(stats.intlRatioCut)}, nên bị hạ một nhóm.`;
  }
  return GOAL_REASON_TEXT[code] ?? code;
}

// Mọi mức đích đều bị chặn giống nhau (cùng nhóm cao nhất, cùng lý do): gộp thành một khối thay vì lặp lại
function sameBlock(targets: GoalTargetResult[]): GoalTargetResult | null {
  if (targets.length < 2 || targets.some((t) => t.outcome !== "blocked")) return null;
  const [first, ...rest] = targets;
  const same = rest.every((t) => t.best === first.best && t.reasons.join() === first.reasons.join());
  return same ? first : null;
}

function BlockedTogether({ target, stats }: { target: GoalTargetResult; stats: GoalSchoolStats | null | undefined }) {
  return (
    <div className="space-y-1">
      <p className="text-xs font-semibold text-foreground">
        Trường sẽ vẫn ở nhóm {groupLabel(target.best)} dù bạn cải thiện hồ sơ, vì:
      </p>
      <div className="space-y-0.5 text-xs text-ink-2">
        {target.reasons.map((r) => (
          <p key={r}>• {reasonLine(r, stats)}</p>
        ))}
        <p>• Bạn có thể nộp để thử sức nhưng cơ hội thấp, nên giữ thêm các trường Vừa sức và An toàn hơn.</p>
      </div>
    </div>
  );
}

// Một mức đích: "Sẽ thành Vừa sức nếu: ..." hoặc lý do chưa đạt được
function TargetBlock({ target, stats }: { target: GoalTargetResult; stats: GoalSchoolStats | null | undefined }) {
  const label = groupLabel(target.target);
  const combined = target.cheapest && target.cheapest.changes.length > 1 ? target.cheapest : null;

  return (
    <div className="space-y-1">
      <p className="text-xs font-semibold text-foreground">Sẽ thành {label} nếu:</p>
      {target.outcome === "reachable" && (
        <ul className="space-y-0.5 text-xs text-ink-2">
          {target.alone.map((plan) => (
            <li key={plan.changes[0].factor}>
              • {planText(plan)} <span className="text-ink-3">({difficulty(plan)})</span>
            </li>
          ))}
          {target.alone.length === 0 && <li>• Một yếu tố riêng chưa đủ, cần kết hợp nhiều yếu tố.</li>}
          {combined && (
            <li>
              • Kết hợp ít công sức nhất: {planText(combined)} <span className="text-ink-3">({difficulty(combined)})</span>
            </li>
          )}
          {target.recommended && (
            <li>
              • Nên đặt mục tiêu (có đệm): {planText(target.recommended)}{" "}
              <span className="text-ink-3">({difficulty(target.recommended)})</span>
            </li>
          )}
        </ul>
      )}
      {target.outcome === "blocked" && (
        <div className="space-y-0.5 text-xs text-ink-2">
          {target.reasons.map((r) => (
            <p key={r}>• {reasonLine(r, stats)}</p>
          ))}
          <p>• Nhóm cao nhất bạn đạt được ở trường này là {groupLabel(target.best)}.</p>
        </div>
      )}
      {target.outcome === "no_data" && (
        <p className="text-xs text-ink-2">
          • {target.reasons[0] ? reasonLine(target.reasons[0], stats) : "Trường chưa đủ dữ liệu."} Hệ thống chưa tính được mức cần đạt.
        </p>
      )}
    </div>
  );
}

function Alternatives({ items, group }: { items: GoalAlternative[]; group: string }) {
  return (
    <p className="text-xs text-ink-2">
      Trường tương tự đang ở nhóm {groupLabel(group)} với bạn: {items.map((a) => a.name).join(", ")}.
    </p>
  );
}

export function GoalSeekCard({ result }: { result: SchoolGoalResult }) {
  const { sendAction, loading } = useAssistant();
  const position = result.position;
  if (!position) return null; // bản backend cũ chưa trả vị trí: để lời viết bên trên tự nói

  const badge =
    position.status === "excluded"
      ? { text: "Chưa trong danh sách", variant: "neutral" as const }
      : position.needAid
        ? { text: "Cần học bổng lớn", variant: groupVariant("AID") }
        : { text: groupLabel(position.group), variant: groupVariant(position.group) };
  const targets = result.targets ?? [];
  const merged = sameBlock(targets);
  // Trường bị loại: nhóm "nếu đủ điều kiện"; trường đã An toàn: không còn mức đích
  const ifEligible = position.status === "excluded" && position.group !== "NA";

  return (
    <div className="mt-2.5 space-y-2.5 rounded-xl border border-brand/30 bg-brand-wash/50 p-3 text-sm">
      {/* Goal-seeking luôn tính từ hồ sơ thật, kể cả khi trang đang xem giả định */}
      <BasisBadge kind="real" />
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] font-bold text-brand">Cần gì để vào {result.name}?</p>
        <Badge variant={badge.variant}>{badge.text}</Badge>
      </div>

      <p className="text-xs text-foreground">{positionText(result.name, position, result.english, result.budget)}</p>
      {ifEligible && (
        <p className="text-xs text-ink-2">Khi đủ điều kiện này, trường sẽ ở nhóm {groupLabel(position.group)} với hồ sơ của bạn.</p>
      )}
      <p className="text-xs text-ink-2">{budgetText(result.budget)}</p>

      {position.group === "S" && position.status === "in_list" && (
        <p className="text-xs text-ink-2">Trường đã nằm trong nhóm An toàn hơn, bạn không cần thay đổi gì thêm.</p>
      )}
      {merged ? (
        <BlockedTogether target={merged} stats={result.stats} />
      ) : (
        targets.map((t) => <TargetBlock key={t.target} target={t} stats={result.stats} />)
      )}

      {result.alternatives.length > 0 && (
        <Alternatives items={result.alternatives} group={targets[0]?.target ?? result.target} />
      )}
      {/* Giải thích dùng lần gợi ý đã lưu nên chỉ hỏi được trường đang trong danh sách */}
      {position.status === "in_list" && (
        <Button
          variant="outline"
          size="sm"
          disabled={loading}
          onClick={() => sendAction(`Vì sao ${result.name} xếp ở nhóm này?`, { type: "explain", schoolCode: result.code })}
          className="gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
        >
          <MessageSquareTextIcon className="size-3.5" />
          Vì sao xếp nhóm này?
        </Button>
      )}
      <p className="text-[11px] text-ink-3">
        Tính từ hồ sơ thật của bạn, so với sinh viên đã trúng tuyển. Đây là mức tham chiếu, không phải cam kết đỗ.
      </p>
    </div>
  );
}
