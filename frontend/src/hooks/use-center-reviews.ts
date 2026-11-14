"use client";

import { useCallback, useEffect, useState } from "react";
import { studyCentersApi } from "@/lib/api";
import type { CenterReviewsDto } from "@/types/api";

const PAGE_SIZE = 20;

interface ReviewsState {
  /** Trung tâm mà dữ liệu này thuộc về; khác `centerId` hiện tại thì coi như chưa có dữ liệu. */
  centerId: string;
  reviews: CenterReviewsDto | null;
  page: number;
  error: boolean;
  loadingMore: boolean;
  loadMoreError: boolean;
  /** Trang vừa tải không có đánh giá mới (đánh giá bị gỡ hoặc dịch chuyển giữa chừng): ngừng đề nghị "xem thêm". */
  exhausted: boolean;
}

/**
 * Đánh giá thật của trung tâm lấy từ backend: danh sách (phân trang, "xem thêm"), điểm trung bình, tổng số lượng.
 * State gắn với `centerId`, nên khi đổi trung tâm dữ liệu cũ không còn hiển thị và hook trở về trạng thái đang tải.
 */
export function useCenterReviews(centerId: string) {
  const [state, setState] = useState<ReviewsState | null>(null);
  const current = state?.centerId === centerId ? state : null;

  useEffect(() => {
    let active = true;
    studyCentersApi
      .getReviews(centerId, 1, PAGE_SIZE)
      .then((reviews) => {
        if (active)
          setState({ centerId, reviews, page: 1, error: false, loadingMore: false, loadMoreError: false, exhausted: false });
      })
      .catch(() => {
        if (active)
          setState({ centerId, reviews: null, page: 1, error: true, loadingMore: false, loadMoreError: false, exhausted: false });
      });
    return () => {
      active = false;
    };
  }, [centerId]);

  const loadMore = useCallback(async () => {
    if (!current?.reviews || current.loadingMore) return;
    const { reviews, page } = current;
    const next = page + 1;
    // Chỉ cập nhật khi state vẫn thuộc về trung tâm đang tải thêm.
    const patch = (fn: (s: ReviewsState) => ReviewsState) =>
      setState((s) => (s && s.centerId === centerId ? fn(s) : s));

    patch((s) => ({ ...s, loadingMore: true, loadMoreError: false }));
    try {
      const r = await studyCentersApi.getReviews(centerId, next, PAGE_SIZE);
      const seen = new Set(reviews.items.map((x) => x.id));
      const fresh = r.items.filter((x) => !seen.has(x.id));
      patch((s) => ({
        ...s,
        reviews: { ...r, items: [...reviews.items, ...fresh] },
        page: next,
        loadingMore: false,
        exhausted: fresh.length === 0,
      }));
    } catch {
      patch((s) => ({ ...s, loadingMore: false, loadMoreError: true }));
    }
  }, [centerId, current]);

  const reviews = current?.reviews ?? null;
  const hasMore = !!reviews && !current?.exhausted && reviews.items.length < reviews.totalCount;

  return {
    reviews,
    loading: current === null,
    error: current?.error ?? false,
    loadingMore: current?.loadingMore ?? false,
    loadMoreError: current?.loadMoreError ?? false,
    hasMore,
    loadMore,
    total: reviews?.totalCount ?? 0,
    average: reviews?.averageRating ?? null,
  };
}
