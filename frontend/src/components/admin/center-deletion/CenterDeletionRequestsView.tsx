"use client";

import { useState } from "react";
import { CheckIcon, ScaleIcon, XIcon } from "lucide-react";
import Link from "next/link";
import { CenterLegalHoldDialog, type LegalHoldAction } from "./CenterLegalHoldDialog";
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
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Yêu cầu xóa trung tâm</h1>
        <p className="text-sm text-ink-2">
          Trung tâm gửi yêu cầu xóa tài khoản và dữ liệu từ trang hồ sơ. Kiểm tra danh tính người gửi (đúng tài khoản chủ trung tâm, liên hệ
          lại qua email hoặc số điện thoại đã xác thực) trước khi duyệt. Sau khi duyệt, dữ liệu được xóa hẳn khi hết thời hạn chờ.
          Nếu trung tâm bị nghi lừa đảo, có khiếu nại hoặc cơ quan có thẩm quyền yêu cầu giữ dữ liệu, bật giữ pháp lý: yêu cầu xóa không
          thể duyệt và dữ liệu không bị xóa hẳn tới khi gỡ. Khi xóa hẳn, nhật ký và tin nhắn được giữ lại ở dạng ẩn danh để truy vết.
        </p>
      </header>

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
                      <div className="mt-1 text-xs text-ink-2">
                        {item.isLegalHold ? "Tạm dừng xóa (đang giữ). Hạn cũ: " : "Xóa hẳn: "}
                        {formatDateTime(item.scheduledPurgeAt)}
                      </div>
                    ) : null}
                    {item.status === "completed" && item.completedAt ? (
                      <div className="mt-1 text-xs text-ink-2">Đã xóa: {formatDateTime(item.completedAt)}</div>
                    ) : null}
                  </TableCell>
                  <TableCell>
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
              disabled={busy || !note.trim()}
              className="gap-1.5"
            >
              {busy ? <Spinner className="size-3.5" /> : null}
              {decisionText?.confirm}
            </Button>
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
