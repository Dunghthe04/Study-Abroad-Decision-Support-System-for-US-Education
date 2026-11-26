"use client";

import { useState } from "react";
import { Building2Icon, FlagIcon, StarIcon, Trash2Icon } from "lucide-react";
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
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError, studyCentersApi } from "@/lib/api";
import type { CenterReviewDto, CenterReviewReportReason, CenterReviewsDto } from "@/types/api";

const MAX_STARS = 5;

export const CENTER_REVIEW_REPORT_REASON_OPTIONS: { value: CenterReviewReportReason; label: string }[] = [
  { value: "offensive_language", label: "Ngôn từ xúc phạm, công kích" },
  { value: "personal_info", label: "Tiết lộ thông tin cá nhân" },
  { value: "not_real_experience", label: "Không phải trải nghiệm thực tế" },
  { value: "spam_ad", label: "Spam hoặc quảng cáo" },
  { value: "other", label: "Lý do khác" },
];

export const CENTER_REVIEW_REPORT_REASON_LABELS: Record<string, string> = Object.fromEntries(
  CENTER_REVIEW_REPORT_REASON_OPTIONS.map((o) => [o.value, o.label]),
);

interface CenterReviewListProps {
  reviews: CenterReviewsDto | null;
  error: boolean;
  /** Đang tải trang đầu tiên. */
  loading?: boolean;
  /** Còn đánh giá chưa tải thì hiện nút "Xem thêm". */
  hasMore?: boolean;
  loadingMore?: boolean;
  loadMoreError?: boolean;
  onLoadMore?: () => void;
  /** [UC-57 / UC-151 / UC-53] Truyền mã trung tâm để Moderator gỡ đánh giá/phản hồi và người dùng báo cáo đánh giá. */
  centerId?: string;
  /** Gọi sau khi gỡ đánh giá hoặc gỡ phản hồi thành công để tải lại danh sách và điểm trung bình. */
  onReviewRemoved?: () => void;
}

/** Một hàng đủ 5 sao: sao đầy theo điểm, phần còn lại là sao rỗng màu xám. */
function StarRating({ rating }: { rating: number }) {
  return (
    <div className="flex shrink-0" role="img" aria-label={`${rating} trên ${MAX_STARS} sao`}>
      {Array.from({ length: MAX_STARS }, (_, i) => (
        <StarIcon
          key={i}
          aria-hidden="true"
          className={i < rating ? "w-3.5 h-3.5 fill-current text-gold" : "w-3.5 h-3.5 text-ink-3 opacity-40"}
        />
      ))}
    </div>
  );
}

/** Danh sách đánh giá thật của trung tâm; dùng chung cho trang công khai và trang quản trị. */
export function CenterReviewList({
  reviews,
  error,
  loading = false,
  hasMore = false,
  loadingMore = false,
  loadMoreError = false,
  onLoadMore,
  centerId,
  onReviewRemoved,
}: CenterReviewListProps) {
  const total = reviews?.totalCount ?? 0;

  const { user } = useAuth();
  // [UC-57 / UC-53 2b] Chỉ Moderator gỡ được đánh giá hoặc phản hồi vi phạm
  const canRemove = user?.role === "moderator" && !!centerId;
  // [UC-151] Học sinh, phụ huynh hoặc trung tâm (cho đánh giá về chính mình — backend kiểm tra sở hữu) có thể báo cáo
  const canReport = (user?.role === "student" || user?.role === "parent" || user?.role === "center") && !!centerId;

  const [removeTarget, setRemoveTarget] = useState<CenterReviewDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");
  const [isRemoving, setIsRemoving] = useState(false);

  // [UC-53 2b] Moderator gỡ phản hồi của trung tâm
  const [removeReplyTarget, setRemoveReplyTarget] = useState<CenterReviewDto | null>(null);
  const [removeReplyReason, setRemoveReplyReason] = useState("");
  const [isRemovingReply, setIsRemovingReply] = useState(false);

  // [UC-151] Báo cáo đánh giá vi phạm
  const [reportTarget, setReportTarget] = useState<CenterReviewDto | null>(null);
  const [reportReason, setReportReason] = useState<CenterReviewReportReason>("offensive_language");
  const [reportDetail, setReportDetail] = useState("");
  const [isReporting, setIsReporting] = useState(false);
  const [reportedIds, setReportedIds] = useState<Set<string>>(() => new Set());

  const closeRemoveDialog = () => {
    if (isRemoving) return;
    setRemoveTarget(null);
    setRemoveReason("");
  };

  const closeRemoveReplyDialog = () => {
    if (isRemovingReply) return;
    setRemoveReplyTarget(null);
    setRemoveReplyReason("");
  };

  const closeReportDialog = () => {
    if (isReporting) return;
    setReportTarget(null);
    setReportReason("offensive_language");
    setReportDetail("");
  };

  const confirmRemove = async () => {
    if (!removeTarget || !centerId || isRemoving || removeReason.trim().length === 0) return;
    setIsRemoving(true);
    try {
      await studyCentersApi.removeReview(centerId, removeTarget.id, removeReason);
      toast.add({ type: "success", title: "Đã gỡ đánh giá", description: "Đánh giá không còn hiển thị và không tính vào điểm trung bình." });
      setRemoveTarget(null);
      setRemoveReason("");
      onReviewRemoved?.();
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Gỡ đánh giá thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gỡ đánh giá. Vui lòng thử lại.",
      });
    } finally {
      setIsRemoving(false);
    }
  };

  const confirmRemoveReply = async () => {
    if (!removeReplyTarget || !centerId || isRemovingReply || removeReplyReason.trim().length === 0) return;
    setIsRemovingReply(true);
    try {
      await studyCentersApi.removeReviewReply(centerId, removeReplyTarget.id, removeReplyReason);
      toast.add({ type: "success", title: "Đã gỡ phản hồi của trung tâm", description: "Đánh giá gốc vẫn hiển thị bình thường." });
      setRemoveReplyTarget(null);
      setRemoveReplyReason("");
      onReviewRemoved?.();
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Gỡ phản hồi thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gỡ phản hồi. Vui lòng thử lại.",
      });
    } finally {
      setIsRemovingReply(false);
    }
  };

  const confirmReport = async () => {
    if (!reportTarget || !centerId || isReporting) return;
    setIsReporting(true);
    try {
      await studyCentersApi.reportReview(centerId, reportTarget.id, reportReason, reportDetail);
      setReportedIds((prev) => new Set(prev).add(reportTarget.id));
      toast.add({
        type: "success",
        title: "Đã gửi báo cáo đánh giá",
        description: "Kiểm duyệt viên sẽ xem xét báo cáo của bạn.",
      });
      setReportTarget(null);
      setReportReason("offensive_language");
      setReportDetail("");
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Báo cáo thất bại",
        description: err instanceof ApiError ? err.message : "Không thể gửi báo cáo. Vui lòng thử lại.",
      });
    } finally {
      setIsReporting(false);
    }
  };

  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-ink">
        Nhận xét từ học sinh &amp; phụ huynh{total > 0 ? ` (${total})` : ""}
      </h3>

      {loading && !error && <p className="text-sm text-ink-2">Đang tải đánh giá…</p>}

      {error && <p className="text-sm text-ink-2">Không tải được đánh giá. Vui lòng thử lại sau.</p>}

      {!loading && !error && reviews && reviews.items.length === 0 && (
        <p className="text-sm text-ink-2">Chưa có nhận xét nào.</p>
      )}

      {reviews?.items.map((rev) => (
        <Card key={rev.id}>
          <CardHeader className="pb-2 pt-4 px-4 sm:px-6">
            <div className="flex items-start justify-between gap-3">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-semibold text-sm text-ink">
                  {rev.reviewerName}
                  {rev.reviewerRole && ` (${rev.reviewerRole === "parent" ? "Phụ huynh" : "Học sinh"})`}
                </span>
                {rev.isVerified && (
                  <Badge variant="ok" className="text-xs py-0">
                    ✓ Đã sử dụng dịch vụ
                  </Badge>
                )}
                <span className="text-xs text-ink-3">
                  ({new Date(rev.createdAt).toLocaleDateString("vi-VN")})
                </span>
              </div>
              <StarRating rating={rev.rating} />
            </div>
          </CardHeader>
          {rev.comment && (
            <CardContent className="px-4 sm:px-6 pb-3 text-xs sm:text-sm text-ink-2 leading-relaxed whitespace-pre-line">
              {rev.comment}
            </CardContent>
          )}

          {/* [UC-152] Phản hồi công khai từ trung tâm */}
          {rev.centerReply && (
            <CardContent className="px-4 sm:px-6 pb-3 pt-0">
              <div className="rounded-lg border border-brand/20 bg-brand-wash/30 px-3.5 py-2.5 space-y-1.5">
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
                  <span className="inline-flex items-center gap-1.5 font-semibold text-brand-ink">
                    <Building2Icon className="size-3.5 text-brand" />
                    Phản hồi từ trung tâm
                  </span>
                  <div className="flex items-center gap-2">
                    <span className="text-[11px] text-ink-3">
                      {new Date(rev.centerReply.createdAt).toLocaleDateString("vi-VN")}
                      {rev.centerReply.editedAt ? " · Đã chỉnh sửa" : ""}
                    </span>
                    {canRemove && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => setRemoveReplyTarget(rev)}
                        className="h-6 gap-1 px-1.5 text-[11px] text-risk hover:text-risk hover:bg-risk-wash"
                      >
                        <Trash2Icon className="size-3" />
                        Gỡ phản hồi
                      </Button>
                    )}
                  </div>
                </div>
                <p className="text-xs sm:text-sm text-ink-2 leading-relaxed whitespace-pre-line">
                  {rev.centerReply.content}
                </p>
              </div>
            </CardContent>
          )}

          {(canRemove || (canReport && !rev.isOwnReview)) && (
            <CardContent className="flex items-center justify-end gap-2 px-4 sm:px-6 pb-3 pt-0">
              {canReport && !rev.isOwnReview &&
                (reportedIds.has(rev.id) ? (
                  <span className="text-[11px] text-ink-3">Đã báo cáo đánh giá này</span>
                ) : (
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    onClick={() => setReportTarget(rev)}
                    className="h-6 gap-1 px-1.5 text-[11px] text-ink-3 hover:text-risk hover:bg-risk-wash"
                  >
                    <FlagIcon className="size-3" />
                    Báo cáo
                  </Button>
                ))}
              {canRemove && (
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  onClick={() => setRemoveTarget(rev)}
                  className="h-6 gap-1 px-1.5 text-[11px] text-risk hover:text-risk hover:bg-risk-wash"
                >
                  <Trash2Icon className="size-3" />
                  Gỡ đánh giá
                </Button>
              )}
            </CardContent>
          )}
        </Card>
      ))}

      {/* [UC-57] Moderator gỡ đánh giá */}
      <AlertDialog open={removeTarget !== null} onOpenChange={(open) => !open && closeRemoveDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gỡ đánh giá này?</AlertDialogTitle>
            <AlertDialogDescription>
              Đánh giá sẽ biến khỏi danh sách công khai và không còn tính vào điểm trung bình. Việc gỡ được ghi nhật ký kèm lý do.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {removeTarget && (
            <p className="max-h-32 overflow-y-auto whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {removeTarget.rating} sao{removeTarget.comment ? `: ${removeTarget.comment}` : " (không có nhận xét)"}
            </p>
          )}
          <Textarea
            value={removeReason}
            onChange={(e) => setRemoveReason(e.target.value)}
            maxLength={500}
            rows={2}
            disabled={isRemoving}
            placeholder="Lý do gỡ (bắt buộc, tối đa 500 ký tự)"
            required
            aria-label="Lý do gỡ đánh giá"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isRemoving}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              onClick={() => void confirmRemove()}
              disabled={isRemoving || removeReason.trim().length === 0}
              className="gap-1.5"
              loading={isRemoving}
            >
              <Trash2Icon className="size-3.5" />
              Gỡ đánh giá
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* [UC-53 2b] Moderator gỡ phản hồi của trung tâm */}
      <AlertDialog open={removeReplyTarget !== null} onOpenChange={(open) => !open && closeRemoveReplyDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Gỡ phản hồi của trung tâm?</AlertDialogTitle>
            <AlertDialogDescription>
              Phản hồi vi phạm sẽ bị ẩn khỏi trang công khai; đánh giá gốc của học sinh / phụ huynh vẫn giữ nguyên. Việc gỡ được ghi nhật ký kèm lý do.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {removeReplyTarget?.centerReply && (
            <p className="max-h-32 overflow-y-auto whitespace-pre-wrap break-words rounded-md border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
              {removeReplyTarget.centerReply.content}
            </p>
          )}
          <Textarea
            value={removeReplyReason}
            onChange={(e) => setRemoveReplyReason(e.target.value)}
            maxLength={500}
            rows={2}
            disabled={isRemovingReply}
            placeholder="Lý do gỡ phản hồi (bắt buộc, tối đa 500 ký tự)"
            required
            aria-label="Lý do gỡ phản hồi"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isRemovingReply}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant="destructive"
              onClick={() => void confirmRemoveReply()}
              disabled={isRemovingReply || removeReplyReason.trim().length === 0}
              className="gap-1.5"
              loading={isRemovingReply}
            >
              <Trash2Icon className="size-3.5" />
              Gỡ phản hồi
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* [UC-151] Người dùng / trung tâm báo cáo đánh giá */}
      <AlertDialog open={reportTarget !== null} onOpenChange={(open) => !open && closeReportDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Báo cáo đánh giá vi phạm</AlertDialogTitle>
            <AlertDialogDescription>
              Chọn lý do báo cáo để kiểm duyệt viên xem xét. Đánh giá vẫn hiển thị trong khi chờ xử lý.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <RadioGroup
            value={reportReason}
            onValueChange={(v) => setReportReason(v as CenterReviewReportReason)}
            className="gap-2"
          >
            {CENTER_REVIEW_REPORT_REASON_OPTIONS.map((opt) => (
              <div key={opt.value} className="flex items-center gap-2.5">
                <RadioGroupItem id={`review-report-${opt.value}`} value={opt.value} disabled={isReporting} />
                <Label htmlFor={`review-report-${opt.value}`} className="cursor-pointer text-sm font-normal">
                  {opt.label}
                </Label>
              </div>
            ))}
          </RadioGroup>
          <Textarea
            value={reportDetail}
            onChange={(e) => setReportDetail(e.target.value)}
            maxLength={1000}
            rows={3}
            disabled={isReporting}
            placeholder="Chi tiết bổ sung (tùy chọn, tối đa 1.000 ký tự)"
            aria-label="Chi tiết báo cáo đánh giá"
            className="text-sm resize-y"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isReporting}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              onClick={() => void confirmReport()}
              disabled={isReporting}
              loading={isReporting}
              className="gap-1.5"
            >
              <FlagIcon className="size-3.5" />
              Gửi báo cáo
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {hasMore && onLoadMore && (
        <div className="flex flex-col items-center gap-2 pt-1">
          {loadMoreError && (
            <p className="text-sm text-ink-2">Không tải thêm được đánh giá. Vui lòng thử lại.</p>
          )}
          <Button type="button" variant="outline" size="sm" onClick={onLoadMore} disabled={loadingMore}>
            {loadingMore ? "Đang tải…" : `Xem thêm (${total - (reviews?.items.length ?? 0)})`}
          </Button>
        </div>
      )}
    </div>
  );
}
