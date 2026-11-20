"use client";

import React, { useEffect, useState, useCallback } from "react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { StudentDashboardView } from "@/components/dashboard/StudentDashboardView";
import { dashboardApi } from "@/lib/api";
import type { StudentDashboard } from "@/types/dashboard";
import { useAuth } from "@/contexts/AuthContext";
import { Loader2, AlertCircle, RefreshCw } from "lucide-react";
import Link from "next/link";

function DashboardContent() {
  const { user, acting } = useAuth();
  const [data, setData] = useState<StudentDashboard | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const isParentNotActing = user?.role === "parent" && !acting;

  const loadDashboard = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await dashboardApi.getMyDashboard();
      setData(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Không thể tải bảng điều khiển KPI.";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!isParentNotActing) {
      loadDashboard();
    } else {
      setIsLoading(false);
    }
  }, [acting, isParentNotActing, loadDashboard]);

  if (isParentNotActing) {
    return (
      <div className="container mx-auto max-w-4xl py-12 px-4">
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-xs">
          <AlertCircle className="mx-auto h-12 w-12 text-blue-600 mb-3" />
          <h2 className="text-xl font-bold text-slate-900">Bảng điều khiển của Học sinh</h2>
          <p className="mt-2 text-sm text-slate-600 max-w-md mx-auto">
            Trang này dành cho học sinh xem tiến độ cá nhân. Đối với phụ huynh, vui lòng vào trang Quản lý con để xem KPI tổng quan và chỉ số tài chính của từng con.
          </p>
          <div className="mt-6">
            <Link
              href="/parent/children"
              className="inline-flex items-center justify-center rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 transition"
            >
              Đến trang Con của tôi
            </Link>
          </div>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
        <p className="text-sm font-medium text-slate-600">Đang tải bảng điều khiển KPI...</p>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="container mx-auto max-w-4xl py-12 px-4">
        <div className="rounded-2xl border border-rose-200 bg-rose-50/50 p-8 text-center shadow-xs">
          <AlertCircle className="mx-auto h-10 w-10 text-rose-600 mb-3" />
          <h2 className="text-lg font-bold text-rose-900">Không thể tải dữ liệu</h2>
          <p className="mt-1 text-sm text-rose-700">{error || "Đã xảy ra lỗi không xác định."}</p>
          <button
            onClick={loadDashboard}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-rose-600 px-4 py-2 text-xs font-semibold text-white hover:bg-rose-700 transition cursor-pointer"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto max-w-6xl py-8 px-4 sm:px-6">
      <StudentDashboardView
        dashboard={data}
        studentName={acting ? acting.childName : user?.fullName}
      />
    </div>
  );
}

export default function DashboardPage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <DashboardContent />
    </ProtectedRoute>
  );
}
