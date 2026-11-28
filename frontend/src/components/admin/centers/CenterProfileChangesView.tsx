"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckIcon, EyeOffIcon, ExternalLinkIcon, XCircleIcon, XIcon } from "lucide-react";
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { CenterStatusBadge, CenterVisibilityBadge } from "@/components/centers/CenterBadges";
import { ApiError, centerProfileChangesApi, studyCentersApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { CenterProfileChangeDto, CenterProfileChangeFilter } from "@/types/api";

const PAGE_SIZE = 20;
const MAX_REASON = 500;

const FILTER_OPTIONS: { id: CenterProfileChangeFilter; label: string }[] = [
  { id: "pending", label: "Chờ kiểm tra" },
  { id: "passed", label: "Đạt" },
  { id: "failed", label: "Không đạt" },
  { id: "withdrawn", label: "Đã rút lại" },
  { id: "all", label: "Tất cả" },
];

const EMPTY_TEXT: Record<CenterProfileChangeFilter, string> = {
  pending: "Không còn thay đổi nào chờ kiểm tra.",
  passed: "Chưa có thay đổi nào được đánh giá Đạt.",
  failed: "Chưa có thay đổi nào bị đánh giá Không đạt.",
  withdrawn: "Chưa có thay đổi nào được trung tâm tự đổi lại giá trị cũ.",
  all: "Trung tâm chưa tự sửa website hoặc fanpage lần nào.",
};

const FIELD_LABEL: Record<string, string> = { website: "Website", fanpage: "Fanpage" };

/** Chỉ mở liên kết http/https, ở tab mới, không để trang đích truy cập ngược lại cửa sổ quản trị. */
function SafeLink({ value }: { value?: string | null }) {
  if (!value) return <span className="text-ink-2">(trống)</span>;
  let href: string | null = null;
  try {
    const url = new URL(value);
    if (url.protocol === "http:" || url.protocol === "https:") href = url.toString();
  } catch {
    href = null;
  }
  if (!href) return <span className="break-all">{value}</span>;
  return (
    <a href={href} target="_blank" rel="noopener noreferrer nofollow" className="inline-flex items-start gap-1 break-all text-brand underline-offset-2 hover:underline">
      {value}
      <ExternalLinkIcon className="mt-0.5 size-3 shrink-0" />
    </a>
  );
}

function ChangeStatusBadge({ status }: { status: string }) {
  if (status === "passed") return <Badge variant="ok">Đạt</Badge>;
  if (status === "failed") return <Badge variant="risk">Không đạt</Badge>;
  if (status === "withdrawn") return <Badge variant="neutral">Đã rút lại</Badge>;
  return <Badge variant="warn">Chờ kiểm tra</Badge>;
}

/**
 * [UC-114] Moderator kiểm tra thay đổi website / fanpage mà trung tâm tự sửa (UC-92): đối chiếu giá trị cũ và mới rồi chọn
 * Đạt hoặc Không đạt (lý do bắt buộc, trung tâm nhận thông báo). Nếu vi phạm, Moderator ẩn trung tâm (UC-72) ngay tại đây
 * hoặc mở trang trung tâm để thu hồi dấu xác minh (UC-59).
 */
export function CenterProfileChangesView() {
  const [filter, setFilter] = useState<CenterProfileChangeFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [failTarget, setFailTarget] = useState<CenterProfileChangeDto | null>(null);
  const [failReason, setFailReason] = useState("");
  const [hideTarget, setHideTarget] = useState<CenterProfileChangeDto | null>(null);

  const queue = useFetched(`center-profile-changes|${filter}|${page}|${version}`, () =>
    centerProfileChangesApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = queue.data;

  const changeFilter = (next: CenterProfileChangeFilter) => {
    setFilter(next);
    setPage(1);
  };

  const refresh = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((current) => current + 1);
  };

  const errorText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback);

  const pass = async (item: CenterProfileChangeDto) => {
    if (busyId) return;
    setBusyId(item.id);
    try {
      await centerProfileChangesApi.pass(item.id, item.newValue ?? null);
      toast.add({ type: "success", title: "Đã đánh giá Đạt", description: `${FIELD_LABEL[item.field] ?? item.field} của ${item.studyCenterName} được giữ nguyên.` });
      refresh();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Không lưu được kết quả", description: errorText(err, "Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  const closeFail = () => {
    if (busyId) return;
    setFailTarget(null);
    setFailReason("");
  };

  const confirmFail = async () => {
    if (!failTarget || busyId) return;
    const reason = failReason.trim();
    if (!reason) return;
    setBusyId(failTarget.id);
    try {
      await centerProfileChangesApi.fail(failTarget.id, reason, failTarget.newValue ?? null);
      toast.add({ type: "success", title: "Đã đánh giá Không đạt", description: "Trung tâm đã nhận thông báo để sửa lại." });
      setFailTarget(null);
      setFailReason("");
      refresh();
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Không lưu được kết quả", description: errorText(err, "Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  // [UC-72] Ẩn trung tâm dùng endpoint có sẵn; hiện lại làm ở trang quản lý trung tâm
  const confirmHide = async () => {
    if (!hideTarget || busyId) return;
    setBusyId(hideTarget.id);
    try {
      await studyCentersApi.setActiveStatus(hideTarget.studyCenterId, false);
      toast.add({ type: "success", title: "Đã ẩn trung tâm", description: `${hideTarget.studyCenterName} không còn hiện trong danh bạ và gợi ý.` });
      setHideTarget(null);
      setVersion((current) => current + 1);
    } catch (err: unknown) {
      toast.add({ type: "error", title: "Ẩn trung tâm thất bại", description: errorText(err, "Vui lòng thử lại.") });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <Segmented label="Lọc thay đổi" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {queue.error ? (
        <Alert variant="destructive">
          <AlertDescription>{queue.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={queue.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Sửa lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead>Trường</TableHead>
              <TableHead className="min-w-48">Giá trị cũ</TableHead>
              <TableHead className="min-w-48">Giá trị mới</TableHead>
              <TableHead>Kết quả</TableHead>
              <TableHead className="w-64"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const pending = item.status === "pending";
              return (
                <TableRow key={item.id}>
                  <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                  <TableCell>
                    <Link
                      href={`/admin/centers/${encodeURIComponent(item.studyCenterId)}`}
                      className="block font-medium hover:underline text-brand hover:text-brand/80"
                      title="Xem chi tiết trung tâm"
                    >
                      {item.studyCenterName}
                    </Link>
                    <span className="mt-1 flex flex-wrap gap-1">
                      <CenterStatusBadge status={item.centerVerificationStatus} />
                      <CenterVisibilityBadge active={item.centerIsActive} />
                    </span>
                  </TableCell>
                  <TableCell>{FIELD_LABEL[item.field] ?? item.field}</TableCell>
                  <TableCell className="max-w-xs text-sm"><SafeLink value={item.oldValue} /></TableCell>
                  <TableCell className="max-w-xs text-sm"><SafeLink value={item.newValue} /></TableCell>
                  <TableCell>
                    <div className="flex flex-col items-start gap-1">
                      <ChangeStatusBadge status={item.status} />
                      {item.reviewReason ? <span className="text-xs text-ink-2">{item.reviewReason}</span> : null}
                      {item.reviewedAt ? <span className="text-xs text-ink-2">{formatDateTime(item.reviewedAt)}</span> : null}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="inline-flex flex-wrap justify-end gap-1.5">
                      {pending ? (
                        <>
                          <Button type="button" variant="outline" size="sm" disabled={busyId !== null} loading={busyId === item.id} onClick={() => void pass(item)} className="gap-1">
                            <CheckIcon className="size-3.5" />
                            Đạt
                          </Button>
                          <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => { setFailReason(""); setFailTarget(item); }} className="gap-1 text-risk hover:text-risk">
                            <XIcon className="size-3.5" />
                            Không đạt
                          </Button>
                        </>
                      ) : null}
                      {item.centerIsActive ? (
                        <Button type="button" variant="outline" size="sm" disabled={busyId !== null} onClick={() => setHideTarget(item)} className="gap-1">
                          <EyeOffIcon className="size-3.5" />
                          Ẩn trung tâm
                        </Button>
                      ) : null}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">{EMPTY_TEXT[filter]}</TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">{queue.loading ? "Đang tải..." : "Không có dữ liệu."}</TableCell>
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
          disabled={queue.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={failTarget !== null} onOpenChange={(open) => !open && !busyId && closeFail()}>
        <AlertDialogContent variant="form">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <XCircleIcon />
            </AlertDialogIcon>
            <AlertDialogTitle>Đánh giá Không đạt?</AlertDialogTitle>
            <AlertDialogDescription>
              {failTarget
                ? `${FIELD_LABEL[failTarget.field] ?? failTarget.field} mới của ${failTarget.studyCenterName} sẽ bị đánh dấu Không đạt và trung tâm nhận thông báo kèm lý do để sửa lại. Hành động được ghi vào nhật ký.`
                : null}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogBody>
            <Textarea
              value={failReason}
              onChange={(event) => setFailReason(event.target.value)}
              maxLength={MAX_REASON}
              rows={3}
              disabled={busyId !== null}
              placeholder={`Lý do Không đạt (bắt buộc, tối đa ${MAX_REASON} ký tự)`}
              aria-label="Lý do Không đạt"
              aria-required
              className="resize-y text-sm"
            />
          </AlertDialogBody>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <AlertDialogAction
              tone="destructive"
              onClick={() => void confirmFail()}
              disabled={!failReason.trim()}
              loading={busyId !== null}
            >
              Xác nhận Không đạt
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={hideTarget !== null} onOpenChange={(open) => !open && !busyId && setHideTarget(null)}>
        <AlertDialogContent variant="action">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <EyeOffIcon />
            </AlertDialogIcon>
            <AlertDialogTitle>Ẩn trung tâm?</AlertDialogTitle>
            <AlertDialogDescription>
              {hideTarget
                ? `${hideTarget.studyCenterName} sẽ không còn hiện trong danh bạ và luồng gợi ý. Có thể hiện lại ở trang quản lý trung tâm.`
                : null}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <AlertDialogAction
              tone="destructive"
              onClick={() => void confirmHide()}
              loading={busyId !== null}
            >
              Ẩn trung tâm
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
