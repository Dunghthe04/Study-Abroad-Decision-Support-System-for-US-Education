"use client";

import { useCallback, useEffect, useState } from "react";
import { adminCenterVerificationsApi } from "@/lib/api";

const POLL_MS = 60_000;

/**
 * Số hồ sơ xác minh trung tâm đang chờ moderator duyệt (UC-59).
 * Chỉ gọi API khi `enabled` (moderator); tự làm mới mỗi phút và khi tab được mở lại.
 */
export function usePendingCenterCount(enabled: boolean) {
  const [count, setCount] = useState(0);

  const refresh = useCallback(async () => {
    if (!enabled) return;
    try {
      const res = await adminCenterVerificationsApi.list({
        status: "pending_review",
        page: 1,
        pageSize: 1,
      });
      setCount(res.totalCount);
    } catch {
      // Không chặn giao diện nếu không đếm được
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) return;
    const first = setTimeout(() => void refresh(), 0);
    const timer = setInterval(() => void refresh(), POLL_MS);
    const onVisible = () => {
      if (document.visibilityState === "visible") void refresh();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      clearTimeout(first);
      clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [enabled, refresh]);

  return { count: enabled ? count : 0, refresh };
}
