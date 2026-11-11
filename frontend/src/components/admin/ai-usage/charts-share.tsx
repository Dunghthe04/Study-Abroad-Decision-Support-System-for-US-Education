"use client";

// Biểu đồ so sánh các phần: thanh ngang xếp hạng theo skill, thanh phân đoạn cơ cấu token, danh sách thanh đo tỉ lệ lỗi.
// Tự vẽ bằng HTML: mỗi mark có tooltip riêng (không crosshair), và mọi giá trị đều có ở bảng của thẻ biểu đồ.

import { CircleCheckIcon, TriangleAlertIcon } from "lucide-react";
import { cn } from "cn";
import { EmptyNote, Legend } from "@/components/admin/ai-usage/parts";
import { TipRow, useTip } from "@/components/admin/ai-usage/Tip";
import {
  formatCompact, formatInt, formatMs, formatPct, formatPurposeValue, formatUsd, kindLabel, purposeValue,
  ratio, type PurposeMetric, type PurposeRow,
} from "@/lib/ai-usage-format";
import type { AiUsageMetrics } from "@/types/ai-usage";

const MAX_ROWS = 8;

function PurposeTip({ row }: { row: PurposeRow }) {
  const m = row.metrics;
  return (
    <div className="flex min-w-48 flex-col gap-0.5">
      <p className="font-medium text-ink">{row.label}</p>
      <p className="mb-1 text-ink-3">{row.purpose ?? "không có tên gốc"} · {kindLabel(row.kind)}</p>
      <TipRow label="Lần gọi" value={formatInt(m.calls)} />
      <TipRow label="Token vào / ra" value={`${formatCompact(m.inputTokens)} / ${formatCompact(m.outputTokens)}`} />
      <TipRow label="Chi phí" value={formatUsd(m.costUsd)} />
      <TipRow label="Thời gian TB" value={formatMs(m.avgLatencyMs)} />
      <TipRow label="Lỗi" value={`${formatInt(m.failedCalls)} (${formatPct(ratio(m.failedCalls, m.calls))})`} />
    </div>
  );
}

// ------------------------------------------------------------------ thanh ngang xếp hạng (một màu: thứ tự đã nói lên so sánh)
export function RankedBars({ rows, metric }: { rows: PurposeRow[]; metric: PurposeMetric }) {
  const { ref, show, hide, layer } = useTip();
  const ranked = rows
    .map((row) => ({ row, value: purposeValue(metric, row.metrics) }))
    .filter((entry) => entry.row.metrics.calls > 0)
    .sort((a, b) => b.value - a.value);
  if (ranked.length === 0) return <EmptyNote />;

  const shown = ranked.slice(0, MAX_ROWS);
  const hidden = ranked.length - shown.length;
  const max = Math.max(...shown.map((entry) => entry.value), 0);

  return (
    <div ref={ref} className="relative flex flex-col gap-2" onPointerLeave={hide}>
      {shown.map(({ row, value }) => (
        <div
          key={row.key}
          tabIndex={0}
          role="group"
          aria-label={`${row.label}: ${formatPurposeValue(metric, value)}`}
          className="grid grid-cols-[minmax(6rem,11rem)_1fr] items-center gap-3 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
          onPointerMove={(event) => show(event, <PurposeTip row={row} />)}
          onFocus={(event) => show(event, <PurposeTip row={row} />)}
          onBlur={hide}
        >
          <span className="truncate text-sm text-ink-2" title={row.purpose ?? undefined}>{row.label}</span>
          <div className="flex items-center gap-2">
            {/* Thanh dày 12px (mức tối đa 24px), đầu bo 4px, gốc vuông; giá trị ở đầu thanh */}
            <div
              className="h-3 rounded-r-[4px] bg-[var(--viz-1)] transition-opacity hover:opacity-80"
              style={{ width: `calc((100% - 5.5rem) * ${max > 0 ? value / max : 0})`, minWidth: value > 0 ? 2 : 0 }}
            />
            <span className="tabular shrink-0 text-xs font-medium text-ink">{formatPurposeValue(metric, value)}</span>
          </div>
        </div>
      ))}
      {hidden > 0 ? <p className="text-xs text-ink-3">và {hidden} tính năng khác, xem ở bảng.</p> : null}
      {layer}
    </div>
  );
}

// ------------------------------------------------------------------ thanh phân đoạn: cơ cấu token (một tổng, ba phần)
const TOKEN_PARTS = [
  { key: "input", label: "Vào", color: "var(--viz-1)", pick: (m: AiUsageMetrics) => m.inputTokens },
  { key: "output", label: "Ra", color: "var(--viz-2)", pick: (m: AiUsageMetrics) => m.outputTokens },
] as const;

export function TokenSegmentedBar({ metrics }: { metrics: AiUsageMetrics }) {
  const { ref, show, hide, layer } = useTip();
  const parts = TOKEN_PARTS.map((part) => ({ ...part, value: part.pick(metrics) }));
  const total = parts.reduce((sum, part) => sum + part.value, 0);
  if (total === 0) return <EmptyNote />;

  const summary = parts.map((part) => `${part.label} ${formatInt(part.value)}`).join(", ");
  return (
    <div className="flex flex-col gap-3">
      <div ref={ref} className="relative">
        <div role="img" aria-label={`Cơ cấu token: ${summary}`} className="flex h-7 gap-0.5" onPointerLeave={hide}>
          {parts.filter((part) => part.value > 0).map((part) => {
            const share = part.value / total;
            return (
              <div
                key={part.key}
                tabIndex={0}
                className="flex min-w-0 items-center rounded-[4px] px-2 text-xs font-semibold text-white outline-none hover:opacity-85 focus-visible:ring-2 focus-visible:ring-ring"
                style={{ flexGrow: part.value, flexBasis: 0, background: part.color }}
                onPointerMove={(event) => show(event, (
                  <div className="flex min-w-40 flex-col gap-0.5">
                    <p className="font-medium text-ink">{part.label}</p>
                    <TipRow label="Token" value={formatInt(part.value)} />
                    <TipRow label="Tỉ trọng" value={formatPct(share)} />
                  </div>
                ))}
                onFocus={(event) => show(event, <p className="text-ink">{part.label}: {formatInt(part.value)} ({formatPct(share)})</p>)}
                onBlur={hide}
              >
                {/* Chỉ in số khi đoạn đủ rộng; đoạn hẹp để tooltip và bảng giữ giá trị */}
                {share >= 0.1 ? formatPct(share, 0) : null}
              </div>
            );
          })}
        </div>
        {layer}
      </div>
      <Legend items={TOKEN_PARTS.map((part) => ({ label: part.label, color: part.color }))} />
      <dl className="grid grid-cols-2 gap-2 text-xs">
        {parts.map((part) => (
          <div key={part.key} className="rounded-md bg-ground px-2.5 py-2">
            <dt className="text-ink-2">{part.label}</dt>
            <dd className="tabular text-sm font-semibold text-ink">{formatCompact(part.value)}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

// ------------------------------------------------------------------ danh sách thanh đo: tỉ lệ lỗi của từng tính năng
const WARN_FROM = 0.05;
const HIGH_FROM = 0.15;

function severity(rate: number): { color: string; label: string | null } {
  if (rate >= HIGH_FROM) return { color: "var(--risk)", label: "Cao" };
  if (rate >= WARN_FROM) return { color: "var(--warn)", label: "Cần để ý" };
  return { color: "var(--viz-1)", label: null };
}

export function FailureMeters({ rows }: { rows: PurposeRow[] }) {
  const { ref, show, hide, layer } = useTip();
  const active = rows.filter((row) => row.metrics.calls > 0);
  if (active.length === 0) return <EmptyNote />;

  const sorted = [...active].sort(
    (a, b) => ratio(b.metrics.failedCalls, b.metrics.calls) - ratio(a.metrics.failedCalls, a.metrics.calls) || b.metrics.calls - a.metrics.calls,
  );
  const shown = sorted.slice(0, MAX_ROWS);
  const failedTotal = active.reduce((sum, row) => sum + row.metrics.failedCalls, 0);

  return (
    <div ref={ref} className="relative flex flex-col gap-2.5" onPointerLeave={hide}>
      {failedTotal === 0 ? (
        <p className="flex items-center gap-1.5 text-sm text-ok">
          <CircleCheckIcon aria-hidden className="size-4" /> Không có lần gọi nào bị lỗi trong khoảng này.
        </p>
      ) : null}
      {shown.map((row) => {
        const rate = ratio(row.metrics.failedCalls, row.metrics.calls);
        const level = severity(rate);
        return (
          <div
            key={row.key}
            tabIndex={0}
            role="group"
            aria-label={`${row.label}: lỗi ${formatPct(rate)}, ${row.metrics.failedCalls} trên ${row.metrics.calls} lần gọi`}
            className="grid grid-cols-[minmax(6rem,11rem)_1fr_auto] items-center gap-3 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
            onPointerMove={(event) => show(event, <PurposeTip row={row} />)}
            onFocus={(event) => show(event, <PurposeTip row={row} />)}
            onBlur={hide}
          >
            <span className="truncate text-sm text-ink-2" title={row.purpose ?? undefined}>{row.label}</span>
            {/* Phần tô mang mức độ; nền thanh là bậc nhạt của chính màu đó */}
            <div className="h-2 overflow-hidden rounded-full" style={{ background: `color-mix(in srgb, ${level.color} 18%, var(--viz-surface))` }}>
              <div className="h-full rounded-full" style={{ width: `${Math.min(rate, 1) * 100}%`, minWidth: rate > 0 ? 3 : 0, background: level.color }} />
            </div>
            <span className={cn("tabular flex min-w-24 items-center justify-end gap-1 text-xs font-medium", level.label ? "" : "text-ink")} style={level.label ? { color: level.color } : undefined}>
              {level.label ? <TriangleAlertIcon aria-label={level.label} className="size-3.5" /> : null}
              {formatPct(rate)}
              <span className="font-normal text-ink-3">({row.metrics.failedCalls}/{row.metrics.calls})</span>
            </span>
          </div>
        );
      })}
      <p className="text-xs text-ink-3">Dưới 5% bình thường · từ 5% cần để ý · từ 15% cao.</p>
      {layer}
    </div>
  );
}

