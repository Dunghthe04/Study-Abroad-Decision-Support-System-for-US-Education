"use client";

import React from "react";
import type { StudentDashboard } from "@/types/dashboard";
import { KpiCard } from "./KpiCard";
import {
  GraduationCap,
  ClipboardCheck,
  CalendarClock,
  Database,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Clock,
} from "lucide-react";

interface StudentDashboardViewProps {
  dashboard: StudentDashboard;
  studentName?: string;
}

export function StudentDashboardView({
  dashboard,
  studentName,
}: StudentDashboardViewProps) {
  const { recommendation, profileProgress, upcomingDeadlines, dataCompleteness } =
    dashboard;

  return (
    <div className="space-y-6">
      {/* Header Banner */}
      <div className="rounded-2xl bg-linear-to-r from-blue-900 via-indigo-900 to-slate-900 p-6 sm:p-8 text-white shadow-md">
        <div className="max-w-3xl">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 text-xs font-medium text-blue-200 backdrop-blur-xs">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-400"></span>
            Bảng điều khiển KPI du học
          </span>
          <h1 className="mt-3 text-2xl font-bold tracking-tight sm:text-3xl text-white">
            {studentName ? `Tổng quan của ${studentName}` : "Tổng quan hồ sơ & mục tiêu du học"}
          </h1>
          <p className="mt-2 text-sm text-slate-300 leading-relaxed">
            Theo dõi tức thời 4 chỉ số cốt lõi: kết quả gợi ý trường, tiến độ hoàn thiện hồ sơ,
            các hạn nộp hồ sơ sắp tới và độ đầy đủ dữ liệu theo khuyến nghị tuyển sinh Mỹ.
          </p>
        </div>
      </div>

      {/* Grid 4 KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        {/* KPI 1: Gợi ý mới nhất */}
        <KpiCard
          title="Kết quả gợi ý trường mới nhất"
          icon={<GraduationCap className="h-5 w-5 text-blue-600" />}
          badge={
            recommendation.hasRun
              ? { text: "Đã có dữ liệu", variant: "info" }
              : { text: "Chưa chạy", variant: "warning" }
          }
          explanation={recommendation.explanation}
          action={{
            label: recommendation.hasRun
              ? "Xem chi tiết danh sách gợi ý"
              : "Thực hiện gợi ý trường ngay",
            href: "/recommendations",
          }}
        >
          {recommendation.hasRun ? (
            <div className="space-y-3">
              <div className="flex items-baseline gap-2">
                <span className="text-3xl font-bold tracking-tight text-slate-900">
                  {recommendation.total}
                </span>
                <span className="text-sm font-medium text-slate-500">
                  trường trong danh sách
                </span>
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1">
                <div className="rounded-xl border border-rose-200 bg-rose-50/70 p-2.5 text-center">
                  <div className="text-xs font-semibold text-rose-700">Reach</div>
                  <div className="text-lg font-bold text-rose-900">
                    {recommendation.reach}
                  </div>
                </div>
                <div className="rounded-xl border border-blue-200 bg-blue-50/70 p-2.5 text-center">
                  <div className="text-xs font-semibold text-blue-700">Match</div>
                  <div className="text-lg font-bold text-blue-900">
                    {recommendation.match}
                  </div>
                </div>
                <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 p-2.5 text-center">
                  <div className="text-xs font-semibold text-emerald-700">Safety</div>
                  <div className="text-lg font-bold text-emerald-900">
                    {recommendation.safety}
                  </div>
                </div>
                <div className="rounded-xl border border-amber-200 bg-amber-50/70 p-2.5 text-center">
                  <div className="text-xs font-semibold text-amber-700">Học bổng</div>
                  <div className="text-lg font-bold text-amber-900">
                    {recommendation.aid}
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <div className="rounded-xl bg-slate-50 p-4 text-center">
              <AlertCircle className="mx-auto h-8 w-8 text-amber-500 mb-1" />
              <p className="text-sm font-medium text-slate-700">Chưa có kết quả gợi ý trường</p>
              <p className="text-xs text-slate-500 mt-1">
                Hãy hoàn thiện hồ sơ và bấm &quot;Gợi ý trường&quot; để thuật toán đề xuất danh sách phù hợp.
              </p>
            </div>
          )}
        </KpiCard>

        {/* KPI 2: Tiến độ hồ sơ */}
        <KpiCard
          title="Tiến độ hoàn thiện hồ sơ"
          icon={<ClipboardCheck className="h-5 w-5 text-indigo-600" />}
          badge={
            profileProgress.hasCommittedProfile
              ? { text: "Đã nộp", variant: "success" }
              : { text: "Bản nháp", variant: "warning" }
          }
          explanation={profileProgress.explanation}
          action={{
            label: "Tiếp tục nhập hồ sơ (Bước 1-5)",
            href: "/profile-flow",
          }}
        >
          <div className="space-y-3">
            <div className="flex items-baseline justify-between">
              <div className="flex items-baseline gap-1.5">
                <span className="text-3xl font-bold tracking-tight text-slate-900">
                  {profileProgress.completedSteps}
                </span>
                <span className="text-sm font-medium text-slate-500">
                  / {profileProgress.totalSteps} bước hoàn thành
                </span>
              </div>
              <span className="text-xs font-semibold text-slate-700">
                {Math.round((profileProgress.completedSteps / profileProgress.totalSteps) * 100)}%
              </span>
            </div>

            {/* Progress bar */}
            <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
              <div
                className="h-full bg-linear-to-r from-indigo-500 to-blue-600 transition-all duration-300"
                style={{
                  width: `${(profileProgress.completedSteps / profileProgress.totalSteps) * 100}%`,
                }}
              />
            </div>

            <div className="flex items-center gap-2 pt-1">
              <span
                className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-medium ${
                  profileProgress.hasCommittedProfile
                    ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                    : "bg-amber-50 text-amber-700 border border-amber-200"
                }`}
              >
                {profileProgress.hasCommittedProfile ? (
                  <>
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Hồ sơ chính thức đã lưu
                  </>
                ) : (
                  <>
                    <Clock className="h-3.5 w-3.5" />
                    Chưa bấm lưu chính thức ở bước 6
                  </>
                )}
              </span>
            </div>
          </div>
        </KpiCard>

        {/* KPI 3: Hạn nộp sắp tới */}
        <KpiCard
          title="Hạn nộp hồ sơ sắp tới"
          icon={<CalendarClock className="h-5 w-5 text-amber-600" />}
          badge={{
            text: `${upcomingDeadlines.totalTargetSchools} trường mục tiêu`,
            variant: "neutral",
          }}
          explanation={upcomingDeadlines.explanation}
          action={{
            label: "Quản lý danh sách trường mục tiêu",
            href: "/target-schools",
          }}
        >
          {upcomingDeadlines.upcoming.length > 0 ? (
            <div className="space-y-2">
              <div className="divide-y divide-slate-100 max-h-48 overflow-y-auto pr-1">
                {upcomingDeadlines.upcoming.map((school) => {
                  const days = school.daysRemaining ?? 0;
                  const isUrgent = days <= 7;
                  const isSoon = days <= 30;

                  return (
                    <div
                      key={school.targetSchoolId}
                      className="py-2 first:pt-0 last:pb-0 flex items-center justify-between gap-3 text-xs"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="font-semibold text-slate-800 truncate">
                          {school.universityName}
                        </p>
                        <p className="text-slate-500 flex items-center gap-1.5 mt-0.5">
                          <span className="font-medium text-slate-700">
                            {school.applicationRound || "Chưa chọn đợt"}
                          </span>
                          {school.deadline && (
                            <span>
                              • {new Date(school.deadline).toLocaleDateString("vi-VN")}
                            </span>
                          )}
                        </p>
                      </div>

                      <span
                        className={`shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold border ${
                          isUrgent
                            ? "bg-rose-50 text-rose-700 border-rose-200"
                            : isSoon
                            ? "bg-amber-50 text-amber-700 border-amber-200"
                            : "bg-slate-50 text-slate-700 border-slate-200"
                        }`}
                      >
                        {days === 0 ? "Hôm nay" : `Còn ${days} ngày`}
                      </span>
                    </div>
                  );
                })}
              </div>

              {upcomingDeadlines.undecidedRoundCount > 0 && (
                <p className="text-xs text-amber-600 bg-amber-50/60 rounded-lg p-2 border border-amber-200/60">
                  Lưu ý: Có <strong>{upcomingDeadlines.undecidedRoundCount}</strong> trường chưa chọn vòng nộp hoặc chưa có hạn nộp.
                </p>
              )}
            </div>
          ) : (
            <div className="rounded-xl bg-slate-50 p-4 text-center">
              <p className="text-sm font-medium text-slate-700">Không có hạn nộp sắp tới</p>
              <p className="text-xs text-slate-500 mt-1">
                {upcomingDeadlines.totalTargetSchools === 0
                  ? "Bạn chưa thêm trường nào vào danh sách mục tiêu."
                  : "Chưa có trường nào được thiết lập ngày nộp hoặc hạn nộp đã qua."}
              </p>
            </div>
          )}
        </KpiCard>

        {/* KPI 4: Mức đầy đủ dữ liệu */}
        <KpiCard
          title="Mức đầy đủ dữ liệu gợi ý"
          icon={<Database className="h-5 w-5 text-emerald-600" />}
          badge={{
            text: `${dataCompleteness.filledCount}/${dataCompleteness.totalCount} tiêu chí`,
            variant: dataCompleteness.filledCount === 5 ? "success" : "warning",
          }}
          explanation={dataCompleteness.explanation}
          action={{
            label: "Bổ sung thông tin hồ sơ",
            href: "/profile-flow",
          }}
        >
          <div className="space-y-3">
            <div className="space-y-1.5">
              {dataCompleteness.items.map((item) => (
                <div
                  key={item.key}
                  className="flex items-center justify-between text-xs py-1 px-2 rounded-lg bg-slate-50/60"
                >
                  <span className="font-medium text-slate-700">{item.label}</span>
                  {item.isFilled ? (
                    <span className="flex items-center gap-1 text-emerald-700 font-medium">
                      <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                      Đã có
                    </span>
                  ) : (
                    <span className="flex items-center gap-1 text-slate-400">
                      <XCircle className="h-4 w-4 text-slate-400" />
                      Chưa có
                    </span>
                  )}
                </div>
              ))}
            </div>
          </div>
        </KpiCard>
      </div>
    </div>
  );
}
