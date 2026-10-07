"use client";

import React, { Suspense } from "react";
import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AcademicAnalysisView } from "@/components/academic/AcademicAnalysisView";

export default function AcademicAnalysisRoutePage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <Suspense
        fallback={
          <div className="flex min-h-[50vh] items-center justify-center">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-blue-600 border-r-transparent"></div>
          </div>
        }
      >
        <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8 space-y-6">
          {/* Breadcrumb Navigation */}
          <nav className="flex items-center gap-2 text-xs font-medium text-slate-500">
            <Link href="/" className="hover:text-blue-600 transition">
              Trang chủ
            </Link>
            <span>/</span>
            <Link href="/account" className="hover:text-blue-600 transition">
              Tài khoản
            </Link>
            <span>/</span>
            <Link href="/profile/academic" className="hover:text-blue-600 transition">
              Hồ sơ học thuật
            </Link>
            <span>/</span>
            <span className="text-slate-900 font-semibold">Phân tích GPA (WES 4.0)</span>
          </nav>

          {/* Academic Analysis Dashboard */}
          <AcademicAnalysisView />
        </div>
      </Suspense>
    </ProtectedRoute>
  );
}
