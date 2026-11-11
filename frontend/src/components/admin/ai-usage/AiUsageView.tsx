"use client";

import { useMemo, useState } from "react";
import { TriangleAlertIcon } from "lucide-react";
import "@/components/admin/ai-usage/viz.css";
import { CalendarHeatmap, LatencyBubbleChart } from "@/components/admin/ai-usage/charts-grid";
import { FailureMeters, RankedBars, TokenSegmentedBar } from "@/components/admin/ai-usage/charts-share";
import { CallsColumnsChart, CostAreaChart } from "@/components/admin/ai-usage/charts-time";
import { ChartCard, Segmented, StatTile } from "@/components/admin/ai-usage/parts";
import { PricingPanel } from "@/components/admin/ai-usage/PricingPanel";
import { UsersPanel } from "@/components/admin/ai-usage/UsersPanel";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { adminAiUsageApi, type DateRange } from "@/lib/ai-usage-api";
import {
  changeRatio, dailySeries, daysBetween, formatCompact, formatDayFull, formatInt, formatMs, formatPct, formatUsd,
  kindLabel, MAX_RANGE_DAYS, previousRange, purposeRows, PURPOSE_METRICS, rangeOfLastDays, ratio, totalTokens,
  CLOUD_KIND, LOCAL_KIND, type PurposeMetric,
} from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";

type Preset = "7" | "30" | "90" | "custom";

const PRESETS: { id: Preset; label: string }[] = [
  { id: "7", label: "7 ngày" },
  { id: "30", label: "30 ngày" },
  { id: "90", label: "90 ngày" },
  { id: "custom", label: "Tự chọn" },
];

function validateRange(range: DateRange): string | null {
  if (!range.from || !range.to) return "Chọn đủ ngày bắt đầu và ngày kết thúc.";
  if (range.from > range.to) return "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.";
  if (daysBetween(range.from, range.to) > MAX_RANGE_DAYS) return `Khoảng tối đa ${MAX_RANGE_DAYS} ngày.`;
  return null;
}

/** Trang admin: token, chi phí và chất lượng các lần gọi AI (UC-83). Chỉ admin. Một hàng bộ lọc phía trên áp cho mọi biểu đồ. */
export function AiUsageView() {
  const [preset, setPreset] = useState<Preset>("30");
  const [applied, setApplied] = useState<DateRange>(() => rangeOfLastDays(30));   // khoảng tự chọn đã áp dụng
  const [draft, setDraft] = useState<DateRange>(() => rangeOfLastDays(30));
  const [draftError, setDraftError] = useState<string | null>(null);
  const [metric, setMetric] = useState<PurposeMetric>("cost");

  const range = useMemo<DateRange>(
    () => (preset === "custom" ? applied : rangeOfLastDays(Number(preset))),
    [preset, applied],
  );
  const rangeKey = `${range.from}|${range.to}`;
  const previous = useMemo(() => previousRange(range), [range]);

  const overview = useFetched(`overview|${rangeKey}`, () => adminAiUsageApi.overview(range));
  const before = useFetched(`overview-prev|${rangeKey}`, () => adminAiUsageApi.overview(previous));
  const prices = useFetched("pricing", () => adminAiUsageApi.pricing());

  const data = overview.data;
  const total = data?.total ?? null;
  const prior = before.data?.total ?? null;
  const daily = useMemo(() => (data ? dailySeries(data) : []), [data]);
  const purposes = useMemo(() => (data ? purposeRows(data.byPurpose) : []), [data]);
  const loading = overview.loading;

  const applyDraft = () => {
    const problem = validateRange(draft);
    setDraftError(problem);
    if (!problem) setApplied(draft);
  };

  const kindCalls = (kind: string) => data?.byKind.find((entry) => entry.kind === kind)?.metrics.calls ?? 0;
  const failRate = total ? ratio(total.failedCalls, total.calls) : 0;
  const priorFailRate = prior ? ratio(prior.failedCalls, prior.calls) : 0;

  return (
    <div className="ai-viz flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Quản lý token AI</h1>
        <p className="text-sm text-ink-2">
          Token, tiền và chất lượng các lần gọi AI. Ngày tính theo UTC. Chỉ có số đo, không có nội dung chat.
        </p>
      </header>

      {/* Một hàng bộ lọc duy nhất, áp cho mọi thẻ bên dưới */}
      <div className="flex flex-wrap items-end gap-3">
        <Segmented label="Khoảng thời gian" value={preset} options={PRESETS} onChange={setPreset} />
        {preset === "custom" ? (
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1 text-xs text-ink-2">
              Từ ngày
              <Input type="date" value={draft.from} max={draft.to || undefined} onChange={(event) => setDraft({ ...draft, from: event.target.value })} className="h-8 w-40" />
            </label>
            <label className="flex flex-col gap-1 text-xs text-ink-2">
              Đến ngày
              <Input type="date" value={draft.to} min={draft.from || undefined} onChange={(event) => setDraft({ ...draft, to: event.target.value })} className="h-8 w-40" />
            </label>
            <Button type="button" size="sm" onClick={applyDraft}>Áp dụng</Button>
          </div>
        ) : null}
        <p className="pb-1 text-xs text-ink-3">
          {formatDayFull(range.from)} – {formatDayFull(range.to)} ({daysBetween(range.from, range.to)} ngày), so với {formatDayFull(previous.from)} – {formatDayFull(previous.to)}
        </p>
      </div>
      {draftError ? <Alert variant="destructive"><AlertDescription>{draftError}</AlertDescription></Alert> : null}
      {overview.error ? <Alert variant="destructive"><AlertDescription>{overview.error}</AlertDescription></Alert> : null}

      {total && total.unpricedCalls > 0 ? (
        <Alert variant="warning">
          <TriangleAlertIcon />
          <AlertDescription>
            {formatInt(total.unpricedCalls)} lần gọi Claude chưa có giá (model không nằm trong bảng giá), nên tổng chi phí bên dưới đang thiếu
            phần này. Thêm model vào bảng giá (AiPricing) để tính đủ.
          </AlertDescription>
        </Alert>
      ) : null}

      {/* Thẻ số: một con số dẫn đầu (chi phí), các thẻ còn lại so với kỳ trước */}
      <section aria-label="Tổng quan" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
        <div className="sm:col-span-2 lg:col-span-2 [&>*]:h-full">
          <StatTile
            hero label="Chi phí" value={total ? formatUsd(total.costUsd) : "—"} loading={loading}
            change={total && prior ? changeRatio(total.costUsd, prior.costUsd) : undefined}
            note="cloud (Claude); local 0 đồng"
            trend={daily.map((point) => point.cost)}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Lần gọi" value={total ? formatInt(total.calls) : "—"} loading={loading}
            change={total && prior ? changeRatio(total.calls, prior.calls) : undefined}
            note={`${formatInt(kindCalls(CLOUD_KIND))} cloud · ${formatInt(kindCalls(LOCAL_KIND))} local`}
            trend={daily.map((point) => point.calls)}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Token vào + ra" value={total ? formatCompact(totalTokens(total)) : "—"} loading={loading}
            change={total && prior ? changeRatio(totalTokens(total), totalTokens(prior)) : undefined}
            note={total ? `${formatCompact(total.inputTokens)} vào · ${formatCompact(total.outputTokens)} ra` : undefined}
            trend={daily.map((point) => point.inputTokens + point.outputTokens)}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Tỉ lệ lỗi" value={total ? formatPct(failRate) : "—"} loading={loading} upIs="bad"
            change={total && prior ? changeRatio(failRate, priorFailRate) : undefined}
            note={total ? `${formatInt(total.failedCalls)} lần lỗi` : undefined}
            trend={daily.map((point) => point.failed)}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Thời gian TB" value={total ? formatMs(total.avgLatencyMs) : "—"} loading={loading} upIs="bad"
            change={total && prior ? changeRatio(total.avgLatencyMs, prior.avgLatencyMs) : undefined}
            note="mỗi lần gọi"
          />
        </div>
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <ChartCard
          title="Chi phí theo ngày" description="Chỉ tính các lần gọi cloud đã có giá (Ollama chạy local, 0 đồng)." loading={loading}
          table={{
            columns: ["Ngày", "Chi phí", "Lần gọi", "Token vào", "Token ra", "Lỗi"],
            rows: daily.map((p) => [p.date, formatUsd(p.cost), p.calls, p.inputTokens, p.outputTokens, p.failed]),
          }}
        >
          <CostAreaChart data={daily} />
        </ChartCard>

        <ChartCard
          title="Số lần gọi theo ngày" description="Tách cloud (Claude) và local (Ollama)." loading={loading}
          table={{
            columns: ["Ngày", "Cloud", "Local", "Khác", "Lỗi"],
            rows: daily.map((p) => [p.date, p.cloudCalls, p.localCalls, p.otherCalls, p.failed]),
          }}
        >
          <CallsColumnsChart data={daily} />
        </ChartCard>

        <ChartCard
          title="Theo tính năng" description="Skill nào tốn nhiều nhất. Đổi chỉ số để xếp lại." loading={loading}
          controls={<Segmented label="Chỉ số xếp hạng" value={metric} options={PURPOSE_METRICS} onChange={setMetric} />}
          table={{
            columns: ["Tính năng", "Tên gốc", "Nguồn", "Lần gọi", "Token vào", "Token ra", "Chi phí", "TB", "Lỗi"],
            rows: purposes.map((p) => [
              p.label, p.purpose ?? "—", kindLabel(p.kind), p.metrics.calls, p.metrics.inputTokens, p.metrics.outputTokens,
              formatUsd(p.metrics.costUsd), formatMs(p.metrics.avgLatencyMs), p.metrics.failedCalls,
            ]),
          }}
        >
          <RankedBars rows={purposes} metric={metric} />
        </ChartCard>

        <ChartCard
          title="Cơ cấu token" description="Token vào (prompt gửi đi) so với token ra (câu AI viết)." loading={loading}
          table={{
            columns: ["Loại token", "Số token", "Tỉ trọng"],
            rows: total
              ? (() => {
                  const sum = total.inputTokens + total.outputTokens;
                  return [
                    ["Vào", total.inputTokens, formatPct(ratio(total.inputTokens, sum))],
                    ["Ra", total.outputTokens, formatPct(ratio(total.outputTokens, sum))],
                  ];
                })()
              : [],
          }}
        >
          {total ? <TokenSegmentedBar metrics={total} /> : null}
        </ChartCard>

        <ChartCard
          title="Tỉ lệ lỗi theo tính năng" description="Tính năng nào hay hỏng, xếp từ cao xuống thấp." loading={loading}
          table={{
            columns: ["Tính năng", "Lần gọi", "Lỗi", "Tỉ lệ lỗi"],
            rows: purposes.map((p) => [p.label, p.metrics.calls, p.metrics.failedCalls, formatPct(ratio(p.metrics.failedCalls, p.metrics.calls))]),
          }}
        >
          <FailureMeters rows={purposes} />
        </ChartCard>

        <ChartCard
          title="Nặng và chậm: tính năng nào?" loading={loading}
          description="Ngang: token mỗi lần gọi. Dọc: thời gian trung bình. Cỡ bong bóng: số lần gọi."
          table={{
            columns: ["Tính năng", "Lần gọi", "Token mỗi lần", "Thời gian TB", "Chi phí"],
            rows: purposes
              .filter((p) => p.metrics.calls > 0)
              .map((p) => [p.label, p.metrics.calls, Math.round(totalTokens(p.metrics) / p.metrics.calls), formatMs(p.metrics.avgLatencyMs), formatUsd(p.metrics.costUsd)]),
          }}
        >
          <LatencyBubbleChart rows={purposes} />
        </ChartCard>

        <ChartCard
          className="lg:col-span-2" title="Lịch hoạt động" description="Mỗi ô một ngày; màu đậm hơn là nhiều lần gọi hơn." loading={loading}
          table={{
            columns: ["Ngày", "Lần gọi", "Chi phí", "Lỗi"],
            rows: daily.map((p) => [p.date, p.calls, formatUsd(p.cost), p.failed]),
          }}
        >
          <CalendarHeatmap data={daily} />
        </ChartCard>
      </div>

      <UsersPanel range={range} />
      <PricingPanel prices={prices} />
    </div>
  );
}
