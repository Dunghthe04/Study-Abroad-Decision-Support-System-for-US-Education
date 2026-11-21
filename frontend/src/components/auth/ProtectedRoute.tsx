"use client";

import React, { useEffect, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import type { UserRole } from "@/types/api";
import { Spinner } from "@/components/ui/spinner";

interface ProtectedRouteProps {
  children: ReactNode;
  allowedRoles?: UserRole[];
}

export function ProtectedRoute({ children, allowedRoles }: ProtectedRouteProps) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isDenied =
    !isLoading &&
    user !== null &&
    Boolean(allowedRoles && allowedRoles.length > 0 && !allowedRoles.includes(user.role));

  useEffect(() => {
    if (isLoading) return;
    if (!user) {
      const redirectUrl = pathname ? `/login?redirect=${encodeURIComponent(pathname)}` : "/login";
      router.push(redirectUrl);
    } else if (isDenied) {
      router.replace("/");
    }
  }, [user, isLoading, isDenied, router, pathname]);

  if (isLoading) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 text-sm text-muted-foreground">
        <Spinner />
        <p>Đang kiểm tra phiên làm việc...</p>
      </div>
    );
  }

  if (!user || isDenied) {
    return null;
  }

  return <>{children}</>;
}
