import { StarIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import type { CenterReviewsDto } from "@/types/api";

interface CenterReviewListProps {
  reviews: CenterReviewsDto | null;
  error: boolean;
}

/** Danh sách đánh giá thật của trung tâm; dùng chung cho trang công khai và trang quản trị. */
export function CenterReviewList({ reviews, error }: CenterReviewListProps) {
  const total = reviews?.totalCount ?? 0;

  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-ink">
        Nhận xét từ học sinh &amp; phụ huynh{total > 0 ? ` (${total})` : ""}
      </h3>

      {error && <p className="text-sm text-ink-2">Không tải được đánh giá. Vui lòng thử lại sau.</p>}

      {!error && reviews && reviews.items.length === 0 && (
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
              <div className="flex shrink-0 text-gold" aria-label={`${rev.rating} trên 5 sao`}>
                {Array.from({ length: rev.rating }, (_, i) => (
                  <StarIcon key={i} className="w-3.5 h-3.5 fill-current" />
                ))}
              </div>
            </div>
          </CardHeader>
          {rev.comment && (
            <CardContent className="px-4 sm:px-6 pb-4 text-xs sm:text-sm text-ink-2 leading-relaxed whitespace-pre-line">
              {rev.comment}
            </CardContent>
          )}
        </Card>
      ))}
    </div>
  );
}
