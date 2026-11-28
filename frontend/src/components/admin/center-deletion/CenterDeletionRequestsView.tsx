"use client";

import { useState } from "react";
import { AlertTriangleIcon, CheckIcon, ClockIcon, ScaleIcon, ShieldCheckIcon, XIcon } from "lucide-react";
import Link from "next/link";
import { CenterLegalHoldDialog, type LegalHoldAction } from "./CenterLegalHoldDialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogIcon,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { DataTable, type DataTableColumn } from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { ApiError, centerDeletionRequestsApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { AdminCenterDeletionRequestDto, CenterDeletionRequestStatus } from "@/types/api";

const PAGE_SIZE = 20;
const MAX_NOTE = 1000;

const TAB_TRIGGER_CLASS =
  "flex-none rounded-none border-0 border-b-2 border-transparent px-3 data-active:border-b-brand data-active:bg-transparent data-active:text-foreground data-active:shadow-none dark:data-active:border-transparent dark:data-active:border-b-brand dark:data-active:bg-transparent after:hidden";

type Filter = CenterDeletionRequestStatus | "all";

const FILTER_OPTIONS: { id: Filter; label: string }[] = [
  { id: "pending", label: "Chờ xử lý" },
  { id: "approved", label: "Chờ xóa hẳn" },
  { id: "completed", label: "Đã xóa" },
  { id: "rejected", label: "Đã từ chối" },
  { id: "all", label: "Tất cả" },
];

const STATUS_BADGE: Record<CenterDeletionRequestStatus, { label: string; variant: "risk" | "warn" | "neutral" }> = {
  pending: { label: "Chờ xử lý", variant: "risk" },
  approved: { label: "Đã duyệt, chờ xóa hẳn", variant: "warn" },
  completed: { label: "Đã xóa", variant: "neutral" },
  rejected: { label: "Đã từ chối", variant: "neutral" },
};

type Decision = "approve" | "reject" | "extend";

const DAY_MS = 24 * 60 * 60 * 1000;

/** Số ngày còn lại tới hạn (âm là đã quá hạn). */
function daysUntil(iso: string, now: number): number {
  return Math.ceil((new Date(iso).getTime() - now) / DAY_MS);
}

const DECISION_TEXT: Record<Decision, { title: string; description: string; placeholder: string; confirm: string; done: string }> = {
  approve: {
    title: "Duyệt yêu cầu xóa trung tâm?",
    description:
      "Tài khoản trung tâm bị khóa ngay, trung tâm không còn hiển thị, các yêu cầu liên hệ đang mở được đóng và người dùng liên quan nhận thông báo. Dữ liệu sẽ bị xóa hẳn sau thời hạn chờ. Người gửi nhận email kết quả.",
    placeholder: "Kết quả kiểm tra danh tính người gửi (bắt buộc), ví dụ: đã gọi điện xác nhận với người đại diện",
    confirm: "Duyệt và khóa tài khoản",
    done: "Đã duyệt yêu cầu xóa",
  },
  extend: {
    title: "Gia hạn thời gian xử lý?",
    description:
      "Theo Nghị định 356/2025/NĐ-CP Điều 5 khoản 4, yêu cầu xóa phải xử lý trong 20 ngày và chỉ được gia hạn một lần, tối đa 20 ngày. Lý do gia hạn được gửi cho người yêu cầu qua email. Nếu yêu cầu đã duyệt, ngày xóa hẳn dời theo hạn mới.",
    placeholder: "Lý do gia hạn (bắt buộc), ví dụ: cần xác minh thêm danh tính người đại diện",
    confirm: "Gia hạn thêm 20 ngày",
    done: "Đã gia hạn thời gian xử lý",
  },
  reject: {
    title: "Từ chối yêu cầu xóa?",
    description: "Tài khoản trung tâm vẫn hoạt động bình thường. Lý do được gửi tới người yêu cầu qua email.",
    placeholder: "Lý do từ chối (bắt buộc)",
    confirm: "Từ chối",
    done: "Đã từ chối yêu cầu",
  },
};

/** Danh sách yêu cầu xóa trung tâm; Admin kiểm tra danh tính rồi duyệt hoặc từ chối. */
export function CenterDeletionRequestsView() {
  const [filter, setFilter] = useState<Filter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [target, setTarget] = useState<{ decision: Decision; item: AdminCenterDeletionRequestDto } | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  // Mốc thời gian chốt một lần khi mở trang, để tính "còn X ngày" mà không gọi Date.now() lúc render
  const [now] = useState(() => Date.now());
  const [holdTarget, setHoldTarget] = useState<{ action: LegalHoldAction; centerId: string; centerName: string } | null>(null);

  const query = useFetched(`center-deletion-requests|${filter}|${page}|${version}`, () =>
    centerDeletionRequestsApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = query.data;

  const changeFilter = (next: Filter) => {
    setFilter(next);
    setPage(1);
  };

  const open = (decision: Decision, item: AdminCenterDeletionRequestDto) => {
    setNote("");
    setTarget({ decision, item });
  };

  const close = () => {
    if (busy) return;
    setTarget(null);
    setNote("");
  };

  const confirm = async () => {
    if (!target || busy) return;
    const text = note.trim();
    if (!text) return;
    setBusy(true);
    try {
      if (target.decision === "approve") await centerDeletionRequestsApi.approve(target.item.id, text);
      else if (target.decision === "extend") await centerDeletionRequestsApi.extend(target.item.id, text);
      else await centerDeletionRequestsApi.reject(target.item.id, text);
      toast.add({ type: "success", title: DECISION_TEXT[target.decision].done, description: "Người gửi đã được báo kết quả qua email." });
      setTarget(null);
      setNote("");
      if (list && list.items.length === 1 && page > 1) setPage(page - 1);
      setVersion((v) => v + 1);
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Xử lý yêu cầu thất bại",
        description: err instanceof ApiError ? err.message : "Không thể xử lý yêu cầu. Vui lòng thử lại.",
      });
    } finally {
      setBusy(false);
    }
  };

  const columns: DataTableColumn<AdminCenterDeletionRequestDto>[] = [
    {
      id: "createdAt",
      label: "Gửi lúc",
      cellClassName: "tabular whitespace-nowrap",
      render: (item) => formatDateTime(item.createdAt),
    },
    {
      id: "center",
      label: "Trung tâm",
      required: true,
      render: (item) => (
        <>
          <div className="font-medium">
            {item.status === "completed" ? (
              item.centerName
            ) : (
              <Link href={`/admin/centers/${item.studyCenterId}`} className="hover:underline">
                {item.centerName}
              </Link>
            )}
          </div>
          <div className="text-xs text-ink-2">{[item.centerEmail, item.centerPhone].filter(Boolean).join(" · ") || "—"}</div>
          {item.isLegalHold ? (
            <div className="mt-1 flex flex-col gap-0.5">
              <Badge variant="risk" className="w-fit gap-1">
                <ScaleIcon className="size-3" />
                Đang giữ pháp lý
              </Badge>
              {item.legalHoldReason ? (
                <span className="whitespace-pre-wrap break-words text-xs text-ink-2">{item.legalHoldReason}</span>
              ) : null}
            </div>
          ) : null}
          {item.openReportCount > 0 && item.status !== "completed" ? (
            <Badge variant="warn" className="mt-1">
              {item.openReportCount} tin nhắn bị báo cáo chưa xử lý
            </Badge>
          ) : null}
        </>
      ),
    },
    {
      id: "requester",
      label: "Người gửi",
      render: (item) => (
        <>
          <div>{item.requesterName ?? "Tài khoản đã xóa"}</div>
          <div className="text-xs text-ink-2">{item.requesterEmail ?? "—"}</div>
          {item.requesterIsOwner ? (
            <Badge variant="neutral" className="mt-1">Chủ trung tâm</Badge>
          ) : (
            <Badge variant="warn" className="mt-1">Không phải chủ trung tâm</Badge>
          )}
        </>
      ),
    },
    {
      id: "reason",
      label: "Lý do",
      headerClassName: "min-w-56",
      cellClassName: "whitespace-pre-wrap break-words text-sm",
      render: (item) => (
        <>
          {item.reason}
          {item.decisionNote ? (
            <div className="mt-1 text-xs text-ink-2">
              {item.status === "rejected" ? "Lý do từ chối" : "Ghi chú kiểm tra"}: {item.decisionNote}
            </div>
          ) : null}
        </>
      ),
    },
    {
      id: "status",
      label: "Trạng thái",
      required: true,
      render: (item) => {
        const badge = STATUS_BADGE[item.status] ?? STATUS_BADGE.pending;
        return (
          <>
            <Badge variant={badge.variant}>{badge.label}</Badge>
            {item.status === "approved" && item.scheduledPurgeAt ? (
              <div className="mt-1 text-xs text-ink-2">
                {item.isLegalHold ? "Tạm dừng xóa (đang giữ). Hạn cũ: " : "Xóa hẳn: "}
                {formatDateTime(item.scheduledPurgeAt)}
              </div>
            ) : null}
            {(item.status === "pending" || item.status === "approved") && item.dueAt ? (
              <div className="mt-1 text-xs text-ink-2">
                Hạn xử lý: {formatDateTime(item.dueAt)}
                {(() => {
                  const left = daysUntil(item.dueAt, now);
                  // Đang giữ pháp lý thì tạm dừng đếm hạn, không báo "quá hạn" gây hiểu nhầm
                  if (item.isLegalHold) return <span className="ml-1">tạm dừng (đang giữ pháp lý)</span>;
                  if (left < 0) return <span className="ml-1 font-medium text-risk">quá hạn {-left} ngày</span>;
                  return <span className="ml-1">còn {left} ngày</span>;
                })()}
                {item.extendedAt ? (
                  <div title={item.extensionNote ?? undefined}>Đã gia hạn một lần{item.extensionNote ? `: ${item.extensionNote}` : ""}</div>
                ) : null}
              </div>
            ) : null}
            {item.status === "completed" && item.completedAt ? (
              <div className="mt-1 text-xs text-ink-2">Đã xóa: {formatDateTime(item.completedAt)}</div>
            ) : null}
          </>
        );
      },
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-52",
      render: (item) => (
        <div className="flex flex-wrap gap-2">
          {item.status === "pending" ? (
            <>
              <Button
                type="button"
                size="sm"
                variant="destructive"
                className="gap-1.5"
                disabled={item.isLegalHold}
                title={item.isLegalHold ? "Đang giữ pháp lý: chỉ có thể từ chối hoặc gỡ giữ trước" : undefined}
                onClick={() => open("approve", item)}
              >
                <CheckIcon className="size-3.5" />
                Duyệt
              </Button>
              <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => open("reject", item)}>
                <XIcon className="size-3.5" />
                Từ chối
              </Button>
            </>
          ) : null}
          {(item.status === "pending" || item.status === "approved") && !item.extendedAt ? (
            <Button type="button" size="sm" variant="ghost" className="gap-1.5" onClick={() => open("extend", item)}>
              <ClockIcon className="size-3.5" />
              Gia hạn
            </Button>
          ) : null}
          {item.status === "pending" || item.status === "approved" ? (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              className="gap-1.5"
              onClick={() =>
                setHoldTarget({
                  action: item.isLegalHold ? "release" : "set",
                  centerId: item.studyCenterId,
                  centerName: item.centerName,
                })
              }
            >
              <ScaleIcon className="size-3.5" />
              {item.isLegalHold ? "Gỡ giữ pháp lý" : "Giữ pháp lý"}
            </Button>
          ) : null}
        </div>
      ),
    },
  ];

  const decisionText = target ? DECISION_TEXT[target.decision] : null;

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <Tabs value={filter} onValueChange={(value) => changeFilter(value as Filter)}>
        <TabsList variant="line" className="h-auto w-full justify-start gap-0 border-b border-border p-0">
          {FILTER_OPTIONS.map((opt) => (
            <TabsTrigger key={opt.id} value={opt.id} className={TAB_TRIGGER_CLASS}>
              {opt.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <DataTable
        columns={columns}
        items={list?.items ?? []}
        getRowId={(item) => item.id}
        isLoading={query.loading && !list}
        error={query.error}
        emptyMessage="Không có yêu cầu nào."
        label="Danh sách yêu cầu xóa trung tâm"
        footer={
          list ? (
            <TablePagination
              page={list.page}
              totalPages={list.totalPages}
              totalCount={list.totalCount}
              itemLabel="yêu cầu"
              disabled={query.loading}
              onPageChange={setPage}
            />
          ) : null
        }
      />

      <AlertDialog open={target !== null} onOpenChange={(isOpen) => !isOpen && !busy && close()}>
        <AlertDialogContent variant="form">
          <AlertDialogHeader>
            <AlertDialogIcon
              tone={
                target?.decision === "approve"
                  ? "destructive"
                  : target?.decision === "extend"
                    ? "warning"
                    : "brand"
              }
            >
              {target?.decision === "approve" ? (
                <AlertTriangleIcon />
              ) : target?.decision === "extend" ? (
                <ClockIcon />
              ) : (
                <ShieldCheckIcon />
              )}
            </AlertDialogIcon>
            <AlertDialogTitle>{decisionText?.title}</AlertDialogTitle>
            <AlertDialogDescription>{decisionText?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogBody className="space-y-3">
            {target ? (
              <p className="rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
                {target.item.centerName} · {target.item.requesterEmail ?? "người gửi đã xóa tài khoản"}
                {target.item.requesterIsOwner ? "" : " · người gửi không phải tài khoản chủ trung tâm"}
              </p>
            ) : null}
            {target?.decision === "approve" && target.item.openReportCount > 0 ? (
              <Alert variant="destructive">
                <AlertDescription>
                  Trung tâm có {target.item.openReportCount} tin nhắn bị người dùng báo cáo chưa xử lý. Nếu có dấu hiệu lừa đảo, hãy bật giữ pháp lý
                  thay vì duyệt xóa để không mất bằng chứng.
                </AlertDescription>
              </Alert>
            ) : null}
            <Textarea
              value={note}
              onChange={(event) => setNote(event.target.value)}
              maxLength={MAX_NOTE}
              rows={3}
              disabled={busy}
              placeholder={decisionText?.placeholder}
              aria-label={
                target?.decision === "approve" ? "Kết quả kiểm tra danh tính" : target?.decision === "extend" ? "Lý do gia hạn" : "Lý do từ chối"
              }
              aria-required
              className="resize-y text-sm"
            />
          </AlertDialogBody>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <AlertDialogAction
              tone={target?.decision === "approve" ? "destructive" : "primary"}
              onClick={() => void confirm()}
              disabled={!note.trim()}
              loading={busy}
            >
              {decisionText?.confirm}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <CenterLegalHoldDialog
        target={holdTarget}
        onClose={() => setHoldTarget(null)}
        onDone={() => {
          setHoldTarget(null);
          setVersion((v) => v + 1);
        }}
      />
    </div>
  );
}
