"use client";

import { useState } from "react";
import { StarIcon, Trash2Icon } from "lucide-react";
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
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError, studyCentersApi } from "@/lib/api";
import type { CenterReviewDto, CenterReviewsDto } from "@/types/api";

const MAX_STARS = 5;

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
  /** [UC-57] Có truyền thì Moderator thấy nút "Gỡ" trên từng đánh giá. */
  centerId?: string;
  /** Gọi sau khi gỡ thành công để tải lại danh sách và điểm trung bình. */
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

  // [UC-57] Chỉ Moderator gỡ được đánh giá vi phạm
  const { user } = useAuth();
  const canRemove = user?.role === "moderator" && !!centerId;
  const [removeTarget, setRemoveTarget] = useState<CenterReviewDto | null>(null);
  const [removeReason, setRemoveReason] = useState("");
  const [isRemoving, setIsRemoving] = useState(false);

  const closeRemoveDialog = () => {
    if (isRemoving) return;
    setRemoveTarget(null);
    setRemoveReason("");
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
                  {rev.reviewerName} ({rev.reviewerRole === "parent" ? "Phụ huynh" : "Học sinh"})
                </span>
                {rev.isVerified && (
                  <Badge variant="ok" className="text-xs py-0">
                    ✓ Đã xác thực
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
            <CardContent className="px-4 sm:px-6 pb-4 text-xs sm:text-sm text-ink-2 leading-relaxed whitespace-pre-line">
              {rev.comment}
            </CardContent>
          )}
          {canRemove && (
            <CardContent className="flex justify-end px-4 sm:px-6 pb-3 pt-0">
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
            </CardContent>
          )}
        </Card>
      ))}

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
            <Button type="button" variant="destructive" onClick={() => void confirmRemove()} disabled={isRemoving || removeReason.trim().length === 0} className="gap-1.5" loading={isRemoving}>
              <Trash2Icon className="size-3.5" />
              Gỡ đánh giá
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
