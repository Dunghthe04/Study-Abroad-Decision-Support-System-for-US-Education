"use client";

import { useState } from "react";
import { BanIcon, CheckIcon } from "lucide-react";
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
import { LeadMessageAttachment } from "@/components/lead/LeadMessageParts";
import { ApiError, leadMessageReportsApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { formatLeadMessageRoleLabel } from "@/lib/lead-message";
import { useFetched } from "@/lib/use-fetched";
import type { AdminLeadMessageReportDto, LeadMessageReportFilter } from "@/types/api";

const PAGE_SIZE = 20;

const FILTER_OPTIONS: { id: LeadMessageReportFilter; label: string }[] = [
  { id: "pending", label: "Chờ xử lý" },
  { id: "resolved", label: "Đã xử lý" },
  { id: "all", label: "Tất cả" },
];

const EMPTY_TEXT: Record<LeadMessageReportFilter, string> = {
  pending: "Không còn báo cáo nào chờ xử lý.",
  resolved: "Chưa có báo cáo nào được xử lý.",
  all: "Chưa có tin nhắn nào bị báo cáo.",
};

/**
 * [UC-58] Admin xem các tin nhắn liên hệ bị báo cáo (lời lẽ thô tục, quấy rối...) từ học sinh / phụ huynh hoặc trung tâm.
 * Nếu đúng vi phạm: khóa tài khoản người gửi (hệ thống gỡ luôn tin nhắn của họ). Nếu không: bỏ qua báo cáo.
 */
export function LeadMessageReportsView() {
  const [filter, setFilter] = useState<LeadMessageReportFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [lockTarget, setLockTarget] = useState<AdminLeadMessageReportDto | null>(null);
  const [lockReason, setLockReason] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);

  const queue = useFetched(`lead-message-reports|${filter}|${page}|${version}`, () =>
    leadMessageReportsApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = queue.data;

  const changeFilter = (next: LeadMessageReportFilter) => {
    setFilter(next);
    setPage(1);
  };

  const refreshAfterChange = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((current) => current + 1);
  };

  const errorText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback);

  const dismiss = async (item: AdminLeadMessageReportDto) => {
    if (busyId) return;
    setBusyId(item.id);
    try {
      await leadMessageReportsApi.dismiss(item.leadId, item.id);
      toast.add({ type: "success", title: "Đã bỏ qua báo cáo", description: "Tin nhắn vẫn hiển thị bình thường." });
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Bỏ qua báo cáo thất bại", description: errorText(err, "Không thể xử lý báo cáo. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  const closeLockDialog = () => {
    if (busyId) return;
    setLockTarget(null);
    setLockReason("");
  };

  const confirmLock = async () => {
    if (!lockTarget || busyId) return;
    const reason = lockReason.trim();
    if (!reason) return;
    const target = lockTarget;
    setBusyId(target.id);
    try {
      await leadMessageReportsApi.lockSender(target.leadId, target.id, reason);
      toast.add({ type: "success", title: "Đã khóa tài khoản", description: "Các tin nhắn của người gửi đã được gỡ." });
      setLockTarget(null);
      setLockReason("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Khóa tài khoản thất bại", description: errorText(err, "Không thể khóa tài khoản. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Tin nhắn bị báo cáo</h1>
        <p className="text-sm text-ink-2">
          Tin nhắn trong yêu cầu liên hệ bị học sinh, phụ huynh hoặc trung tâm báo cáo. Nếu đúng vi phạm, hãy khóa tài khoản người gửi:
          tài khoản không còn đăng nhập hay thao tác được và các tin nhắn của họ bị gỡ. Nếu không, chọn bỏ qua báo cáo.
        </p>
      </header>

      <Segmented label="Lọc báo cáo" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {queue.error ? (
        <Alert variant="destructive">
          <AlertDescription>{queue.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={queue.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Báo cáo lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead>Người gửi</TableHead>
              <TableHead className="min-w-64">Nội dung gốc</TableHead>
              <TableHead>Lý do báo cáo</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-48"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const senderLocked = item.senderStatus === "locked";
              const resolved = item.status === "removed" || senderLocked;
              return (
                <TableRow key={item.id}>
                  <TableCell className="tabular whitespace-nowrap">{item.reportedAt ? formatDateTime(item.reportedAt) : "—"}</TableCell>
                  <TableCell>{item.studyCenterName}</TableCell>
                  <TableCell>
                    <span className="block font-medium">{item.senderName}</span>
                    <span className="block text-xs text-ink-2">
                      {formatLeadMessageRoleLabel(item.senderRole)}
                      {item.senderEmail ? ` · ${item.senderEmail}` : ""}
                    </span>
                  </TableCell>
                  <TableCell className="max-w-md whitespace-pre-wrap break-words">
                    {item.content}
                    <LeadMessageAttachment url={item.attachmentUrl} />
                  </TableCell>
                  <TableCell className="text-xs text-ink-2">{item.reportReason || "Không nêu lý do"}</TableCell>
                  <TableCell>
                    <div className="flex flex-col items-start gap-1">
                      {item.status === "removed" ? <Badge variant="neutral">Đã gỡ</Badge> : <Badge variant="risk">Bị báo cáo</Badge>}
                      {senderLocked ? <Badge variant="warn">Người gửi đã bị khóa</Badge> : null}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">
                    {resolved ? null : (
                      <div className="inline-flex gap-1.5">
                        <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => void dismiss(item)} className="gap-1">
                          {busyId === item.id ? <Spinner className="size-3.5" /> : <CheckIcon className="size-3.5" />}
                          Bỏ qua báo cáo
                        </Button>
                        <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => setLockTarget(item)} className="gap-1 text-risk hover:text-risk">
                          <BanIcon className="size-3.5" />
                          Khóa tài khoản người gửi
                        </Button>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">
                  {EMPTY_TEXT[filter]}
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">
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
          itemLabel="báo cáo"
          disabled={queue.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={lockTarget !== null} onOpenChange={(open) => !open && closeLockDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Khóa tài khoản người gửi?</AlertDialogTitle>
            <AlertDialogDescription>
              Tài khoản {lockTarget?.senderName} sẽ bị khóa, đăng xuất khỏi mọi thiết bị và không thể thao tác nữa. Toàn bộ tin nhắn của
              người này trong các yêu cầu liên hệ sẽ hiển thị là đã bị gỡ. Hành động được ghi vào nhật ký.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {lockTarget ? (
            <p className="max-h-32 overflow-y-auto whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {lockTarget.content || "(tin nhắn chỉ có ảnh)"}
            </p>
          ) : null}
          <Textarea
            value={lockReason}
            onChange={(event) => setLockReason(event.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder="Lý do khóa (bắt buộc, tối đa 500 ký tự)"
            aria-label="Lý do khóa tài khoản"
            aria-required
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirmLock()} disabled={busyId !== null || !lockReason.trim()} className="gap-1.5">
              {busyId !== null ? <Spinner className="size-3.5" /> : <BanIcon className="size-3.5" />}
              Khóa tài khoản
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
