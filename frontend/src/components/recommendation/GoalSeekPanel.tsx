"use client";

// Khung "Cần gì để vào trường này": tài chính trước, rồi mức cần đạt của từng yếu tố, nút áp dụng vào thanh kéo.
// Chỉ hiển thị số backend đã tính (GoalSeeker), không tính lại gì.

import { PinIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import {
  DIFFICULTY_LABELS,
  FACTOR_LABELS,
  GOAL_REASON_TEXT,
  GOAL_TARGET_OPTIONS,
  formatUsd,
  goalValue,
  groupLabel,
} from "@/lib/recommendation-labels";
import type { GoalSeek } from "@/lib/use-goal-seek";
import type { GoalAlternative, GoalBudget, GoalEnglish, GoalPlan, GoalTarget, SchoolGoalResult } from "@/types/recommendation";

interface GoalSeekPanelProps {
  goal: GoalSeek;
  schoolName: string; // tên trường đang xét, hiện ngay cả khi kết quả chưa về
  simulating: boolean; // đang xem hồ sơ giả định: nói rõ kết quả vẫn tính từ hồ sơ thật
  pinned?: boolean; // khung ghim ở đầu danh sách (không gắn dưới thẻ trường)
  onRestore?: () => void; // có khi các thanh giả định vừa được đưa về hồ sơ thật để tính: khôi phục lại giả định trước
  onApply: (plan: GoalPlan) => void; // đặt các giá trị của phương án vào thanh kéo
}

const TARGET_BY_CODE: Record<string, GoalTarget> = { R: "reach", M: "match", S: "safety" };

export function GoalSeekPanel({ goal, schoolName, simulating, pinned = false, onRestore, onApply }: GoalSeekPanelProps) {
  const result = goal.result;
  const selected: GoalTarget = goal.target ?? (result ? (TARGET_BY_CODE[result.target] ?? "match") : "match");

  return (
    <section className="space-y-4 rounded-2xl border border-brand/30 bg-brand-wash p-5" aria-live="polite">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {pinned && <PinIcon className="size-4 text-brand" aria-label="Đã ghim ở đầu danh sách" />}
          <p className="text-sm font-bold text-brand">Cần gì để vào {schoolName}?</p>
          <label className="flex items-center gap-1.5 text-xs text-ink-2">
            nhóm muốn đạt
            <select
              value={selected}
              onChange={(e) => goal.setTarget(e.target.value as GoalTarget)}
              className="h-7 rounded-md border border-line bg-surface px-2 text-xs font-semibold text-foreground"
            >
              {GOAL_TARGET_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </label>
          {goal.loading && <Spinner className="size-4 text-brand" />}
        </div>
        <Button variant="outline" size="sm" onClick={goal.close} aria-label="Đóng">
          <XIcon className="size-3.5" />
        </Button>
      </div>

      {onRestore && (
        <p className="flex flex-wrap items-center gap-x-2 text-xs text-ink-2">
          Mình đã đưa các thanh giả định về <strong>hồ sơ thật</strong> để tính mức cần đạt.
          <button type="button" onClick={onRestore} className="font-semibold text-brand underline-offset-4 hover:underline">
            Khôi phục giả định trước
          </button>
        </p>
      )}

      {simulating && (
        <p className="text-xs text-ink-2">
          Bạn đang xem hồ sơ giả định, còn kết quả dưới đây vẫn tính từ <strong>hồ sơ thật</strong> của bạn.
        </p>
      )}

      {goal.error && <p className="text-xs text-risk">{goal.error}</p>}

      {result && (
        <div className="space-y-4 text-sm">
          <BudgetLine budget={result.budget} />
          {result.filterReason && <FilterNote reason={result.filterReason} english={result.english ?? null} />}
          <Academic result={result} onApply={onApply} />
          {result.alternatives.length > 0 && <Alternatives items={result.alternatives} />}
          <p className="text-[11px] text-ink-3">
            Tính từ hồ sơ thật của bạn, so với sinh viên đã trúng tuyển. Đây là mức tham chiếu, không phải cam kết đỗ.
          </p>
        </div>
      )}
    </section>
  );
}

// ---------------------------------------------------------------- tài chính

function BudgetLine({ budget }: { budget: GoalBudget }) {
  const cost = budget.costUsd != null ? formatUsd(budget.costUsd) : null;
  const min = budget.minBudgetUsd != null ? formatUsd(budget.minBudgetUsd) : null;
  const aid = budget.intlAidAvgUsd != null ? ` Học bổng quốc tế trung bình của trường khoảng ${formatUsd(budget.intlAidAvgUsd)} mỗi năm.` : "";

  let text: string;
  switch (budget.status) {
    case "ok":
      text = `Chi phí khoảng ${cost} mỗi năm, nằm trong ngân sách của bạn.`;
      break;
    case "need_aid":
      text = `Chi phí khoảng ${cost} mỗi năm, vượt ngân sách khoảng ${formatUsd(budget.shortfallUsd)}. Cần ngân sách từ ${min} mỗi năm hoặc học bổng lớn.${aid}`;
      break;
    case "over_budget":
      text = `Chi phí khoảng ${cost} mỗi năm, vượt ngân sách và trường không có học bổng phù hợp nên không nằm trong danh sách của bạn. Cần ngân sách từ ${min} mỗi năm.`;
      break;
    case "no_budget":
      text = `Hồ sơ của bạn không đặt trần ngân sách mỗi năm. Chi phí khoảng ${cost} mỗi năm, cần ngân sách từ ${min} mỗi năm.`;
      break;
    default:
      text = "Trường chưa có số liệu chi phí.";
  }
  const warn = budget.status === "need_aid" || budget.status === "over_budget";

  return (
    <div className="space-y-0.5">
      <p className="text-[11px] font-bold tracking-wide text-brand uppercase">Tài chính</p>
      <p className={warn ? "text-foreground" : "text-ink-2"}>{text}</p>
    </div>
  );
}

const FILTER_TEXT: Record<string, string> = {
  MAJOR: "ngành bạn chọn",
  STATE: "bang bạn chọn",
  CONTROL: "loại trường (công lập hoặc tư thục) bạn chọn",
};

// Mức tiếng Anh tối thiểu của trường, mỗi bài thi một mức ("IELTS 6,5, TOEFL 80 hoặc Duolingo 115")
function englishNeed(english: GoalEnglish | null): string | null {
  if (!english) return null;
  const parts = [
    english.minIelts != null ? `IELTS ${String(english.minIelts).replace(".", ",")}` : null,
    english.minToefl != null ? `TOEFL ${english.minToefl}` : null,
    english.minDuolingo != null ? `Duolingo ${english.minDuolingo}` : null,
  ].filter(Boolean) as string[];
  if (parts.length === 0) return null;
  return parts.length === 1 ? parts[0] : `${parts.slice(0, -1).join(", ")} hoặc ${parts[parts.length - 1]}`;
}

function FilterNote({ reason, english }: { reason: string; english: GoalEnglish | null }) {
  if (reason === "ENGLISH" || reason === "ENGLISH_NO_CERT") {
    const need = englishNeed(english);
    return (
      <div className="space-y-0.5 text-xs text-warn">
        <p className="text-[11px] font-bold tracking-wide text-brand uppercase">Tiếng Anh</p>
        <p>
          {reason === "ENGLISH_NO_CERT"
            ? "Bạn chưa có chứng chỉ tiếng Anh nên trường chưa nằm trong danh sách."
            : "Điểm tiếng Anh của bạn chưa đủ nên trường chưa nằm trong danh sách."}
          {need && ` Cần đạt ${need} (một bài là đủ).`}
        </p>
        <p className="text-ink-2">Phần học thuật dưới đây tính khi trường đã vào danh sách, tức là khi bạn đủ tiếng Anh.</p>
      </div>
    );
  }
  return (
    <p className="text-xs text-warn">
      Trường này không nằm trong danh sách của bạn vì không khớp {FILTER_TEXT[reason] ?? "bộ lọc"}.
    </p>
  );
}

// ---------------------------------------------------------------- học thuật

function Academic({ result, onApply }: { result: SchoolGoalResult; onApply: (plan: GoalPlan) => void }) {
  const target = groupLabel(result.target);

  return (
    <div className="space-y-2">
      <p className="text-[11px] font-bold tracking-wide text-brand uppercase">Học thuật</p>

      {result.outcome === "already_reached" && (
        <p className="text-ink-2">Bạn đã ở nhóm {groupLabel(result.current)}, không cần thay đổi gì để đạt {target}.</p>
      )}

      {result.outcome === "blocked" && (
        <div className="space-y-1 text-ink-2">
          {result.reasons.map((r) => (
            <p key={r}>{GOAL_REASON_TEXT[r] ?? r}</p>
          ))}
          <p>
            Nhóm cao nhất bạn có thể đạt ở trường này là <strong className="text-foreground">{groupLabel(result.best)}</strong>, nên nên xem đây là
            mục tiêu cao và giữ thêm các trường khác ở nhóm Vừa sức.
          </p>
        </div>
      )}

      {result.outcome === "no_data" && (
        <p className="text-ink-2">
          {GOAL_REASON_TEXT[result.reasons[0]] ?? "Trường chưa đủ dữ liệu để tính."} Hệ thống chưa tính được mức cần đạt.
        </p>
      )}

      {result.outcome === "reachable" && <Plans result={result} onApply={onApply} />}
    </div>
  );
}

function Plans({ result, onApply }: { result: SchoolGoalResult; onApply: (plan: GoalPlan) => void }) {
  const { cheapest, recommended } = result;
  // Bảng yếu tố đứng riêng; phương án kết hợp chỉ hiện khi khác các yếu tố riêng
  const combined = cheapest && cheapest.changes.length > 1 ? cheapest : null;
  const best = recommended ?? cheapest;

  return (
    <div className="space-y-3">
      {result.reasons.includes("NEED_TEST") && (
        <p className="text-ink-2">{GOAL_REASON_TEXT.NEED_TEST} Bạn cần có điểm thi trước.</p>
      )}

      {result.alone.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[420px] text-left text-xs">
            <thead className="text-ink-3">
              <tr>
                <th className="pb-1 font-medium">Chỉ thay đổi</th>
                <th className="pb-1 font-medium">Hiện tại</th>
                <th className="pb-1 font-medium">Cần đạt</th>
                <th className="pb-1 font-medium">Còn thiếu</th>
                <th className="pb-1 font-medium">Mức độ</th>
              </tr>
            </thead>
            <tbody>
              {result.alone.map((plan) => (
                <PlanRow key={plan.changes[0].factor} plan={plan} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result.alone.length === 0 && (
        <p className="text-ink-2">Một yếu tố riêng chưa đủ, cần kết hợp nhiều yếu tố.</p>
      )}

      {combined && (
        <p className="text-foreground">
          <strong>Kết hợp ít công sức nhất:</strong> {describe(combined)} <Difficulty value={combined.difficulty} />
        </p>
      )}

      {recommended && (
        <p className="text-foreground">
          <strong>Nên đặt mục tiêu:</strong> {describe(recommended)} <Difficulty value={recommended.difficulty} />
          <span className="block text-[11px] text-ink-3">Cao hơn mức tối thiểu một khoảng đệm, vì số liệu của trường và hồ sơ tự khai có sai số.</span>
        </p>
      )}

      {result.noEffect.length > 0 && (
        <p className="text-[11px] text-ink-3">
          Ở trường này {result.noEffect.map((f) => FACTOR_LABELS[f] ?? f).join(", ")} không làm đổi nhóm.
        </p>
      )}

      {best && (
        <div className="flex flex-wrap gap-2">
          <Button size="sm" onClick={() => onApply(best)}>
            Áp dụng vào thanh kéo
          </Button>
          {recommended && cheapest && (
            <Button size="sm" variant="outline" onClick={() => onApply(cheapest)}>
              Chỉ mức tối thiểu
            </Button>
          )}
        </div>
      )}
    </div>
  );
}

function PlanRow({ plan }: { plan: GoalPlan }) {
  const change = plan.changes[0];
  const missing = change.from != null ? change.to - change.from : null;
  return (
    <tr className="border-t border-line/70">
      <td className="py-1.5 font-medium text-foreground">{FACTOR_LABELS[change.factor] ?? change.factor}</td>
      <td className="py-1.5 text-ink-2">{goalValue(change.factor, change.from)}</td>
      <td className="py-1.5 font-semibold text-foreground">{goalValue(change.factor, change.to)}</td>
      <td className="py-1.5 text-ink-2">{missing != null ? `+${goalValue(change.factor, missing)}` : "–"}</td>
      <td className="py-1.5">
        <Difficulty value={plan.difficulty} />
      </td>
    </tr>
  );
}

function describe(plan: GoalPlan): string {
  return plan.changes
    .map((c) => `${FACTOR_LABELS[c.factor] ?? c.factor} ${goalValue(c.factor, c.from)} → ${goalValue(c.factor, c.to)}`)
    .join(" và ");
}

function Difficulty({ value }: { value: GoalPlan["difficulty"] }) {
  const tone = value === "easy" ? "text-ok" : value === "hard" ? "text-warn" : "text-risk";
  return <span className={`text-xs font-semibold ${tone}`}>({DIFFICULTY_LABELS[value]})</span>;
}

// ---------------------------------------------------------------- trường thay thế

function Alternatives({ items }: { items: GoalAlternative[] }) {
  return (
    <div className="space-y-1">
      <p className="text-[11px] font-bold tracking-wide text-brand uppercase">Trường tương tự đang ở nhóm này với bạn</p>
      <ul className="space-y-0.5 text-ink-2">
        {items.map((a) => (
          <li key={a.code}>
            <span className="font-medium text-foreground">{a.name}</span>
            {[a.state, a.control === "public" ? "Công lập" : a.control === "private" ? "Tư thục" : null]
              .filter(Boolean)
              .map((part) => ` · ${part}`)
              .join("")}
            {a.costUsd != null && ` · ${formatUsd(a.costUsd)} mỗi năm`}
          </li>
        ))}
      </ul>
    </div>
  );
}
