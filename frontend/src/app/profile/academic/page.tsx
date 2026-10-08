"use client";

import React, { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRightIcon, ChartColumnIcon, MessageCircleMoreIcon, RocketIcon } from "lucide-react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AcademicProfileForm } from "@/components/profile/AcademicProfileForm";
import { profileApi, ApiError } from "@/lib/api";
import type { AcademicProfileResponse } from "@/types/api";
import { Alert, AlertTitle } from "@/components/ui/alert";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";

function AcademicProfileContent() {
  const [profile, setProfile] = useState<AcademicProfileResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadProfile() {
      setIsLoading(true);
      setFetchError(null);
      try {
        const data = await profileApi.getAcademicProfile();
        if (isMounted) {
          setProfile(data);
        }
      } catch (err: unknown) {
        if (isMounted) {
          if (
            (err instanceof ApiError && err.status === 404) ||
            (err instanceof Error &&
              (err.message.includes("404") ||
                err.message.toLowerCase().includes("not found") ||
                err.message.includes("Chưa tìm thấy")))
          ) {
            setProfile(null);
          } else if (err instanceof Error) {
            setFetchError(err.message);
          } else {
            setFetchError("Không thể tải thông tin hồ sơ học thuật.");
          }
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    loadProfile();

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6 lg:px-8">
      {/* Breadcrumb Navigation */}
      <div className="mb-6 flex items-center justify-between">
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
              <BreadcrumbPage>Hồ sơ học thuật</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <Link
          href="/profile/academic/analysis"
          className={buttonVariants({ variant: "link", size: "xs" })}
        >
          <ChartColumnIcon aria-hidden="true" /> Xem phân tích GPA WES 4.0 <ArrowRightIcon aria-hidden="true" />
        </Link>
      </div>

      {/* Header Banner */}
      <div className="mb-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-h1">
              Hồ sơ Học thuật & Bảng điểm
            </h1>
            <p className="mt-2 max-w-2xl text-body text-muted-foreground">
              Nhập chi tiết bảng điểm các năm học theo thang điểm của trường, cùng chứng chỉ IELTS/SAT để hệ thống phân tích năng lực học thuật và tư vấn lộ trình du học Mỹ.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Link
              href="/profile/academic/analysis"
              className={buttonVariants({ size: "lg" })}
            >
              <RocketIcon aria-hidden="true" /> Phân tích GPA
            </Link>
            <Link
              href="/advisor"
              className={buttonVariants({ variant: "outline", size: "lg" })}
            >
              <MessageCircleMoreIcon aria-hidden="true" />
              Chat Advisor
            </Link>
          </div>
        </div>
      </div>

      {isLoading ? (
        <Card className="min-h-[300px] justify-center">
          <CardContent className="flex flex-col items-center gap-3 text-muted-foreground">
            <Spinner />
            <p>Đang tải dữ liệu hồ sơ học thuật...</p>
          </CardContent>
        </Card>
      ) : fetchError ? (
        <Alert variant="destructive">
          <AlertTitle>Lỗi khi tải hồ sơ: {fetchError}</AlertTitle>
          <div className="mt-3">
            <Button
              variant="destructive"
              size="lg"
              onClick={() => window.location.reload()}
            >
              Tải lại trang
            </Button>
          </div>
        </Alert>
      ) : (
        <AcademicProfileForm initialProfile={profile} />
      )}
    </div>
  );
}

export default function AcademicProfilePage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <Suspense
        fallback={
          <div className="flex min-h-[50vh] items-center justify-center">
            <Spinner />
          </div>
        }
      >
        <AcademicProfileContent />
      </Suspense>
    </ProtectedRoute>
  );
}
