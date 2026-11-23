import { chartSegments, TUITION_MISSING_TEXT, tuitionMissing, usd } from "@/lib/cost-format";
import type { SchoolCost } from "@/types/cost";

const COLORS: Record<string, string> = {
  tuition: "var(--viz-1, #2563eb)",
  living: "var(--viz-2, #0d9488)",
  insurance: "var(--viz-3, #d97706)",
  rest: "var(--viz-4, #9333ea)",
};

/** Thanh chi phí mỗi năm của từng trường, vạch đứng là ngân sách. Số liệu chính xác nằm ở bảng bên dưới. */
export function CostBar({ schools, scaleMax }: { schools: SchoolCost[]; scaleMax: number }) {
  const budget = schools.find((s) => s.budget?.annualBudgetUsd)?.budget?.annualBudgetUsd ?? null;
  const pct = (n: number) => `${Math.min((n / scaleMax) * 100, 100)}%`;
  return (
    <div className="space-y-3" role="img" aria-label="Biểu đồ chi phí mỗi năm của từng trường so với ngân sách. Số liệu chính xác ở bảng bên dưới.">
      {schools.map((s) => (
        <div key={s.code}>
          <div className="mb-1 flex justify-between text-xs text-muted-foreground">
            <span>{s.name}</span>
            <span>{s.annualUsd === null ? "Chưa có" : usd(s.annualUsd)}</span>
          </div>
          {tuitionMissing(s) ? (
            <p className="text-xs text-muted-foreground">{TUITION_MISSING_TEXT}</p>
          ) : (
            <div className="relative h-5 overflow-hidden rounded bg-muted">
              <div className="flex h-full">
                {chartSegments(s.lines).map((seg) => (
                  <div key={seg.key} style={{ width: pct(seg.value), background: COLORS[seg.key] }} title={`${seg.label}: ${usd(seg.value)}`} />
                ))}
              </div>
              {budget !== null && (
                <div className="absolute inset-y-0 w-0.5 bg-red-600" style={{ left: pct(budget) }} title={`Ngân sách: ${usd(budget)}`} />
              )}
            </div>
          )}
        </div>
      ))}
      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
        {chartSegments(schools[0]?.lines ?? []).map((seg) => (
          <span key={seg.key} className="inline-flex items-center gap-1">
            <span className="size-2.5 rounded-sm" style={{ background: COLORS[seg.key] }} />{seg.label}
          </span>
        ))}
        {budget !== null && <span className="inline-flex items-center gap-1"><span className="h-3 w-0.5 bg-red-600" />Ngân sách {usd(budget)}/năm</span>}
      </div>
    </div>
  );
}
