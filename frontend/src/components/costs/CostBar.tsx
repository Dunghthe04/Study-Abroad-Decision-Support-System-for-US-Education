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

const TICKS = [0, 0.25, 0.5, 0.75, 1];

/** Cột đứng chi phí mỗi năm của từng trường, vạch ngang là ngân sách. Khoản thiếu tách riêng, không vẽ $0 (UC-26). */
export function CostBar({ schools, scaleMax }: { schools: SchoolCost[]; scaleMax: number }) {
  const budget = schools.find((s) => s.budget?.annualBudgetUsd)?.budget?.annualBudgetUsd ?? null;
  const pct = (n: number) => `${Math.min((n / scaleMax) * 100, 100)}%`;
  const anyMissing = schools.some((s) => !tuitionMissing(s) && chartData(s.lines).missingLabels.length > 0);
  const columns = { gridTemplateColumns: `repeat(${schools.length}, minmax(0, 1fr))` };

  return (
    <div className="space-y-4" role="img" aria-label="Biểu đồ cột chi phí mỗi năm của từng trường so với ngân sách. Số liệu chính xác ở bảng bên dưới.">
      <div className="grid grid-cols-[4.75rem_1fr] gap-x-3">
        {/* Trục dọc */}
        <div className="relative h-80" aria-hidden>
          {TICKS.map((t) => (
            <span key={t} className="absolute right-0 -translate-y-1/2 text-sm text-ink-2 tabular-nums" style={{ bottom: `${t * 100}%` }}>
              {usd(scaleMax * t)}
            </span>
          ))}
        </div>

        <div>
          <div className="relative h-80">
            {TICKS.map((t) => (
              <div key={t} className="absolute inset-x-0 border-t border-line" style={{ bottom: `${t * 100}%` }} aria-hidden />
            ))}

            <div className="absolute inset-0 grid items-end gap-4 px-2" style={columns}>
              {schools.map((s) => {
                const { segments, missingLabels } = chartData(s.lines);
                const missing = tuitionMissing(s);
                const total = s.annualUsd ?? segments.reduce((sum, seg) => sum + seg.value, 0);
                // Khoản thiếu chiếm một đoạn nhỏ cố định trên đỉnh cột, tách khỏi phần có số
                const barValue = total + (missingLabels.length > 0 ? scaleMax * 0.04 : 0);
                return (
                  <div key={s.code} className="relative mx-auto h-full w-full max-w-24">
                    {missing ? (
                      <p className="absolute inset-x-0 bottom-2 text-center text-sm text-ink-2">{TUITION_MISSING_TEXT}</p>
                    ) : (
                      <div className="absolute inset-x-0 bottom-0 flex flex-col-reverse" style={{ height: pct(barValue) }}>
                        <span className="absolute inset-x-0 bottom-full mb-2 text-center text-base font-semibold text-ink tabular-nums">
                          {s.annualUsd === null ? <MissingValue /> : usd(s.annualUsd)}
                        </span>
                        {segments.map((seg) => (
                          <div
                            key={seg.key}
                            style={{ height: `${(seg.value / (barValue || 1)) * 100}%`, background: COLORS[seg.key] }}
                            title={`${seg.label}: ${usd(seg.value)}`}
                          />
                        ))}
                        {missingLabels.length > 0 && (
                          <div
                            className="min-h-3 flex-1 border border-dashed border-warn bg-warn-wash"
                            title={`Chưa có dữ liệu: ${missingLabels.join(", ")}`}
                          />
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            {budget !== null && (
              <div className="pointer-events-none absolute inset-x-0 border-t-2 border-risk" style={{ bottom: pct(budget) }} title={`Ngân sách: ${usd(budget)}`}>
                <span className="absolute right-0 bottom-1 bg-background px-1.5 text-sm font-semibold text-risk tabular-nums">Ngân sách {usd(budget)}</span>
              </div>
            )}
          </div>

          {/* Tên trường dưới từng cột */}
          <div className="mt-3 grid gap-4 px-2" style={columns}>
            {schools.map((s) => (
              <div key={s.code} className="mx-auto w-full max-w-40 text-center">
                <p className="line-clamp-2 text-base font-semibold text-ink">{s.name}</p>
                {!tuitionMissing(s) && !s.isComplete && <p className="mt-0.5 text-sm font-medium text-warn">Chưa đủ khoản</p>}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="flex flex-wrap justify-center gap-x-5 gap-y-1.5 text-sm text-ink-2">
        {LEGEND_ITEMS.map((seg) => (
          <span key={seg.key} className="inline-flex items-center gap-1.5">
            <span className="size-3" style={{ background: COLORS[seg.key] }} />{seg.label}
          </span>
        ))}
        {anyMissing && (
          <span className="inline-flex items-center gap-1.5">
            <span className="size-3 border border-dashed border-warn bg-warn-wash" />
            Khoản chưa có dữ liệu
          </span>
        )}
      </div>
    </div>
  );
}
