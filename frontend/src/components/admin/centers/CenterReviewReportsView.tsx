"use client";

import { useState } from "react";
import { AlertTriangleIcon, CheckIcon, StarIcon, Trash2Icon } from "lucide-react";
import { CENTER_REVIEW_REPORT_REASON_LABELS } from "@/components/centers/CenterReviewList";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
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
import { formatDateTime } from "@/lib/ai-usage-format";
import { ApiError, centerReviewReportsApi, studyCentersApi } from "@/lib/api";
import { useFetched } from "@/lib/use-fetched";
import type { CenterReviewReportDto, CenterReviewReportFilter } from "@/types/api";

const PAGE_SIZE = 20;

const FILTER_OPTIONS: { id: CenterReviewReportFilter; label: string }[] = [
  { id: "open", label: "Chờ xử lý" },
  { id: "resolved", label: "Đã xử lý" },
  { id: "all", label: "Tất cả" },
];

const EMPTY_TEXT: Record<CenterReviewReportFilter, string> = {
  open: "Không còn báo cáo đánh giá nào đang chờ xử lý.",
  resolved: "Chưa có báo cáo đánh giá nào được xử lý.",
  all: "Chưa có báo cáo đánh giá trung tâm nào.",
};

const ROLE_LABEL: Record<string, string> = {
  student: "Học sinh",
  parent: "Phụ huynh",
  center: "Trung tâm",
};

/**
 * [UC-151 / UC-53] Hàng đợi báo cáo đánh giá trung tâm dành cho Moderator.
 * Đánh dấu rõ trường hợp trung tâm tự báo cáo đánh giá về chính mình (xung đột lợi ích)
 * để Moderator cân nhắc kỹ trước khi Gỡ đánh giá hoặc Bác bỏ báo cáo.
 */
export function CenterReviewReportsView() {
  const [filter, setFilter] = useState<CenterReviewReportFilter>("open");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);

  const [removeTarget, setRemoveTarget] = useState<CenterReviewReportDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");

  const [dismissTarget, setDismissTarget] = useState<CenterReviewReportDto | null>(null);
  const [dismissNote, setDismissNote] = useState("");

  const queue = useFetched(`center-review-reports|${filter}|${page}|${version}`, () =>
    centerReviewReportsApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = queue.data;

  const refreshAfterChange = () => {
    if (list && list.items.length === 1 && page > 1) setPage(page - 1);
    setVersion((v) => v + 1);
  };

  const confirmRemoveReview = async () => {
    if (!removeTarget || busyId || !removeReason.trim()) return;
    setBusyId(removeTarget.id);
    try {
      await studyCentersApi.removeReview(removeTarget.studyCenterId, removeTarget.centerReviewId, removeReason);
      toast.add({
        type: "success",
        title: "Đã gỡ đánh giá",
        description: "Đánh giá không còn hiển thị và các báo cáo đang mở liên quan đã được đóng.",
      });
      setRemoveTarget(null);
      setRemoveReason("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Gỡ đánh giá thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gỡ đánh giá. Vui lòng thử lại.",
      });
    } finally {
      setBusyId(null);
    }
  };

  const confirmDismiss = async () => {
    if (!dismissTarget || busyId) return;
    setBusyId(dismissTarget.id);
    try {
      await centerReviewReportsApi.dismiss(dismissTarget.id, dismissNote);
      toast.add({
        type: "success",
        title: "Đã bác bỏ báo cáo",
        description: "Đánh giá tiếp tục hiển thị bình thường.",
      });
      setDismissTarget(null);
      setDismissNote("");
      refreshAfterChange();
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Bác bỏ báo cáo thất bại",
        description: err instanceof ApiError ? err.message : "Không thể xử lý báo cáo. Vui lòng thử lại.",
      });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <Segmented
        label="Lọc báo cáo đánh giá"
        value={filter}
        options={FILTER_OPTIONS}
        onChange={(next) => {
          setFilter(next);
          setPage(1);
        }}
      />

      {queue.error ? (
        <Alert variant="destructive">
          <AlertDescription>{queue.error}</AlertDescription>
        </Alert>
      ) : null}

      <div
        data-loading={queue.loading ? "true" : "false"}
        className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60"
      >
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Thời điểm</TableHead>
              <TableHead>Trung tâm &amp; Đánh giá</TableHead>
              <TableHead>Người báo cáo</TableHead>
              <TableHead>Lý do</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-52">
                <span className="sr-only">Thao tác</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const isOpen = item.status === "open";
              return (
                <TableRow key={item.id}>
                  <TableCell className="tabular whitespace-nowrap text-xs">
                    {formatDateTime(item.createdAt)}
                  </TableCell>
                  <TableCell className="max-w-md space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium text-ink">{item.studyCenterName}</span>
                      <span className="inline-flex items-center gap-0.5 text-xs font-semibold text-gold">
                        <StarIcon className="size-3.5 fill-current" />
                        {item.reviewRating}/5
                      </span>
                      <span className="text-xs text-ink-3">bởi {item.reviewerName}</span>
                    </div>
                    <p className="text-xs text-ink-2 whitespace-pre-wrap break-words">
                      {item.reviewComment || "(Không có nhận xét bằng chữ)"}
                    </p>
                  </TableCell>
                  <TableCell className="text-xs space-y-1">
                    <div className="font-medium text-ink">{item.reporterName}</div>
                    <div className="text-ink-3">{ROLE_LABEL[item.reporterRole] ?? item.reporterRole}</div>
                    {item.isCenterSelfReport ? (
                      <Badge variant="warn" className="gap-1 text-[11px] whitespace-normal">
                        <AlertTriangleIcon className="size-3 shrink-0" />
                        Trung tâm tự báo cáo — lưu ý xung đột lợi ích
                      </Badge>
                    ) : null}
                  </TableCell>
                  <TableCell className="max-w-xs text-xs text-ink-2">
                    <span className="block font-medium text-ink">
                      {CENTER_REVIEW_REPORT_REASON_LABELS[item.reason] ?? item.reason}
                    </span>
                    {item.detail ? (
                      <p className="mt-0.5 whitespace-pre-wrap break-words">{item.detail}</p>
                    ) : null}
                  </TableCell>
                  <TableCell>
                    {item.status === "open" ? (
                      <Badge variant="risk">Chờ xử lý</Badge>
                    ) : item.status === "resolved_removed" ? (
                      <Badge variant="neutral">Đã gỡ đánh giá</Badge>
                    ) : (
                      <Badge variant="neutral">Đã bác bỏ</Badge>
                    )}
                    {item.resolutionNote ? (
                      <p className="mt-1 max-w-44 text-[11px] text-ink-3 whitespace-pre-wrap break-words">
                        {item.resolutionNote}
                      </p>
                    ) : null}
                  </TableCell>
                  <TableCell className="text-right">
                    {isOpen ? (
                      <div className="flex flex-col gap-1.5 sm:flex-row sm:justify-end">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={busyId !== null}
                          onClick={() => {
                            setDismissNote("");
                            setDismissTarget(item);
                          }}
                          className="gap-1 whitespace-nowrap"
                        >
                          <CheckIcon className="size-3.5" />
                          Bác bỏ
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={busyId !== null}
                          onClick={() => {
                            setRemoveReason("");
                            setRemoveTarget(item);
                          }}
                          className="gap-1 whitespace-nowrap text-risk hover:text-risk"
                        >
                          <Trash2Icon className="size-3.5" />
                          Gỡ đánh giá
                        </Button>
                      </div>
                    ) : null}
                  </TableCell>
                </TableRow>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">
                  {EMPTY_TEXT[filter]}
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-ink-2">
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

      {/* Dialog gỡ đánh giá */}
      <AlertDialog
        open={removeTarget !== null}
        onOpenChange={(open) => {
          if (!open && !busyId) {
            setRemoveTarget(null);
            setRemoveReason("");
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gỡ đánh giá vi phạm?</AlertDialogTitle>
            <AlertDialogDescription>
              Đánh giá sẽ bị ẩn khỏi trang công khai và không còn tính vào điểm trung bình của trung tâm. Mọi báo cáo đang mở của đánh giá này sẽ được đóng.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {removeTarget?.isCenterSelfReport ? (
            <Alert>
              <AlertDescription className="text-xs">
                Lưu ý: Báo cáo này do chính trung tâm gửi. Chỉ gỡ khi đánh giá thật sự vi phạm quy chuẩn cộng đồng (không gỡ chỉ vì điểm thấp hoặc chê dịch vụ).
              </AlertDescription>
            </Alert>
          ) : null}
          <Textarea
            value={removeReason}
            onChange={(e) => setRemoveReason(e.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder="Lý do gỡ đánh giá (bắt buộc, tối đa 500 ký tự)"
            aria-label="Lý do gỡ đánh giá"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              onClick={() => void confirmRemoveReview()}
              disabled={busyId !== null || !removeReason.trim()}
              loading={busyId !== null}
              className="gap-1.5"
            >
              <Trash2Icon className="size-3.5" />
              Gỡ đánh giá
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Dialog bác bỏ báo cáo */}
      <AlertDialog
        open={dismissTarget !== null}
        onOpenChange={(open) => {
          if (!open && !busyId) {
            setDismissTarget(null);
            setDismissNote("");
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Bác bỏ báo cáo đánh giá?</AlertDialogTitle>
            <AlertDialogDescription>
              Đánh giá sẽ tiếp tục hiển thị bình thường. Bạn có thể ghi chú lý do bác bỏ (tùy chọn) để lưu nhật ký kiểm duyệt.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={dismissNote}
            onChange={(e) => setDismissNote(e.target.value)}
            maxLength={500}
            rows={2}
            disabled={busyId !== null}
            placeholder="Ghi chú kiểm duyệt (tùy chọn, tối đa 500 ký tự)"
            aria-label="Ghi chú bác bỏ báo cáo"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyId !== null}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              onClick={() => void confirmDismiss()}
              disabled={busyId !== null}
              loading={busyId !== null}
              className="gap-1.5"
            >
              <CheckIcon className="size-3.5" />
              Bác bỏ báo cáo
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
