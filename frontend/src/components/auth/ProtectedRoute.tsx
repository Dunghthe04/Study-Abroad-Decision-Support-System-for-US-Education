"use client";

import { useEffect, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";

interface ProtectedRouteProps {
  children: ReactNode;
}

/**
 * Component bảo vệ trang (Task B4):
 * - Nếu đang kiểm tra phiên: Hiện màn hình chờ (spinner)
 * - Nếu chưa đăng nhập: Tự động chuyển hướng sang /login?redirect=<trang_hien_tai>
 * - Nếu đã đăng nhập: Hiển thị nội dung trang bình thường
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!isLoading && !user) {
      const redirectQuery = pathname ? `?redirect=${encodeURIComponent(pathname)}` : "";
      router.replace(`/login${redirectQuery}`);
    }
  }, [user, isLoading, router, pathname]);

  if (isLoading) {
    return (
      <div className="flex min-h-[360px] flex-col items-center justify-center gap-3">
        <span className="inline-block h-8 w-8 animate-spin rounded-full border-2 border-slate-900 border-t-transparent" />
        <p className="text-sm text-slate-500">Đang kiểm tra quyền truy cập...</p>
      </div>
    );
  }

  // Khi chưa đăng nhập (đang chờ router chuyển hướng)
  if (!user) {
    return null;
  }

  return <>{children}</>;
}
