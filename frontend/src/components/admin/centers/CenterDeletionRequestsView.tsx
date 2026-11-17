"use client";

import { useState } from "react";
import { Trash2Icon, XIcon } from "lucide-react";
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
import { ApiError } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import {
  adminCenterDeletionApi,
  type CenterDeletionFilter,
  type CenterDeletionRequestDto,
} from "@/lib/center-admin-api";
import { useFetched } from "@/lib/use-fetched";

const PAGE_SIZE = 20;

const FILTER_OPTIONS: { id: CenterDeletionFilter; label: string }[] = [
  { id: "pending", label: "Chờ xử lý" },
  { id: "completed", label: "Đã xóa" },
  { id: "rejected", label: "Từ chối" },
  { id: "all", label: "Tất cả" },
];

const formatDate = (value: string) => new Date(value).toLocaleDateString("vi-VN");

function StatusBadge({ item, now }: { item: CenterDeletionRequestDto; now: number }) {
  if (item.status === "completed") return <Badge variant="neutral">Đã xóa</Badge>;
  if (item.status === "rejected") return <Badge variant="risk">Từ chối</Badge>;
  if (item.status === "cancelled") return <Badge variant="neutral">Trung tâm đã rút</Badge>;
  const overdue = new Date(item.dueAt).getTime() < now;
  return overdue ? <Badge variant="risk">Quá hạn xử lý</Badge> : <Badge variant="warn">Chờ xử lý</Badge>;
}

type Action = { type: "complete" | "reject"; item: CenterDeletionRequestDto };

/**
 * [UC-93] Administrator nhận yêu cầu xóa do trung tâm gửi (UC-92), kiểm tra danh tính người yêu cầu, thực hiện xóa tài khoản
 * và dữ liệu trung tâm theo thời hạn đã công bố. Kết quả được báo qua email; các yêu cầu liên hệ đang mở bị đóng và người dùng
 * liên quan nhận thông báo.
 */
export function CenterDeletionRequestsView() {
  const [filter, setFilter] = useState<CenterDeletionFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [action, setAction] = useState<Action | null>(null);
  const [note, setNote] = useState("");
  const [identityVerified, setIdentityVerified] = useState(false);
  const [busy, setBusy] = useState(false);
  // Mốc thời gian lấy một lần khi mở trang, dùng để đánh dấu yêu cầu quá hạn
  const [now] = useState(() => Date.now());

  const fetched = useFetched(`center-deletion-requests|${filter}|${page}|${version}`, () =>
    adminCenterDeletionApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = fetched.data;

  const changeFilter = (next: CenterDeletionFilter) => {
    setFilter(next);
    setPage(1);
  };

  const openAction = (next: Action) => {
    setNote("");
    setIdentityVerified(false);
    setAction(next);
  };

  const closeDialog = () => {
    if (busy) return;
    setAction(null);
  };

  const canConfirm = action?.type === "complete" ? identityVerified : note.trim().length > 0;

  const confirm = async () => {
    if (!action || busy || !canConfirm) return;
    setBusy(true);
    try {
      if (action.type === "complete") {
        await adminCenterDeletionApi.complete(action.item.id, true, note);
        toast.add({ type: "success", title: "Đã xóa tài khoản và dữ liệu trung tâm", description: "Kết quả đã được gửi qua email." });
      } else {
        await adminCenterDeletionApi.reject(action.item.id, note);
        toast.add({ type: "success", title: "Đã từ chối yêu cầu xóa", description: "Trung tâm đã được báo lý do." });
      }
      setAction(null);
      if (list && list.items.length === 1 && page > 1) setPage(page - 1);
      setVersion((current) => current + 1);
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

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Yêu cầu xóa tài khoản trung tâm</h1>
        <p className="text-sm text-ink-2">
          Trung tâm gửi yêu cầu từ trang hồ sơ. Kiểm tra danh tính người yêu cầu trước khi xóa và xử lý trong hạn đã công bố (30 ngày).
          Khi xóa, trung tâm bị gỡ khỏi danh bạ, thông tin hồ sơ và tài liệu xác minh bị xóa, các yêu cầu liên hệ đang mở được đóng và
          tài khoản không đăng nhập được nữa. Việc xóa không hoàn tác được.
        </p>
      </header>

      <Segmented label="Lọc yêu cầu" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {fetched.error ? (
        <Alert variant="destructive">
          <AlertDescription>{fetched.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={fetched.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Gửi lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead className="min-w-48">Lý do</TableHead>
              <TableHead>Hạn xử lý</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-52"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                <TableCell>
                  <span className="block font-medium">{item.centerName}</span>
                  {item.contactEmail ? <span className="block text-xs text-ink-2">{item.contactEmail}</span> : null}
                </TableCell>
                <TableCell className="max-w-sm whitespace-pre-wrap break-words text-sm text-ink-2">{item.reason || "Không nêu lý do"}</TableCell>
                <TableCell className="tabular whitespace-nowrap">{formatDate(item.dueAt)}</TableCell>
                <TableCell>
                  <div className="flex flex-col items-start gap-1">
                    <StatusBadge item={item} now={now} />
                    {item.adminNote ? <span className="text-xs text-ink-2">{item.adminNote}</span> : null}
                  </div>
                </TableCell>
                <TableCell className="text-right">
                  {item.status === "pending" ? (
                    <div className="inline-flex gap-1.5">
                      <Button type="button" variant="outline" size="sm" onClick={() => openAction({ type: "reject", item })} className="gap-1">
                        <XIcon className="size-3.5" />
                        Từ chối
                      </Button>
                      <Button type="button" variant="outline" size="sm" onClick={() => openAction({ type: "complete", item })} className="gap-1 text-risk hover:text-risk">
                        <Trash2Icon className="size-3.5" />
                        Thực hiện xóa
                      </Button>
                    </div>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">
                  {filter === "pending" ? "Không còn yêu cầu xóa nào chờ xử lý." : "Không có yêu cầu nào."}
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">
                  {fetched.loading ? "Đang tải..." : "Không có dữ liệu."}
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
          disabled={fetched.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={action !== null} onOpenChange={(open) => !open && closeDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {action?.type === "complete" ? "Xóa tài khoản và dữ liệu trung tâm?" : "Từ chối yêu cầu xóa?"}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {action?.type === "complete"
                ? `${action.item.centerName} sẽ bị gỡ khỏi danh bạ, dữ liệu hồ sơ và tài liệu xác minh bị xóa, các yêu cầu liên hệ đang mở được đóng và tài khoản bị vô hiệu. Không hoàn tác được. Kết quả được gửi tới ${action.item.contactEmail ?? "email của trung tâm"}.`
                : `${action?.item.centerName ?? "Trung tâm"} được giữ nguyên tài khoản và dữ liệu, và nhận lý do từ chối qua email và thông báo.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {action?.type === "complete" ? (
            <label className="flex items-start gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={identityVerified}
                onChange={(event) => setIdentityVerified(event.target.checked)}
                disabled={busy}
                className="mt-0.5 size-4"
              />
              <span>Tôi đã kiểm tra danh tính người yêu cầu và xác nhận đây là người có quyền của trung tâm.</span>
            </label>
          ) : null}
          <Textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={1000}
            rows={2}
            disabled={busy}
            placeholder={action?.type === "complete" ? "Ghi chú (không bắt buộc), ví dụ cách đã kiểm tra danh tính" : "Lý do từ chối (bắt buộc)"}
            aria-label={action?.type === "complete" ? "Ghi chú" : "Lý do từ chối"}
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirm()} disabled={busy || !canConfirm} className="gap-1.5">
              {busy ? <Spinner className="size-3.5" /> : null}
              {action?.type === "complete" ? "Xóa vĩnh viễn" : "Từ chối yêu cầu"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
