"use client";

import { useAuth } from "@/contexts/AuthContext";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { useFetched } from "@/lib/use-fetched";

const NOT_PARENT = Promise.resolve(0);

/**
 * Chức năng 1f: người đang đăng nhập có phải phụ huynh đã liên kết với ít nhất một con không.
 * Phụ huynh đã liên kết không có hồ sơ riêng: trang hồ sơ / gợi ý / trường mục tiêu chuyển sang "Con của tôi", menu đổi theo.
 * linked = null khi chưa biết (đang tải); lỗi tải thì coi là chưa liên kết để không chặn trang.
 */
export function useIsLinkedParent(): { linked: boolean | null } {
  const { user, isLoading } = useAuth();
  const isParent = user?.role === "parent";
  // Không phải phụ huynh thì không gọi API: key riêng, kết quả 0 con
  const { data, error, loading } = useFetched(isParent ? `linked-parent-${user.id}` : "linked-parent-none", () =>
    isParent ? parentChildrenApi.listChildren().then((children) => children.length) : NOT_PARENT,
  );

  if (isLoading || (isParent && loading)) return { linked: null };
  if (!isParent || error) return { linked: false };
  return { linked: (data ?? 0) > 0 };
}
