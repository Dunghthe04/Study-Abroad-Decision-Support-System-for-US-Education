"use client";

import { useState } from "react";
import { PolicyContent } from "@/components/policy/PolicyContent";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { adminPoliciesApi } from "@/lib/admin-policies-api";
import { STATUS_LABELS, formatDateTime } from "@/lib/policy-format";
import { useFetched } from "@/lib/use-fetched";
import type { PolicyDocumentSummary } from "@/types/policy";

interface PolicyHistoryProps {
  rows: PolicyDocumentSummary[] | null;
  loading: boolean;
}

/** Lịch sử các phiên bản của một loại; bấm Xem để đọc toàn văn của bất kỳ bản nào (chỉ đọc). */
export function PolicyHistory({ rows, loading }: PolicyHistoryProps) {
  const [viewing, setViewing] = useState<string | null>(null);

  return (
    <Card data-loading={loading ? "true" : "false"}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Lịch sử phiên bản</h2>
        </CardTitle>
        <CardDescription>Bản đã công bố không bao giờ bị sửa hay xóa: người dùng đã đồng ý đúng nội dung đó.</CardDescription>
      </CardHeader>
      <CardContent>
        {rows && rows.length === 0 ? (
          <p className="text-sm text-ink-2">Chưa có phiên bản nào. Bấm Soạn bản mới để bắt đầu.</p>
        ) : (
          <div className="overflow-x-auto rounded-md border border-line">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Phiên bản</TableHead>
                  <TableHead>Trạng thái</TableHead>
                  <TableHead>Tóm tắt thay đổi</TableHead>
                  <TableHead>Công bố lúc</TableHead>
                  <TableHead>Hết hiệu lực lúc</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(rows ?? []).map((row) => (
                  <TableRow key={row.id}>
                    <TableCell className="tabular font-medium">{row.version ?? "—"}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        <Badge variant={STATUS_LABELS[row.status].variant}>{STATUS_LABELS[row.status].name}</Badge>
                        {row.requiresReconsent && row.status !== "draft" ? <Badge variant="brand">Đã yêu cầu đồng ý lại</Badge> : null}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-sm text-ink-2">{row.changeSummary ?? "—"}</TableCell>
                    <TableCell className="tabular">{formatDateTime(row.publishedAt)}</TableCell>
                    <TableCell className="tabular">{formatDateTime(row.retiredAt)}</TableCell>
                    <TableCell className="text-right">
                      <Button type="button" size="xs" variant="ghost" onClick={() => setViewing(row.id)}>Xem</Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
      <VersionViewer id={viewing} onClose={() => setViewing(null)} />
    </Card>
  );
}

function VersionViewer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const doc = useFetched(`policy-version|${id ?? ""}`, () => (id ? adminPoliciesApi.get(id) : Promise.resolve(null)));
  const shown = doc.data && doc.data.id === id ? doc.data : null;

  return (
    <Dialog open={id !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{shown ? shown.title : "Đang tải..."}</DialogTitle>
          <DialogDescription>
            {shown
              ? `${shown.version ? `Phiên bản ${shown.version}` : "Bản nháp"} · ${STATUS_LABELS[shown.status].name}${shown.publishedAt ? ` · công bố ${formatDateTime(shown.publishedAt)}` : ""} · chỉ đọc`
              : null}
          </DialogDescription>
        </DialogHeader>
        {doc.error && !shown ? <Alert variant="destructive"><AlertDescription>{doc.error}</AlertDescription></Alert> : null}
        {shown ? (
          shown.content.trim() ? <PolicyContent content={shown.content} /> : <p className="text-sm text-ink-3">Chưa có nội dung.</p>
        ) : doc.error ? null : (
          <div className="flex flex-col gap-2"><Skeleton className="h-4 w-2/3" /><Skeleton className="h-4 w-full" /><Skeleton className="h-4 w-5/6" /></div>
        )}
      </DialogContent>
    </Dialog>
  );
}
