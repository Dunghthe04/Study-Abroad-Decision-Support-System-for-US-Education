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
  "Lập trình C/C++ căn bản",
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
  "Khóa luận tốt nghiệp (Graduation Thesis)",
];

// Dữ liệu mẫu THPT 3 năm (25 môn học, xu hướng Upward Trend)
const SAMPLE_HIGHSCHOOL_SCORES: UpsertTranscriptScoreItem[] = [
  // Lớp 10 HK1
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

  // Lớp 11 HK1
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

  // Lớp 12 HK1
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Toán Nâng cao", score: 9.5, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Ngữ văn", score: 8.8, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tiếng Anh", score: 9.6, credits: 3 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Vật lý", score: 9.2, credits: 2 },
  { termOrder: 5, termName: "Lớp 12 HK1", subject: "Tin học", score: 9.8, credits: 2 },
];

// Dữ liệu mẫu Đại học 4 năm (28 môn, tính theo tín chỉ, nộp Thạc sĩ/Tiến sĩ)
const SAMPLE_UNIVERSITY_SCORES: UpsertTranscriptScoreItem[] = [
  // Năm 1 HK1
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Giải tích 1 (Calculus I)", score: 8.2, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Đại số tuyến tính", score: 8.0, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Triết học Mác - Lênin", score: 7.5, credits: 3 },
  { termOrder: 1, termName: "Năm 1 HK1", subject: "Tiếng Anh học thuật 1", score: 8.5, credits: 3 },

  // Năm 1 HK2
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Giải tích 2 (Calculus II)", score: 8.5, credits: 3 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Vật lý đại cương", score: 8.2, credits: 3 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Lập trình C/C++ căn bản", score: 9.0, credits: 4 },
  { termOrder: 2, termName: "Năm 1 HK2", subject: "Tiếng Anh học thuật 2", score: 8.8, credits: 3 },

  // Năm 2 HK1
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Cấu trúc dữ liệu & Giải thuật", score: 9.0, credits: 4 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Toán rời rạc", score: 8.6, credits: 3 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Kiến trúc máy tính", score: 8.8, credits: 3 },
  { termOrder: 3, termName: "Năm 2 HK1", subject: "Kinh tế chính trị", score: 8.0, credits: 2 },

  // Năm 2 HK2
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Cơ sở dữ liệu (Database Systems)", score: 9.2, credits: 4 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Hệ điều hành (Operating Systems)", score: 8.8, credits: 3 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Xác suất thống kê ứng dụng", score: 8.5, credits: 3 },
  { termOrder: 4, termName: "Năm 2 HK2", subject: "Mạng máy tính", score: 8.7, credits: 3 },

  // Năm 3 HK1
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Trí tuệ nhân tạo (AI)", score: 9.4, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Phát triển ứng dụng Web", score: 9.2, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Thiết kế & Phân tích giải thuật", score: 9.0, credits: 3 },
  { termOrder: 5, termName: "Năm 3 HK1", subject: "Học máy cơ bản (Machine Learning)", score: 9.5, credits: 3 },

  // Năm 3 HK2
  { termOrder: 6, termName: "Năm 3 HK2", subject: "Xử lý ngôn ngữ tự nhiên (NLP)", score: 9.5, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "Thị giác máy tính (Computer Vision)", score: 9.6, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "An toàn & Bảo mật hệ thống", score: 9.0, credits: 3 },
  { termOrder: 6, termName: "Năm 3 HK2", subject: "Dự án kỹ thuật phần mềm", score: 9.5, credits: 4 },

  // Năm 4 HK1
  { termOrder: 7, termName: "Năm 4 HK1", subject: "Điện toán đám mây (Cloud Computing)", score: 9.6, credits: 3 },
  { termOrder: 7, termName: "Năm 4 HK1", subject: "Khai phá dữ liệu lớn (Big Data)", score: 9.7, credits: 3 },
  { termOrder: 7, termName: "Năm 4 HK1", subject: "Quản trị dự án CNTT", score: 9.2, credits: 3 },

  // Năm 4 HK2
  { termOrder: 8, termName: "Năm 4 HK2", subject: "Khóa luận tốt nghiệp (Graduation Thesis)", score: 9.8, credits: 6 },
];

/**
 * [USAS-365] Bảng quản lý điểm chi tiết từng môn:
 * Hỗ trợ linh hoạt cho cả Học sinh THPT (nộp Đại học) và Sinh viên Đại học (nộp Master/PhD).
 */
export function TranscriptScoreTable({
  scores,
  onSaveScores,
  onDeleteScore,
  onAnalyze,
  isAnalyzing,
}: TranscriptScoreTableProps) {
  // Chọn bậc học hiện tại
  const [level, setLevel] = useState<EducationLevel>("highschool");

  // Danh sách kỳ theo bậc học
  const currentTerms = level === "highschool" ? HIGHSCHOOL_TERMS : UNIVERSITY_TERMS;
  const currentSubjects = level === "highschool" ? HIGHSCHOOL_SUBJECTS : UNIVERSITY_SUBJECTS;

  // State form thêm môn học
  const [selectedTermOrder, setSelectedTermOrder] = useState<number>(1);
  const [isCustomTerm, setIsCustomTerm] = useState(false);
  const [customTermName, setCustomTermName] = useState("");
  const [customTermOrder, setCustomTermOrder] = useState<number>(1);

  const [subject, setSubject] = useState("");
  const [score, setScore] = useState<string>("8.5");
  const [credits, setCredits] = useState<string>(level === "university" ? "3" : "2");

  // State bộ lọc và tìm kiếm
  const [filterTerm, setFilterTerm] = useState<number | "all">("all");
  const [searchSubject, setSearchSubject] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  // Cập nhật số tín chỉ mặc định khi đổi bậc học
  const handleLevelChange = (newLevel: EducationLevel) => {
    setLevel(newLevel);
    setSelectedTermOrder(1);
    setIsCustomTerm(false);
    setCredits(newLevel === "university" ? "3" : "2");
  };

  const handleAddSubject = async () => {
    if (!subject.trim()) {
      alert("Vui lòng nhập hoặc chọn tên môn học.");
      return;
    }

    const numScore = parseFloat(score);
    if (isNaN(numScore) || numScore < 0 || numScore > 10) {
      alert("Điểm số phải từ 0.0 đến 10.0.");
      return;
    }

    let finalTermName = "";
    let finalTermOrder = 1;

    if (isCustomTerm) {
      if (!customTermName.trim()) {
        alert("Vui lòng nhập tên kỳ học tùy chỉnh (VD: Kỳ Hè 2025).");
        return;
      }
      finalTermName = customTermName.trim();
      finalTermOrder = customTermOrder;
    } else {
      const found = currentTerms.find((t) => t.order === selectedTermOrder);
      finalTermName = found ? found.name : `Học kỳ ${selectedTermOrder}`;
      finalTermOrder = selectedTermOrder;
    }

    const numCredits = credits ? parseFloat(credits) : null;
    if (numCredits !== null && (isNaN(numCredits) || numCredits <= 0 || numCredits > 30)) {
      alert("Số tín chỉ/hệ số phải lớn hơn 0 và không vượt quá 30.");
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
      await onSaveScores([newItem]);
      setSubject("");
    } finally {
      setIsSaving(false);
    }
  };

  const handleLoadSample = async (sampleType: EducationLevel) => {
    const isHs = sampleType === "highschool";
    const label = isHs ? "THPT (3 năm - 25 môn)" : "Đại học (4 năm - 28 môn có tín chỉ)";
    const dataset = isHs ? SAMPLE_HIGHSCHOOL_SCORES : SAMPLE_UNIVERSITY_SCORES;

    if (
      confirm(
        `Thao tác này sẽ nạp dữ liệu bảng điểm mẫu ${label} với xu hướng học tập tiến bộ (Upward Trend). Tiếp tục?`
      )
    ) {
      setIsSaving(true);
      try {
        await onSaveScores(dataset);
        setLevel(sampleType);
      } finally {
        setIsSaving(false);
      }
    }
  };

  // Thống kê nhanh từ danh sách điểm hiện tại
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

  // Danh sách các kỳ thực tế có trong bảng điểm để làm bộ lọc
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

  // Lọc dữ liệu hiển thị
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
          <span className="inline-flex items-center gap-1 rounded bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700 border border-blue-200">
            <span>🔬</span> {groupName}
          </span>
        );
      case "languages":
        return (
          <span className="inline-flex items-center gap-1 rounded bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700 border border-emerald-200">
            <span>🌐</span> {groupName}
          </span>
        );
      case "social_sciences":
        return (
          <span className="inline-flex items-center gap-1 rounded bg-purple-50 px-2 py-0.5 text-[11px] font-semibold text-purple-700 border border-purple-200">
            <span>📚</span> {groupName}
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 rounded bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 border border-slate-200">
            <span>📌</span> {groupName}
          </span>
        );
    }
  };

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      {/* Header bảng điểm */}
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-100 pb-5">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-lg font-bold text-slate-900">
              Bảng Điểm Chi Tiết & Nhập Điểm Môn Học
            </h3>
            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700 border border-slate-200">
              {scores.length} môn đã lưu
            </span>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            Hỗ trợ nhập học bạ THPT (nộp Đại học Mỹ) hoặc bảng điểm Đại học theo tín chỉ (nộp Thạc sĩ / Tiến sĩ).
          </p>
        </div>

        {/* Nút hành động */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Dropdown nạp mẫu */}
          <div className="flex items-center rounded-lg border border-slate-200 bg-slate-50 p-0.5 text-xs">
            <button
              type="button"
              onClick={() => handleLoadSample("highschool")}
              disabled={isSaving}
              className="rounded-md px-2.5 py-1.5 font-medium text-slate-700 hover:bg-white hover:shadow-xs disabled:opacity-50 transition"
              title="Nạp dữ liệu mẫu 3 năm THPT (25 môn)"
            >
              🎓 Mẫu THPT (3 năm)
            </button>
            <span className="text-slate-300">|</span>
            <button
              type="button"
              onClick={() => handleLoadSample("university")}
              disabled={isSaving}
              className="rounded-md px-2.5 py-1.5 font-medium text-slate-700 hover:bg-white hover:shadow-xs disabled:opacity-50 transition"
              title="Nạp dữ liệu mẫu 4 năm Đại học có tín chỉ (28 môn)"
            >
              🏛️ Mẫu Đại học (4 năm)
            </button>
          </div>

          <button
            type="button"
            onClick={onAnalyze}
            disabled={isAnalyzing || scores.length === 0}
            className="flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-xs font-bold text-white hover:bg-blue-700 disabled:opacity-50 shadow-xs transition"
          >
            {isAnalyzing ? (
              <>
                <span className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-white border-r-transparent" />
                Đang phân tích...
              </>
            ) : (
              <>⚡ Phân tích điểm học thuật</>
            )}
          </button>
        </div>
      </div>

      {/* Form nhập môn học có Tab chuyển bậc học */}
      <div className="mt-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4.5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-200/80 pb-3">
          <div className="flex items-center gap-2">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-700">
              Thêm môn học mới:
            </span>
            <div className="inline-flex rounded-lg bg-white p-0.5 border border-slate-200 shadow-2xs">
              <button
                type="button"
                onClick={() => handleLevelChange("highschool")}
                className={`rounded-md px-2.5 py-1 text-xs font-semibold transition ${
                  level === "highschool"
                    ? "bg-blue-600 text-white shadow-xs"
                    : "text-slate-600 hover:text-slate-900"
                }`}
              >
                🎓 Bậc THPT (Cấp 3)
              </button>
              <button
                type="button"
                onClick={() => handleLevelChange("university")}
                className={`rounded-md px-2.5 py-1 text-xs font-semibold transition ${
                  level === "university"
                    ? "bg-blue-600 text-white shadow-xs"
                    : "text-slate-600 hover:text-slate-900"
                }`}
              >
                🏛️ Bậc Đại học / Cao đẳng
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 text-xs text-slate-500">
            <label className="flex items-center gap-1.5 cursor-pointer">
              <input
                type="checkbox"
                checked={isCustomTerm}
                onChange={(e) => setIsCustomTerm(e.target.checked)}
                className="rounded border-slate-300 text-blue-600 focus:ring-blue-500"
              />
              <span>Nhập kỳ học tùy chỉnh</span>
            </label>
          </div>
        </div>

        {/* Input fields */}
        <div className="mt-3.5 grid gap-3 sm:grid-cols-12 items-end">
          {/* Học kỳ */}
          <div className="sm:col-span-3">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              {isCustomTerm ? "Tên kỳ tùy chỉnh" : "Học kỳ"}
            </label>
            {isCustomTerm ? (
              <div className="flex gap-1.5">
                <input
                  type="text"
                  value={customTermName}
                  onChange={(e) => setCustomTermName(e.target.value)}
                  placeholder="VD: Kỳ Hè 2025"
                  className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
                />
                <input
                  type="number"
                  min="1"
                  max="20"
                  value={customTermOrder}
                  onChange={(e) => setCustomTermOrder(parseInt(e.target.value) || 1)}
                  title="Thứ tự thời gian của kỳ (1-20)"
                  className="w-16 rounded-md border border-slate-300 bg-white px-2 py-1.5 text-xs text-center text-slate-800 focus:border-blue-500 focus:outline-hidden"
                />
              </div>
            ) : (
              <select
                value={selectedTermOrder}
                onChange={(e) => setSelectedTermOrder(parseInt(e.target.value))}
                className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
              >
                {currentTerms.map((t) => (
                  <option key={t.order} value={t.order}>
                    {t.name} (Kỳ {t.order})
                  </option>
                ))}
              </select>
            )}
          </div>

          {/* Tên môn học */}
          <div className="sm:col-span-4">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Tên môn học
            </label>
            <input
              type="text"
              list="subjects-datalist"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder={level === "highschool" ? "VD: Toán, Vật lý, Tiếng Anh..." : "VD: Giải tích, Cấu trúc dữ liệu..."}
              className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
            />
            <datalist id="subjects-datalist">
              {currentSubjects.map((s) => (
                <option key={s} value={s} />
              ))}
            </datalist>
          </div>

          {/* Điểm hệ 10 */}
          <div className="sm:col-span-2">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Điểm hệ 10 (0 - 10)
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

          {/* Số tín chỉ / Hệ số */}
          <div className="sm:col-span-2">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              {level === "university" ? "Số tín chỉ (Credits)" : "Hệ số / Tín chỉ"}
            </label>
            <input
              type="number"
              step="0.5"
              min="0.5"
              max="30"
              value={credits}
              onChange={(e) => setCredits(e.target.value)}
              placeholder={level === "university" ? "3" : "2"}
              className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs text-slate-800 focus:border-blue-500 focus:outline-hidden"
            />
          </div>

          {/* Nút thêm môn */}
          <div className="sm:col-span-1">
            <button
              type="button"
              onClick={handleAddSubject}
              disabled={isSaving}
              className="w-full rounded-md bg-slate-900 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-50 transition"
            >
              + Thêm
            </button>
          </div>
        </div>
      </div>

      {/* Toolbar: Tìm kiếm & Lọc theo kỳ */}
      <div className="mt-6 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        {/* Bộ lọc học kỳ */}
        <div className="flex flex-wrap items-center gap-1.5 text-xs">
          <span className="text-slate-400 font-medium text-[11px] mr-1">Lọc kỳ:</span>
          <button
            type="button"
            onClick={() => setFilterTerm("all")}
            className={`rounded-md px-2.5 py-1 font-semibold transition ${
              filterTerm === "all"
                ? "bg-blue-600 text-white shadow-2xs"
                : "bg-slate-100 text-slate-600 hover:bg-slate-200"
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
                className={`rounded-md px-2.5 py-1 font-semibold transition ${
                  filterTerm === t.order
                    ? "bg-blue-600 text-white shadow-2xs"
                    : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                }`}
              >
                {t.name} ({count})
              </button>
            );
          })}
        </div>

        {/* Ô tìm kiếm môn học */}
        <div className="w-full sm:w-60">
          <input
            type="text"
            value={searchSubject}
            onChange={(e) => setSearchSubject(e.target.value)}
            placeholder="🔍 Tìm theo tên môn học..."
            className="w-full rounded-md border border-slate-300 bg-white px-2.5 py-1 text-xs text-slate-800 placeholder:text-slate-400 focus:border-blue-500 focus:outline-hidden"
          />
        </div>
      </div>

      {/* Tóm tắt nhanh số liệu */}
      {scores.length > 0 && (
        <div className="mt-3 flex flex-wrap items-center gap-4 rounded-lg bg-blue-50/50 border border-blue-100 px-3.5 py-2 text-xs text-blue-900">
          <div className="flex items-center gap-1">
            <span className="font-semibold">Tổng môn:</span>
            <span className="font-bold text-blue-700">{stats.total} môn</span>
          </div>
          {stats.totalCredits > 0 && (
            <div className="flex items-center gap-1">
              <span className="font-semibold">Tổng tín chỉ tích lũy:</span>
              <span className="font-bold text-blue-700">{stats.totalCredits} TC</span>
            </div>
          )}
          <div className="flex items-center gap-1">
            <span className="font-semibold">Điểm TB hệ 10 tạm tính:</span>
            <span className="font-bold text-blue-700">{stats.avg} / 10.0</span>
          </div>
        </div>
      )}

      {/* Bảng dữ liệu điểm môn học */}
      <div className="mt-3 overflow-x-auto rounded-lg border border-slate-200">
        <table className="w-full text-left text-xs">
          <thead className="bg-slate-50 text-slate-600 uppercase tracking-wider text-[11px] border-b border-slate-200">
            <tr>
              <th className="px-4 py-3">Học kỳ</th>
              <th className="px-4 py-3">Môn học</th>
              <th className="px-4 py-3">Phân nhóm môn</th>
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
                  {scores.length === 0 ? (
                    <div>
                      <p className="font-medium text-slate-600">Chưa có môn học nào trong bảng điểm.</p>
                      <p className="mt-1 text-xs text-slate-400">
                        Bấm nút <strong>"🎓 Mẫu THPT"</strong> hoặc <strong>"🏛️ Mẫu Đại học"</strong> ở trên để nạp dữ liệu trải nghiệm ngay.
                      </p>
                    </div>
                  ) : (
                    "Không tìm thấy môn học nào khớp với bộ lọc."
                  )}
                </td>
              </tr>
            ) : (
              filteredScores.map((item) => (
                <tr key={item.id} className="hover:bg-slate-50/70 transition-colors">
                  <td className="px-4 py-2.5 font-medium text-slate-800 whitespace-nowrap">
                    {item.termName}
                  </td>
                  <td className="px-4 py-2.5 font-bold text-slate-900">
                    {item.subject}
                  </td>
                  <td className="px-4 py-2.5 whitespace-nowrap">
                    {getGroupBadge(item.subjectGroup, item.subjectGroupName)}
                  </td>
                  <td className="px-4 py-2.5 text-center font-bold text-slate-800">
                    {item.score.toFixed(1)}
                  </td>
                  <td className="px-4 py-2.5 text-center text-slate-600 font-medium">
                    {item.credits ? `${item.credits} TC` : "-"}
                  </td>
                  <td className="px-4 py-2.5 text-center font-black text-blue-600 text-sm">
                    {item.gpa4.toFixed(2)}
                  </td>
                  <td className="px-4 py-2.5 text-right whitespace-nowrap">
                    <button
                      type="button"
                      onClick={() => onDeleteScore(item.id)}
                      className="text-red-500 hover:text-red-700 text-[11px] font-semibold hover:underline"
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
