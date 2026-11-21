"use client";

import { useState } from "react";
import { BanIcon, CheckIcon, EyeIcon, Trash2Icon, TriangleAlertIcon } from "lucide-react";
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
import type {
  AdminLeadMessageReportContextDto,
  AdminLeadMessageReportDto,
  LeadMessageReportFilter,
} from "@/types/api";

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

/** Ba cách xử lý cần lý do: gỡ riêng tin, cảnh cáo người gửi, khóa tài khoản người gửi. */
type ResolveAction = "remove" | "warn" | "lock";

const ACTION_TEXT: Record<
  ResolveAction,
  { title: string; description: (name: string) => string; placeholder: string; confirm: string; done: string; doneDetail: string; failed: string }
> = {
  remove: {
    title: "Gỡ tin nhắn này?",
    description: () =>
      "Tin nhắn sẽ hiển thị là đã bị gỡ với cả hai bên, tài khoản người gửi không bị khóa. Người gửi và người báo cáo đều nhận thông báo. Hành động được ghi vào nhật ký.",
    placeholder: "Lý do gỡ (bắt buộc, tối đa 500 ký tự)",
    confirm: "Gỡ tin nhắn",
    done: "Đã gỡ tin nhắn",
    doneDetail: "Người gửi và người báo cáo đã được thông báo.",
    failed: "Gỡ tin nhắn thất bại",
  },
  warn: {
    title: "Cảnh cáo người gửi?",
    description: (name) =>
      `${name} sẽ nhận thông báo cảnh cáo kèm lý do, tin nhắn vẫn hiển thị và tài khoản không bị khóa. Người báo cáo nhận thông báo kết quả. Hành động được ghi vào nhật ký.`,
    placeholder: "Lý do cảnh cáo (bắt buộc, tối đa 500 ký tự)",
    confirm: "Gửi cảnh cáo",
    done: "Đã cảnh cáo người gửi",
    doneDetail: "Người gửi và người báo cáo đã được thông báo.",
    failed: "Cảnh cáo thất bại",
  },
  lock: {
    title: "Khóa tài khoản người gửi?",
    description: (name) =>
      `Tài khoản ${name} sẽ bị khóa, đăng xuất khỏi mọi thiết bị và không thể thao tác nữa. Toàn bộ tin nhắn của người này trong các yêu cầu liên hệ sẽ hiển thị là đã bị gỡ. Người báo cáo nhận thông báo kết quả. Hành động được ghi vào nhật ký.`,
    placeholder: "Lý do khóa (bắt buộc, tối đa 500 ký tự)",
    confirm: "Khóa tài khoản",
    done: "Đã khóa tài khoản",
    doneDetail: "Các tin nhắn của người gửi đã được gỡ.",
    failed: "Khóa tài khoản thất bại",
  },
};

/**
 * [UC-58] Admin xử lý các tin nhắn liên hệ bị báo cáo (lời lẽ thô tục, quấy rối...) từ học sinh / phụ huynh hoặc trung tâm.
 * Admin đọc tin bị báo cáo kèm vài tin ngữ cảnh liền kề (không đọc cả cuộc trò chuyện), rồi chọn: gỡ tin, cảnh cáo,
 * khóa tài khoản người gửi hoặc bỏ qua báo cáo. Người báo cáo được thông báo kết quả.
 */
export function LeadMessageReportsView() {
  const [filter, setFilter] = useState<LeadMessageReportFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [actionTarget, setActionTarget] = useState<{ action: ResolveAction; item: AdminLeadMessageReportDto } | null>(null);
  const [actionReason, setActionReason] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [contextTarget, setContextTarget] = useState<AdminLeadMessageReportDto | null>(null);
  const [context, setContext] = useState<AdminLeadMessageReportContextDto | null>(null);
  const [contextError, setContextError] = useState<string | null>(null);

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
      toast.add({ type: "success", title: "Đã bỏ qua báo cáo", description: "Tin nhắn vẫn hiển thị bình thường. Người báo cáo đã được thông báo." });
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Bỏ qua báo cáo thất bại", description: errorText(err, "Không thể xử lý báo cáo. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  const openAction = (action: ResolveAction, item: AdminLeadMessageReportDto) => {
    setActionReason("");
    setActionTarget({ action, item });
  };

  const closeActionDialog = () => {
    if (busyId) return;
    setActionTarget(null);
    setActionReason("");
  };

  const confirmAction = async () => {
    if (!actionTarget || busyId) return;
    const reason = actionReason.trim();
    if (!reason) return;
    const { action, item } = actionTarget;
    const text = ACTION_TEXT[action];
    setBusyId(item.id);
    try {
      if (action === "remove") await leadMessageReportsApi.remove(item.leadId, item.id, reason);
      else if (action === "warn") await leadMessageReportsApi.warn(item.leadId, item.id, reason);
      else await leadMessageReportsApi.lockSender(item.leadId, item.id, reason);
      toast.add({ type: "success", title: text.done, description: text.doneDetail });
      setActionTarget(null);
      setActionReason("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: text.failed, description: errorText(err, "Không thể xử lý báo cáo. Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  // Mỗi lần mở ngữ cảnh là một lần đọc được backend ghi vào nhật ký truy cập, nên chỉ tải khi Admin bấm xem
  const openContext = async (item: AdminLeadMessageReportDto) => {
    setContextTarget(item);
    setContext(null);
    setContextError(null);
    try {
      setContext(await leadMessageReportsApi.getContext(item.leadId, item.id));
    } catch (err: unknown) {
      setContextError(errorText(err, "Không tải được ngữ cảnh. Vui lòng thử lại."));
    }
  };

  const closeContext = () => {
    setContextTarget(null);
    setContext(null);
    setContextError(null);
  };

  const actionText = actionTarget ? ACTION_TEXT[actionTarget.action] : null;

  return (
    <div className="flex flex-col gap-4">
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
              <TableHead className="w-72"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const senderLocked = item.senderStatus === "locked";
              const warned = item.reportResolution === "warned";
              const resolved = item.status === "removed" || senderLocked || !!item.reportResolution;
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
                      {warned ? <Badge variant="warn">Đã cảnh cáo</Badge> : null}
                      {senderLocked ? <Badge variant="warn">Người gửi đã bị khóa</Badge> : null}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">
                    {/* Lưới 2 cột: Xem ngữ cảnh chiếm cả hàng đầu; bốn cách xử lý xếp từ nhẹ tới nặng, Khóa tài khoản nằm cuối */}
                    <div className="ml-auto grid w-64 grid-cols-2 gap-1.5">
                      <Button type="button" variant="outline" size="sm" onClick={() => void openContext(item)} className="col-span-2 gap-1 whitespace-nowrap">
                        <EyeIcon className="size-3.5" />
                        Xem ngữ cảnh
                      </Button>
                      {resolved ? null : (
                        <>
                          <Button type="button" variant="outline" size="sm" loading={busyId === item.id} disabled={busyId !== null} onClick={() => void dismiss(item)} className="gap-1 whitespace-nowrap">
                            <CheckIcon className="size-3.5" />
                            Bỏ qua
                          </Button>
                          <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => openAction("warn", item)} className="gap-1 whitespace-nowrap">
                            <TriangleAlertIcon className="size-3.5" />
                            Cảnh cáo
                          </Button>
                          <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => openAction("remove", item)} className="gap-1 whitespace-nowrap text-risk hover:text-risk">
                            <Trash2Icon className="size-3.5" />
                            Gỡ tin nhắn
                          </Button>
                          <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => openAction("lock", item)} className="gap-1 whitespace-nowrap text-risk hover:text-risk">
                            <BanIcon className="size-3.5" />
                            Khóa tài khoản
                          </Button>
                        </>
                      )}
                    </div>
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

      {/* Gỡ tin, cảnh cáo, khóa tài khoản: cùng một hộp thoại xác nhận, lý do bắt buộc */}
      <AlertDialog open={actionTarget !== null} onOpenChange={(open) => !open && closeActionDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{actionText?.title}</AlertDialogTitle>
            <AlertDialogDescription>{actionText?.description(actionTarget?.item.senderName ?? "Người gửi")}</AlertDialogDescription>
          </AlertDialogHeader>
          {actionTarget ? (
            <p className="max-h-32 overflow-y-auto whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {actionTarget.item.content || "(tin nhắn chỉ có ảnh)"}
            </p>
          ) : null}
          <Textarea
            value={actionReason}
            onChange={(event) => setActionReason(event.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder={actionText?.placeholder}
            aria-label="Lý do xử lý"
            aria-required
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirmAction()} loading={busyId !== null} disabled={!actionReason.trim()} className="gap-1.5">
              {actionText?.confirm}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Tin bị báo cáo kèm vài tin liền kề; Admin không đọc toàn bộ cuộc trò chuyện */}
      <AlertDialog open={contextTarget !== null} onOpenChange={(open) => !open && closeContext()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ngữ cảnh tin nhắn bị báo cáo</AlertDialogTitle>
            <AlertDialogDescription>
              {context
                ? `Hiển thị tối đa ${context.contextSize} tin liền trước và ${context.contextSize} tin liền sau tin bị báo cáo, không phải toàn bộ cuộc trò chuyện. Lần xem này được ghi vào nhật ký truy cập.`
                : "Chỉ hiển thị một số tin liền kề tin bị báo cáo. Lần xem này được ghi vào nhật ký truy cập."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {contextError ? (
            <Alert variant="destructive">
              <AlertDescription>{contextError}</AlertDescription>
            </Alert>
          ) : null}
          {!context && !contextError ? <p className="text-sm text-ink-2">Đang tải...</p> : null}
          {context ? (
            <ol className="flex max-h-80 flex-col gap-2 overflow-y-auto">
              {context.messages.map((m) => (
                <li
                  key={m.id}
                  className={`rounded-md border px-3 py-2 text-sm ${m.isReported ? "border-risk bg-risk-wash" : "border-line"}`}
                >
                  <div className="flex flex-wrap items-center gap-x-2 text-xs text-ink-2">
                    <span className="font-medium text-ink">{m.senderName}</span>
                    <span>{m.isFromCenter ? "Trung tâm" : "Học sinh / phụ huynh"}</span>
                    <span className="tabular">{formatDateTime(m.createdAt)}</span>
                    {m.isReported ? <Badge variant="risk">Bị báo cáo</Badge> : null}
                    {m.status === "removed" ? <Badge variant="neutral">Đã gỡ</Badge> : null}
                  </div>
                  <p className="mt-1 whitespace-pre-wrap break-words">{m.content || "(tin nhắn chỉ có ảnh)"}</p>
                  <LeadMessageAttachment url={m.attachmentUrl} />
                </li>
              ))}
            </ol>
          ) : null}
          <AlertDialogFooter>
            <AlertDialogCancel>Đóng</AlertDialogCancel>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
