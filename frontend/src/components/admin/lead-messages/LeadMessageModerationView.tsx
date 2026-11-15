"use client";

import { useState } from "react";
import { CheckIcon, Trash2Icon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { useAuth } from "@/contexts/AuthContext";
import { RepeatRemovalCenters } from "@/components/admin/lead-messages/RepeatRemovalCenters";
import { ApiError, centerLeadsApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { formatLeadMessageRoleLabel } from "@/lib/lead-message-filter";
import { useFetched } from "@/lib/use-fetched";
import type { ModerationLeadMessageDto, ModerationMessageFilter } from "@/types/api";

const PAGE_SIZE = 20;

const FILTER_OPTIONS: { id: ModerationMessageFilter; label: string }[] = [
  { id: "pending", label: "Chờ xem" },
  { id: "flagged", label: "Bị gắn cờ" },
  { id: "reported", label: "Bị báo cáo" },
  { id: "removed", label: "Đã gỡ" },
  { id: "all", label: "Tất cả" },
];

const EMPTY_TEXT: Record<ModerationMessageFilter, string> = {
  pending: "Không còn tin nhắn nào chờ xem.",
  flagged: "Không còn tin nhắn nào bị gắn cờ.",
  reported: "Không còn tin nhắn nào bị báo cáo.",
  removed: "Chưa có tin nhắn nào bị gỡ.",
  all: "Không có tin nhắn nào cần xem.",
};

/**
 * [USAS-2080 / UC-58] Hàng đợi tin nhắn yêu cầu liên hệ bị gắn cờ tự động (số điện thoại, liên kết ngoài, quảng cáo).
 * Moderator xem, chọn Giữ (bỏ cờ, tin vẫn hiện) hoặc Gỡ (hiện "Tin nhắn đã bị gỡ" cho cả hai bên).
 * Nội dung gốc của tin đã gỡ vẫn hiện ở đây cho Moderator và Admin. Admin chỉ xem.
 */
export function LeadMessageModerationView() {
  const { user } = useAuth();
  const canAct = user?.role === "moderator";

  const [filter, setFilter] = useState<ModerationMessageFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [removeTarget, setRemoveTarget] = useState<ModerationLeadMessageDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);

  const queue = useFetched(`lead-messages|${filter}|${page}|${version}`, () =>
    centerLeadsApi.moderationMessages({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = queue.data;

  const changeFilter = (next: ModerationMessageFilter) => {
    setFilter(next);
    setPage(1);
  };

  // Sau khi giữ hoặc gỡ, dòng rời khỏi danh sách đang lọc; nếu là dòng cuối của trang cuối thì lùi một trang
  const refreshAfterChange = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((current) => current + 1);
  };

  const errorText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback);

  const keep = async (item: ModerationLeadMessageDto) => {
    if (busyId) return;
    setBusyId(item.id);
    try {
      await centerLeadsApi.keepMessage(item.leadId, item.id);
      toast.add({ type: "success", title: "Đã giữ tin nhắn", description: "Tin vẫn hiển thị và đã bỏ khỏi hàng đợi gắn cờ." });
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Giữ tin nhắn thất bại", description: errorText(err, "Không thể giữ tin nhắn. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  const closeRemoveDialog = () => {
    if (busyId) return;
    setRemoveTarget(null);
    setRemoveReason("");
  };

  const confirmRemove = async () => {
    if (!removeTarget || busyId) return;
    const target = removeTarget;
    setBusyId(target.id);
    try {
      await centerLeadsApi.removeMessage(target.leadId, target.id, removeReason);
      toast.add({ type: "success", title: "Đã gỡ tin nhắn", description: "Tin hiển thị là đã bị gỡ ở cả hai phía và đã được ghi nhật ký." });
      setRemoveTarget(null);
      setRemoveReason("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Gỡ tin nhắn thất bại", description: errorText(err, "Không thể gỡ tin nhắn. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Tin nhắn liên hệ</h1>
        <p className="text-sm text-ink-2">
          Tin nhắn trong yêu cầu liên hệ bị bộ lọc gắn cờ (số điện thoại, liên kết ngoài, quảng cáo) hoặc bị học sinh / phụ huynh báo cáo.
          {canAct
            ? " Chọn Giữ nếu tin hợp lệ, hoặc Gỡ để tin hiển thị là đã bị gỡ ở cả hai phía."
            : " Bạn chỉ xem, thao tác giữ hoặc gỡ do Moderator thực hiện."}
        </p>
      </header>

      <RepeatRemovalCenters canAct={canAct} refreshKey={version} />

      <Segmented label="Lọc tin nhắn" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {queue.error ? (
        <Alert variant="destructive">
          <AlertDescription>{queue.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={queue.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Thời gian</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead>Người gửi</TableHead>
              <TableHead className="min-w-64">Nội dung gốc</TableHead>
              <TableHead>Lý do gắn cờ</TableHead>
              <TableHead>Trạng thái</TableHead>
              {canAct ? <TableHead className="w-40"><span className="sr-only">Thao tác</span></TableHead> : null}
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                <TableCell>{item.studyCenterName}</TableCell>
                <TableCell className="whitespace-nowrap">{formatLeadMessageRoleLabel(item.senderRole)}</TableCell>
                <TableCell className="max-w-md whitespace-pre-wrap break-words">{item.content}</TableCell>
                <TableCell className="text-xs text-ink-2">
                  {item.flagReason ? <span className="block">{item.flagReason}</span> : null}
                  {item.reportedAt ? (
                    <span className="block">
                      Báo cáo: {item.reportReason || "không nêu lý do"} ({formatDateTime(item.reportedAt)})
                    </span>
                  ) : null}
                  {!item.flagReason && !item.reportedAt ? "—" : null}
                </TableCell>
                <TableCell>
                  <div className="flex flex-col items-start gap-1">
                    {item.status === "removed" ? <Badge variant="neutral">Đã gỡ</Badge> : null}
                    {item.status !== "removed" && item.isFlagged ? <Badge variant="warn">Bị gắn cờ</Badge> : null}
                    {item.status !== "removed" && item.reportedAt ? <Badge variant="risk">Bị báo cáo</Badge> : null}
                  </div>
                </TableCell>
                {canAct ? (
                  <TableCell className="text-right">
                    {item.status === "removed" ? null : (
                      <div className="inline-flex gap-1.5">
                        <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => void keep(item)} className="gap-1">
                          {busyId === item.id ? <Spinner className="size-3.5" /> : <CheckIcon className="size-3.5" />}
                          Giữ
                        </Button>
                        <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => setRemoveTarget(item)} className="gap-1 text-risk hover:text-risk">
                          <Trash2Icon className="size-3.5" />
                          Gỡ
                        </Button>
                      </div>
                    )}
                  </TableCell>
                ) : null}
              </TableRow>
            ))}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={canAct ? 7 : 6} className="py-8 text-center text-ink-2">
                  {EMPTY_TEXT[filter]}
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={canAct ? 7 : 6} className="py-8 text-center text-ink-2">
                  {queue.loading ? "Đang tải..." : "Không có dữ liệu."}
                </TableCell>
              </TableRow>
            ) : null}
          </TableBody>
        </Table>
      </div>

      {list ? (
        <TablePagination
          page={list.page}
          totalPages={list.totalPages}
          totalCount={list.totalCount}
          itemLabel="tin nhắn"
          disabled={queue.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={removeTarget !== null} onOpenChange={(open) => !open && closeRemoveDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gỡ tin nhắn này?</AlertDialogTitle>
            <AlertDialogDescription>
              Tin sẽ hiển thị &quot;Tin nhắn đã bị gỡ do vi phạm quy tắc&quot; với cả trung tâm và học sinh / phụ huynh.
              Nội dung gốc vẫn được giữ cho Moderator và Admin, và việc gỡ được ghi nhật ký.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {removeTarget ? (
            <p className="max-h-32 overflow-y-auto whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {removeTarget.content}
            </p>
          ) : null}
          <Textarea
            value={removeReason}
            onChange={(event) => setRemoveReason(event.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder="Lý do gỡ (không bắt buộc, tối đa 500 ký tự)"
            aria-label="Lý do gỡ tin nhắn"
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirmRemove()} disabled={busyId !== null} className="gap-1.5">
              {busyId !== null ? <Spinner className="size-3.5" /> : <Trash2Icon className="size-3.5" />}
              Gỡ tin nhắn
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
