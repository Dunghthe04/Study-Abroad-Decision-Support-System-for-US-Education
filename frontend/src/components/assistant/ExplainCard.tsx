"use client";

// Thẻ giải thích một trường trong khung chat. Dựng từ số đã lưu và mã lý do của lần gợi ý (code, không có AI), nên hiện ngay;
// lời tư vấn do AI viết hiện sau, bên dưới thẻ. Chỉ nêu dữ kiện: nhóm, lý do xếp nhóm, các chỉ số chính.

import { TargetIcon } from "lucide-react";
import { formatUsd, groupLabel, groupVariant, whyText } from "@/lib/recommendation-labels";
import { Button } from "@/components/ui/button";
import { useAssistant } from "./AssistantProvider";
import { Badge } from "@/components/ui/badge";
import { BasisBadge } from "./BasisBadge";
import type { ExplainBasis, ExplainCardData } from "@/types/recommendation";

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
          {place && <p className="text-[11px] text-ink-3">{place}</p>}
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
          <p className="text-[11px] font-bold tracking-wide text-brand uppercase">Vì sao xếp nhóm này</p>
          <ul className="space-y-0.5 text-xs text-ink-2">
            {card.why.map((code) => (
              <li key={code}>• {whyText(code)}</li>
            ))}
          </ul>
        </div>
      )}
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
      <p className="text-[11px] text-ink-3">Tính từ hồ sơ thật của bạn, so với sinh viên đã trúng tuyển. Đây là mức tham chiếu, không phải cam kết đỗ.</p>
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="min-w-0">
      <p className="text-sm font-bold text-foreground">{value}</p>
      <p className="text-[11px] font-medium text-ink-2">{label}</p>
      {hint && <p className="text-[10px] text-ink-3">{hint}</p>}
    </div>
  );
}
