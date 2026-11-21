"use client";

import { useState } from "react";
import { CheckIcon, XIcon } from "lucide-react";
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
import { ApiError, centerDeletionRequestsApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { AdminCenterDeletionRequestDto, CenterDeletionRequestStatus } from "@/types/api";

const PAGE_SIZE = 20;
const MAX_NOTE = 1000;

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

type Decision = "approve" | "reject";

const DECISION_TEXT: Record<Decision, { title: string; description: string; placeholder: string; confirm: string; done: string }> = {
  approve: {
    title: "Duyệt yêu cầu xóa trung tâm?",
    description:
      "Tài khoản trung tâm bị khóa ngay, trung tâm không còn hiển thị, các yêu cầu liên hệ đang mở được đóng và người dùng liên quan nhận thông báo. Dữ liệu sẽ bị xóa hẳn sau thời hạn chờ. Người gửi nhận email kết quả.",
    placeholder: "Kết quả kiểm tra danh tính người gửi (bắt buộc), ví dụ: đã gọi điện xác nhận với người đại diện",
    confirm: "Duyệt và khóa tài khoản",
    done: "Đã duyệt yêu cầu xóa",
  },
  reject: {
    title: "Từ chối yêu cầu xóa?",
    description: "Tài khoản trung tâm vẫn hoạt động bình thường. Lý do được gửi tới người yêu cầu qua email.",
    placeholder: "Lý do từ chối (bắt buộc)",
    confirm: "Từ chối",
    done: "Đã từ chối yêu cầu",
  },
};

/** [UC-93] Danh sách yêu cầu xóa trung tâm; Admin kiểm tra danh tính rồi duyệt hoặc từ chối. */
export function CenterDeletionRequestsView() {
  const [filter, setFilter] = useState<Filter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [target, setTarget] = useState<{ decision: Decision; item: AdminCenterDeletionRequestDto } | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

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

  const decisionText = target ? DECISION_TEXT[target.decision] : null;

  return (
    <div className="flex flex-col gap-4">
      <Segmented label="Lọc yêu cầu" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {query.error ? (
        <Alert variant="destructive">
          <AlertDescription>{query.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={query.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Gửi lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead>Người gửi</TableHead>
              <TableHead className="min-w-56">Lý do</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-52"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const badge = STATUS_BADGE[item.status] ?? STATUS_BADGE.pending;
              return (
                <TableRow key={item.id}>
                  <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                  <TableCell>
                    <div className="font-medium">{item.centerName}</div>
                    <div className="text-xs text-ink-2">{[item.centerEmail, item.centerPhone].filter(Boolean).join(" · ") || "—"}</div>
                  </TableCell>
                  <TableCell>
                    <div>{item.requesterName ?? "Tài khoản đã xóa"}</div>
                    <div className="text-xs text-ink-2">{item.requesterEmail ?? "—"}</div>
                    {item.requesterIsOwner ? (
                      <Badge variant="neutral" className="mt-1">Chủ trung tâm</Badge>
                    ) : (
                      <Badge variant="warn" className="mt-1">Không phải chủ trung tâm</Badge>
                    )}
                  </TableCell>
                  <TableCell className="whitespace-pre-wrap break-words text-sm">
                    {item.reason}
                    {item.decisionNote ? (
                      <div className="mt-1 text-xs text-ink-2">
                        {item.status === "rejected" ? "Lý do từ chối" : "Ghi chú kiểm tra"}: {item.decisionNote}
                      </div>
                    ) : null}
                  </TableCell>
                  <TableCell>
                    <Badge variant={badge.variant}>{badge.label}</Badge>
                    {item.status === "approved" && item.scheduledPurgeAt ? (
                      <div className="mt-1 text-xs text-ink-2">Xóa hẳn: {formatDateTime(item.scheduledPurgeAt)}</div>
                    ) : null}
                    {item.status === "completed" && item.completedAt ? (
                      <div className="mt-1 text-xs text-ink-2">Đã xóa: {formatDateTime(item.completedAt)}</div>
                    ) : null}
                  </TableCell>
                  <TableCell>
                    {item.status === "pending" ? (
                      <div className="flex flex-wrap gap-2">
                        <Button type="button" size="sm" variant="destructive" className="gap-1.5" onClick={() => open("approve", item)}>
                          <CheckIcon className="size-3.5" />
                          Duyệt
                        </Button>
                        <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => open("reject", item)}>
                          <XIcon className="size-3.5" />
                          Từ chối
                        </Button>
                      </div>
                    ) : null}
                  </TableCell>
                </TableRow>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-sm text-ink-2">
                  Không có yêu cầu nào.
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
          itemLabel="yêu cầu"
          disabled={query.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={target !== null} onOpenChange={(isOpen) => !isOpen && close()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{decisionText?.title}</AlertDialogTitle>
            <AlertDialogDescription>{decisionText?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          {target ? (
            <p className="rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {target.item.centerName} · {target.item.requesterEmail ?? "người gửi đã xóa tài khoản"}
              {target.item.requesterIsOwner ? "" : " · người gửi không phải tài khoản chủ trung tâm"}
            </p>
          ) : null}
          <Textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={MAX_NOTE}
            rows={3}
            disabled={busy}
            placeholder={decisionText?.placeholder}
            aria-label={target?.decision === "approve" ? "Kết quả kiểm tra danh tính" : "Lý do từ chối"}
            aria-required
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant={target?.decision === "approve" ? "destructive" : "default"}
              onClick={() => void confirm()}
              disabled={!note.trim()}
              loading={busy}
              className="gap-1.5"
            >
              {decisionText?.confirm}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
