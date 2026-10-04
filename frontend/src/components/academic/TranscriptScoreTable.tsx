"use client";

import { useState } from "react";
import type { TranscriptScore, UpsertTranscriptScoreItem } from "@/types/academic";

interface TranscriptScoreTableProps {
  scores: TranscriptScore[];
  onSaveScores: (scores: UpsertTranscriptScoreItem[]) => Promise<void>;
  onDeleteScore: (scoreId: string) => Promise<void>;
  onAnalyze: () => Promise<void>;
  isAnalyzing: boolean;
}

const COMMON_TERMS = [
  { order: 1, name: "Lớp 10 HK1" },
  { order: 2, name: "Lớp 10 HK2" },
  { order: 3, name: "Lớp 11 HK1" },
  { order: 4, name: "Lớp 11 HK2" },
  { order: 5, name: "Lớp 12 HK1" },
  { order: 6, name: "Lớp 12 HK2" },
];

const COMMON_SUBJECTS = [
  "Toán",
  "Ngữ văn",
  "Tiếng Anh",
  "Vật lý",
  "Hóa học",
  "Sinh học",
  "Lịch sử",
  "Địa lý",
  "Tin học",
  "Giáo dục công dân",
];

// Dữ liệu mẫu học sinh 3 năm chuẩn bị du học Mỹ
const SAMPLE_SCORES: UpsertTranscriptScoreItem[] = [
  // Lớp 10 HK1 (Nền tảng khởi đầu)
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Toán", score: 8.5, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Ngữ văn", score: 8.0, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Tiếng Anh", score: 8.8, credits: 3 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Vật lý", score: 8.2, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Hóa học", score: 8.0, credits: 2 },

  // Lớp 10 HK2
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Toán", score: 8.8, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Ngữ văn", score: 8.2, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Tiếng Anh", score: 9.0, credits: 3 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Vật lý", score: 8.5, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Hóa học", score: 8.3, credits: 2 },

  // Lớp 11 HK1 (Tăng tốc)
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Toán Nâng cao", score: 9.0, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Ngữ văn", score: 8.5, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Tiếng Anh", score: 9.2, credits: 3 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Vật lý", score: 8.8, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Tin học", score: 9.5, credits: 2 },

  // Lớp 11 HK2
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Toán Nâng cao", score: 9.2, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Ngữ văn", score: 8.6, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Tiếng Anh", score: 9.4, credits: 3 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Vật lý", score: 9.0, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Tin học", score: 9.6, credits: 2 },

  // Lớp 12 HK1 (Xu hướng bứt phá - Upward Trend)
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Toán Nâng cao", score: 9.5, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Ngữ văn", score: 8.8, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tiếng Anh", score: 9.6, credits: 3 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Vật lý", score: 9.2, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tin học", score: 9.8, credits: 2 },
];

/**
 * [USAS-365] Bảng quản lý điểm chi tiết từng môn theo học kỳ.
 */
export function TranscriptScoreTable({
  scores,
  onSaveScores,
  onDeleteScore,
  onAnalyze,
  isAnalyzing,
}: TranscriptScoreTableProps) {
  // State form thêm môn nhanh
  const [selectedTerm, setSelectedTerm] = useState(COMMON_TERMS[0]);
  const [subject, setSubject] = useState(COMMON_SUBJECTS[0]);
  const [score, setScore] = useState<string>("8.5");
  const [credits, setCredits] = useState<string>("2");
  const [filterTerm, setFilterTerm] = useState<number | "all">("all");
  const [isSaving, setIsSaving] = useState(false);

  const handleAddSubject = async () => {
    const numScore = parseFloat(score);
    if (isNaN(numScore) || numScore < 0 || numScore > 10) {
      alert("Điểm số phải từ 0.0 đến 10.0");
      return;
    }

    const numCredits = credits ? parseFloat(credits) : null;
    if (numCredits !== null && (isNaN(numCredits) || numCredits <= 0)) {
      alert("Số tín chỉ/hệ số phải lớn hơn 0");
      return;
    }

    const newItem: UpsertTranscriptScoreItem = {
      termOrder: selectedTerm.order,
      termName: selectedTerm.name,
      subject: subject.trim(),
      score: numScore,
      credits: numCredits,
    };

    setIsSaving(true);
    try {
      await onSaveScores([newItem]);
    } finally {
      setIsSaving(false);
    }
  };

  const handleLoadSample = async () => {
    if (confirm("Thao tác này sẽ nạp dữ liệu bảng điểm mẫu (3 năm gồm 25 môn học có xu hướng tiến bộ). Bạn có muốn tiếp tục?")) {
      setIsSaving(true);
      try {
        await onSaveScores(SAMPLE_SCORES);
      } finally {
        setIsSaving(false);
      }
    }
  };

  const filteredScores = filterTerm === "all"
    ? scores
    : scores.filter((s) => s.termOrder === filterTerm);

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-5">
        <div>
          <h3 className="text-lg font-bold text-slate-900">
            Bảng Điểm Chi Tiết Từng Môn Theo Học Kỳ
          </h3>
          <p className="text-xs text-slate-500 mt-1">
            Hệ thống hỗ trợ nhập bảng điểm 3 năm gần nhất để thuật toán quy đổi GPA và nhận diện xu hướng học tập.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={handleLoadSample}
            disabled={isSaving}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            📋 Nạp dữ liệu mẫu (3 năm)
          </button>
          <button
            type="button"
            onClick={onAnalyze}
            disabled={isAnalyzing || scores.length === 0}
            className="flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-xs font-bold text-white hover:bg-blue-700 disabled:opacity-50 shadow-xs"
          >
            {isAnalyzing ? "Đang phân tích..." : "⚡ Phân tích điểm học thuật"}
          </button>
        </div>
      </div>

      {/* Form nhập môn học nhanh */}
      <div className="mt-5 rounded-lg bg-slate-50 p-4 border border-slate-200">
        <span className="text-xs font-bold uppercase tracking-wider text-slate-700">
          Thêm Môn Học Vào Bảng Điểm
        </span>
        <div className="mt-3 grid gap-3 sm:grid-cols-5">
          {/* Học kỳ */}
          <div className="sm:col-span-1">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Học kỳ
            </label>
            <select
              value={selectedTerm.order}
              onChange={(e) => {
                const found = COMMON_TERMS.find((t) => t.order === parseInt(e.target.value));
                if (found) setSelectedTerm(found);
              }}
              className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
            >
              {COMMON_TERMS.map((t) => (
                <option key={t.order} value={t.order}>
                  {t.name}
                </option>
              ))}
            </select>
          </div>

          {/* Môn học */}
          <div className="sm:col-span-2">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Tên môn học
            </label>
            <input
              type="text"
              list="subjects-list"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="VD: Toán, Ngữ văn, Tiếng Anh..."
              className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
            />
            <datalist id="subjects-list">
              {COMMON_SUBJECTS.map((s) => (
                <option key={s} value={s} />
              ))}
            </datalist>
          </div>

          {/* Điểm hệ 10 */}
          <div className="sm:col-span-1">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Điểm hệ 10
            </label>
            <input
              type="number"
              step="0.1"
              min="0"
              max="10"
              value={score}
              onChange={(e) => setScore(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
            />
          </div>

          {/* Nút thêm */}
          <div className="sm:col-span-1 flex items-end">
            <button
              type="button"
              onClick={handleAddSubject}
              disabled={isSaving}
              className="w-full rounded-md bg-slate-900 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-50"
            >
              + Thêm môn
            </button>
          </div>
        </div>
      </div>

      {/* Bộ lọc học kỳ */}
      <div className="mt-5 flex items-center justify-between">
        <div className="flex flex-wrap gap-1 text-xs">
          <button
            type="button"
            onClick={() => setFilterTerm("all")}
            className={`rounded-md px-2.5 py-1 font-medium ${
              filterTerm === "all"
                ? "bg-blue-600 text-white"
                : "bg-slate-100 text-slate-600 hover:bg-slate-200"
            }`}
          >
            Tất cả ({scores.length})
          </button>
          {COMMON_TERMS.map((term) => {
            const count = scores.filter((s) => s.termOrder === term.order).length;
            if (count === 0) return null;
            return (
              <button
                key={term.order}
                type="button"
                onClick={() => setFilterTerm(term.order)}
                className={`rounded-md px-2.5 py-1 font-medium ${
                  filterTerm === term.order
                    ? "bg-blue-600 text-white"
                    : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                }`}
              >
                {term.name} ({count})
              </button>
            );
          })}
        </div>
      </div>

      {/* Bảng dữ liệu điểm */}
      <div className="mt-3 overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-50 text-slate-600 uppercase tracking-wider text-[11px] border-b border-slate-200">
            <tr>
              <th className="px-4 py-3">Học kỳ</th>
              <th className="px-4 py-3">Môn học</th>
              <th className="px-4 py-3">Phân nhóm</th>
              <th className="px-4 py-3 text-center">Điểm hệ 10</th>
              <th className="px-4 py-3 text-center">Tín chỉ / Hệ số</th>
              <th className="px-4 py-3 text-center">GPA 4.0 (Quy đổi)</th>
              <th className="px-4 py-3 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredScores.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                  Chưa có đầu điểm nào. Nhấn <strong>"Nạp dữ liệu mẫu (3 năm)"</strong> hoặc thêm môn học ở form trên.
                </td>
              </tr>
            ) : (
              filteredScores.map((item) => (
                <tr key={item.id} className="hover:bg-slate-50/70 transition-colors">
                  <td className="px-4 py-2.5 font-medium text-slate-800">
                    {item.termName}
                  </td>
                  <td className="px-4 py-2.5 font-bold text-slate-900">
                    {item.subject}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="rounded bg-slate-100 px-2 py-0.5 text-[10px] font-medium text-slate-700">
                      {item.subjectGroupName}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-center font-bold text-slate-800">
                    {item.score.toFixed(1)}
                  </td>
                  <td className="px-4 py-2.5 text-center text-slate-500">
                    {item.credits ?? "-"}
                  </td>
                  <td className="px-4 py-2.5 text-center font-black text-blue-600">
                    {item.gpa4.toFixed(2)}
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    <button
                      type="button"
                      onClick={() => onDeleteScore(item.id)}
                      className="text-red-500 hover:text-red-700 text-[11px] font-medium"
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
