"use client";

import { DownloadIcon, GitCompareArrowsIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatInt } from "@/lib/ai-usage-format";
import {
  formatRunTime, KIND_TITLES, LABELS, paramValue, pct, pointAt, rateOf, STEP_COLUMNS, stepLabel,
} from "@/lib/sensitivity-format";
import type { SensitivityKind, SensitivityLabel, SensitivityParameterResult, SensitivityRunDetail } from "@/types/sensitivity";

export function LabelBadge({ label }: { label: SensitivityLabel | null }) {
  if (!label) return <Badge variant="neutral">Không gắn nhãn</Badge>;
  return <Badge variant={LABELS[label].variant}>{LABELS[label].name}</Badge>;
}

interface RunResultProps {
  run: SensitivityRunDetail;
  loading: boolean;
  downloading: boolean;
  onCompare: () => void;
  onDownload: () => void;
}

/** Kết quả một lần chạy: thông tin điều kiện đo, rồi hai bảng (tham số xếp nhóm, trọng số fit) với từng mức nhích và nhãn. */
export function RunResult({ run, loading, downloading, onCompare, onDownload }: RunResultProps) {
  const { summary, report, request } = run;
  const columns = STEP_COLUMNS.filter((step) => request.steps.some((s) => Math.abs(s - Math.abs(step)) < 1e-9));

  return (
    <Card data-loading={loading ? "true" : "false"}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Kết quả lần chạy {formatRunTime(summary.createdAt)}</h2>
        </CardTitle>
        <CardDescription>
          {summary.createdByName ?? (summary.createdByUserId ? "—" : "Tài khoản đã xóa")} ·{formatInt(report.profileCount)} hồ sơ giả lập (bộ {report.profileSetVersion}) ·{" "}
          {formatInt(report.schoolCount)} trường · thuật toán {report.algorithmVersion} · ngưỡng nhãn {pct(report.stableBelow, 0)} và{" "}
          {pct(report.watchUpTo, 0)} · chạy {(summary.durationMs / 1000).toFixed(1).replace(".", ",")} giây
        </CardDescription>
        <CardAction className="flex gap-2">
          <Button type="button" variant="outline" size="sm" onClick={onCompare}>
            <GitCompareArrowsIcon data-icon="inline-start" />
            So với lần trước
          </Button>
          <Button type="button" variant="outline" size="sm" onClick={onDownload} loading={downloading}>
            <DownloadIcon data-icon="inline-start" />
            Tải CSV
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <div className="flex flex-wrap gap-2">
          <Badge variant="ok">{summary.stableCount} Ổn định</Badge>
          <Badge variant="warn">{summary.watchCount} Cần theo dõi</Badge>
          <Badge variant="risk">{summary.sensitiveCount} Nhạy</Badge>
        </div>
        {(["grouping", "fit_weight"] as SensitivityKind[]).map((kind) => {
          const rows = report.parameters.filter((p) => p.kind === kind);
          if (rows.length === 0) return null;
          return <ResultTable key={kind} kind={kind} rows={rows} columns={columns} />;
        })}
      </CardContent>
    </Card>
  );
}

function ResultTable({ kind, rows, columns }: { kind: SensitivityKind; rows: SensitivityParameterResult[]; columns: number[] }) {
  return (
    <section className="flex flex-col gap-2">
      <div>
        <h3 className="text-sm font-semibold text-ink">{KIND_TITLES[kind].title}</h3>
        <p className="max-w-4xl text-xs text-ink-3">{KIND_TITLES[kind].explain}</p>
      </div>
      <div className="overflow-x-auto rounded-md border border-line">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tham số</TableHead>
              <TableHead className="text-right">Gốc</TableHead>
              {columns.map((step) => (
                <TableHead key={step} className="text-right">{stepLabel(step)}</TableHead>
              ))}
              <TableHead>Nhãn</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.key}>
                <TableCell className="font-medium">
                  {row.name}
                  <div className="text-xs font-normal text-ink-3">{row.key}</div>
                </TableCell>
                <TableCell className="tabular text-right">{paramValue(row.baseline)}</TableCell>
                {columns.map((step) => {
                  const point = pointAt(row, step);
                  if (!point) return <TableCell key={step} className="text-right">—</TableCell>;
                  const direction = kind === "grouping" && point.changedPairs > 0
                    ? `${formatInt(point.movedSafer)} cặp dễ hơn · ${formatInt(point.movedHarder)} cặp khó hơn`
                    : `${formatInt(kind === "grouping" ? point.changedPairs : point.changedSlots)}/${formatInt(kind === "grouping" ? point.pairs : point.slots)}`;
                  return (
                    <TableCell key={step} className="tabular text-right" title={direction}>
                      <div className="font-medium text-ink">{pct(rateOf(kind, point))}</div>
                      <div className="text-xs text-ink-3">
                        {paramValue(point.value)}
                        {point.clamped ? " (đã kẹp)" : ""}
                      </div>
                    </TableCell>
                  );
                })}
                <TableCell>
                  <LabelBadge label={row.label} />
                  {row.rate != null ? <div className="mt-0.5 text-xs text-ink-3">{pct(row.rate)}</div> : null}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </section>
  );
}
