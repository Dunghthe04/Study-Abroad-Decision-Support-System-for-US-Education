"use client";

import React, { useEffect, useState } from "react";
import { dashboardApi } from "@/lib/api";
import type { ParentChildDashboard } from "@/types/dashboard";
import { StudentDashboardView } from "@/components/dashboard/StudentDashboardView";
import {
  DollarSign,
  TrendingUp,
  AlertTriangle,
  CheckCircle2,
  HelpCircle,
  Loader2,
  Building2,
} from "lucide-react";

interface ParentDashboardKpiSectionProps {
  studentId: string;
  childName: string;
}

export function ParentDashboardKpiSection({
  studentId,
  childName,
}: ParentDashboardKpiSectionProps) {
  const [data, setData] = useState<ParentChildDashboard | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    setIsLoading(true);
    setError(null);

    dashboardApi
      .getChildDashboard(studentId)
      .then((res) => {
        if (isMounted) setData(res);
      })
      .catch((err) => {
        if (isMounted) {
          setError(err?.message || "Không thể tải dữ liệu Dashboard của con.");
        }
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [studentId]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-10 gap-2 text-slate-500">
        <Loader2 className="h-5 w-5 animate-spin text-blue-600" />
        <span className="text-sm">Đang tải chỉ số tổng quan & tài chính...</span>
      </div>
    );
  }

  if (error || !data) {
    return null; // Nếu lỗi hoặc chưa có dữ liệu, không làm gián đoạn phần tóm tắt học thuật bên dưới
  }

  const { studentKpis, financialKpis } = data;
  const fin = financialKpis;

  return (
    <div className="space-y-6 mb-8">
      {/* 1. KHỐI CHỈ SỐ TÀI CHÍNH PHỤ HUYNH */}
      <div className="rounded-2xl border border-slate-200/80 bg-white p-6 shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-3 pb-4 border-b border-slate-100">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-emerald-100/70 text-emerald-800">
              <DollarSign className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900">
                Chỉ số Tài chính & Ước tính Học phí
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">{fin.budgetSourceLabel}</p>
            </div>
          </div>

          {fin.budgetPerYear && (
            <div className="text-right">
              <span className="text-xs text-slate-500">Ngân sách dự kiến/năm:</span>
              <div className="text-lg font-bold text-emerald-700">
                ${fin.budgetPerYear.toLocaleString("en-US")}
              </div>
            </div>
          )}
        </div>

        {/* 3 Thẻ thống kê tài chính nhanh */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 my-4">
          <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
            <div className="flex items-center justify-between text-xs text-slate-500">
              <span>Trong ngân sách</span>
              <CheckCircle2 className="h-4 w-4 text-emerald-600" />
            </div>
            <div className="mt-1 flex items-baseline gap-1.5">
              <span className="text-2xl font-bold text-slate-900">
                {fin.schoolsWithinBudgetCount}
              </span>
              <span className="text-xs text-slate-500">
                / {fin.schoolsTotalWithCostCount} trường có số liệu
              </span>
            </div>
            <p className="mt-1 text-[11px] text-slate-500 leading-tight">
              {fin.withinBudgetExplanation}
            </p>
          </div>

          <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
            <div className="flex items-center justify-between text-xs text-slate-500">
              <span>Thiếu nhiều nhất</span>
              <AlertTriangle className="h-4 w-4 text-rose-500" />
            </div>
            <div className="mt-1">
              {fin.mostShortfallSchool && (fin.mostShortfallSchool.shortfall4Years ?? 0) > 0 ? (
                <>
                  <div className="text-base font-bold text-rose-700 truncate">
                    {fin.mostShortfallSchool.universityCode}
                  </div>
                  <div className="text-xs font-semibold text-rose-600">
                    Thiếu ${(fin.mostShortfallSchool.shortfall4Years ?? 0).toLocaleString("en-US")} / 4 năm
                  </div>
                </>
              ) : (
                <div className="text-sm font-medium text-emerald-600 mt-2">
                  Không trường nào vượt ngân sách
                </div>
              )}
            </div>
            <p className="mt-1 text-[11px] text-slate-500 leading-tight">
              {fin.shortfallExplanation}
            </p>
          </div>

          <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
            <div className="flex items-center justify-between text-xs text-slate-500">
              <span>Thiếu ít nhất / Vừa vặn</span>
              <TrendingUp className="h-4 w-4 text-blue-600" />
            </div>
            <div className="mt-1">
              {fin.leastShortfallSchool ? (
                <>
                  <div className="text-base font-bold text-slate-800 truncate">
                    {fin.leastShortfallSchool.universityCode}
                  </div>
                  <div className="text-xs font-semibold text-slate-600">
                    {(fin.leastShortfallSchool.shortfall4Years ?? 0) === 0
                      ? "Đủ ngân sách (0 USD)"
                      : `Thiếu $${(fin.leastShortfallSchool.shortfall4Years ?? 0).toLocaleString("en-US")} / 4 năm`}
                  </div>
                </>
              ) : (
                <div className="text-sm font-medium text-slate-500 mt-2">
                  Chưa có dữ liệu trường
                </div>
              )}
            </div>
            <p className="mt-1 text-[11px] text-slate-500 leading-tight">
              {fin.totalCost4YearsExplanation}
            </p>
          </div>
        </div>

        {/* Bảng chi tiết trường mục tiêu và mức chênh lệch tài chính */}
        {fin.shortfallList.length > 0 && (
          <div className="mt-4 pt-3 border-t border-slate-100">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">
              So sánh chi phí các trường mục tiêu ({fin.shortfallList.length} trường)
            </h3>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-slate-200/80 text-slate-500">
                    <th className="py-2 pr-3 font-medium">Trường</th>
                    <th className="py-2 px-3 font-medium text-right">Chi phí 1 năm</th>
                    <th className="py-2 px-3 font-medium text-right">Chi phí 4 năm</th>
                    <th className="py-2 px-3 font-medium text-right">Khoản thiếu (4 năm)</th>
                    <th className="py-2 pl-3 font-medium text-center">Đánh giá</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {fin.shortfallList.map((item) => {
                    const isWithin =
                      item.hasCostData &&
                      fin.budgetPerYear != null &&
                      (item.totalCost1Year ?? 0) <= fin.budgetPerYear;

                    return (
                      <tr key={item.universityId} className="hover:bg-slate-50/50">
                        <td className="py-2.5 pr-3 font-medium text-slate-900">
                          <div className="flex items-center gap-1.5">
                            <Building2 className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                            <span className="truncate max-w-xs">{item.universityName}</span>
                            <span className="text-slate-400 font-mono text-[11px]">
                              ({item.universityCode})
                            </span>
                          </div>
                        </td>
                        <td className="py-2.5 px-3 text-right">
                          {item.totalCost1Year != null
                            ? `$${item.totalCost1Year.toLocaleString("en-US")}`
                            : "—"}
                        </td>
                        <td className="py-2.5 px-3 text-right font-medium">
                          {item.totalCost4Years != null
                            ? `$${item.totalCost4Years.toLocaleString("en-US")}`
                            : "—"}
                        </td>
                        <td className="py-2.5 px-3 text-right font-semibold">
                          {item.shortfall4Years != null ? (
                            item.shortfall4Years === 0 ? (
                              <span className="text-emerald-600">0 USD</span>
                            ) : (
                              <span className="text-rose-600">
                                +${item.shortfall4Years.toLocaleString("en-US")}
                              </span>
                            )
                          ) : (
                            <span className="text-slate-400">—</span>
                          )}
                        </td>
                        <td className="py-2.5 pl-3 text-center">
                          {!item.hasCostData ? (
                            <span className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                              <HelpCircle className="h-3 w-3" />
                              Chưa có số liệu
                            </span>
                          ) : isWithin ? (
                            <span className="inline-flex items-center gap-1 rounded-md bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700 border border-emerald-200">
                              <CheckCircle2 className="h-3 w-3" />
                              Trong ngân sách
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 rounded-md bg-rose-50 px-2 py-0.5 text-[11px] font-medium text-rose-700 border border-rose-200">
                              <AlertTriangle className="h-3 w-3" />
                              Vượt ngân sách
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* 2. KHỐI 4 CHỈ SỐ HỌC SINH CỦA CON */}
      <div>
        <h3 className="text-sm font-bold uppercase tracking-wider text-slate-700 mb-3">
          4 Chỉ số KPI học tập & tuyển sinh của {childName}
        </h3>
        <StudentDashboardView dashboard={studentKpis} studentName={childName} />
      </div>
    </div>
  );
}
