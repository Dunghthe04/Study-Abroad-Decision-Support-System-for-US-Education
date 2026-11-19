"use client";

import { useEffect } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { onRealtime, startRealtime, stopRealtime } from "@/lib/realtime";
import { toast } from "@/components/ui/toast";

interface NotificationPush {
  title: string;
  body?: string | null;
  linkUrl?: string | null;
}

/** Chỉ các vai trò tham gia chat yêu cầu liên hệ mới cần kết nối realtime. */
const CHAT_ROLES = new Set(["student", "parent", "center"]);

/**
 * [Chat realtime] Giữ kết nối realtime khi người dùng đã đăng nhập (đóng khi đăng xuất) và báo ngay khi có thông báo mới:
 * hiện toast và làm mới chuông thông báo. Không hiển thị gì.
 */
export function RealtimeProvider() {
  const { user } = useAuth();
  const eligible = !!user && CHAT_ROLES.has(user.role);
  const userId = user?.id;

  useEffect(() => {
    if (!eligible) return;
    startRealtime();
    return () => stopRealtime();
  }, [eligible, userId]);

  useEffect(
    () =>
      onRealtime<NotificationPush>("notification", (push) => {
        toast.add({ type: "info", title: push.title, description: push.body ?? undefined });
        // NotificationBell đã nghe sự kiện này để tải lại số chưa đọc
        window.dispatchEvent(new Event("notifications:changed"));
      }),
    [],
  );

  return null;
}
