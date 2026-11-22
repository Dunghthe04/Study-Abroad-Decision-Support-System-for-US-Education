"use client";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatRunTime, STEP_OPTIONS } from "@/lib/sensitivity-format";
import type { SensitivityRunListRow } from "@/types/sensitivity";

interface RunHistoryProps {
  runs: SensitivityRunListRow[] | null;
  loading: boolean;
  selectedId: string | null;
  onSelect: (id: string) => void;
  /** So lần đang xem với lần chạy này. */
  onCompareWith: (id: string) => void;
}

const stepsText = (steps: number[]) =>
  STEP_OPTIONS.filter((o) => steps.some((s) => Math.abs(s - o.value) < 1e-9)).map((o) => o.label).join(", ");

/** Lịch sử các lần chạy (mới nhất trước). Bấm Xem để mở kết quả, So sánh để so với lần đang xem. */
export function RunHistory({ runs, loading, selectedId, onSelect, onCompareWith }: RunHistoryProps) {
  return (
    <Card data-loading={loading ? "true" : "false"}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Lịch sử phân tích</h2>
        </CardTitle>
        <CardDescription>50 lần chạy gần nhất. Mọi lần chạy và mọi lần tải CSV đều được ghi nhật ký.</CardDescription>
      </CardHeader>
      <CardContent>
        {runs && runs.length === 0 ? (
          <p className="text-sm text-ink-2">Chưa có lần phân tích nào. Chọn tham số ở trên rồi bấm Chạy phân tích.</p>
        ) : (
          <div className="overflow-x-auto rounded-md border border-line">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Thời gian</TableHead>
                  <TableHead>Người chạy</TableHead>
                  <TableHead className="text-right">Số tham số</TableHead>
                  <TableHead>Mức nhích</TableHead>
                  <TableHead>Nhãn</TableHead>
                  <TableHead className="text-right">Số trường</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(runs ?? []).map((run) => {
                  const current = run.id === selectedId;
                  return (
                    <TableRow key={run.id} data-state={current ? "selected" : undefined}>
                      <TableCell className="tabular">{formatRunTime(run.createdAt)}</TableCell>
                      <TableCell>{run.createdByName ?? (run.createdByUserId ? "—" : "Tài khoản đã xóa")}</TableCell>
                      <TableCell className="tabular text-right">{run.parameterKeys.length}</TableCell>
                      <TableCell>{stepsText(run.steps)}</TableCell>
                      <TableCell className="text-xs text-ink-2">
                        {run.stableCount} Ổn định · {run.watchCount} Theo dõi · {run.sensitiveCount} Nhạy
                      </TableCell>
                      <TableCell className="tabular text-right">{run.schoolCount}</TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-1">
                          <Button type="button" size="xs" variant={current ? "secondary" : "ghost"} onClick={() => onSelect(run.id)} disabled={current}>
                            {current ? "Đang xem" : "Xem"}
                          </Button>
                          {!current && selectedId ? (
                            <Button type="button" size="xs" variant="ghost" onClick={() => onCompareWith(run.id)}>
                              So sánh
                            </Button>
                          ) : null}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
