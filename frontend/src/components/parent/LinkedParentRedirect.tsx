"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/contexts/AuthContext";
import { useIsLinkedParent } from "@/hooks/use-is-linked-parent";
import type { ChildTab } from "@/lib/child-tabs";

/**
 * Chức năng 1f: bọc các trang của hồ sơ riêng (layout.tsx của /profile, /analysis, /recommendations, /target-schools).
 * Phụ huynh đã liên kết không có hồ sơ riêng nên được chuyển sang "Con của tôi" đúng thẻ.
 * Học sinh, phụ huynh chưa liên kết, hoặc tải lỗi thì hiện trang như cũ.
 * 1.5b: phụ huynh đang nhập hồ sơ thay con managed thì trang hồ sơ là của con đó, không chuyển.
 */
export function LinkedParentRedirect({ tab, children }: { tab: ChildTab; children: ReactNode }) {
  const router = useRouter();
  const { acting } = useAuth();
  const { linked } = useIsLinkedParent();
  const redirect = linked && !acting;

  useEffect(() => {
    if (redirect) router.replace(`/parent/children?tab=${tab}`);
  }, [redirect, router, tab]);

  // Chưa biết thì chờ, để phụ huynh không thấy trang hồ sơ nháy lên rồi mới chuyển
  if (linked === false || (linked && acting)) return <>{children}</>;
  return (
    <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
      <Spinner /> {linked ? "Đang chuyển sang Con của tôi…" : "Đang tải…"}
    </div>
  );
}
