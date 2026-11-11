"use client";

import { useState } from "react";
import { EyeIcon } from "lucide-react";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { UserUsageSheet } from "@/components/admin/ai-usage/UserUsageSheet";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { adminAiUsageApi, type DateRange } from "@/lib/ai-usage-api";
import { formatCompact, formatInt, formatMs, formatPct, formatUsd, ratio } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { AiUsageUserRank } from "@/types/ai-usage";

const PAGE_SIZE = 10;

/** Bảng xếp hạng người dùng theo token. Bấm một người để xem từng lượt chat của họ (chỉ số đo, không có nội dung chat). */
export function UsersPanel({ range }: { range: DateRange }) {
  const rangeKey = `${range.from}|${range.to}`;
  // Đổi khoảng ngày thì về trang 1: trang được nhớ kèm khoảng ngày của nó
  const [pageState, setPageState] = useState({ rangeKey, page: 1 });
  const page = pageState.rangeKey === rangeKey ? pageState.page : 1;
  const [selected, setSelected] = useState<AiUsageUserRank | null>(null);

  const users = useFetched(`users|${rangeKey}|${page}`, () => adminAiUsageApi.users(range, page, PAGE_SIZE));
  const list = users.data;

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Người dùng dùng nhiều AI nhất</h2>
        </CardTitle>
        <CardDescription>
          Xếp theo token vào và ra. Chỉ gồm các lần gọi có tài khoản; bấm &ldquo;Xem&rdquo; để xem từng lượt chat. Không hiện nội dung chat.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {users.error ? <Alert variant="destructive"><AlertDescription>{users.error}</AlertDescription></Alert> : null}
        <div data-loading={users.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Người dùng</TableHead>
                <TableHead className="text-right">Lần gọi</TableHead>
                <TableHead className="text-right">Token vào</TableHead>
                <TableHead className="text-right">Token ra</TableHead>
                <TableHead className="text-right">Chi phí</TableHead>
                <TableHead className="text-right">Lỗi</TableHead>
                <TableHead className="text-right">TB / lần</TableHead>
                <TableHead><span className="sr-only">Xem</span></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {list?.items.map((user) => (
                <TableRow key={user.userId}>
                  <TableCell>
                    <p className="font-medium text-ink">{user.fullName ?? "(tài khoản đã xóa)"}</p>
                    {user.email ? <p className="text-xs text-ink-3">{user.email}</p> : null}
                  </TableCell>
                  <TableCell className="tabular text-right">{formatInt(user.metrics.calls)}</TableCell>
                  <TableCell className="tabular text-right">{formatCompact(user.metrics.inputTokens)}</TableCell>
                  <TableCell className="tabular text-right">{formatCompact(user.metrics.outputTokens)}</TableCell>
                  <TableCell className="tabular text-right">
                    {formatUsd(user.metrics.costUsd)}
                    {user.metrics.unpricedCalls > 0 ? <span className="block text-xs text-warn">+{user.metrics.unpricedCalls} chưa có giá</span> : null}
                  </TableCell>
                  <TableCell className="tabular text-right">
                    {formatInt(user.metrics.failedCalls)}
                    <span className="block text-xs text-ink-3">{formatPct(ratio(user.metrics.failedCalls, user.metrics.calls))}</span>
                  </TableCell>
                  <TableCell className="tabular text-right">{formatMs(user.metrics.avgLatencyMs)}</TableCell>
                  <TableCell className="text-right">
                    <Button type="button" variant="ghost" size="sm" onClick={() => setSelected(user)}>
                      <EyeIcon /> Xem
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {list && list.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} className="py-8 text-center text-ink-2">Chưa có người dùng nào gọi AI trong khoảng này.</TableCell>
                </TableRow>
              ) : null}
              {!list ? (
                <TableRow>
                  <TableCell colSpan={8} className="py-8 text-center text-ink-2">{users.loading ? "Đang tải..." : "Không có dữ liệu."}</TableCell>
                </TableRow>
              ) : null}
            </TableBody>
          </Table>
        </div>
        {list ? (
          <TablePagination
            page={list.page} totalPages={list.totalPages} totalCount={list.totalCount} itemLabel="người dùng"
            disabled={users.loading} onPageChange={(next) => setPageState({ rangeKey, page: next })}
          />
        ) : null}
      </CardContent>
      <UserUsageSheet user={selected} range={range} onClose={() => setSelected(null)} />
    </Card>
  );
}
