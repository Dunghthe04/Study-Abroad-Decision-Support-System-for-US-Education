"use client";

import { useEffect, useState } from "react";
import { studyCentersApi } from "@/lib/api";
import type { CenterReviewsDto } from "@/types/api";

/** Đánh giá thật của trung tâm (danh sách, điểm trung bình, số lượng) lấy từ backend. */
export function useCenterReviews(centerId: string) {
  const [reviews, setReviews] = useState<CenterReviewsDto | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    studyCentersApi
      .getReviews(centerId)
      .then((r) => {
        if (active) {
          setReviews(r);
          setError(false);
        }
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, [centerId]);

  return {
    reviews,
    error,
    total: reviews?.totalCount ?? 0,
    average: reviews?.averageRating ?? null,
  };
}
