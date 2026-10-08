"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import type { TranscriptScore } from "@/types/academic";

interface ReadOnlyTranscriptTableProps {
  scores: TranscriptScore[];
  onRefreshAnalysis?: () => void;
  isAnalyzing?: boolean;
}

export function ReadOnlyTranscriptTable({
  scores,
  onRefreshAnalysis,
  isAnalyzing = false,
}: ReadOnlyTranscriptTableProps) {
  const [filterTerm, setFilterTerm] = useState<number | "all">("all");
  const [searchSubject, setSearchSubject] = useState("");

  const uniqueTerms = useMemo(() => {
    const map = new Map<number, string>();
    scores.forEach((s) => {
      if (!map.has(s.termOrder)) {
        map.set(s.termOrder, s.termName);
      }
    });
    return Array.from(map.entries())
      .map(([order, name]) => ({ order, name }))
      .sort((a, b) => a.order - b.order);
  }, [scores]);

  const filteredScores = useMemo(() => {
    return scores.filter((item) => {
      const matchTerm = filterTerm === "all" || item.termOrder === filterTerm;
      const matchSearch =
        !searchSubject.trim() ||
        item.subject.toLowerCase().includes(searchSubject.toLowerCase().trim());
      return matchTerm && matchSearch;
    });
  }, [scores, filterTerm, searchSubject]);

  const stats = useMemo(() => {
    if (filteredScores.length === 0) return { count: 0, totalCredits: 0, avgRaw: 0 };
    const count = filteredScores.length;
    const totalCredits = filteredScores.reduce((acc, cur) => acc + (cur.credits || 1), 0);
    const sumWeightedScore = filteredScores.reduce(
      (acc, cur) => acc + cur.score * (cur.credits || 1),
      0
    );
    const avgRaw = totalCredits > 0 ? sumWeightedScore / totalCredits : 0;
    return { count, totalCredits, avgRaw: Math.round(avgRaw * 100) / 100 };
  }, [filteredScores]);

  const getGroupBadgeClass = (groupKey: string) => {
    switch (groupKey) {
      case "stem":
        return "bg-blue-50 text-blue-700 border-blue-200";
      case "languages":
        return "bg-emerald-50 text-emerald-700 border-emerald-200";
      case "social_sciences":
        return "bg-purple-50 text-purple-700 border-purple-200";
      default:
        return "bg-slate-50 text-slate-700 border-slate-200";
    }
  };

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 sm:p-6 shadow-xs space-y-4">
      {/* Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-slate-900 text-sm sm:text-base">
              Bảng Điểm Học Tập & Quy Đổi WES 4.0
            </h3>
            <span className="rounded-full bg-blue-50 text-blue-700 font-semibold px-2 py-0.5 text-[11px] border border-blue-200">
              {scores.length} đầu điểm
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-0.5">
            Dữ liệu trích xuất từ hồ sơ học bạ học sinh đã khai báo, được quy đổi theo thang điểm chuẩn WES của Mỹ.
          </p>
        </div>

        <div className="flex items-center gap-2">
          {onRefreshAnalysis && (
            <button
              type="button"
              onClick={onRefreshAnalysis}
              disabled={isAnalyzing}
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 transition cursor-pointer disabled:opacity-50"
            >
              <span>🔄</span>
              {isAnalyzing ? "Đang tính..." : "Cập nhật kết quả"}
            </button>
          )}

          <Link
            href="/profile/academic"
            className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-blue-700 transition"
          >
            <span>✏️</span> Chỉnh sửa bảng điểm
          </Link>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        {/* Term Tabs */}
        <div className="flex flex-wrap gap-1 text-xs">
          <button
            type="button"
            onClick={() => setFilterTerm("all")}
            className={`rounded-md px-2.5 py-1 font-medium transition cursor-pointer ${
              filterTerm === "all"
                ? "bg-slate-900 text-white"
                : "text-slate-600 hover:bg-slate-100 bg-slate-50"
            }`}
          >
            Tất cả ({scores.length})
          </button>
          {uniqueTerms.map((t) => {
            const count = scores.filter((s) => s.termOrder === t.order).length;
            return (
              <button
                key={t.order}
                type="button"
                onClick={() => setFilterTerm(t.order)}
                className={`rounded-md px-2.5 py-1 font-medium transition cursor-pointer ${
                  filterTerm === t.order
                    ? "bg-slate-900 text-white"
                    : "text-slate-600 hover:bg-slate-100 bg-slate-50"
                }`}
              >
                {t.name} ({count})
              </button>
            );
          })}
        </div>

        {/* Search Input */}
        <div className="w-full sm:w-56">
          <input
            id="readonly-search-subject"
            type="text"
            aria-label="Lọc theo tên môn học"
            value={searchSubject}
            onChange={(e) => setSearchSubject(e.target.value)}
            placeholder="Tìm kiếm môn học..."
            className="w-full rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-hidden"
          />
        </div>
      </div>

      {/* Table */}
      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider text-[10px] border-b border-slate-200 font-semibold">
            <tr>
              <th className="px-4 py-3">Học kỳ</th>
              <th className="px-4 py-3">Tên môn học</th>
              <th className="px-4 py-3">Phân nhóm</th>
              <th className="px-4 py-3 text-center">Điểm hệ 10</th>
              <th className="px-4 py-3 text-center">Tín chỉ / Hệ số</th>
              <th className="px-4 py-3 text-center font-bold text-slate-900">Điểm GPA 4.0 (WES)</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredScores.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-500">
                  {scores.length === 0
                    ? "Chưa có môn học nào trong bảng điểm. Vui lòng bấm 'Chỉnh sửa bảng điểm' để nhập liệu."
                    : "Không tìm thấy môn học nào khớp với bộ lọc."}
                </td>
              </tr>
            ) : (
              filteredScores.map((item) => (
                <tr key={item.id} className="hover:bg-slate-50/70 transition">
                  <td className="px-4 py-2.5 font-medium text-slate-700 whitespace-nowrap">
                    {item.termName}
                  </td>
                  <td className="px-4 py-2.5 font-semibold text-slate-900">
                    {item.subject}
                  </td>
                  <td className="px-4 py-2.5">
                    <span
                      className={`inline-block rounded-full border px-2 py-0.5 text-[10px] font-semibold whitespace-nowrap ${getGroupBadgeClass(
                        item.subjectGroup
                      )}`}
                    >
                      {item.subjectGroupName}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-center font-medium text-slate-800">
                    {item.score.toFixed(1)}
                  </td>
                  <td className="px-4 py-2.5 text-center text-slate-600">
                    {item.credits || 1}
                  </td>
                  <td className="px-4 py-2.5 text-center font-bold text-slate-900">
                    {item.gpa4.toFixed(2)}
                  </td>
                </tr>
              ))
            )}
          </tbody>
          {filteredScores.length > 0 && (
            <tfoot className="bg-slate-50 border-t border-slate-200 font-semibold text-slate-700">
              <tr>
                <td colSpan={3} className="px-4 py-2.5">
                  Tổng kết hiển thị ({stats.count} môn):
                </td>
                <td className="px-4 py-2.5 text-center">
                  TB: {stats.avgRaw.toFixed(2)}
                </td>
                <td className="px-4 py-2.5 text-center">
                  {stats.totalCredits} TC
                </td>
                <td className="px-4 py-2.5 text-center text-slate-500 text-[11px] font-normal">
                  * Thang chuẩn WES
                </td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
