"use client";

import React, { useEffect, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import type { UserRole } from "@/types/api";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";

interface ProtectedRouteProps {
  children: ReactNode;
  allowedRoles?: UserRole[];
}

export function ProtectedRoute({ children, allowedRoles }: ProtectedRouteProps) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!isLoading && !user) {
      const redirectUrl = pathname ? `/login?redirect=${encodeURIComponent(pathname)}` : "/login";
      router.push(redirectUrl);
    }
  }, [user, isLoading, router, pathname]);

  if (isLoading) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 text-sm text-muted-foreground">
        <Spinner />
        <p>Đang kiểm tra phiên làm việc...</p>
      </div>
    );
  }

  if (!user) {
    return null;
  }

  if (allowedRoles && allowedRoles.length > 0 && !allowedRoles.includes(user.role)) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center p-4">
        <Card className="w-full max-w-md text-center">
          <CardHeader>
            <CardTitle>
              <h2>Không có quyền truy cập</h2>
            </CardTitle>
            <CardDescription>
              Khu vực này chỉ dành cho tài khoản <strong>Học sinh</strong> hoặc <strong>Phụ huynh</strong> để quản lý hồ sơ học thuật.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Link href="/" className={buttonVariants({ size: "lg" })}>
              Về Trang chủ
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return <>{children}</>;
}
