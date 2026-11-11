"use client";

// Hai biểu đồ còn lại: lịch nhiệt (số lần gọi mỗi ngày) và bong bóng (độ trễ so với kích thước lần gọi của từng tính năng).

import {
  CartesianGrid, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis,
} from "recharts";
import { EmptyNote } from "@/components/admin/ai-usage/parts";
import { TipRow, useTip } from "@/components/admin/ai-usage/Tip";
import {
  formatCompact, formatDayFull, formatInt, formatMs, formatUsd, kindLabel, ratio,
  totalTokens, weekdayMon0, type DailyPoint, type PurposeRow,
} from "@/lib/ai-usage-format";

// ------------------------------------------------------------------ lịch nhiệt: mỗi ô một ngày, màu đậm dần theo số lần gọi
const CELL = 16;
const GAP = 3;
const WEEKDAYS = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
// Bốn bậc của một màu (đậm dần), trộn với màu nền; ngày không có lần gọi dùng màu ô trống
const LEVELS = [28, 48, 72, 100];

function levelOf(calls: number, max: number): number {
  if (calls <= 0 || max <= 0) return 0;
  return Math.min(LEVELS.length, Math.max(1, Math.ceil((calls / max) * LEVELS.length)));
}

function levelColor(level: number): string {
  return level === 0 ? "var(--viz-track)" : `color-mix(in srgb, var(--viz-1) ${LEVELS[level - 1]}%, var(--viz-surface))`;
}

export function CalendarHeatmap({ data }: { data: DailyPoint[] }) {
  const { ref, show, hide, layer } = useTip();
  if (data.length === 0 || data.every((point) => point.calls === 0)) return <EmptyNote />;

  const max = Math.max(...data.map((point) => point.calls));
  const lead = weekdayMon0(data[0].date);   // ô trống đầu tuần đầu tiên
  const cells: (DailyPoint | null)[] = [...Array<null>(lead).fill(null), ...data];
  const weeks = Math.ceil(cells.length / 7);

  // Nhãn tháng ở cột đầu tiên của mỗi tháng
  const monthLabels = Array.from({ length: weeks }, (_, week) => {
    const first = cells.slice(week * 7, week * 7 + 7).find((cell) => cell !== null);
    if (!first) return "";
    const month = first.date.slice(5, 7);
    const before = week === 0 ? "" : cells.slice((week - 1) * 7, week * 7).find((cell) => cell !== null)?.date.slice(5, 7);
    return month !== before ? `Th${Number(month)}` : "";
  });

  return (
    <div ref={ref} className="relative flex flex-col gap-3" onPointerLeave={hide}>
      <div className="overflow-x-auto pb-1">
        <div className="flex gap-2" role="img" aria-label="Lịch số lần gọi AI theo ngày. Xem số liệu chính xác ở bảng.">
          <div className="grid shrink-0 text-[10px] text-ink-3" style={{ gridTemplateRows: `repeat(7, ${CELL}px)`, rowGap: GAP, marginTop: CELL + GAP }}>
            {WEEKDAYS.map((day) => <span key={day} className="leading-4">{day}</span>)}
          </div>
          <div>
            <div className="grid text-[10px] text-ink-3" style={{ gridTemplateColumns: `repeat(${weeks}, ${CELL}px)`, columnGap: GAP, height: CELL, marginBottom: GAP }}>
              {monthLabels.map((label, week) => <span key={week} className="whitespace-nowrap leading-4">{label}</span>)}
            </div>
            <div className="grid grid-flow-col" style={{ gridTemplateRows: `repeat(7, ${CELL}px)`, gridAutoColumns: CELL, gap: GAP }}>
              {cells.map((cell, index) =>
                cell === null ? (
                  <span key={`pad-${index}`} aria-hidden />
                ) : (
                  <span
                    key={cell.date}
                    className="rounded-[3px] hover:outline hover:outline-2 hover:outline-offset-0 hover:outline-[var(--viz-1)]"
                    style={{ background: levelColor(levelOf(cell.calls, max)) }}
                    onPointerMove={(event) => show(event, (
                      <div className="flex min-w-40 flex-col gap-0.5">
                        <p className="font-medium text-ink">{formatDayFull(cell.date)}</p>
                        <TipRow label="Lần gọi" value={formatInt(cell.calls)} />
                        <TipRow label="Chi phí" value={formatUsd(cell.cost)} />
                        <TipRow label="Lỗi" value={formatInt(cell.failed)} />
                      </div>
                    ))}
                  />
                ),
              )}
            </div>
          </div>
        </div>
      </div>
      <div className="flex items-center gap-1.5 text-xs text-ink-2">
        Ít
        <span aria-hidden className="size-3 rounded-[3px]" style={{ background: levelColor(0) }} />
        {LEVELS.map((_, index) => <span key={index} aria-hidden className="size-3 rounded-[3px]" style={{ background: levelColor(index + 1) }} />)}
        Nhiều
        <span className="ml-2 text-ink-3">(nhiều nhất {formatInt(max)} lần gọi trong một ngày)</span>
      </div>
      {layer}
    </div>
  );
}

// ------------------------------------------------------------------ bong bóng: tính năng nào vừa nặng vừa chậm
interface Bubble {
  label: string;
  raw: string | null;
  kind: string;
  x: number;   // token (vào + ra) mỗi lần gọi
  y: number;   // thời gian trung bình, ms
  z: number;   // số lần gọi
  cost: number;
  failRate: number;
}

interface ShapeProps {
  cx?: number;
  cy?: number;
  size?: number;
  payload?: Bubble;
}

const LABELED = 3;   // chỉ dán nhãn vài bong bóng lớn nhất, còn lại để tooltip

export function LatencyBubbleChart({ rows }: { rows: PurposeRow[] }) {
  const bubbles: Bubble[] = rows
    .filter((row) => row.metrics.calls > 0)
    .map((row) => ({
      label: row.label,
      raw: row.purpose,
      kind: row.kind,
      x: totalTokens(row.metrics) / row.metrics.calls,
      y: row.metrics.avgLatencyMs,
      z: row.metrics.calls,
      cost: row.metrics.costUsd,
      failRate: ratio(row.metrics.failedCalls, row.metrics.calls),
    }));
  if (bubbles.length < 2) return <EmptyNote>Cần ít nhất hai tính năng có lần gọi để so sánh.</EmptyNote>;

  const labeled = new Set([...bubbles].sort((a, b) => b.z - a.z).slice(0, LABELED).map((bubble) => bubble.label));

  return (
    <ResponsiveContainer width="100%" height={280}>
      <ScatterChart margin={{ top: 12, right: 24, bottom: 24, left: 0 }}>
        <CartesianGrid stroke="var(--viz-grid)" />
        <XAxis
          type="number" dataKey="x" name="Token mỗi lần gọi" tickFormatter={(value: number) => formatCompact(value)}
          tick={{ fill: "var(--viz-muted)", fontSize: 11 }} tickLine={false} axisLine={{ stroke: "var(--viz-axis)" }}
          label={{ value: "Token mỗi lần gọi", position: "insideBottom", offset: -12, fill: "var(--viz-muted)", fontSize: 11 }}
        />
        <YAxis
          type="number" dataKey="y" name="Thời gian trung bình" tickFormatter={(value: number) => formatMs(value)}
          tick={{ fill: "var(--viz-muted)", fontSize: 11 }} tickLine={false} axisLine={false} width={56}
        />
        <ZAxis type="number" dataKey="z" range={[110, 560]} />
        <Tooltip
          cursor={{ stroke: "var(--viz-axis)" }}
          content={(props) => {
            const entry = (props as unknown as { active?: boolean; payload?: ReadonlyArray<{ payload: Bubble }> });
            const bubble = entry.payload?.[0]?.payload;
            if (!entry.active || !bubble) return null;
            return (
              <div className="w-max min-w-48 rounded-md border border-line bg-surface px-2.5 py-2 text-xs shadow-md">
                <p className="font-medium text-ink">{bubble.label}</p>
                <p className="mb-1 text-ink-3">{bubble.raw ?? "không có tên gốc"} · {kindLabel(bubble.kind)}</p>
                <div className="flex flex-col gap-0.5">
                  <TipRow label="Lần gọi" value={formatInt(bubble.z)} />
                  <TipRow label="Token mỗi lần" value={formatInt(bubble.x)} />
                  <TipRow label="Thời gian TB" value={formatMs(bubble.y)} />
                  <TipRow label="Chi phí" value={formatUsd(bubble.cost)} />
                </div>
              </div>
            );
          }}
        />
        <Scatter
          data={bubbles} fill="var(--viz-1)" isAnimationActive={false}
          shape={(props: ShapeProps) => {
            const { cx, cy, size, payload } = props;
            if (cx === undefined || cy === undefined || !payload) return <g />;
            const radius = Math.sqrt((size ?? 200) / Math.PI);
            return (
              <g>
                {/* vùng rê chuột rộng hơn bong bóng (ít nhất 24px) */}
                <circle cx={cx} cy={cy} r={Math.max(radius, 12)} fill="transparent" />
                <circle cx={cx} cy={cy} r={radius} fill="var(--viz-1)" fillOpacity={0.75} stroke="var(--viz-surface)" strokeWidth={2} />
                {labeled.has(payload.label) ? (
                  <text x={cx + radius + 4} y={cy + 4} fontSize={11} fill="var(--ink-2)">{payload.label}</text>
                ) : null}
              </g>
            );
          }}
        />
      </ScatterChart>
    </ResponsiveContainer>
  );
}
