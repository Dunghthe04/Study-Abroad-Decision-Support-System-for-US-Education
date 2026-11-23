"use client";

import { TriangleAlertIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LabelBadge } from "@/components/admin/sensitivity/RunResult";
import { deltaPoints, formatRunTime, paramValue, pct, WARNING_TEXT } from "@/lib/sensitivity-format";
import type { SensitivityComparison } from "@/types/sensitivity";

interface ComparisonPanelProps {
  comparison: SensitivityComparison | null;
  loading: boolean;
  error: string | null;
  onClose: () => void;
}

/** So hai lần chạy, luôn đọc "trước → sau". Chưa có lần trước (AF-5) thì báo rỗng. */
export function ComparisonPanel({ comparison, loading, error, onClose }: ComparisonPanelProps) {
  return (
    <Card data-loading={loading ? "true" : "false"}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">So sánh hai lần chạy</h2>
        </CardTitle>
        {comparison?.other ? (
          <CardDescription>
            Lần trước {formatRunTime(comparison.base.createdAt)} → lần sau {formatRunTime(comparison.other.createdAt)}. Chênh lệch tính
            bằng điểm phần trăm, lấy lần sau trừ lần trước.
          </CardDescription>
        ) : null}
        <CardAction>
          <Button type="button" variant="ghost" size="icon-sm" onClick={onClose} aria-label="Đóng so sánh">
            <XIcon />
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
        {comparison && !comparison.hasEarlierRun ? (
          <p className="text-sm text-ink-2">Chưa có lần chạy nào trước lần này để so sánh.</p>
        ) : null}
        {comparison?.warnings.map((w) => (
          <Alert key={w} variant="warning">
            <TriangleAlertIcon />
            <AlertDescription>{WARNING_TEXT[w]}</AlertDescription>
          </Alert>
        ))}
        {comparison && comparison.parameters.length > 0 ? (
          <div className="overflow-x-auto rounded-md border border-line">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Tham số</TableHead>
                  <TableHead className="text-right">Lần trước</TableHead>
                  <TableHead className="text-right">Lần sau</TableHead>
                  <TableHead className="text-right">Chênh lệch</TableHead>
                  <TableHead>Nhãn trước → sau</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {comparison.parameters.map((p) => (
                  <TableRow key={p.key}>
                    <TableCell className="font-medium">
                      {p.name}
                      {p.baselineChanged ? (
                        <div className="text-xs font-normal text-warn">
                          Giá trị gốc đã đổi: {paramValue(p.baseBaseline)} → {paramValue(p.otherBaseline)}
                        </div>
                      ) : null}
                    </TableCell>
                    <TableCell className="tabular text-right">{p.baseRate == null ? "Không chọn" : pct(p.baseRate)}</TableCell>
                    <TableCell className="tabular text-right">{p.otherRate == null ? "Không chọn" : pct(p.otherRate)}</TableCell>
                    <TableCell className="tabular text-right">{deltaPoints(p.delta)}</TableCell>
                    <TableCell>
                      <span className="inline-flex items-center gap-1.5">
                        <LabelBadge label={p.baseLabel} />
                        →
                        <LabelBadge label={p.otherLabel} />
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
