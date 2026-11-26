"use client";

import { useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { RoadmapSection } from "@/components/dashboard/RoadmapSection";
import { StudentDashboardView } from "@/components/dashboard/StudentDashboardView";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/contexts/AuthContext";
import { dashboardApi } from "@/lib/api";
import type { StudentDashboard } from "@/types/dashboard";

function Page({ children }: { children: ReactNode }) {
  return <div className="mx-auto max-w-5xl space-y-6 px-4 py-6 lg:px-8">{children}</div>;
}

function DashboardContent() {
  const { user, acting } = useAuth();
  const [data, setData] = useState<StudentDashboard | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Phụ huynh không acts-as con managed: xem KPI của con ở trang Con của tôi
  const isParentNotActing = user?.role === "parent" && !acting;

  const [reloadKey, setReloadKey] = useState(0);

  // setState chỉ trong callback của promise (không gọi đồng bộ trong effect)
  useEffect(() => {
    if (isParentNotActing) return;
    let alive = true;
    dashboardApi
      .getMyDashboard()
      .then((res) => { if (alive) { setData(res); setError(null); } })
      .catch((err: unknown) => { if (alive) setError(err instanceof Error ? err.message : "Chưa tải được tổng quan."); })
      .finally(() => { if (alive) setIsLoading(false); });
    return () => { alive = false; };
  }, [acting, isParentNotActing, reloadKey]);

  const retry = () => {
    setIsLoading(true);
    setReloadKey((k) => k + 1);
  };

  if (isParentNotActing) {
    return (
      <Page>
        <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
          Tổng quan của từng con nằm ở tab Tổng quan trong{" "}
          <Link href="/parent/children" className="font-semibold text-brand underline-offset-4 hover:underline">
            Con của tôi
          </Link>
          , kèm số trường trong ngân sách và chi phí 4 năm.
        </div>
      </Page>
    );
  }

  return (
    <Page>
      <div className="space-y-1">
        <h1 className="text-h1 text-foreground">Tổng quan</h1>
        <p className="text-sm text-muted-foreground">
          {acting ? `Hồ sơ của ${acting.childName}: ` : ""}
          tiến độ hồ sơ, dữ liệu dùng cho gợi ý, lần gợi ý mới nhất và hạn nộp sắp tới. Mỗi thẻ ghi rõ cách tính.
        </p>
      </div>

      {isLoading ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner className="size-6 text-brand" />
        </div>
      ) : error || !data ? (
        <ErrorFallback description={error ?? "Chưa tải được tổng quan."} action={{ label: "Thử lại", onClick: retry }} />
      ) : (
        <>
          <StudentDashboardView dashboard={data} />
          <RoadmapSection />
        </>
      )}
    </Page>
  );
}

export default function DashboardPage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <DashboardContent />
    </ProtectedRoute>
  );
}
