import { MissingValue } from "@/components/shared/missing-value";
import { chartData, TUITION_MISSING_TEXT, tuitionMissing, usd } from "@/lib/cost-format";
import type { SchoolCost } from "@/types/cost";

const COLORS: Record<string, string> = {
  tuition: "var(--viz-1, #2563eb)",
  living: "var(--viz-2, #0d9488)",
  insurance: "var(--viz-3, #d97706)",
  rest: "var(--viz-4, #9333ea)",
};

const LEGEND_ITEMS = [
  { key: "tuition", label: "Học phí" },
  { key: "living", label: "Sinh hoạt" },
  { key: "insurance", label: "Bảo hiểm y tế" },
  { key: "rest", label: "Phí và khác" },
];

/** Thanh chi phí mỗi năm của từng trường, vạch đứng là ngân sách. Khoản thiếu tách riêng, không vẽ $0 (UC-26). */
export function CostBar({ schools, scaleMax }: { schools: SchoolCost[]; scaleMax: number }) {
  const budget = schools.find((s) => s.budget?.annualBudgetUsd)?.budget?.annualBudgetUsd ?? null;
  const pct = (n: number) => `${Math.min((n / scaleMax) * 100, 100)}%`;
  const anyMissing = schools.some((s) => !tuitionMissing(s) && chartData(s.lines).missingLabels.length > 0);

  return (
    <div className="space-y-3" role="img" aria-label="Biểu đồ chi phí mỗi năm của từng trường so với ngân sách. Số liệu chính xác ở bảng bên dưới.">
      {schools.map((s) => {
        const { segments, missingLabels } = chartData(s.lines);
        return (
          <div key={s.code}>
            <div className="mb-1 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
              <span className="font-medium text-foreground">{s.name}</span>
              <span className="inline-flex items-center gap-2">
                {!tuitionMissing(s) && !s.isComplete && (
                  <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-900 dark:bg-amber-950/60 dark:text-amber-200">
                    Chưa đủ khoản
                  </span>
                )}
                <span>{s.annualUsd === null ? <MissingValue /> : usd(s.annualUsd)}</span>
              </span>
            </div>
            {tuitionMissing(s) ? (
              <p className="text-xs text-muted-foreground">{TUITION_MISSING_TEXT}</p>
            ) : (
              <div className="space-y-1">
                <div className="relative h-5 overflow-hidden rounded bg-muted">
                  <div className="flex h-full">
                    {segments.map((seg) => (
                      <div key={seg.key} style={{ width: pct(seg.value), background: COLORS[seg.key] }} title={`${seg.label}: ${usd(seg.value)}`} />
                    ))}
                    {missingLabels.length > 0 && (
                      <div
                        className="h-full min-w-3 border-l border-dashed border-amber-500 bg-amber-200/50 dark:bg-amber-900/40"
                        style={{ width: "4%" }}
                        title={`Chưa có dữ liệu: ${missingLabels.join(", ")}`}
                      />
                    )}
                  </div>
                  {budget !== null && (
                    <div className="absolute inset-y-0 w-0.5 bg-red-600" style={{ left: pct(budget) }} title={`Ngân sách: ${usd(budget)}`} />
                  )}
                </div>
                {missingLabels.length > 0 && (
                  <p className="text-[11px] text-amber-800 dark:text-amber-300">
                    Khoản chưa có dữ liệu (không tính $0): {missingLabels.join(", ")}
                  </p>
                )}
              </div>
            )}
          </div>
        );
      })}
      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
        {LEGEND_ITEMS.map((seg) => (
          <span key={seg.key} className="inline-flex items-center gap-1">
            <span className="size-2.5 rounded-sm" style={{ background: COLORS[seg.key] }} />{seg.label}
          </span>
        ))}
        {anyMissing && (
          <span className="inline-flex items-center gap-1">
            <span className="size-2.5 rounded-sm border border-dashed border-amber-500 bg-amber-200/60" />
            Khoản chưa có dữ liệu
          </span>
        )}
        {budget !== null && <span className="inline-flex items-center gap-1"><span className="h-3 w-0.5 bg-red-600" />Ngân sách {usd(budget)}/năm</span>}
      </div>
    </div>
  );
}
