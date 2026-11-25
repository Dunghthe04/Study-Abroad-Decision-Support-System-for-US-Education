"use client";

// Thẻ giải thích một trường trong khung chat. Dựng từ số đã lưu và mã lý do của lần gợi ý (code, không có AI), nên hiện ngay;
// lời tư vấn do AI viết hiện sau, bên dưới thẻ. Chỉ nêu dữ kiện: nhóm, lý do xếp nhóm, các chỉ số chính.

import { TargetIcon } from "lucide-react";
import { formatUsd, groupLabel, groupVariant, whyText } from "@/lib/recommendation-labels";
import { Button } from "@/components/ui/button";
import { useAssistant } from "./AssistantProvider";
import { Badge } from "@/components/ui/badge";
import { BasisBadge } from "./BasisBadge";
import type { ExplainBasis, ExplainCardData, FitBreakdown, FitPartKey } from "@/types/recommendation";

const percent = (value: number | null | undefined) => (value == null ? "–" : `${Math.round(value)}%`);

// Trường đã An toàn thì không còn nhóm cao hơn để hướng tới, nên không có nút "Cần gì để vào?"
const GOAL_GROUPS = ["R", "RF", "M", "NA"];

export function ExplainCard({ code, card, basis }: { code: string; card: ExplainCardData; basis: ExplainBasis }) {
  const { sendAction, loading } = useAssistant();
  const control = card.control === "public" ? "Công lập" : card.control === "private" ? "Tư thục" : null;
  const place = [card.state, control].filter(Boolean).join(" · ");
  const badge =
    card.blk === "AID"
      ? { text: "Cần học bổng lớn", variant: groupVariant("AID") }
      : { text: groupLabel(card.g2), variant: groupVariant(card.g2) };

  return (
    <div className="mt-2.5 space-y-2.5 rounded-xl border border-brand/30 bg-brand-wash/50 p-3 text-sm">
      <BasisBadge kind={basis.kind} label={basis.label} />
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-[13px] font-bold text-brand">{card.name}</p>
          {place && <p className="text-xs text-ink-3">{place}</p>}
        </div>
        <Badge variant={badge.variant}>{badge.text}</Badge>
      </div>

      <div className="grid grid-cols-2 gap-x-3 gap-y-2 text-xs sm:grid-cols-4">
        <Stat label="Chi phí / năm" value={card.cost != null ? formatUsd(card.cost) : "Chưa có"} />
        <Stat label="Tỷ lệ nhận" value={card.acc != null ? percent(card.acc * 100) : "–"} />
        <Stat label="GPA của bạn" value={percent(card.pg)} hint="cao hơn sinh viên của trường" />
        <Stat label="SAT của bạn" value={percent(card.ps)} hint="cao hơn sinh viên của trường" />
      </div>

      {card.blk === "AID" && card.gap != null && (
        <p className="text-xs font-medium text-warn">Vượt ngân sách khoảng {formatUsd(card.gap)} mỗi năm.</p>
      )}

      {card.why.length > 0 && (
        <div className="space-y-0.5">
          <p className="text-xs font-bold text-brand">Vì sao xếp nhóm này</p>
          <ul className="space-y-0.5 text-xs text-ink-2">
            {card.why.map((code) => (
              <li key={code}>• {whyText(code)}</li>
            ))}
          </ul>
        </div>
      )}
      {card.fit && <FitBreakdownView fit={card.fit} />}
      {/* Đang giải thích theo giả định: một cú bấm để xem lại theo hồ sơ thật */}
      {basis.kind === "simulated" && (
        <Button
          variant="outline"
          size="sm"
          disabled={loading}
          onClick={() =>
            sendAction(`Vì sao ${card.name} xếp ở nhóm này theo hồ sơ thật?`, { type: "explain", schoolCode: code, basis: "real" })
          }
          className="mr-2 gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
        >
          Xem theo hồ sơ thật
        </Button>
      )}
      {GOAL_GROUPS.includes(card.g2) && (
        <Button
          variant="outline"
          size="sm"
          disabled={loading}
          onClick={() => sendAction(`Cần gì để vào ${card.name}?`, { type: "goal_seek", schoolCode: code })}
          className="gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
        >
          <TargetIcon className="size-3.5" />
          Cần gì để vào?
        </Button>
      )}
      <p className="text-xs text-ink-3">Tính từ hồ sơ thật của bạn, so với sinh viên đã trúng tuyển. Đây là mức tham chiếu, không phải cam kết đỗ.</p>
    </div>
  );
}

// UC-24: tên và lời diễn giải từng thành phần của điểm phù hợp, theo thứ tự đặc tả (khớp cách SchoolRanker tính)
const FIT_PART_INFO: Record<FitPartKey, { label: string; text: string }> = {
  position: {
    label: "Vừa tầm học thuật",
    text: "Cao nhất khi hồ sơ của bạn ở giữa nhóm sinh viên của trường; quá khó hay quá dễ đều thấp.",
  },
  budget: { label: "Ngân sách", text: "Càng rẻ hơn ngân sách càng cao; vượt ngân sách thì bằng 0." },
  quality: { label: "Chất lượng trường", text: "Tính từ thứ hạng của trường trong các bảng xếp hạng." },
  aid: {
    label: "Học bổng",
    text: "Phần trăm chi phí cả năm mà học bổng trung bình dành cho du học sinh chi trả.",
  },
  english: {
    label: "Tiếng Anh",
    text: "Đạt mức tối thiểu của trường thì được điểm tối đa; mỗi band thiếu mất 1 điểm.",
  },
};

const formatWeight = (weight: number) => weight.toFixed(2).replace(".", ",");

// Điểm phù hợp của trường: năm thành phần kèm điểm, trọng số đã dùng và lời diễn giải. Chỉ dùng để sắp xếp trong cùng nhóm
function FitBreakdownView({ fit }: { fit: FitBreakdown }) {
  return (
    <div className="space-y-1.5">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="text-xs font-bold text-brand">Điểm phù hợp</p>
        <p className="text-xs font-semibold text-foreground">
          {fit.total == null ? "Chưa tính được" : `${Math.round(fit.total * 100)} / 100`}
        </p>
      </div>
      <ul className="space-y-1.5">
        {fit.parts.map((part) => {
          const info = FIT_PART_INFO[part.key];
          return (
            <li key={part.key} className="text-xs">
              <div className="flex items-baseline justify-between gap-2">
                <span className="font-medium text-ink-2">{info.label}</span>
                <span className="tabular shrink-0 text-ink-2">
                  {part.score == null ? "Chưa có dữ liệu" : `${Math.round(part.score * 100)} / 100`}
                  <span className="text-ink-3"> · trọng số {formatWeight(part.weight)}</span>
                </span>
              </div>
              <p className="text-ink-3">
                {info.text}
                {part.score == null ? " Thiếu dữ liệu nên thành phần này bị bỏ khỏi tổng điểm." : ""}
              </p>
            </li>
          );
        })}
      </ul>
      <p className="text-xs text-ink-3">
        {fit.priorityAdjusted ? "Trọng số đã đổi theo các nút ưu tiên của bạn. " : ""}
        Điểm phù hợp chỉ dùng để sắp xếp các trường trong cùng một nhóm, không đổi nhóm của trường.
      </p>
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="min-w-0">
      <p className="text-sm font-bold text-foreground">{value}</p>
      <p className="text-xs font-medium text-ink-2">{label}</p>
      {hint && <p className="text-xs text-ink-3">{hint}</p>}
    </div>
  );
}
