"use client";

import React, { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AcademicProfileForm } from "@/components/profile/AcademicProfileForm";
import { AcademicAnalysisView } from "@/components/academic/AcademicAnalysisView";
import { profileApi, ApiError } from "@/lib/api";
import type { AcademicProfileResponse } from "@/types/api";

function AcademicProfileContent() {
  const searchParams = useSearchParams();
  const autoAnalyze = searchParams.get("autoAnalyze") === "true";
  const requestedTab = searchParams.get("tab");

  const [selectedTab, setSelectedTab] = useState<"profile" | "analysis" | null>(null);
  const activeTab = selectedTab ?? (autoAnalyze || requestedTab === "analysis" ? "analysis" : "profile");

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
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      {/* Breadcrumb Navigation */}
      <nav className="mb-6 flex items-center gap-2 text-xs font-medium text-slate-500">
        <Link href="/" className="hover:text-blue-600 transition">
          Trang chủ
        </Link>
        <span>/</span>
        <Link href="/account" className="hover:text-blue-600 transition">
          Tài khoản
        </Link>
        <span>/</span>
        <span className="text-slate-900 font-semibold">
          {activeTab === "profile" ? "Hồ sơ học thuật" : "Đánh giá Năng lực & GPA"}
        </span>
      </nav>

      {/* Header Banner */}
      <div className="mb-6 rounded-2xl border border-slate-200 bg-gradient-to-r from-blue-900 via-indigo-900 to-slate-900 p-6 sm:p-8 text-white shadow-sm">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">
              Hồ sơ Học thuật & Đánh giá Năng lực
            </h1>
            <p className="mt-2 max-w-2xl text-sm text-slate-300">
              Khai báo kết quả học tập, chứng chỉ chuẩn hóa (IELTS/SAT) và quy đổi điểm chuẩn WES 4.0 phục vụ tư vấn tuyển sinh đại học Mỹ.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Link
              href="/advisor"
              className="inline-flex items-center gap-1.5 rounded-xl bg-white/10 px-4 py-2 text-xs font-semibold text-white backdrop-blur transition hover:bg-white/20 border border-white/20"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
              </svg>
              Chat Advisor
            </Link>
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="mb-8 flex border-b border-slate-200 bg-white rounded-t-xl px-2">
        <button
          type="button"
          onClick={() => setSelectedTab("profile")}
          className={`flex items-center gap-2 border-b-2 px-5 py-3 text-sm font-semibold transition cursor-pointer ${
            activeTab === "profile"
              ? "border-blue-600 text-blue-600"
              : "border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700"
          }`}
        >
          <span>📑</span> Hồ sơ học thuật & Chứng chỉ
        </button>
        <button
          type="button"
          onClick={() => setSelectedTab("analysis")}
          className={`flex items-center gap-2 border-b-2 px-5 py-3 text-sm font-semibold transition cursor-pointer ${
            activeTab === "analysis"
              ? "border-blue-600 text-blue-600"
              : "border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700"
          }`}
        >
          <span>📊</span> Phân tích GPA WES 4.0 & Bảng điểm
        </button>
      </div>

      {activeTab === "profile" ? (
        isLoading ? (
          <div className="flex min-h-[300px] items-center justify-center rounded-2xl border border-slate-200 bg-white p-12">
            <div className="text-center">
              <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-blue-600 border-r-transparent"></div>
              <p className="mt-3 text-sm font-medium text-slate-600">Đang tải dữ liệu hồ sơ học thuật...</p>
            </div>
          </div>
        ) : fetchError ? (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center">
            <p className="text-sm font-semibold text-red-700">Lỗi khi tải hồ sơ: {fetchError}</p>
            <button
              onClick={() => window.location.reload()}
              className="mt-3 inline-flex items-center rounded-lg bg-red-600 px-4 py-2 text-xs font-semibold text-white hover:bg-red-700"
            >
              Tải lại trang
            </button>
          </div>
        ) : (
          <AcademicProfileForm initialProfile={profile} autoAnalyze={autoAnalyze} />
        )
      ) : (
        <AcademicAnalysisView autoAnalyze={autoAnalyze} />
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
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-blue-600 border-r-transparent"></div>
          </div>
        }
      >
        <AcademicProfileContent />
      </Suspense>
    </ProtectedRoute>
  );
}
