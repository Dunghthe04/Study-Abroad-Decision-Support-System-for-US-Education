"use client";

// Biểu đồ của trang Chất lượng AI (UC-90): kết quả theo ngày (cột chồng), thời gian phản hồi theo ngày (đường),
// kết quả theo tính năng (thanh chồng 100%) và các bước AI hay hỏng (thanh xếp hạng).
// Màu của bốn loại kết quả cố định (xem OUTCOMES) và luôn đi kèm chú giải; mọi giá trị đều có ở bảng của thẻ biểu đồ.

import type { ReactNode } from "react";
import {
  Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { EmptyNote, Legend } from "@/components/admin/ai-usage/parts";
import { TipRow, useTip } from "@/components/admin/ai-usage/Tip";
import {
  featureLabel, OUTCOMES, outcomeName, rateOf, stepLabel, type QualityDayPoint,
} from "@/lib/ai-quality-format";
import { formatCompact, formatDay, formatDayFull, formatInt, formatMs, formatPct } from "@/lib/ai-usage-format";
import type { AiQualityFeature, FailedStepCount } from "@/types/ai-quality";

const AXIS_TICK = { fill: "var(--viz-muted)", fontSize: 11 } as const;
const MARGIN = { top: 12, right: 16, bottom: 0, left: 0 } as const;
const UNCLASSIFIED_COLOR = "var(--viz-muted)";

interface TooltipProps {
  active?: boolean;
  payload?: ReadonlyArray<{ payload: QualityDayPoint }>;
}

function DayTip({ active, payload, rows }: TooltipProps & { rows: (point: QualityDayPoint) => ReactNode }) {
  const point = payload?.[0]?.payload;
  if (!active || !point) return null;
  return (
    <div className="w-max min-w-44 rounded-md border border-line bg-surface px-2.5 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-ink">{formatDayFull(point.date)}</p>
      <div className="flex flex-col gap-0.5">{rows(point)}</div>
    </div>
  );
}

// ------------------------------------------------------------------ kết quả theo ngày (cột chồng)
export function OutcomeColumnsChart({ data }: { data: QualityDayPoint[] }) {
  const hasUnclassified = data.some((point) => point.unclassified > 0);
  if (data.every((point) => point.total === 0)) return <EmptyNote>Chưa có lượt chat nào trong khoảng này.</EmptyNote>;
  return (
    <div className="flex flex-col gap-2">
      <Legend
        items={[
          ...OUTCOMES.map((o) => ({ label: outcomeName(o.key), color: o.color })),
          ...(hasUnclassified ? [{ label: "Chưa phân loại", color: UNCLASSIFIED_COLOR }] : []),
        ]}
      />
      <ResponsiveContainer width="100%" height={240}>
        <BarChart data={data} margin={MARGIN}>
          <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
          <XAxis dataKey="date" tickFormatter={formatDay} tick={AXIS_TICK} tickLine={false} minTickGap={32} axisLine={{ stroke: "var(--viz-axis)" }} />
          <YAxis tickFormatter={(value: number) => formatCompact(value)} tick={AXIS_TICK} tickLine={false} axisLine={false} width={44} tickCount={5} allowDecimals={false} />
          <Tooltip
            cursor={{ fill: "var(--viz-track)", opacity: 0.6 }}
            content={(props) => (
              <DayTip
                {...(props as unknown as TooltipProps)}
                rows={(point) => (
                  <>
                    {OUTCOMES.map((o) => <TipRow key={o.key} color={o.color} label={outcomeName(o.key)} value={formatInt(point[o.key])} />)}
                    {point.unclassified > 0 ? <TipRow color={UNCLASSIFIED_COLOR} label="Chưa phân loại" value={formatInt(point.unclassified)} /> : null}
                    <TipRow label="Tổng" value={formatInt(point.total)} />
                  </>
                )}
              />
            )}
          />
          {OUTCOMES.map((o) => (
            <Bar key={o.key} dataKey={o.key} stackId="turns" fill={o.color} stroke="var(--viz-surface)" strokeWidth={2} maxBarSize={24} radius={[3, 3, 0, 0]} isAnimationActive={false} />
          ))}
          {hasUnclassified ? (
            <Bar dataKey="unclassified" stackId="turns" fill={UNCLASSIFIED_COLOR} stroke="var(--viz-surface)" strokeWidth={2} maxBarSize={24} radius={[3, 3, 0, 0]} isAnimationActive={false} />
          ) : null}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

// ------------------------------------------------------------------ thời gian phản hồi theo ngày: p50 (thường) và p95 (chậm)
export function LatencyLinesChart({ data }: { data: QualityDayPoint[] }) {
  if (data.every((point) => point.p50 === null)) return <EmptyNote>Chưa đo được thời gian phản hồi nào trong khoảng này.</EmptyNote>;
  return (
    <div className="flex flex-col gap-2">
      <Legend items={[{ label: "Thường gặp (p50)", color: "var(--viz-1)" }, { label: "Chậm nhất 5% (p95)", color: "var(--viz-2)" }]} />
      <ResponsiveContainer width="100%" height={240}>
        <LineChart data={data} margin={MARGIN}>
          <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
          <XAxis dataKey="date" tickFormatter={formatDay} tick={AXIS_TICK} tickLine={false} minTickGap={32} axisLine={{ stroke: "var(--viz-axis)" }} />
          <YAxis tickFormatter={(value: number) => formatMs(value)} tick={AXIS_TICK} tickLine={false} axisLine={false} width={64} tickCount={5} domain={[0, "auto"]} />
          <Tooltip
            cursor={{ stroke: "var(--viz-axis)", strokeWidth: 1 }}
            content={(props) => (
              <DayTip
                {...(props as unknown as TooltipProps)}
                rows={(point) => (
                  <>
                    <TipRow color="var(--viz-1)" label="p50" value={point.p50 === null ? "—" : formatMs(point.p50)} />
                    <TipRow color="var(--viz-2)" label="p95" value={point.p95 === null ? "—" : formatMs(point.p95)} />
                    <TipRow label="Số lượt" value={formatInt(point.total)} />
                  </>
                )}
              />
            )}
          />
          {/* Ngày không có lượt nào để trống (không nối qua), vì khi đó không có thời gian để vẽ */}
          <Line type="monotone" dataKey="p50" stroke="var(--viz-1)" strokeWidth={2} dot={false} connectNulls={false} isAnimationActive={false} activeDot={{ r: 4, stroke: "var(--viz-surface)", strokeWidth: 2 }} />
          <Line type="monotone" dataKey="p95" stroke="var(--viz-2)" strokeWidth={2} dot={false} connectNulls={false} isAnimationActive={false} activeDot={{ r: 4, stroke: "var(--viz-surface)", strokeWidth: 2 }} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

// ------------------------------------------------------------------ kết quả theo tính năng (mỗi thanh là 100% lượt của tính năng đó)
export function FeatureOutcomeBars({ rows }: { rows: AiQualityFeature[] }) {
  const { ref, show, hide, layer } = useTip();
  const shown = rows.filter((row) => row.counts.total > 0);
  if (shown.length === 0) return <EmptyNote>Chưa có lượt chat nào trong khoảng này.</EmptyNote>;

  return (
    <div className="flex flex-col gap-3">
      <Legend items={OUTCOMES.map((o) => ({ label: outcomeName(o.key), color: o.color }))} />
      <div ref={ref} className="relative flex flex-col gap-2" onPointerLeave={hide}>
        {shown.map((row) => {
          const tip = (
            <div className="flex min-w-48 flex-col gap-0.5">
              <p className="mb-1 font-medium text-ink">{featureLabel(row.feature)} · {formatInt(row.counts.total)} lượt</p>
              {OUTCOMES.map((o) => (
                <TipRow key={o.key} color={o.color} label={outcomeName(o.key)} value={`${formatInt(row.counts[o.key])} (${formatPct(rateOf(row.counts, o.key))})`} />
              ))}
              {row.latency.p95Ms !== null ? <TipRow label="Thời gian p95" value={formatMs(row.latency.p95Ms)} /> : null}
            </div>
          );
          return (
            <div
              key={row.feature}
              tabIndex={0}
              role="group"
              aria-label={`${featureLabel(row.feature)}: ${formatInt(row.counts.total)} lượt, trả lời được ${formatPct(row.counts.answeredRate)}`}
              className="grid grid-cols-[minmax(6rem,11rem)_1fr_auto] items-center gap-3 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
              onPointerMove={(event) => show(event, tip)}
              onFocus={(event) => show(event, tip)}
              onBlur={hide}
            >
              <span className="truncate text-sm text-ink-2">{featureLabel(row.feature)}</span>
              <div className="flex h-4 overflow-hidden rounded-[3px] bg-[var(--viz-track)]">
                {OUTCOMES.map((o) => {
                  const part = row.counts[o.key];
                  return part > 0 ? (
                    <span key={o.key} aria-hidden style={{ width: `${(part / row.counts.total) * 100}%`, background: o.color, borderRight: "2px solid var(--viz-surface)" }} />
                  ) : null;
                })}
              </div>
              <span className="tabular w-12 text-right text-sm text-ink">{formatInt(row.counts.total)}</span>
            </div>
          );
        })}
        {layer}
      </div>
    </div>
  );
}

// ------------------------------------------------------------------ bước AI hay hỏng (một màu: thứ tự đã nói lên so sánh)
export function FailedStepBars({ rows }: { rows: FailedStepCount[] }) {
  if (rows.length === 0) return <EmptyNote>Không có bước AI nào bị hỏng trong khoảng này.</EmptyNote>;
  const max = Math.max(...rows.map((row) => row.turns), 1);
  return (
    <div className="flex flex-col gap-2">
      {rows.slice(0, 8).map((row) => (
        <div key={row.step} role="group" aria-label={`${stepLabel(row.step)}: ${formatInt(row.turns)} lượt`} className="grid grid-cols-[minmax(6rem,11rem)_1fr] items-center gap-3">
          <span className="truncate text-sm text-ink-2" title={row.step}>{stepLabel(row.step)}</span>
          <div className="flex items-center gap-2">
            <div className="h-4 flex-1 rounded-[3px] bg-[var(--viz-track)]">
              <div className="h-full rounded-[3px]" style={{ width: `${(row.turns / max) * 100}%`, background: "var(--viz-4)" }} />
            </div>
            <span className="tabular w-12 text-right text-sm text-ink">{formatInt(row.turns)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
