"use client";

import React, { Suspense } from "react";
import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AcademicAnalysisView } from "@/components/academic/AcademicAnalysisView";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Spinner } from "@/components/ui/spinner";

export default function AcademicAnalysisRoutePage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <Suspense
        fallback={
          <div className="flex min-h-[50vh] items-center justify-center">
            <Spinner />
          </div>
        }
      >
        <div className="mx-auto max-w-5xl space-y-6 px-4 py-8 sm:px-6 lg:px-8">
          {/* Breadcrumb Navigation */}
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink render={<Link href="/" />}>
                  Trang chủ
                </BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator>/</BreadcrumbSeparator>
              <BreadcrumbItem>
                <BreadcrumbLink render={<Link href="/account" />}>
                  Tài khoản
                </BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator>/</BreadcrumbSeparator>
              <BreadcrumbItem>
                <BreadcrumbLink render={<Link href="/profile/academic" />}>
                  Hồ sơ học thuật
                </BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator>/</BreadcrumbSeparator>
              <BreadcrumbItem>
                <BreadcrumbPage>Phân tích GPA (WES 4.0)</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>

          {/* Academic Analysis Dashboard */}
          <AcademicAnalysisView />
        </div>
      </Suspense>
    </ProtectedRoute>
  );
}
