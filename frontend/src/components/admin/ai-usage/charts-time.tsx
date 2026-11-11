"use client";

// Biểu đồ theo thời gian: chi phí theo ngày (vùng) và số lần gọi theo ngày (cột chồng, local và cloud).
// Một trục duy nhất cho mỗi biểu đồ; hai đại lượng khác thang đo (tiền, lần gọi) nằm ở hai biểu đồ riêng.

import type { ReactNode } from "react";
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { Legend } from "@/components/admin/ai-usage/parts";
import { TipRow } from "@/components/admin/ai-usage/Tip";
import {
  formatCompact, formatDay, formatDayFull, formatInt, formatUsd, type DailyPoint,
} from "@/lib/ai-usage-format";

const AXIS_TICK = { fill: "var(--viz-muted)", fontSize: 11 } as const;
const MARGIN = { top: 12, right: 16, bottom: 0, left: 0 } as const;

interface TooltipProps {
  active?: boolean;
  payload?: ReadonlyArray<{ payload: DailyPoint }>;
}

/** Khung tooltip chung: ngày ở trên, mỗi chuỗi một dòng (giá trị đậm, nhãn phụ). */
function DayTip({ active, payload, rows }: TooltipProps & { rows: (point: DailyPoint) => ReactNode }) {
  const point = payload?.[0]?.payload;
  if (!active || !point) return null;
  return (
    <div className="w-max min-w-44 rounded-md border border-line bg-surface px-2.5 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium text-ink">{formatDayFull(point.date)}</p>
      <div className="flex flex-col gap-0.5">{rows(point)}</div>
    </div>
  );
}

// ------------------------------------------------------------------ chi phí theo ngày
interface DotProps {
  cx?: number;
  cy?: number;
  index?: number;
}

export function CostAreaChart({ data }: { data: DailyPoint[] }) {
  const last = data.length - 1;
  return (
    <ResponsiveContainer width="100%" height={240}>
      <AreaChart data={data} margin={MARGIN}>
        <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
        <XAxis
          dataKey="date" tickFormatter={formatDay} tick={AXIS_TICK} tickLine={false} minTickGap={32}
          axisLine={{ stroke: "var(--viz-axis)" }}
        />
        <YAxis
          tickFormatter={(value: number) => formatUsd(value)} tick={AXIS_TICK} tickLine={false} axisLine={false}
          width={64} tickCount={5} domain={[0, "auto"]}
        />
        <Tooltip
          cursor={{ stroke: "var(--viz-axis)", strokeWidth: 1 }}
          content={(props) => (
            <DayTip
              {...(props as unknown as TooltipProps)}
              rows={(point) => (
                <>
                  <TipRow color="var(--viz-1)" label="Chi phí" value={formatUsd(point.cost)} />
                  <TipRow label="Lần gọi" value={formatInt(point.calls)} />
                  <TipRow label="Token vào / ra" value={`${formatCompact(point.inputTokens)} / ${formatCompact(point.outputTokens)}`} />
                </>
              )}
            />
          )}
        />
        <Area
          type="monotone" dataKey="cost" stroke="var(--viz-1)" strokeWidth={2}
          fill="var(--viz-1)" fillOpacity={0.1} isAnimationActive={false}
          activeDot={{ r: 4, fill: "var(--viz-1)", stroke: "var(--viz-surface)", strokeWidth: 2 }}
          // Chỉ điểm cuối có chấm (kèm vòng nền để không dính vào đường)
          dot={(props: DotProps) =>
            props.index === last && props.cx !== undefined && props.cy !== undefined ? (
              <circle key="end" cx={props.cx} cy={props.cy} r={4} fill="var(--viz-1)" stroke="var(--viz-surface)" strokeWidth={2} />
            ) : (
              <g key={props.index} />
            )
          }
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}

// ------------------------------------------------------------------ số lần gọi theo ngày, tách cloud và local
export function CallsColumnsChart({ data }: { data: DailyPoint[] }) {
  const hasOther = data.some((point) => point.otherCalls > 0);
  return (
    <div className="flex flex-col gap-2">
      <Legend
        items={[
          { label: "Cloud (Claude)", color: "var(--viz-1)" },
          { label: "Local (Ollama)", color: "var(--viz-2)" },
          ...(hasOther ? [{ label: "Khác", color: "var(--viz-3)" }] : []),
        ]}
      />
      <ResponsiveContainer width="100%" height={240}>
        <BarChart data={data} margin={MARGIN}>
          <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
          <XAxis
            dataKey="date" tickFormatter={formatDay} tick={AXIS_TICK} tickLine={false} minTickGap={32}
            axisLine={{ stroke: "var(--viz-axis)" }}
          />
          <YAxis
            tickFormatter={(value: number) => formatCompact(value)} tick={AXIS_TICK} tickLine={false} axisLine={false}
            width={44} tickCount={5} allowDecimals={false}
          />
          <Tooltip
            cursor={{ fill: "var(--viz-track)", opacity: 0.6 }}
            content={(props) => (
              <DayTip
                {...(props as unknown as TooltipProps)}
                rows={(point) => (
                  <>
                    <TipRow color="var(--viz-1)" label="Cloud" value={formatInt(point.cloudCalls)} />
                    <TipRow color="var(--viz-2)" label="Local" value={formatInt(point.localCalls)} />
                    {point.otherCalls > 0 ? <TipRow color="var(--viz-3)" label="Khác" value={formatInt(point.otherCalls)} /> : null}
                    <TipRow label="Lỗi" value={formatInt(point.failed)} />
                  </>
                )}
              />
            )}
          />
          {/* Khe 2px màu nền giữa các đoạn chồng (không viền); cột dày tối đa 24px */}
          <Bar dataKey="cloudCalls" stackId="calls" fill="var(--viz-1)" stroke="var(--viz-surface)" strokeWidth={2} maxBarSize={24} radius={[3, 3, 0, 0]} isAnimationActive={false} />
          <Bar dataKey="localCalls" stackId="calls" fill="var(--viz-2)" stroke="var(--viz-surface)" strokeWidth={2} maxBarSize={24} radius={[3, 3, 0, 0]} isAnimationActive={false} />
          {hasOther ? (
            <Bar dataKey="otherCalls" stackId="calls" fill="var(--viz-3)" stroke="var(--viz-surface)" strokeWidth={2} maxBarSize={24} radius={[3, 3, 0, 0]} isAnimationActive={false} />
          ) : null}
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
