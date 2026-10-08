"use client";

import { useMemo, useState } from "react";
import type { TranscriptScore, UpsertTranscriptScoreItem } from "@/types/academic";

interface TranscriptScoreTableProps {
  scores: TranscriptScore[];
  onSaveScores: (scores: UpsertTranscriptScoreItem[]) => Promise<void>;
  onDeleteScore: (scoreId: string) => Promise<void>;
  onAnalyze: () => Promise<void>;
  isAnalyzing: boolean;
}

export type EducationLevel = "highschool" | "university";

const HIGHSCHOOL_TERMS = [
  { order: 1, name: "Lớp 10 HK1" },
  { order: 2, name: "Lớp 10 HK2" },
  { order: 3, name: "Lớp 11 HK1" },
  { order: 4, name: "Lớp 11 HK2" },
  { order: 5, name: "Lớp 12 HK1" },
  { order: 6, name: "Lớp 12 HK2" },
];

const HIGHSCHOOL_SUBJECTS = [
  "Toán",
  "Toán Nâng cao",
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

const UNIVERSITY_TERMS = [
  { order: 1, name: "Năm 1 HK1" },
  { order: 2, name: "Năm 1 HK2" },
  { order: 3, name: "Năm 2 HK1" },
  { order: 4, name: "Năm 2 HK2" },
  { order: 5, name: "Năm 3 HK1" },
  { order: 6, name: "Năm 3 HK2" },
  { order: 7, name: "Năm 4 HK1" },
  { order: 8, name: "Năm 4 HK2" },
];

const UNIVERSITY_SUBJECTS = [
  "Giải tích 1 (Calculus I)",
  "Giải tích 2 (Calculus II)",
  "Đại số tuyến tính",
  "Xác suất thống kê ứng dụng",
  "Toán rời rạc",
  "Lập trình C/C++",
  "Lập trình hướng đối tượng (OOP)",
  "Cấu trúc dữ liệu & Giải thuật",
  "Kiến trúc máy tính",
  "Hệ điều hành (Operating Systems)",
  "Cơ sở dữ liệu (Database Systems)",
  "Mạng máy tính (Computer Networks)",
  "Trí tuệ nhân tạo (AI)",
  "Học máy cơ bản (Machine Learning)",
  "Thị giác máy tính (Computer Vision)",
  "Xử lý ngôn ngữ tự nhiên (NLP)",
  "Điện toán đám mây (Cloud Computing)",
  "Khai phá dữ liệu lớn (Big Data)",
  "Kinh tế vi mô",
  "Kinh tế vĩ mô",
  "Triết học Mác - Lênin",
  "Tiếng Anh học thuật (Academic English)",
  "Khóa luận tốt nghiệp",
];

// Dữ liệu mẫu THPT 3 năm (25 môn học, xu hướng tiến bộ Upward Trend)
const SAMPLE_HIGHSCHOOL_SCORES: UpsertTranscriptScoreItem[] = [
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Toán", score: 8.5, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Ngữ văn", score: 8.0, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Tiếng Anh", score: 8.8, credits: 3 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Vật lý", score: 8.2, credits: 2 },
  { termOrder: 1, termName: "Lớp 10 HK1", subject: "Hóa học", score: 8.0, credits: 2 },

  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Toán", score: 8.8, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Ngữ văn", score: 8.2, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Tiếng Anh", score: 9.0, credits: 3 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Vật lý", score: 8.5, credits: 2 },
  { termOrder: 2, termName: "Lớp 10 HK2", subject: "Hóa học", score: 8.3, credits: 2 },

  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Toán Nâng cao", score: 9.0, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Ngữ văn", score: 8.5, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Tiếng Anh", score: 9.2, credits: 3 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Vật lý", score: 8.8, credits: 2 },
  { termOrder: 3, termName: "Lớp 11 HK1", subject: "Tin học", score: 9.5, credits: 2 },

  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Toán Nâng cao", score: 9.2, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Ngữ văn", score: 8.6, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Tiếng Anh", score: 9.4, credits: 3 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Vật lý", score: 9.0, credits: 2 },
  { termOrder: 4, termName: "Lớp 11 HK2", subject: "Tin học", score: 9.6, credits: 2 },

  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Toán Nâng cao", score: 9.5, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Ngữ văn", score: 8.8, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tiếng Anh", score: 9.6, credits: 3 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Vật lý", score: 9.2, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tin học", score: 9.8, credits: 2 },
];

// Dữ liệu mẫu Đại học 4 năm (28 môn, tính theo tín chỉ tích lũy)
const SAMPLE_UNIVERSITY_SCORES: UpsertTranscriptScoreItem[] = [
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Giải tích 1 (Calculus I)", score: 8.2, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Đại số tuyến tính", score: 8.0, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Triết học Mác - Lênin", score: 7.5, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Tiếng Anh học thuật 1", score: 8.5, credits: 3 },

  { termOrder: 2, termName: "Năm 1 HK2", subject: "Giải tích 2 (Calculus II)", score: 8.5, credits: 3 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Vật lý đại cương", score: 8.2, credits: 3 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Lập trình C/C++", score: 9.0, credits: 4 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Tiếng Anh học thuật 2", score: 8.8, credits: 3 },

  { termOrder: 3, termName: "Năm 2 HK1", subject: "Cấu trúc dữ liệu & Giải thuật", score: 9.0, credits: 4 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Toán rời rạc", score: 8.6, credits: 3 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Kiến trúc máy tính", score: 8.8, credits: 3 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Kinh tế chính trị", score: 8.0, credits: 2 },

  { termOrder: 4, termName: "Năm 2 HK2", subject: "Cơ sở dữ liệu (Database Systems)", score: 9.2, credits: 4 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Hệ điều hành (Operating Systems)", score: 8.8, credits: 3 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Xác suất thống kê ứng dụng", score: 8.5, credits: 3 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Mạng máy tính", score: 8.7, credits: 3 },

  { termOrder: 5, termName: "Năm 3 HK1", subject: "Trí tuệ nhân tạo (AI)", score: 9.4, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Phát triển ứng dụng Web", score: 9.2, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Thiết kế & Phân tích giải thuật", score: 9.0, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Học máy cơ bản (Machine Learning)", score: 9.5, credits: 3 },

  { termOrder: 6, termName: "Năm 3 HK2", subject: "Xử lý ngôn ngữ tự nhiên (NLP)", score: 9.5, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "Thị giác máy tính (Computer Vision)", score: 9.6, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "An toàn & Bảo mật hệ thống", score: 9.0, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "Dự án kỹ thuật phần mềm", score: 9.5, credits: 4 },

  { termOrder: 7, termName: "Năm 4 HK1", subject: "Điện toán đám mây", score: 9.6, credits: 3 },
  { termOrder: 7, termName: "Năm 4 HK1", subject: "Khai phá dữ liệu lớn", score: 9.7, credits: 3 },
  { termOrder: 7, termName: "Năm 4 HK1", subject: "Quản trị dự án CNTT", score: 9.2, credits: 3 },

  { termOrder: 8, termName: "Năm 4 HK2", subject: "Khóa luận tốt nghiệp", score: 9.8, credits: 6 },
];

export function TranscriptScoreTable({
  scores,
  onSaveScores,
  onDeleteScore,
  onAnalyze,
  isAnalyzing,
}: TranscriptScoreTableProps) {
  const [level, setLevel] = useState<EducationLevel>("highschool");

  const currentTerms = level === "highschool" ? HIGHSCHOOL_TERMS : UNIVERSITY_TERMS;
  const currentSubjects = level === "highschool" ? HIGHSCHOOL_SUBJECTS : UNIVERSITY_SUBJECTS;

  const [selectedTermOrder, setSelectedTermOrder] = useState<number>(1);
  const [isCustomTerm, setIsCustomTerm] = useState(false);
  const [customTermName, setCustomTermName] = useState("");
  const [customTermOrder, setCustomTermOrder] = useState<number>(1);

  const [subject, setSubject] = useState("");
  const [score, setScore] = useState<string>("8.5");
  const [credits, setCredits] = useState<string>(level === "university" ? "3" : "2");

  const [filterTerm, setFilterTerm] = useState<number | "all">("all");
  const [searchSubject, setSearchSubject] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  const handleLevelChange = (newLevel: EducationLevel) => {
    setLevel(newLevel);
    setSelectedTermOrder(1);
    setIsCustomTerm(false);
    setCredits(newLevel === "university" ? "3" : "2");
  };

  const handleAddSubject = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!subject.trim()) {
      alert("Vui lòng nhập tên môn học.");
      return;
    }

    // Fix C365-02: Cho phép độ chính xác thập phân tùy ý trong [0.0, 10.0] (ví dụ: 8.25)
    const numScore = parseFloat(score);
    if (isNaN(numScore) || numScore < 0 || numScore > 10) {
      alert("Điểm số phải từ 0.0 đến 10.0.");
      return;
    }

    let finalTermName = "";
    let finalTermOrder = 1;

    if (isCustomTerm) {
      if (!customTermName.trim()) {
        alert("Vui lòng nhập tên kỳ học tùy chỉnh.");
        return;
      }
      finalTermName = customTermName.trim();
      finalTermOrder = customTermOrder;
    } else {
      const found = currentTerms.find((t) => t.order === selectedTermOrder);
      finalTermName = found ? found.name : `Kỳ ${selectedTermOrder}`;
      finalTermOrder = selectedTermOrder;
    }

    const numCredits = credits ? parseFloat(credits) : null;
    if (numCredits !== null && (isNaN(numCredits) || numCredits <= 0 || numCredits > 30)) {
      alert("Số tín chỉ/hệ số phải lớn hơn 0 và không quá 30.");
      return;
    }

    const newItem: UpsertTranscriptScoreItem = {
      termOrder: finalTermOrder,
      termName: finalTermName,
      subject: subject.trim(),
      score: numScore,
      credits: numCredits,
    };

    setIsSaving(true);
    try {
      // Fix C365-03: CHỈ xóa trường tên môn khi lưu thành công vào API
      await onSaveScores([newItem]);
      setSubject("");
    } catch {
      // Khi API trả lỗi (500/400), giữ nguyên dữ liệu trong form để người dùng thử lại
    } finally {
      setIsSaving(false);
    }
  };

  const handleLoadSample = async (sampleType: EducationLevel) => {
    const isHs = sampleType === "highschool";
    const label = isHs ? "THPT (3 năm - 25 môn)" : "Đại học (4 năm - 28 môn có tín chỉ)";
    const dataset = isHs ? SAMPLE_HIGHSCHOOL_SCORES : SAMPLE_UNIVERSITY_SCORES;

    if (confirm(`Nạp dữ liệu học tập mẫu ${label} để kiểm thử thuật toán?`)) {
      setIsSaving(true);
      try {
        await onSaveScores(dataset);
        setLevel(sampleType);
      } catch {
        // Lưu thất bại
      } finally {
        setIsSaving(false);
      }
    }
  };

  const handleDeleteScore = async (scoreId: string) => {
    try {
      await onDeleteScore(scoreId);
    } catch {
      // Bắt lỗi nếu caller rethrow, đảm bảo không gây unhandled rejection
    }
  };

  const stats = useMemo(() => {
    const total = scores.length;
    if (total === 0) return { total: 0, avg: 0, totalCredits: 0 };
    const avg = scores.reduce((sum, s) => sum + s.score, 0) / total;
    const totalCredits = scores.reduce((sum, s) => sum + (s.credits || 0), 0);
    return {
      total,
      avg: Math.round(avg * 10) / 10,
      totalCredits: Math.round(totalCredits * 10) / 10,
    };
  }, [scores]);

  const uniqueTermsInScores = useMemo(() => {
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
    return scores.filter((s) => {
      const matchTerm = filterTerm === "all" || s.termOrder === filterTerm;
      const matchSearch =
        !searchSubject.trim() ||
        s.subject.toLowerCase().includes(searchSubject.toLowerCase()) ||
        s.termName.toLowerCase().includes(searchSubject.toLowerCase());
      return matchTerm && matchSearch;
    });
  }, [scores, filterTerm, searchSubject]);

  const getGroupBadge = (groupKey: string, groupName: string) => {
    switch (groupKey) {
      case "natural_sciences":
        return (
          <span className="inline-flex items-center rounded-sm bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700">
            {groupName}
          </span>
        );
      case "languages":
        return (
          <span className="inline-flex items-center rounded-sm bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">
            {groupName}
          </span>
        );
      case "social_sciences":
        return (
          <span className="inline-flex items-center rounded-sm bg-purple-50 px-2 py-0.5 text-xs font-medium text-purple-700">
            {groupName}
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center rounded-sm bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700">
            {groupName}
          </span>
        );
    }
  };

  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6 shadow-xs space-y-6">
      {/* Top Action Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-5">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-base font-bold text-slate-900">
              Bảng Điểm & Quản Lý Môn Học
            </h3>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-600">
              {scores.length} đầu điểm
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-0.5">
            Dữ liệu đầu vào để tính toán điểm trung bình và phân loại hồ sơ học thuật
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Secondary sample buttons */}
          <div className="flex items-center gap-1.5 text-xs">
            <button
              type="button"
              onClick={() => handleLoadSample("highschool")}
              disabled={isSaving}
              className="rounded-md border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-50 disabled:opacity-50 transition"
            >
              Nạp mẫu THPT (3 năm)
            </button>
            <button
              type="button"
              onClick={() => handleLoadSample("university")}
              disabled={isSaving}
              className="rounded-md border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-50 disabled:opacity-50 transition"
            >
              Nạp mẫu Đại học (4 năm)
            </button>
          </div>

          {/* Primary Action Button */}
          <button
            type="button"
            onClick={onAnalyze}
            disabled={isAnalyzing || scores.length === 0}
            className="inline-flex items-center justify-center rounded-lg bg-slate-900 px-4 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-50 transition shadow-2xs"
          >
            {isAnalyzing ? "Đang tính toán..." : "Phân tích điểm GPA"}
          </button>
        </div>
      </div>

      {/* Input Section */}
      <div className="rounded-lg border border-slate-200/70 bg-slate-50/60 p-4 space-y-3.5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-200/60 pb-3">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-slate-700">Bậc học:</span>
            <div className="inline-flex rounded-md bg-slate-200/70 p-0.5 text-xs">
              <button
                type="button"
                onClick={() => handleLevelChange("highschool")}
                className={`rounded px-2.5 py-1 font-semibold transition ${
                  level === "highschool"
                    ? "bg-white text-slate-900 shadow-2xs"
                    : "text-slate-600 hover:text-slate-900"
                }`}
              >
                Học sinh THPT
              </button>
              <button
                type="button"
                onClick={() => handleLevelChange("university")}
                className={`rounded px-2.5 py-1 font-semibold transition ${
                  level === "university"
                    ? "bg-white text-slate-900 shadow-2xs"
                    : "text-slate-600 hover:text-slate-900"
                }`}
              >
                Sinh viên Đại học
              </button>
            </div>
          </div>

          {/* Fix C365-07: Liên kết label và input toggle kỳ tùy chỉnh */}
          <div className="flex items-center gap-1.5 text-xs text-slate-500">
            <input
              id="transcript-custom-term-toggle"
              type="checkbox"
              checked={isCustomTerm}
              onChange={(e) => setIsCustomTerm(e.target.checked)}
              className="rounded border-slate-300 text-slate-900 focus:ring-slate-900 cursor-pointer"
            />
            <label htmlFor="transcript-custom-term-toggle" className="cursor-pointer">
              Tùy chỉnh tên học kỳ
            </label>
          </div>
        </div>

        {/* Form fields - Fix C365-07: Explicit id & htmlFor on all inputs */}
        <form onSubmit={handleAddSubject} className="grid gap-3 sm:grid-cols-12 items-end">
          <div className="sm:col-span-3">
            <label
              htmlFor={isCustomTerm ? "transcript-custom-term-name" : "transcript-term-select"}
              className="block text-xs font-medium text-slate-600 mb-1"
            >
              {isCustomTerm ? "Tên kỳ tùy chỉnh" : "Học kỳ"}
            </label>
            {isCustomTerm ? (
              <div className="flex gap-1.5">
                <input
                  id="transcript-custom-term-name"
                  type="text"
                  value={customTermName}
                  onChange={(e) => setCustomTermName(e.target.value)}
                  placeholder="VD: Kỳ Hè 2025"
                  className="w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-slate-500 focus:outline-hidden"
                />
                <input
                  id="transcript-custom-term-order"
                  type="number"
                  min="1"
                  max="20"
                  aria-label="Thứ tự học kỳ (1-20)"
                  value={customTermOrder}
                  onChange={(e) => setCustomTermOrder(parseInt(e.target.value) || 1)}
                  className="w-14 rounded border border-slate-300 bg-white px-1.5 py-1.5 text-xs text-center text-slate-900 focus:border-slate-500 focus:outline-hidden"
                  title="Thứ tự thời gian (1-20)"
                />
              </div>
            ) : (
              <select
                id="transcript-term-select"
                value={selectedTermOrder}
                onChange={(e) => setSelectedTermOrder(parseInt(e.target.value))}
                className="w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-slate-500 focus:outline-hidden"
              >
                {currentTerms.map((t) => (
                  <option key={t.order} value={t.order}>
                    {t.name}
                  </option>
                ))}
              </select>
            )}
          </div>

          <div className="sm:col-span-4">
            <label htmlFor="transcript-subject-name" className="block text-xs font-medium text-slate-600 mb-1">
              Tên môn học
            </label>
            <input
              id="transcript-subject-name"
              type="text"
              list="sub-datalist"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder={level === "highschool" ? "VD: Toán, Ngữ văn, Tiếng Anh..." : "VD: Giải tích, Lập trình..."}
              className="w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-slate-500 focus:outline-hidden"
            />
            <datalist id="sub-datalist">
              {currentSubjects.map((s) => (
                <option key={s} value={s} />
              ))}
            </datalist>
          </div>

          {/* Fix C365-02: step="any" cho phép nhập điểm số lẻ như 8.25 */}
          <div className="sm:col-span-2">
            <label htmlFor="transcript-score" className="block text-xs font-medium text-slate-600 mb-1">
              Điểm hệ 10
            </label>
            <input
              id="transcript-score"
              type="number"
              step="any"
              min="0"
              max="10"
              value={score}
              onChange={(e) => setScore(e.target.value)}
              className="w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-slate-500 focus:outline-hidden"
            />
          </div>

          <div className="sm:col-span-2">
            <label htmlFor="transcript-credits" className="block text-xs font-medium text-slate-600 mb-1">
              {level === "university" ? "Số tín chỉ" : "Hệ số / Tín chỉ"}
            </label>
            <input
              id="transcript-credits"
              type="number"
              step="any"
              min="0.5"
              max="30"
              value={credits}
              onChange={(e) => setCredits(e.target.value)}
              className="w-full rounded border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-900 focus:border-slate-500 focus:outline-hidden"
            />
          </div>

          <div className="sm:col-span-1">
            <button
              type="submit"
              disabled={isSaving}
              className="w-full rounded bg-slate-900 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-50 transition"
            >
              Thêm
            </button>
          </div>
        </form>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-1 text-xs">
          <span className="text-xs text-slate-500 mr-1 font-medium">Lọc kỳ:</span>
          <button
            type="button"
            onClick={() => setFilterTerm("all")}
            className={`rounded px-2.5 py-1 font-medium transition ${
              filterTerm === "all"
                ? "bg-slate-900 text-white"
                : "text-slate-600 hover:bg-slate-100"
            }`}
          >
            Tất cả ({scores.length})
          </button>
          {uniqueTermsInScores.map((t) => {
            const count = scores.filter((s) => s.termOrder === t.order).length;
            return (
              <button
                key={t.order}
                type="button"
                onClick={() => setFilterTerm(t.order)}
                className={`rounded px-2.5 py-1 font-medium transition ${
                  filterTerm === t.order
                    ? "bg-slate-900 text-white"
                    : "text-slate-600 hover:bg-slate-100"
                }`}
              >
                {t.name} ({count})
              </button>
            );
          })}
        </div>

        {/* Fix C365-07: aria-label cho input tìm kiếm */}
        <div className="w-full sm:w-56">
          <input
            id="transcript-search-subject"
            type="text"
            aria-label="Lọc theo tên môn học"
            value={searchSubject}
            onChange={(e) => setSearchSubject(e.target.value)}
            placeholder="Lọc theo tên môn..."
            className="w-full rounded border border-slate-200 bg-white px-2.5 py-1 text-xs text-slate-900 placeholder:text-slate-400 focus:border-slate-500 focus:outline-hidden"
          />
        </div>
      </div>

      {/* Compact Data Table */}
      <div className="overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider text-[10px] border-b border-slate-200 font-semibold">
            <tr>
              <th className="px-4 py-2.5">Học kỳ</th>
              <th className="px-4 py-2.5">Môn học</th>
              <th className="px-4 py-2.5">Phân nhóm</th>
              <th className="px-4 py-2.5 text-center">Điểm 10</th>
              <th className="px-4 py-2.5 text-center">Tín chỉ</th>
              <th className="px-4 py-2.5 text-center font-bold text-slate-900">GPA 4.0 (WES)</th>
              <th className="px-4 py-2.5 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredScores.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-500">
                  {scores.length === 0
                    ? "Chưa có môn học nào. Sử dụng nút nạp mẫu ở trên hoặc thêm môn học thủ công."
                    : "Không có môn học nào phù hợp với bộ lọc."}
                </td>
              </tr>
            ) : (
              filteredScores.map((item) => (
                <tr key={item.id} className="hover:bg-slate-50/70 transition-colors">
                  <td className="px-4 py-2 text-slate-700 font-medium whitespace-nowrap">
                    {item.termName}
                  </td>
                  <td className="px-4 py-2 font-semibold text-slate-900">
                    {item.subject}
                  </td>
                  <td className="px-4 py-2 whitespace-nowrap">
                    {getGroupBadge(item.subjectGroup, item.subjectGroupName)}
                  </td>
                  <td className="px-4 py-2 text-center text-slate-700 font-medium">
                    {item.score.toFixed(item.score % 1 === 0 ? 1 : 2)}
                  </td>
                  <td className="px-4 py-2 text-center text-slate-500 font-medium">
                    {item.credits ? `${item.credits}` : "-"}
                  </td>
                  <td className="px-4 py-2 text-center font-bold text-slate-900">
                    {item.gpa4.toFixed(2)}
                  </td>
                  <td className="px-4 py-2 text-right whitespace-nowrap">
                    <button
                      type="button"
                      onClick={() => handleDeleteScore(item.id)}
                      className="text-slate-400 hover:text-red-600 text-xs font-medium transition-colors"
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
          {scores.length > 0 && (
            <tfoot className="bg-slate-50/80 border-t border-slate-200 text-slate-700 text-xs font-semibold">
              <tr>
                <td colSpan={3} className="px-4 py-2">
                  Tổng kết ({stats.total} môn học):
                </td>
                <td className="px-4 py-2 text-center">
                  TB {stats.avg}
                </td>
                <td className="px-4 py-2 text-center">
                  {stats.totalCredits > 0 ? `${stats.totalCredits} TC` : "-"}
                </td>
                <td colSpan={2} className="px-4 py-2 text-right text-slate-500 font-normal text-xs">
                  * Bấm &quot;Phân tích điểm GPA&quot; để tính toán toàn diện
                </td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
