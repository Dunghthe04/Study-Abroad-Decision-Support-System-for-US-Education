"use client";

import { useCallback, useEffect, useState } from "react";
import { NOTIFICATIONS_CHANGED_EVENT, notificationsApi } from "@/lib/notification-api";

const POLL_MS = 60_000;

/**
 * [UC-110] Số thông báo chưa đọc của người đang đăng nhập, cho huy hiệu trên chuông.
 * Chỉ gọi API khi `enabled` (đã đăng nhập); tự làm mới mỗi phút, khi tab được mở lại và khi trang thông báo báo có thay đổi.
 */
export function useUnreadNotifications(enabled: boolean) {
  const [count, setCount] = useState(0);

  const refresh = useCallback(async () => {
    if (!enabled) return;
    try {
      const res = await notificationsApi.unreadCount();
      setCount(res.unreadCount);
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
    const onChanged = () => void refresh();
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
    return () => {
      clearTimeout(first);
      clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener(NOTIFICATIONS_CHANGED_EVENT, onChanged);
    };
  }, [enabled, refresh]);

  return { count: enabled ? count : 0, refresh };
}
