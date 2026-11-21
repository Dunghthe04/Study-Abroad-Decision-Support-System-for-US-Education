"use client";

import { useMemo, useState } from "react";
import { TriangleAlertIcon } from "lucide-react";
import "@/components/admin/ai-usage/viz.css";
import {
  FailedStepBars, FeatureOutcomeBars, LatencyLinesChart, OutcomeColumnsChart,
} from "@/components/admin/ai-quality/charts";
import { SamplesPanel } from "@/components/admin/ai-quality/SamplesPanel";
import { ChartCard, Segmented, StatTile } from "@/components/admin/ai-usage/parts";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { adminAiQualityApi } from "@/lib/ai-quality-api";
import {
  dayPoints, fallbackRate, featureLabel, hasUnclassified, OUTCOMES, outcomeName, rateOf, stepLabel,
} from "@/lib/ai-quality-format";
import type { DateRange } from "@/lib/ai-usage-api";
import {
  changeRatio, daysBetween, formatDayFull, formatInt, formatMs, formatPct, MAX_RANGE_DAYS, previousRange, rangeOfLastDays,
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

const msOrDash = (ms: number | null) => (ms === null ? "—" : formatMs(ms));

/**
 * Trang admin: chất lượng AI (UC-90). Chỉ admin. Phần số liệu chỉ là phép đếm, không có nội dung chat; phần hội thoại mẫu
 * (SamplesPanel) là dữ liệu cá nhân đã che. Một hàng bộ lọc phía trên áp cho mọi thẻ.
 */
export function AiQualityView() {
  const [preset, setPreset] = useState<Preset>("30");
  const [applied, setApplied] = useState<DateRange>(() => rangeOfLastDays(30));
  const [draft, setDraft] = useState<DateRange>(() => rangeOfLastDays(30));
  const [draftError, setDraftError] = useState<string | null>(null);

  const range = useMemo<DateRange>(
    () => (preset === "custom" ? applied : rangeOfLastDays(Number(preset))),
    [preset, applied],
  );
  const rangeKey = `${range.from}|${range.to}`;
  const previous = useMemo(() => previousRange(range), [range]);

  const current = useFetched(`quality|${rangeKey}`, () => adminAiQualityApi.report(range));
  const before = useFetched(`quality-prev|${rangeKey}`, () => adminAiQualityApi.report(previous));

  const report = current.data;
  const prior = before.data;
  const total = report?.total ?? null;
  const priorTotal = prior?.total ?? null;
  const days = useMemo(() => (report ? dayPoints(report.byDay) : []), [report]);
  const loading = current.loading;

  const applyDraft = () => {
    const problem = validateRange(draft);
    setDraftError(problem);
    if (!problem) setApplied(draft);
  };

  // Thay đổi so với kỳ trước tính trên TỈ LỆ (không phải số lượt), vì hai kỳ có thể có số lượt rất khác nhau
  const rateChange = (key: "answered" | "noAnswer" | "refused" | "failed") =>
    total && priorTotal ? changeRatio(rateOf(total, key), rateOf(priorTotal, key)) : undefined;
  const slmRate = report ? fallbackRate(report.slmFallbackTurns, report.total.total) : 0;
  const callRate = report ? fallbackRate(report.fallbackTurns, report.total.total) : 0;
  const priorSlmRate = prior ? fallbackRate(prior.slmFallbackTurns, prior.total.total) : 0;
  const priorCallRate = prior ? fallbackRate(prior.fallbackTurns, prior.total.total) : 0;

  return (
    <div className="ai-viz flex flex-col gap-4">
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
      {current.error ? <Alert variant="destructive"><AlertDescription>{current.error}</AlertDescription></Alert> : null}

      {hasUnclassified(report) ? (
        <Alert variant="warning">
          <TriangleAlertIcon />
          <AlertDescription>
            {formatInt(report!.total.unclassified)} lượt chưa có kết quả (tin cũ từ trước khi hệ thống ghi kết quả). Các lượt này nằm trong tổng
            nhưng không thuộc loại nào trong bốn loại bên dưới.
          </AlertDescription>
        </Alert>
      ) : null}

      <section aria-label="Tổng quan" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Tổng lượt chat" value={total ? formatInt(total.total) : "—"} loading={loading}
            change={total && priorTotal ? changeRatio(total.total, priorTotal.total) : undefined}
            trend={days.map((point) => point.total)}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label={outcomeName("answered")} value={total ? formatPct(total.answeredRate) : "—"} loading={loading} upIs="good"
            change={rateChange("answered")} note={total ? `${formatInt(total.answered)} lượt` : undefined}
            trend={days.map((point) => (point.total > 0 ? point.answered / point.total : 0))}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label={outcomeName("noAnswer")} value={total ? formatPct(total.noAnswerRate) : "—"} loading={loading} upIs="bad"
            change={rateChange("noAnswer")} note={total ? `${formatInt(total.noAnswer)} lượt` : undefined}
            trend={days.map((point) => (point.total > 0 ? point.noAnswer / point.total : 0))}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label={outcomeName("refused")} value={total ? formatPct(total.refusedRate) : "—"} loading={loading}
            change={rateChange("refused")} note={total ? `${formatInt(total.refused)} lượt` : undefined}
            trend={days.map((point) => (point.total > 0 ? point.refused / point.total : 0))}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label={outcomeName("failed")} value={total ? formatPct(total.failedRate) : "—"} loading={loading} upIs="bad"
            change={rateChange("failed")} note={total ? `${formatInt(total.failed)} lượt` : undefined}
            trend={days.map((point) => (point.total > 0 ? point.failed / point.total : 0))}
          />
        </div>
        <div className="lg:col-span-1 [&>*]:h-full">
          <StatTile
            label="Thời gian phản hồi" value={report ? msOrDash(report.latency.p50Ms) : "—"} loading={loading} upIs="bad"
            change={report && prior && report.latency.p50Ms !== null && prior.latency.p50Ms !== null ? changeRatio(report.latency.p50Ms, prior.latency.p50Ms) : undefined}
            note={report ? `thường gặp · chậm nhất 5%: ${msOrDash(report.latency.p95Ms)}` : undefined}
          />
        </div>
      </section>

      <section aria-label="Đường dự phòng" className="grid gap-3 sm:grid-cols-2">
        <StatTile
          label="SLM phải dùng kết quả dự phòng" value={report ? formatPct(slmRate) : "—"} loading={loading} upIs="bad"
          change={report && prior ? changeRatio(slmRate, priorSlmRate) : undefined}
          note={report ? `${formatInt(report.slmFallbackTurns)} lượt: model đọc tin nhắn sai khuôn hai lần` : undefined}
        />
        <StatTile
          label="Lượt phải dùng lời dự phòng" value={report ? formatPct(callRate) : "—"} loading={loading} upIs="bad"
          change={report && prior ? changeRatio(callRate, priorCallRate) : undefined}
          note={report ? `${formatInt(report.fallbackTurns)} lượt: câu do code dựng thay cho câu AI viết` : undefined}
        />
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <ChartCard
          className="lg:col-span-2" title="Kết quả theo ngày" loading={loading}
          description="Mỗi cột là một ngày, chia theo bốn loại kết quả của lượt chat. Cột nâng lên màu lỗi hoặc không trả lời được là dấu hiệu cần xem."
          table={{
            columns: ["Ngày", "Tổng", ...OUTCOMES.map((o) => outcomeName(o.key)), "Chưa phân loại"],
            rows: days.map((p) => [p.date, p.total, p.answered, p.noAnswer, p.refused, p.failed, p.unclassified]),
          }}
        >
          <OutcomeColumnsChart data={days} />
        </ChartCard>

        <ChartCard
          title="Thời gian phản hồi theo ngày" loading={loading}
          description="Thường gặp (p50) và chậm nhất 5% (p95). Trung bình dễ che đi các lượt chậm nên không dùng."
          table={{
            columns: ["Ngày", "Số lượt", "p50", "p95"],
            rows: days.map((p) => [p.date, p.total, msOrDash(p.p50), msOrDash(p.p95)]),
          }}
        >
          <LatencyLinesChart data={days} />
        </ChartCard>

        <ChartCard
          title="Kết quả theo tính năng" loading={loading}
          description="Mỗi thanh là toàn bộ lượt của một loại câu hỏi. Tính năng nào có nhiều không trả lời được hoặc lỗi hơn?"
          table={{
            columns: ["Tính năng", "Tổng", ...OUTCOMES.map((o) => outcomeName(o.key)), "p95"],
            rows: (report?.byFeature ?? []).map((f) => [
              featureLabel(f.feature), f.counts.total,
              ...OUTCOMES.map((o) => `${f.counts[o.key]} (${formatPct(rateOf(f.counts, o.key))})`),
              msOrDash(f.latency.p95Ms),
            ]),
          }}
        >
          <FeatureOutcomeBars rows={report?.byFeature ?? []} />
        </ChartCard>

        <ChartCard
          className="lg:col-span-2" title="Bước AI hay hỏng" loading={loading}
          description="Số lượt có bước đó bị hỏng (hết thời gian, bị từ chối, trả sai khuôn, hoặc advisor không phản hồi). Mỗi lượt chỉ đếm một lần cho mỗi bước."
          table={{
            columns: ["Bước", "Tên gốc", "Số lượt"],
            rows: (report?.failedSteps ?? []).map((s) => [stepLabel(s.step), s.step, s.turns]),
          }}
        >
          <FailedStepBars rows={report?.failedSteps ?? []} />
        </ChartCard>
      </div>

      <SamplesPanel key={rangeKey} range={range} counts={total} />
    </div>
  );
}
