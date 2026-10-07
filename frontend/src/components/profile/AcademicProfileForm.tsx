"use client";

import React, { useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import { profileApi } from "@/lib/api";
import type {
  AcademicProfileResponse,
  GradeScaleType,
  SaveAcademicProfileRequest,
  TranscriptTerm,
  TranscriptScoreItem,
} from "@/types/api";

const GRADE_SCALES: { id: GradeScaleType; name: string; desc: string; placeholder: string }[] = [
  { id: "10", name: "Thang 10", desc: "0.0 - 10.0 (Phổ biến THPT Việt Nam)", placeholder: "Ví dụ: 8.5" },
  { id: "100", name: "Thang 100", desc: "0 - 100 (Hệ phần trăm)", placeholder: "Ví dụ: 85" },
  { id: "4", name: "Thang 4", desc: "0.0 - 4.0 (Chuẩn Mỹ / Đại học)", placeholder: "Ví dụ: 3.6" },
  { id: "letter", name: "Thang chữ", desc: "A+, A, A-, B+, B, B-, C, D, F", placeholder: "Chọn điểm chữ" },
];

const LETTER_OPTIONS = [
  { label: "A+ (4.0)", value: "A+", points: 4.0 },
  { label: "A (4.0)", value: "A", points: 4.0 },
  { label: "A- (3.7)", value: "A-", points: 3.7 },
  { label: "B+ (3.3)", value: "B+", points: 3.3 },
  { label: "B (3.0)", value: "B", points: 3.0 },
  { label: "B- (2.7)", value: "B-", points: 2.7 },
  { label: "C+ (2.3)", value: "C+", points: 2.3 },
  { label: "C (2.0)", value: "C", points: 2.0 },
  { label: "C- (1.7)", value: "C-", points: 1.7 },
  { label: "D+ (1.3)", value: "D+", points: 1.3 },
  { label: "D (1.0)", value: "D", points: 1.0 },
  { label: "D- (0.7)", value: "D-", points: 0.7 },
  { label: "F (0.0)", value: "F", points: 0.0 },
];

const EDUCATION_SYSTEMS = [
  { value: "standard", label: "Hệ chuẩn công lập Việt Nam" },
  { value: "specialized", label: "Hệ chuyên (Trường chuyên / Năng khiếu)" },
  { value: "dual_degree", label: "Hệ song bằng (Việt - Anh / Cam / v.v.)" },
  { value: "international", label: "Hệ quốc tế hoàn toàn (IB, AP, Cambridge)" },
  { value: "private", label: "Dân lập / Tư thục chất lượng cao" },
  { value: "other", label: "Hệ khác" },
];

const TARGET_LEVELS = [
  { value: "secondary", label: "Trung học phổ thông (Secondary)" },
  { value: "community_college", label: "Cao đẳng cộng đồng 2+2 (Community College)" },
  { value: "undergraduate", label: "Đại học 4 năm (Undergraduate)" },
  { value: "master", label: "Thạc sĩ (Master)" },
  { value: "phd", label: "Tiến sĩ (PhD)" },
];

interface Props {
  initialProfile?: AcademicProfileResponse | null;
  autoAnalyze?: boolean;
}

export function AcademicProfileForm({ initialProfile, autoAnalyze = false }: Props) {
  const router = useRouter();

  // Basic Info State
  const [targetLevel, setTargetLevel] = useState<string>(initialProfile?.targetLevel ?? "undergraduate");
  const [currentSchool, setCurrentSchool] = useState<string>(initialProfile?.currentSchool ?? "");
  const [educationSystem, setEducationSystem] = useState<string>(initialProfile?.educationSystem ?? "standard");
  const [graduationYear, setGraduationYear] = useState<string>(initialProfile?.graduationYear ? String(initialProfile.graduationYear) : "");
  const [currentGrade, setCurrentGrade] = useState<string>(initialProfile?.currentGrade ?? "Lớp 11");
  const [intendedMajor, setIntendedMajor] = useState<string>(initialProfile?.intendedMajor ?? "");

  // Scale State
  const [gradeScale, setGradeScale] = useState<GradeScaleType>(
    (initialProfile?.gradeScale as GradeScaleType) || "10"
  );

  // Terms & Scores State
  const defaultTerms: TranscriptTerm[] = [
    {
      termName: "Lớp 10 - Học kỳ 1",
      termOrder: 1,
      scores: [
        { subject: "Toán học", score: 8.5, rawScore: "8.5", credits: null },
        { subject: "Ngữ văn", score: 8.0, rawScore: "8.0", credits: null },
        { subject: "Tiếng Anh", score: 9.0, rawScore: "9.0", credits: null },
      ],
    },
    {
      termName: "Lớp 10 - Học kỳ 2",
      termOrder: 2,
      scores: [
        { subject: "Toán học", score: 8.8, rawScore: "8.8", credits: null },
        { subject: "Ngữ văn", score: 8.2, rawScore: "8.2", credits: null },
        { subject: "Tiếng Anh", score: 9.2, rawScore: "9.2", credits: null },
      ],
    },
  ];

  const [terms, setTerms] = useState<TranscriptTerm[]>(
    initialProfile?.terms && initialProfile.terms.length > 0
      ? initialProfile.terms
      : defaultTerms
  );

  // Certificates State
  const [ielts, setIelts] = useState<string>(initialProfile?.ielts != null ? String(initialProfile.ielts) : "");
  const [toefl, setToefl] = useState<string>(initialProfile?.toefl != null ? String(initialProfile.toefl) : "");
  const [duolingo, setDuolingo] = useState<string>(initialProfile?.duolingo != null ? String(initialProfile.duolingo) : "");
  const [sat, setSat] = useState<string>(initialProfile?.sat != null ? String(initialProfile.sat) : "");
  const [act, setAct] = useState<string>(initialProfile?.act != null ? String(initialProfile.act) : "");

  // Status & Feedback State
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Term management helpers
  const handleAddTerm = () => {
    if (terms.length >= 6) {
      alert("Hệ thống hỗ trợ nhập tối đa 6 học kỳ gần nhất (tương đương 3 năm học).");
      return;
    }
    const nextOrder = terms.length + 1;
    setTerms([
      ...terms,
      {
        termName: `Học kỳ ${nextOrder}`,
        termOrder: nextOrder,
        scores: [{ subject: "Toán học", score: 8.0, rawScore: "8.0", credits: null }],
      },
    ]);
  };

  const handleRemoveTerm = (index: number) => {
    if (terms.length <= 1) {
      alert("Cần giữ ít nhất 1 học kỳ để theo dõi học lực.");
      return;
    }
    const next = terms.filter((_, i) => i !== index).map((t, idx) => ({ ...t, termOrder: idx + 1 }));
    setTerms(next);
  };

  const handleTermNameChange = (termIndex: number, newName: string) => {
    const next = [...terms];
    next[termIndex].termName = newName;
    setTerms(next);
  };

  // Subject management helpers
  const handleAddSubject = (termIndex: number) => {
    const next = [...terms];
    next[termIndex].scores.push({
      subject: "",
      score: gradeScale === "100" ? 80 : gradeScale === "letter" ? 4.0 : gradeScale === "4" ? 3.5 : 8.0,
      rawScore: gradeScale === "letter" ? "A" : undefined,
      credits: null,
    });
    setTerms(next);
  };

  const handleRemoveSubject = (termIndex: number, subjectIndex: number) => {
    const next = [...terms];
    if (next[termIndex].scores.length <= 1) {
      alert("Mỗi học kỳ cần ít nhất 1 môn học.");
      return;
    }
    next[termIndex].scores = next[termIndex].scores.filter((_, i) => i !== subjectIndex);
    setTerms(next);
  };

  const handleSubjectChange = (
    termIndex: number,
    subjectIndex: number,
    field: keyof TranscriptScoreItem,
    value: string | number | null
  ) => {
    const next = [...terms];
    const item = { ...next[termIndex].scores[subjectIndex] };

    if (field === "subject") {
      item.subject = String(value);
    } else if (field === "score") {
      const num = typeof value === "number" ? value : parseFloat(String(value));
      item.score = isNaN(num) ? 0 : num;
      item.rawScore = String(value);
    } else if (field === "rawScore") {
      item.rawScore = value ? String(value) : null;
      if (gradeScale === "letter" && value) {
        const found = LETTER_OPTIONS.find((o) => o.value.toLowerCase() === String(value).toLowerCase());
        item.score = found ? found.points : 0;
      }
    } else if (field === "credits") {
      const num = value ? parseFloat(String(value)) : null;
      item.credits = num && !isNaN(num) ? num : null;
    }

    next[termIndex].scores[subjectIndex] = item;
    setTerms(next);
  };

  // Realtime validation per term
  const validationErrors = useMemo(() => {
    const errors: { [key: string]: string } = {};

    terms.forEach((term, tIdx) => {
      const subjectsSeen = new Set<string>();

      term.scores.forEach((s, sIdx) => {
        const subKey = `${tIdx}-${sIdx}`;
        const cleanSub = s.subject.trim();

        if (cleanSub) {
          const lower = cleanSub.toLowerCase();
          if (subjectsSeen.has(lower)) {
            errors[subKey] = `Môn "${cleanSub}" bị trùng trong ${term.termName}`;
          } else {
            subjectsSeen.add(lower);
          }
        }

        // Validate score based on scale
        if (gradeScale === "10") {
          if (s.score < 0 || s.score > 10) {
            errors[`${subKey}-score`] = "Điểm phải từ 0.0 đến 10.0";
          }
        } else if (gradeScale === "100") {
          if (s.score < 0 || s.score > 100) {
            errors[`${subKey}-score`] = "Điểm phải từ 0 đến 100";
          }
        } else if (gradeScale === "4") {
          if (s.score < 0 || s.score > 4.0) {
            errors[`${subKey}-score`] = "Điểm phải từ 0.0 đến 4.0";
          }
        } else if (gradeScale === "letter") {
          if (!s.rawScore || !LETTER_OPTIONS.some((o) => o.value.toLowerCase() === s.rawScore?.toLowerCase())) {
            errors[`${subKey}-score`] = "Chọn điểm chữ hợp lệ (A+ đến F)";
          }
        }
      });
    });

    return errors;
  }, [terms, gradeScale]);

  // Realtime GPA Preview
  const { overallGpaPreview, termGpaList } = useMemo(() => {
    let totalWeightedScore = 0;
    let totalCredits = 0;
    let totalSimpleScore = 0;
    let totalSimpleCount = 0;
    let hasCreditsGlobal = false;

    const termList: { termName: string; gpa: number }[] = [];

    for (const term of terms) {
      let termWeighted = 0;
      let termCreds = 0;
      let termSimple = 0;
      let termCount = 0;
      let termHasCredits = false;

      for (const s of term.scores) {
        const val = s.score;
        if (s.credits && s.credits > 0) {
          termHasCredits = true;
          hasCreditsGlobal = true;
          termWeighted += val * s.credits;
          termCreds += s.credits;
        } else {
          termSimple += val;
        }
        termCount += 1;
      }

      const termGpa =
        termCount === 0
          ? 0
          : termHasCredits && termCreds > 0
            ? Math.round((termWeighted / termCreds) * 100) / 100
            : Math.round((termSimple / termCount) * 100) / 100;

      if (termHasCredits && termCreds > 0) {
        totalWeightedScore += termWeighted;
        totalCredits += termCreds;
      } else {
        totalSimpleScore += termSimple;
        totalSimpleCount += termCount;
      }

      termList.push({ termName: term.termName, gpa: termGpa });
    }

    let overall = 0;
    if (hasCreditsGlobal && totalCredits > 0) {
      overall = Math.round((totalWeightedScore / totalCredits) * 100) / 100;
    } else if (totalSimpleCount > 0) {
      overall = Math.round((totalSimpleScore / totalSimpleCount) * 100) / 100;
    }

    return { overallGpaPreview: overall, termGpaList: termList };
  }, [terms]);

  // Save logic
  const handleSave = async (andAnalyze = false) => {
    setErrorMessage(null);
    setSuccessMessage(null);

    // Kiểm tra lỗi trước khi gửi
    const hasFieldErrors = Object.keys(validationErrors).length > 0;
    if (hasFieldErrors) {
      setErrorMessage("Vui lòng sửa các điểm ngoài thang hoặc môn học trùng lặp được đánh dấu đỏ trước khi lưu.");
      return;
    }

    // Validate Certificate ranges
    const ieltsVal = ielts ? parseFloat(ielts) : null;
    if (ieltsVal != null && (ieltsVal < 0 || ieltsVal > 9.0 || (ieltsVal * 10) % 5 !== 0)) {
      setErrorMessage("Điểm IELTS không hợp lệ. Phải từ 0.0 đến 9.0 với bước nhảy 0.5 (ví dụ: 6.5, 7.0, 7.5).");
      return;
    }

    const satVal = sat ? parseInt(sat, 10) : null;
    if (satVal != null && (satVal < 400 || satVal > 1600)) {
      setErrorMessage("Điểm SAT không hợp lệ. Phải nằm trong khoảng từ 400 đến 1600.");
      return;
    }

    const toeflVal = toefl ? parseInt(toefl, 10) : null;
    if (toeflVal != null && (toeflVal < 0 || toeflVal > 120)) {
      setErrorMessage("Điểm TOEFL iBT không hợp lệ. Phải từ 0 đến 120.");
      return;
    }

    const duolingoVal = duolingo ? parseInt(duolingo, 10) : null;
    if (duolingoVal != null && (duolingoVal < 10 || duolingoVal > 160)) {
      setErrorMessage("Điểm Duolingo English Test không hợp lệ. Phải từ 10 đến 160.");
      return;
    }

    const actVal = act ? parseInt(act, 10) : null;
    if (actVal != null && (actVal < 1 || actVal > 36)) {
      setErrorMessage("Điểm ACT không hợp lệ. Phải từ 1 đến 36.");
      return;
    }

    const gradYearNum = graduationYear ? parseInt(graduationYear, 10) : null;

    const payload: SaveAcademicProfileRequest = {
      targetLevel,
      currentSchool: currentSchool.trim() || null,
      educationSystem,
      graduationYear: gradYearNum,
      currentGrade: currentGrade.trim() || null,
      gradeScale,
      intendedMajor: intendedMajor.trim() || null,
      ielts: ieltsVal,
      toefl: toeflVal,
      duolingo: duolingoVal,
      sat: satVal,
      act: actVal,
      terms,
    };

    setIsSaving(true);
    try {
      await profileApi.saveAcademicProfile(payload);
      setSuccessMessage("Đã lưu hồ sơ học thuật thành công!");

      if (andAnalyze) {
        // Điều hướng sang phân tích năng lực (kết nối Story #4 - Xuân Đức)
        router.push("/profile/academic?autoAnalyze=true");
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage("Đã xảy ra lỗi khi lưu hồ sơ. Vui lòng thử lại.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-8">
      {/* Auto-Analyze Banner (Story #4 Link) */}
      {autoAnalyze && (
        <div className="rounded-2xl border border-indigo-200 bg-gradient-to-r from-indigo-50 via-purple-50 to-blue-50 p-6 shadow-sm">
          <div className="flex items-start gap-4">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-md">
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
            <div>
              <span className="inline-flex items-center rounded-full bg-indigo-100 px-2.5 py-0.5 text-xs font-semibold text-indigo-800">
                AI Phân Tích Năng Lực Học Thuật
              </span>
              <h3 className="mt-1 text-lg font-bold text-slate-900">
                Sẵn sàng đánh giá hồ sơ du học Mỹ
              </h3>
              <p className="mt-1 text-sm text-slate-600">
                Dữ liệu bảng điểm ({terms.length} học kỳ, GPA tích lũy:{" "}
                <span className="font-semibold text-indigo-700">{overallGpaPreview || "Chưa có"}</span>) và chứng chỉ
                chuẩn hóa đã được đồng bộ an toàn. Mô hình AI phân tích hồ sơ sẽ sử dụng dữ liệu này để gợi ý trường Reach, Match, Safety tương ứng.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Thông báo Feedback */}
      {errorMessage && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 shadow-sm flex items-start gap-3">
          <svg className="h-5 w-5 shrink-0 text-red-500 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <div className="flex-1 font-medium">{errorMessage}</div>
        </div>
      )}

      {successMessage && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 shadow-sm flex items-start gap-3">
          <svg className="h-5 w-5 shrink-0 text-emerald-600 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
          <div className="flex-1 font-medium">{successMessage}</div>
        </div>
      )}

      {/* KHỐI 1: THÔNG TIN HỌC VẤN */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l9-5-9-5-9 5 9 5z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z" />
            </svg>
          </div>
          <div>
            <h2 className="text-lg font-bold text-slate-900">1. Thông tin học vấn & Mục tiêu du học</h2>
            <p className="text-xs text-slate-500">Trường hiện tại, hệ chương trình đào tạo và ngành học mong muốn tại Mỹ</p>
          </div>
        </div>

        <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-2">
          {/* Bậc muốn học */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Bậc muốn học tại Mỹ <span className="text-red-500">*</span>
            </label>
            <select
              value={targetLevel}
              onChange={(e) => setTargetLevel(e.target.value)}
              className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {TARGET_LEVELS.map((lvl) => (
                <option key={lvl.value} value={lvl.value}>
                  {lvl.label}
                </option>
              ))}
            </select>
          </div>

          {/* Ngành muốn học */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Ngành muốn học (Intended Major)
            </label>
            <input
              type="text"
              value={intendedMajor}
              onChange={(e) => setIntendedMajor(e.target.value)}
              placeholder="Ví dụ: Khoa học Máy tính, Tài chính, Tâm lý học..."
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* Trường hiện tại */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Trường học hiện tại
            </label>
            <input
              type="text"
              value={currentSchool}
              onChange={(e) => setCurrentSchool(e.target.value)}
              placeholder="Ví dụ: THPT Chuyên Hà Nội - Amsterdam"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* Hệ chương trình */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Hệ chương trình đào tạo
            </label>
            <select
              value={educationSystem}
              onChange={(e) => setEducationSystem(e.target.value)}
              className="mt-1 block w-full rounded-xl border border-slate-300 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {EDUCATION_SYSTEMS.map((sys) => (
                <option key={sys.value} value={sys.value}>
                  {sys.label}
                </option>
              ))}
            </select>
          </div>

          {/* Lớp / Khối hiện tại */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Khối / Lớp hiện tại
            </label>
            <input
              type="text"
              value={currentGrade}
              onChange={(e) => setCurrentGrade(e.target.value)}
              placeholder="Ví dụ: Lớp 10, Lớp 11, Lớp 12, Đại học năm 2..."
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* Năm tốt nghiệp dự kiến */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Năm tốt nghiệp dự kiến
            </label>
            <input
              type="number"
              value={graduationYear}
              onChange={(e) => setGraduationYear(e.target.value)}
              placeholder="Ví dụ: 2026, 2027..."
              min={2020}
              max={2040}
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>
      </section>

      {/* KHỐI 2: THANG ĐIỂM & BẢNG ĐIỂM HỌC KỲ */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-4 border-b border-slate-100 pb-4 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-purple-50 text-purple-600">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">2. Bảng điểm học kỳ (3 năm gần nhất)</h2>
              <p className="text-xs text-slate-500">Chọn thang điểm của trường và nhập điểm từng môn theo từng học kỳ</p>
            </div>
          </div>

          {/* Widget Xem Trước GPA */}
          <div className="flex items-center gap-3 rounded-xl bg-slate-50 px-4 py-2 border border-slate-200">
            <span className="text-xs font-medium text-slate-500">GPA Ước tính:</span>
            <span className="text-base font-extrabold text-blue-600">{overallGpaPreview.toFixed(2)}</span>
            <span className="text-xs text-slate-400">({GRADE_SCALES.find((s) => s.id === gradeScale)?.name})</span>
          </div>
        </div>

        {/* BỘ CHỌN THANG ĐIỂM */}
        <div className="mt-5">
          <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
            Chọn thang điểm áp dụng <span className="text-red-500">*</span>
          </label>
          <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {GRADE_SCALES.map((scale) => {
              const selected = gradeScale === scale.id;
              return (
                <button
                  key={scale.id}
                  type="button"
                  onClick={() => setGradeScale(scale.id)}
                  className={`rounded-xl border p-3.5 text-left transition ${
                    selected
                      ? "border-blue-600 bg-blue-50/50 text-blue-900 shadow-sm ring-1 ring-blue-500"
                      : "border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50"
                  }`}
                >
                  <div className="text-sm font-bold">{scale.name}</div>
                  <div className="mt-1 text-xs text-slate-500">{scale.desc}</div>
                </button>
              );
            })}
          </div>
        </div>

        {/* DANH SÁCH CÁC HỌC KỲ */}
        <div className="mt-6 space-y-6">
          {terms.map((term, tIdx) => {
            const currentTermGpa = termGpaList[tIdx]?.gpa ?? 0;
            return (
              <div
                key={tIdx}
                className="rounded-xl border border-slate-200 bg-slate-50/50 p-4 transition hover:border-slate-300"
              >
                {/* Header Học Kỳ */}
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between border-b border-slate-200/80 pb-3">
                  <div className="flex items-center gap-2">
                    <span className="flex h-6 w-6 items-center justify-center rounded-full bg-blue-600 text-xs font-bold text-white">
                      {term.termOrder}
                    </span>
                    <input
                      type="text"
                      value={term.termName}
                      onChange={(e) => handleTermNameChange(tIdx, e.target.value)}
                      className="rounded-lg border border-transparent bg-transparent px-2 py-1 text-sm font-bold text-slate-900 hover:border-slate-300 focus:border-blue-500 focus:bg-white focus:outline-none"
                    />
                  </div>

                  <div className="flex items-center gap-4">
                    <span className="text-xs text-slate-500">
                      GPA kỳ: <strong className="text-slate-800">{currentTermGpa.toFixed(2)}</strong>
                    </span>
                    <button
                      type="button"
                      onClick={() => handleRemoveTerm(tIdx)}
                      className="text-xs text-red-600 hover:text-red-700 hover:underline"
                    >
                      Xóa kỳ này
                    </button>
                  </div>
                </div>

                {/* Bảng Môn Học */}
                <div className="mt-3 overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead>
                      <tr className="text-slate-500 border-b border-slate-200">
                        <th className="pb-2 font-medium">Tên môn học <span className="text-red-500">*</span></th>
                        <th className="pb-2 font-medium w-36">Điểm ({GRADE_SCALES.find((s) => s.id === gradeScale)?.name}) <span className="text-red-500">*</span></th>
                        <th className="pb-2 font-medium w-28">Tín chỉ / Hệ số</th>
                        <th className="pb-2 font-medium w-16 text-center">Xóa</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {term.scores.map((scoreItem, sIdx) => {
                        const subKey = `${tIdx}-${sIdx}`;
                        const subjectError = validationErrors[subKey];
                        const scoreError = validationErrors[`${subKey}-score`];

                        return (
                          <tr key={sIdx} className="group">
                            {/* Tên Môn */}
                            <td className="py-2.5 pr-3 align-top">
                              <input
                                type="text"
                                value={scoreItem.subject}
                                onChange={(e) => handleSubjectChange(tIdx, sIdx, "subject", e.target.value)}
                                placeholder="Ví dụ: Toán học, Ngữ văn..."
                                className={`w-full rounded-lg border px-3 py-1.5 text-xs text-slate-900 shadow-sm focus:outline-none ${
                                  subjectError
                                    ? "border-red-500 bg-red-50/50 focus:border-red-500"
                                    : "border-slate-300 focus:border-blue-500"
                                }`}
                              />
                              {subjectError && <p className="mt-1 text-[11px] text-red-600">{subjectError}</p>}
                            </td>

                            {/* Điểm */}
                            <td className="py-2.5 pr-3 align-top">
                              {gradeScale === "letter" ? (
                                <select
                                  value={scoreItem.rawScore ?? "A"}
                                  onChange={(e) => handleSubjectChange(tIdx, sIdx, "rawScore", e.target.value)}
                                  className={`w-full rounded-lg border bg-white px-2 py-1.5 text-xs text-slate-900 shadow-sm focus:outline-none ${
                                    scoreError
                                      ? "border-red-500 bg-red-50/50"
                                      : "border-slate-300 focus:border-blue-500"
                                  }`}
                                >
                                  {LETTER_OPTIONS.map((opt) => (
                                    <option key={opt.value} value={opt.value}>
                                      {opt.label}
                                    </option>
                                  ))}
                                </select>
                              ) : (
                                <input
                                  type="number"
                                  step={gradeScale === "100" ? "1" : "0.1"}
                                  value={scoreItem.score}
                                  onChange={(e) => handleSubjectChange(tIdx, sIdx, "score", e.target.value)}
                                  placeholder={GRADE_SCALES.find((s) => s.id === gradeScale)?.placeholder}
                                  className={`w-full rounded-lg border px-3 py-1.5 text-xs text-slate-900 shadow-sm focus:outline-none ${
                                    scoreError
                                      ? "border-red-500 bg-red-50/50 focus:border-red-500"
                                      : "border-slate-300 focus:border-blue-500"
                                  }`}
                                />
                              )}
                              {scoreError && <p className="mt-1 text-[11px] text-red-600">{scoreError}</p>}
                            </td>

                            {/* Tín chỉ */}
                            <td className="py-2.5 pr-3 align-top">
                              <input
                                type="number"
                                step="0.5"
                                min="0.5"
                                value={scoreItem.credits ?? ""}
                                onChange={(e) => handleSubjectChange(tIdx, sIdx, "credits", e.target.value)}
                                placeholder="Tùy chọn"
                                className="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-xs text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none"
                              />
                            </td>

                            {/* Nút Xóa Dòng */}
                            <td className="py-2.5 text-center align-top">
                              <button
                                type="button"
                                onClick={() => handleRemoveSubject(tIdx, sIdx)}
                                title="Xóa môn này"
                                className="inline-flex h-7 w-7 items-center justify-center rounded-md text-slate-400 transition hover:bg-red-50 hover:text-red-600"
                              >
                                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                              </button>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Thêm môn học */}
                <div className="mt-3">
                  <button
                    type="button"
                    onClick={() => handleAddSubject(tIdx)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-dashed border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-blue-500 hover:text-blue-600"
                  >
                    <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Thêm môn học vào {term.termName}
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        {/* Nút Thêm Học Kỳ */}
        <div className="mt-4">
          <button
            type="button"
            onClick={handleAddTerm}
            className="inline-flex items-center gap-2 rounded-xl border border-blue-200 bg-blue-50 px-4 py-2.5 text-xs font-bold text-blue-700 shadow-sm transition hover:bg-blue-100"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Thêm học kỳ mới (Tối đa 6 học kỳ)
          </button>
        </div>
      </section>

      {/* KHỐI 3: CHỨNG CHỈ TIẾNG ANH & BÀI THI CHUẨN HÓA */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 text-emerald-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z" />
            </svg>
          </div>
          <div>
            <h2 className="text-lg font-bold text-slate-900">3. Chứng chỉ Tiếng Anh & Chuẩn hóa</h2>
            <p className="text-xs text-slate-500">IELTS/TOEFL, SAT/ACT nếu có (Có thể để trống nếu bạn chưa thi)</p>
          </div>
        </div>

        <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-3">
          {/* IELTS */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              IELTS (0.0 - 9.0)
            </label>
            <input
              type="number"
              step="0.5"
              min="0"
              max="9"
              value={ielts}
              onChange={(e) => setIelts(e.target.value)}
              placeholder="Ví dụ: 7.0 hoặc để trống"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* TOEFL */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              TOEFL iBT (0 - 120)
            </label>
            <input
              type="number"
              min="0"
              max="120"
              value={toefl}
              onChange={(e) => setToefl(e.target.value)}
              placeholder="Ví dụ: 95 hoặc để trống"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* Duolingo */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Duolingo English Test (10 - 160)
            </label>
            <input
              type="number"
              min="10"
              max="160"
              value={duolingo}
              onChange={(e) => setDuolingo(e.target.value)}
              placeholder="Ví dụ: 125 hoặc để trống"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* SAT */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              SAT (400 - 1600)
            </label>
            <input
              type="number"
              min="400"
              max="1600"
              value={sat}
              onChange={(e) => setSat(e.target.value)}
              placeholder="Ví dụ: 1450 hoặc để trống"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          {/* ACT */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              ACT (1 - 36)
            </label>
            <input
              type="number"
              min="1"
              max="36"
              value={act}
              onChange={(e) => setAct(e.target.value)}
              placeholder="Ví dụ: 32 hoặc để trống"
              className="mt-1 block w-full rounded-xl border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>
      </section>

      {/* HÀNH ĐỘNG SUBMIT */}
      <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:items-center sm:justify-end">
        <button
          type="button"
          onClick={() => handleSave(false)}
          disabled={isSaving}
          className="rounded-xl border border-slate-300 bg-white px-6 py-3 text-sm font-bold text-slate-700 shadow-sm transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
        >
          {isSaving ? "Đang lưu..." : "Lưu hồ sơ"}
        </button>

        <button
          type="button"
          onClick={() => handleSave(true)}
          disabled={isSaving}
          className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-blue-600 via-indigo-600 to-purple-600 px-6 py-3 text-sm font-bold text-white shadow-md transition hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          {isSaving ? "Đang xử lý..." : "Lưu & Phân tích năng lực học thuật 🚀"}
        </button>
      </div>
    </div>
  );
}
