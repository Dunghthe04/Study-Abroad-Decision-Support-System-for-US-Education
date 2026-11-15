"use client";

import { Fragment, useState } from "react";
import { ChevronDownIcon, ChevronRightIcon } from "lucide-react";
import "@/components/admin/ai-usage/viz.css";
import { CallsColumnsChart } from "@/components/admin/ai-usage/charts-time";
import { RankedBars } from "@/components/admin/ai-usage/charts-share";
import { ChartCard, Segmented, StatTile } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { adminAiUsageApi, type DateRange } from "@/lib/ai-usage-api";
import {
  dailySeries, formatCompact, formatDateTime, formatInt, formatMs, formatPct, formatUsd, kindLabel, purposeLabel,
  purposeRows, PURPOSE_METRICS, ratio, totalTokens, type PurposeMetric,
} from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { AiUsageUserRank } from "@/types/ai-usage";

const TURN_PAGE_SIZE = 10;

const ROLE_LABELS: Record<string, string> = { assistant: "Trả lời", system: "Lỗi hệ thống" };

interface Props {
  user: AiUsageUserRank | null;
  range: DateRange;
  onClose: () => void;
}

/** Chi tiết dùng AI của một người: tổng quan, theo ngày, theo tính năng và từng lượt chat (bấm để xem từng lần gọi). */
export function UserUsageSheet({ user, range, onClose }: Props) {
  return (
    <Sheet open={user !== null} onOpenChange={(open) => { if (!open) onClose(); }}>
      <SheetContent side="right" className="w-full gap-0 overflow-y-auto sm:max-w-3xl">
        {/* Sheet vẽ ra ngoài khung trang nên tự mang bộ màu biểu đồ */}
        <div className="ai-viz flex flex-col gap-4 p-4">
          {user ? <Body key={user.userId} user={user} range={range} /> : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}

function Body({ user, range }: { user: AiUsageUserRank; range: DateRange }) {
  const rangeKey = `${range.from}|${range.to}`;
  const overview = useFetched(`user|${user.userId}|${rangeKey}`, () => adminAiUsageApi.userOverview(user.userId, range));
  const [metric, setMetric] = useState<PurposeMetric>("cost");
  const data = overview.data?.overview ?? null;
  const total = data?.total ?? user.metrics;
  const daily = data ? dailySeries(data) : [];
  const purposes = data ? purposeRows(data.byPurpose) : [];

  return (
    <>
      <SheetHeader className="p-0">
        <SheetTitle>{user.fullName ?? "(tài khoản đã xóa)"}</SheetTitle>
        <SheetDescription>
          {user.email ?? "Không có email"} · từ {range.from} đến {range.to} (UTC). Chỉ số đo, không có nội dung chat.
        </SheetDescription>
      </SheetHeader>

      {overview.error ? <Alert variant="destructive"><AlertDescription>{overview.error}</AlertDescription></Alert> : null}

      <div className="grid grid-cols-2 gap-3">
        <StatTile label="Chi phí" value={formatUsd(total.costUsd)} loading={overview.loading} note={total.unpricedCalls > 0 ? <span className="text-warn">+{total.unpricedCalls} chưa có giá</span> : undefined} />
        <StatTile label="Lần gọi" value={formatInt(total.calls)} loading={overview.loading} note={`${formatInt(total.failedCalls)} lỗi`} />
        <StatTile label="Token vào + ra" value={formatCompact(totalTokens(total))} loading={overview.loading} />
        <StatTile label="Thời gian TB" value={formatMs(total.avgLatencyMs)} loading={overview.loading} />
      </div>

      <ChartCard
        title="Lần gọi theo ngày" loading={overview.loading}
        table={{
          columns: ["Ngày", "Cloud", "Local", "Lỗi"],
          rows: daily.map((p) => [p.date, p.cloudCalls, p.localCalls, p.failed]),
        }}
      >
        {daily.length > 0 ? <CallsColumnsChart data={daily} /> : null}
      </ChartCard>

      <ChartCard
        title="Theo tính năng" loading={overview.loading}
        controls={<Segmented label="Chỉ số" value={metric} options={PURPOSE_METRICS} onChange={setMetric} />}
        table={{
          columns: ["Tính năng", "Nguồn", "Lần gọi", "Token vào", "Token ra", "Chi phí"],
          rows: purposes.map((p) => [p.label, kindLabel(p.kind), p.metrics.calls, p.metrics.inputTokens, p.metrics.outputTokens, formatUsd(p.metrics.costUsd)]),
        }}
      >
        <RankedBars rows={purposes} metric={metric} />
      </ChartCard>

      <Turns user={user} range={range} />
    </>
  );
}

// ------------------------------------------------------------------ các lượt chat của một người
function Turns({ user, range }: { user: AiUsageUserRank; range: DateRange }) {
  const rangeKey = `${range.from}|${range.to}`;
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);
  const turns = useFetched(`turns|${user.userId}|${rangeKey}|${page}`, () => adminAiUsageApi.userTurns(user.userId, range, page, TURN_PAGE_SIZE));
  const list = turns.data;

  return (
    <section className="flex flex-col gap-2">
      <h3 className="text-base font-semibold text-ink">Từng lượt chat</h3>
      <p className="text-xs text-ink-3">Bấm một lượt để xem từng lần gọi AI trong lượt đó. Nội dung chat nằm ở hàng đợi câu hỏi (có ghi lại người xem).</p>
      {turns.error ? <Alert variant="destructive"><AlertDescription>{turns.error}</AlertDescription></Alert> : null}
      <div data-loading={turns.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-8"><span className="sr-only">Mở</span></TableHead>
              <TableHead>Lượt</TableHead>
              <TableHead className="text-right">Token vào / ra</TableHead>
              <TableHead className="text-right">Chi phí</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((turn) => {
              const open = openId === turn.messageId;
              return (
                <Fragment key={turn.messageId}>
                  <TableRow className="cursor-pointer" onClick={() => setOpenId(open ? null : turn.messageId)}>
                    <TableCell>
                      <button
                        type="button" aria-expanded={open} aria-label={open ? "Thu gọn lượt này" : "Xem từng lần gọi của lượt này"}
                        className="rounded outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        onClick={(event) => { event.stopPropagation(); setOpenId(open ? null : turn.messageId); }}
                      >
                        {open ? <ChevronDownIcon className="size-4" /> : <ChevronRightIcon className="size-4" />}
                      </button>
                    </TableCell>
                    <TableCell>
                      <span className="tabular block">{formatDateTime(turn.createdAt)}</span>
                      <span className="block text-xs text-ink-3">
                        {ROLE_LABELS[turn.role] ?? turn.role}{turn.outcome ? ` · ${turn.outcome}` : ""}
                      </span>
                    </TableCell>
                    <TableCell className="tabular text-right">
                      <span className="block">{formatInt(turn.metrics.inputTokens)} / {formatInt(turn.metrics.outputTokens)}</span>
                      <span className="block text-xs text-ink-3">
                        {formatInt(turn.metrics.calls)} lần gọi{turn.metrics.failedCalls > 0 ? `, ${formatInt(turn.metrics.failedCalls)} lỗi` : ""}
                      </span>
                    </TableCell>
                    <TableCell className="tabular text-right">{formatUsd(turn.metrics.costUsd)}</TableCell>
                  </TableRow>
                  {open ? (
                    <TableRow>
                      <TableCell colSpan={4} className="bg-ground p-2"><TurnCalls messageId={turn.messageId} /></TableCell>
                    </TableRow>
                  ) : null}
                </Fragment>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow><TableCell colSpan={4} className="py-6 text-center text-ink-2">Người này chưa có lượt chat nào trong khoảng này.</TableCell></TableRow>
            ) : null}
            {!list ? (
              <TableRow><TableCell colSpan={4} className="py-6 text-center text-ink-2">{turns.loading ? "Đang tải..." : "Không có dữ liệu."}</TableCell></TableRow>
            ) : null}
          </TableBody>
        </Table>
      </div>
      {list ? (
        <TablePagination
          page={list.page} totalPages={list.totalPages} totalCount={list.totalCount} itemLabel="lượt"
          disabled={turns.loading} onPageChange={(next) => { setPage(next); setOpenId(null); }}
        />
      ) : null}
    </section>
  );
}

function TurnCalls({ messageId }: { messageId: string }) {
  const turn = useFetched(`turn|${messageId}`, () => adminAiUsageApi.turn(messageId));
  if (turn.error) return <Alert variant="destructive"><AlertDescription>{turn.error}</AlertDescription></Alert>;
  if (!turn.data) return <p className="p-2 text-sm text-ink-2">Đang tải...</p>;
  if (turn.data.calls.length === 0) return <p className="p-2 text-sm text-ink-2">Lượt này không có lần gọi AI nào được ghi.</p>;
  return (
    <div className="flex flex-col gap-2">
      {turn.data.calls.map((call) => (
        <article key={call.id} className="rounded-md border border-line bg-surface p-3">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <p className="font-medium text-ink">{purposeLabel(call.purpose)}</p>
              <p className="truncate text-xs text-ink-3">{call.purpose ?? "—"}{call.skillVersion ? ` · v${call.skillVersion}` : ""}</p>
            </div>
            <span className={call.success ? "shrink-0 text-xs font-medium text-ok" : "shrink-0 text-xs font-medium text-risk"}>
              {call.success ? "Thành công" : "Lỗi"}
            </span>
          </div>
          <p className="mt-1 text-xs text-ink-2">{call.model ?? "—"} · {kindLabel(call.kind)}</p>
          <dl className="mt-2 grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
            <Stat label="Token vào" value={formatInt(call.inputTokens)} />
            <Stat label="Token ra" value={formatInt(call.outputTokens)} />
            <Stat label="Thời gian" value={formatMs(call.latencyMs)} />
            <Stat label="Chi phí" value={call.costUsd === null ? "chưa có giá" : formatUsd(call.costUsd)} warn={call.costUsd === null} />
          </dl>
          {call.error ? <p className="mt-2 break-words text-xs text-risk">{call.error}</p> : null}
        </article>
      ))}
      <p className="px-1 text-xs text-ink-3">
        Tổng {formatInt(turn.data.total.calls)} lần gọi: {formatInt(turn.data.total.inputTokens)} token vào, {formatInt(turn.data.total.outputTokens)} token ra,
        lỗi {formatPct(ratio(turn.data.total.failedCalls, turn.data.total.calls))}.
      </p>
    </div>
  );
}

function Stat({ label, value, warn }: { label: string; value: string; warn?: boolean }) {
  return (
    <div>
      <dt className="text-ink-3">{label}</dt>
      <dd className={warn ? "tabular font-semibold text-warn" : "tabular font-semibold text-ink"}>{value}</dd>
    </div>
  );
}
