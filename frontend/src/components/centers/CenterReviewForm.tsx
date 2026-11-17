"use client";

import { useEffect, useState } from "react";
import { StarIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError, studyCentersApi } from "@/lib/api";
import { cn } from "@/lib/utils";
import type { CenterReviewEligibilityDto } from "@/types/api";

const MAX_STARS = 5;
const MAX_COMMENT_LENGTH = 2000;

interface CenterReviewFormProps {
  centerId: string;
  /** Gọi sau khi gửi đánh giá thành công, để tải lại danh sách và điểm trung bình. */
  onCreated: () => void;
}

/**
 * [USAS-2070 / UC-45] Học sinh / phụ huynh đã gửi yêu cầu liên hệ tới trung tâm thì chấm 1 đến 5 sao và viết nhận xét.
 * Chỉ hiện với học sinh và phụ huynh đã đăng nhập; chưa đủ điều kiện thì nói rõ lý do, đã đánh giá thì báo đã đánh giá.
 */
export function CenterReviewForm({ centerId, onCreated }: CenterReviewFormProps) {
  const { user } = useAuth();
  const canUse = user?.role === "student" || user?.role === "parent";

  const [eligibility, setEligibility] = useState<CenterReviewEligibilityDto | null>(null);
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);

  useEffect(() => {
    if (!canUse) return;
    let active = true;
    studentEligibility(centerId).then((result) => {
      if (active) setEligibility(result);
    });
    return () => {
      active = false;
    };
  }, [centerId, canUse]);

  if (!canUse || !eligibility) return null;

  if (eligibility.hasReviewed) {
    return <p className="text-sm text-ink-2">Bạn đã đánh giá trung tâm này. Cảm ơn bạn!</p>;
  }
  if (!eligibility.canReview) {
    return <p className="text-sm text-ink-2">{eligibility.reason ?? "Bạn chưa thể đánh giá trung tâm này."}</p>;
  }

  const submit = async () => {
    if (isSending) return;
    if (rating < 1) {
      setFormError("Vui lòng chọn số sao.");
      return;
    }
    setFormError(null);
    setIsSending(true);
    try {
      await studyCentersApi.createReview(centerId, rating, comment);
      toast.add({ type: "success", title: "Đã gửi đánh giá", description: "Cảm ơn bạn đã chia sẻ trải nghiệm." });
      setEligibility({ canReview: false, hasReviewed: true, reason: null });
      setRating(0);
      setComment("");
      onCreated();
    } catch (err: unknown) {
      const message = err instanceof ApiError ? err.message : "Không thể gửi đánh giá. Vui lòng thử lại.";
      setFormError(message);
      toast.add({ type: "error", title: "Gửi đánh giá thất bại", description: message });
    } finally {
      setIsSending(false);
    }
  };

  return (
    <Card>
      <CardHeader className="pb-2 pt-4 px-4 sm:px-6">
        <h3 className="text-sm font-semibold text-ink">Đánh giá trung tâm này</h3>
        <p className="text-xs text-ink-2">Bạn đã gửi yêu cầu liên hệ tới trung tâm nên có thể chia sẻ trải nghiệm của mình.</p>
      </CardHeader>
      <CardContent className="space-y-3 px-4 sm:px-6 pb-4">
        <div role="radiogroup" aria-label="Chọn số sao" className="flex gap-1">
          {Array.from({ length: MAX_STARS }, (_, i) => {
            const value = i + 1;
            return (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={rating === value}
                aria-label={`${value} sao`}
                disabled={isSending}
                onClick={() => {
                  setRating(value);
                  if (formError) setFormError(null);
                }}
                className="rounded p-0.5 outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <StarIcon aria-hidden="true" className={cn("size-6", value <= rating ? "fill-current text-gold" : "text-ink-3 opacity-40")} />
              </button>
            );
          })}
        </div>
        <Textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={MAX_COMMENT_LENGTH}
          rows={3}
          disabled={isSending}
          placeholder="Nhận xét của bạn về trung tâm (không bắt buộc)"
          aria-label="Nhận xét về trung tâm"
          className="text-sm resize-y"
        />
        {formError && <p className="text-xs font-medium text-destructive">{formError}</p>}
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs text-ink-3">
            {comment.length} / {MAX_COMMENT_LENGTH} ký tự
          </span>
          <Button type="button" size="sm" disabled={isSending || rating < 1} onClick={() => void submit()} className="gap-1.5">
            {isSending ? <Spinner className="size-3.5" /> : null}
            Gửi đánh giá
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** Lỗi mạng hoặc lỗi máy chủ thì coi như chưa đủ điều kiện, không hiện form (tránh cho gửi khi chưa biết). */
async function studentEligibility(centerId: string): Promise<CenterReviewEligibilityDto> {
  try {
    return await studyCentersApi.getReviewEligibility(centerId);
  } catch {
    return { canReview: false, hasReviewed: false, reason: "Không kiểm tra được quyền đánh giá. Vui lòng tải lại trang." };
  }
}
