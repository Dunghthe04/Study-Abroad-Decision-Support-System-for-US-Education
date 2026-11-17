"use client";

import { useState } from "react";
import Link from "next/link";
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
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { ApiError } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import {
  centerProfileChangesApi,
  type CenterProfileChangeDto,
  type CenterProfileChangeFilter,
} from "@/lib/center-admin-api";
import { useFetched } from "@/lib/use-fetched";

const PAGE_SIZE = 20;

const FILTER_OPTIONS: { id: CenterProfileChangeFilter; label: string }[] = [
  { id: "pending", label: "Chờ kiểm tra" },
  { id: "passed", label: "Đạt" },
  { id: "failed", label: "Không đạt" },
  { id: "all", label: "Tất cả" },
];

const FIELD_LABEL: Record<string, string> = { website: "Website", fanpage: "Fanpage" };

/** Chỉ biến giá trị thành liên kết khi đó là đường dẫn http / https; còn lại hiện dạng chữ. */
function UrlValue({ value }: { value?: string | null }) {
  if (!value) return <span className="text-ink-3">(trống)</span>;
  const isHttp = /^https?:\/\//i.test(value);
  return isHttp ? (
    <a href={value} target="_blank" rel="noopener noreferrer nofollow" className="break-all text-brand underline underline-offset-2">
      {value}
    </a>
  ) : (
    <span className="break-all">{value}</span>
  );
}

function StatusBadge({ status }: { status: string }) {
  if (status === "passed") return <Badge variant="ok">Đạt</Badge>;
  if (status === "failed") return <Badge variant="risk">Không đạt</Badge>;
  if (status === "withdrawn") return <Badge variant="neutral">Trung tâm đã đổi lại</Badge>;
  return <Badge variant="warn">Chờ kiểm tra</Badge>;
}

/**
 * [UC-114] Moderator xem các thay đổi website / fanpage mà trung tâm tự sửa (UC-92), đối chiếu giá trị cũ và mới rồi chọn
 * Đạt hoặc Không đạt (kèm lý do bắt buộc). Không đạt thì trung tâm nhận thông báo để sửa lại.
 */
export function CenterProfileChangesView() {
  const [filter, setFilter] = useState<CenterProfileChangeFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [failTarget, setFailTarget] = useState<CenterProfileChangeDto | null>(null);
  const [failReason, setFailReason] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);

  const fetched = useFetched(`center-profile-changes|${filter}|${page}|${version}`, () =>
    centerProfileChangesApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = fetched.data;

  const changeFilter = (next: CenterProfileChangeFilter) => {
    setFilter(next);
    setPage(1);
  };

  const refreshAfterChange = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((current) => current + 1);
  };

  const errorText = (err: unknown) => (err instanceof ApiError ? err.message : "Không thể ghi kết quả kiểm tra. Vui lòng thử lại.");

  const pass = async (item: CenterProfileChangeDto) => {
    if (busyId) return;
    setBusyId(item.id);
    try {
      await centerProfileChangesApi.review(item.id, true);
      toast.add({ type: "success", title: "Đã ghi kết quả: Đạt", description: "Trung tâm đã được thông báo." });
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Ghi kết quả thất bại", description: errorText(err) });
    } finally {
      setBusyId(null);
    }
  };

  const closeFailDialog = () => {
    if (busyId) return;
    setFailTarget(null);
    setFailReason("");
  };

  const confirmFail = async () => {
    if (!failTarget || busyId) return;
    const reason = failReason.trim();
    if (!reason) return;
    const target = failTarget;
    setBusyId(target.id);
    try {
      await centerProfileChangesApi.review(target.id, false, reason);
      toast.add({ type: "success", title: "Đã ghi kết quả: Không đạt", description: "Trung tâm đã nhận thông báo để sửa lại." });
      setFailTarget(null);
      setFailReason("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Ghi kết quả thất bại", description: errorText(err) });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Kiểm tra thay đổi của trung tâm</h1>
        <p className="text-sm text-ink-2">
          Website và fanpage do trung tâm tự sửa được lưu ngay và đưa vào đây để kiểm tra. Đối chiếu giá trị cũ và mới rồi chọn Đạt hoặc
          Không đạt. Nếu thay đổi vi phạm, hãy thu hồi dấu xác minh hoặc ẩn trung tâm ở mục Trung tâm tư vấn. Thay đổi tên pháp lý, mã
          số doanh nghiệp và tài liệu không qua đây mà đi theo luồng duyệt hồ sơ xác minh.
        </p>
      </header>

      <Segmented label="Lọc thay đổi" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {fetched.error ? (
        <Alert variant="destructive">
          <AlertDescription>{fetched.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={fetched.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Sửa lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead>Trường</TableHead>
              <TableHead className="min-w-48">Giá trị cũ</TableHead>
              <TableHead className="min-w-48">Giá trị mới</TableHead>
              <TableHead>Kết quả</TableHead>
              <TableHead className="w-44"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                <TableCell>
                  <Link href={`/admin/centers/${item.studyCenterId}`} className="font-medium text-brand underline-offset-2 hover:underline">
                    {item.studyCenterName}
                  </Link>
                </TableCell>
                <TableCell>{FIELD_LABEL[item.field] ?? item.field}</TableCell>
                <TableCell className="max-w-xs"><UrlValue value={item.oldValue} /></TableCell>
                <TableCell className="max-w-xs"><UrlValue value={item.newValue} /></TableCell>
                <TableCell>
                  <div className="flex flex-col items-start gap-1">
                    <StatusBadge status={item.status} />
                    {item.reviewReason ? <span className="text-xs text-ink-2">{item.reviewReason}</span> : null}
                  </div>
                </TableCell>
                <TableCell className="text-right">
                  {item.status === "pending" ? (
                    <div className="inline-flex gap-1.5">
                      <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => void pass(item)} className="gap-1">
                        {busyId === item.id ? <Spinner className="size-3.5" /> : <CheckIcon className="size-3.5" />}
                        Đạt
                      </Button>
                      <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => setFailTarget(item)} className="gap-1 text-risk hover:text-risk">
                        <XIcon className="size-3.5" />
                        Không đạt
                      </Button>
                    </div>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">
                  {filter === "pending" ? "Không còn thay đổi nào chờ kiểm tra." : "Không có thay đổi nào."}
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">
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
          itemLabel="thay đổi"
          disabled={fetched.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={failTarget !== null} onOpenChange={(open) => !open && closeFailDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Thay đổi này không đạt?</AlertDialogTitle>
            <AlertDialogDescription>
              {failTarget?.studyCenterName} sẽ nhận thông báo kèm lý do để sửa lại {failTarget ? (FIELD_LABEL[failTarget.field] ?? failTarget.field).toLowerCase() : ""}.
              Giá trị mới vẫn đang hiển thị trên hồ sơ cho tới khi trung tâm sửa. Thao tác được ghi vào nhật ký.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={failReason}
            onChange={(event) => setFailReason(event.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder="Lý do không đạt (bắt buộc, tối đa 500 ký tự)"
            aria-label="Lý do không đạt"
            aria-required
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button type="button" variant="destructive" onClick={() => void confirmFail()} disabled={busyId !== null || !failReason.trim()} className="gap-1.5">
              {busyId !== null ? <Spinner className="size-3.5" /> : null}
              Ghi Không đạt
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
