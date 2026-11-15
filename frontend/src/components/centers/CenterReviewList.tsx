import { StarIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import type { CenterReviewsDto } from "@/types/api";

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
}: CenterReviewListProps) {
  const total = reviews?.totalCount ?? 0;

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
        </Card>
      ))}

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
