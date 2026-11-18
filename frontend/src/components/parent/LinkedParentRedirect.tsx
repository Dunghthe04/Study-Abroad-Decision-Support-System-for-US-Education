"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/contexts/AuthContext";
import type { ChildTab } from "@/lib/child-tabs";

/**
 * Chức năng 1f: bọc các trang của hồ sơ riêng (layout.tsx của /profile, /analysis, /recommendations, /target-schools).
 * Phụ huynh đã liên kết không có hồ sơ riêng nên được chuyển sang "Con của tôi" đúng thẻ.
 * Học sinh (và người chưa đăng nhập) thì hiện trang như cũ.
 * 1.5b: phụ huynh đang nhập hồ sơ thay con managed thì trang hồ sơ là của con đó, không chuyển.
 * 1.5d: mọi phụ huynh đều không có hồ sơ riêng (kể cả chưa có con nào), nên chuyển mọi phụ huynh không đang làm thay con.
 */
export function LinkedParentRedirect({ tab, children }: { tab: ChildTab; children: ReactNode }) {
  const router = useRouter();
  const { user, isLoading, acting } = useAuth();
  // null = chưa biết (đang tải phiên đăng nhập)
  const isParent = isLoading ? null : user?.role === "parent";
  const redirect = isParent === true && !acting;

  useEffect(() => {
    if (redirect) router.replace(`/parent/children?tab=${tab}`);
  }, [redirect, router, tab]);

  // Chưa biết thì chờ, để phụ huynh không thấy trang hồ sơ nháy lên rồi mới chuyển
  if (isParent === false || (isParent && acting)) return <>{children}</>;
  return (
    <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
      <Spinner /> {isParent ? "Đang chuyển sang Con của tôi…" : "Đang tải…"}
    </div>
  );
}
