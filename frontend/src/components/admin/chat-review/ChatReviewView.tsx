"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ReviewDetailSheet } from "@/components/admin/chat-review/ReviewDetailSheet";
import { Segmented, StatTile } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { adminChatReviewApi } from "@/lib/chat-review-api";
import { outcomeLabel, queueReason, statusLabel } from "@/lib/chat-review-format";
import { formatDateTime, formatInt, formatMs } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { ChatReviewItem, ChatReviewOutcome, ChatReviewStatus } from "@/types/chat-review";

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { id: ChatReviewStatus; label: string }[] = [
  { id: "open", label: "Đang mở" },
  { id: "resolved", label: "Đã xử lý" },
  { id: "ignored", label: "Bỏ qua" },
];

type OutcomeFilter = "all" | ChatReviewOutcome;

const OUTCOME_OPTIONS: { id: OutcomeFilter; label: string }[] = [
  { id: "all", label: "Tất cả" },
  { id: "no_answer", label: "Không trả lời được" },
  { id: "failed", label: "Lỗi" },
];

interface DateFilter {
  from: string;
  to: string;
}

const NO_DATES: DateFilter = { from: "", to: "" };

/**
 * Trang admin: hàng đợi câu hệ thống không trả lời được hoặc bị lỗi. Danh sách chỉ có mã và số đo, không có nội dung chat;
 * nội dung chỉ mở khi admin bấm vào một câu (mỗi lần mở được ghi nhật ký). Chỉ Admin.
 */
export function ChatReviewView() {
  const [status, setStatus] = useState<ChatReviewStatus>("open");
  const [outcome, setOutcome] = useState<OutcomeFilter>("all");
  const [page, setPage] = useState(1);
  const [draft, setDraft] = useState<DateFilter>(NO_DATES);   // ô ngày đang gõ
  const [applied, setApplied] = useState<DateFilter>(NO_DATES);   // khoảng ngày đang lọc
  const [dateError, setDateError] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  // Tăng sau mỗi lần đổi trạng thái để tải lại danh sách và các thẻ số
  const [version, setVersion] = useState(0);

  const dateKey = `${applied.from}|${applied.to}`;
  const queue = useFetched(`queue|${status}|${outcome}|${dateKey}|${page}|${version}`, () =>
    adminChatReviewApi.queue({
      status,
      outcome: outcome === "all" ? undefined : outcome,
      from: applied.from || undefined,
      to: applied.to || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
  );
  const summary = useFetched(`summary|${dateKey}|${version}`, () =>
    adminChatReviewApi.summary({ from: applied.from || undefined, to: applied.to || undefined }),
  );

  const list = queue.data;
  const counts = summary.data;

  const changeStatus = (next: ChatReviewStatus) => {
    setStatus(next);
    setPage(1);
  };
  const changeOutcome = (next: OutcomeFilter) => {
    setOutcome(next);
    setPage(1);
  };

  const applyDates = () => {
    if (draft.from && draft.to && draft.from > draft.to) {
      setDateError("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
      return;
    }
    setDateError(null);
    setApplied(draft);
    setPage(1);
  };
  const clearDates = () => {
    setDraft(NO_DATES);
    setApplied(NO_DATES);
    setDateError(null);
    setPage(1);
  };

  // Sau khi đổi trạng thái, dòng rời khỏi danh sách đang lọc. Nếu đó là dòng cuối của trang cuối thì lùi một trang.
  const handleChanged = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((current) => current + 1);
    setOpenId(null);
  };

  const handled = counts ? counts.resolved + counts.ignored : null;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Câu cần xem</h1>
        <p className="text-sm text-ink-2">
          Những câu hệ thống không trả lời được hoặc bị lỗi. Danh sách chỉ có mã và số đo; nội dung chat chỉ hiện khi bạn mở một câu, và mỗi lần mở đều được ghi nhật ký.
        </p>
      </header>

      <section aria-label="Tổng quan" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Đang mở" value={counts ? formatInt(counts.open) : "—"} loading={summary.loading} note="chưa ai xem" />
        <StatTile label="Không trả lời được" value={counts ? formatInt(counts.openNoAnswer) : "—"} loading={summary.loading} note="trong số đang mở" />
        <StatTile label="Lỗi" value={counts ? formatInt(counts.openFailed) : "—"} loading={summary.loading} note="trong số đang mở" />
        <StatTile
          label="Đã xử lý hoặc bỏ qua" value={handled === null ? "—" : formatInt(handled)} loading={summary.loading}
          note={counts ? `${formatInt(counts.resolved)} xử lý · ${formatInt(counts.ignored)} bỏ qua` : undefined}
        />
      </section>
      {summary.error ? <Alert variant="destructive"><AlertDescription>{summary.error}</AlertDescription></Alert> : null}

      {/* Một hàng bộ lọc duy nhất */}
      <div className="flex flex-wrap items-end gap-3">
        <Segmented label="Trạng thái" value={status} options={STATUS_OPTIONS} onChange={changeStatus} />
        <Segmented label="Kết quả" value={outcome} options={OUTCOME_OPTIONS} onChange={changeOutcome} />
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-xs text-ink-2">
            Từ ngày
            <Input type="date" value={draft.from} max={draft.to || undefined} onChange={(event) => setDraft({ ...draft, from: event.target.value })} className="h-8 w-40" />
          </label>
          <label className="flex flex-col gap-1 text-xs text-ink-2">
            Đến ngày
            <Input type="date" value={draft.to} min={draft.from || undefined} onChange={(event) => setDraft({ ...draft, to: event.target.value })} className="h-8 w-40" />
          </label>
          <Button type="button" size="sm" onClick={applyDates}>Áp dụng</Button>
          {applied.from || applied.to ? <Button type="button" size="sm" variant="ghost" onClick={clearDates}>Bỏ lọc ngày</Button> : null}
        </div>
      </div>
      {dateError ? <Alert variant="destructive"><AlertDescription>{dateError}</AlertDescription></Alert> : null}
      {queue.error ? <Alert variant="destructive"><AlertDescription>{queue.error}</AlertDescription></Alert> : null}

      <div data-loading={queue.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Thời gian</TableHead>
              <TableHead>Kết quả</TableHead>
              <TableHead>Vì sao vào hàng đợi</TableHead>
              <TableHead className="text-right">Thời gian xử lý</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-24"><span className="sr-only">Xem</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => (
              <QueueRow key={item.messageId} item={item} onOpen={() => setOpenId(item.messageId)} />
            ))}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">{emptyText(status, outcome, applied)}</TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">{queue.loading ? "Đang tải..." : "Không có dữ liệu."}</TableCell>
              </TableRow>
            ) : null}
          </TableBody>
        </Table>
      </div>

      {list ? (
        <TablePagination
          page={list.page} totalPages={list.totalPages} totalCount={list.totalCount} itemLabel="câu"
          disabled={queue.loading} onPageChange={setPage}
        />
      ) : null}

      <ReviewDetailSheet messageId={openId} onClose={() => setOpenId(null)} onChanged={handleChanged} />
    </div>
  );
}

function emptyText(status: ChatReviewStatus, outcome: OutcomeFilter, dates: DateFilter): string {
  const filtered = outcome !== "all" || dates.from !== "" || dates.to !== "";
  if (filtered) return "Không có câu nào khớp bộ lọc.";
  if (status === "open") return "Không còn câu nào đang mở.";
  return `Chưa có câu nào ở trạng thái "${statusLabel(status)}".`;
}

function QueueRow({ item, onOpen }: { item: ChatReviewItem; onOpen: () => void }) {
  const reason = queueReason(item);
  return (
    <TableRow className="cursor-pointer" onClick={onOpen}>
      <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
      <TableCell>
        <Badge variant={item.outcome === "failed" ? "risk" : "warn"}>{outcomeLabel(item.outcome)}</Badge>
      </TableCell>
      <TableCell>
        <span className="block text-ink">{reason.label}</span>
        {reason.detail ? <span className="block text-xs text-ink-3">{reason.detail}</span> : null}
      </TableCell>
      <TableCell className="tabular text-right">{item.turnMs == null ? "—" : formatMs(item.turnMs)}</TableCell>
      <TableCell>
        <Badge variant={item.reviewStatus === "open" ? "brand" : "neutral"}>{statusLabel(item.reviewStatus)}</Badge>
      </TableCell>
      <TableCell className="text-right">
        <Button
          type="button" variant="outline" size="sm"
          onClick={(event) => { event.stopPropagation(); onOpen(); }}
        >
          Xem
        </Button>
      </TableCell>
    </TableRow>
  );
}
