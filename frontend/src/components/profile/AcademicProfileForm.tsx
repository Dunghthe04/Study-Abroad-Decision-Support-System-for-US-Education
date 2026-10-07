"use client";

import React, { useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
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
  { value: "university", label: "Hệ Đại học chính quy / Tiên tiến" },
  { value: "other", label: "Hệ khác" },
];

const TARGET_LEVELS = [
  { value: "secondary", label: "Trung học phổ thông (Secondary)" },
  { value: "community_college", label: "Cao đẳng cộng đồng 2+2 (Community College)" },
  { value: "undergraduate", label: "Đại học 4 năm (Undergraduate)" },
  { value: "master", label: "Thạc sĩ (Master - Sau đại học)" },
  { value: "phd", label: "Tiến sĩ (PhD - Sau đại học)" },
];

/**
 * Quy đổi thông minh giữa các thang điểm (Thang 10, Thang 100, Thang 4, Thang chữ)
 */
function convertScore(
  oldScore: number,
  oldRaw: string | undefined | null,
  fromScale: GradeScaleType,
  toScale: GradeScaleType
): { score: number; rawScore?: string } {
  if (fromScale === toScale) {
    return { score: oldScore, rawScore: oldRaw ?? undefined };
  }

  // 1. Chuyển đổi về mốc chuẩn Thang 4.0 (standard4)
  let standard4 = 3.0;

  if (fromScale === "letter") {
    const opt = LETTER_OPTIONS.find((o) => o.value.toLowerCase() === (oldRaw || "").toLowerCase());
    standard4 = opt ? opt.points : 3.0;
  } else if (fromScale === "10") {
    standard4 = Math.min(4.0, Math.max(0.0, (oldScore / 10) * 4));
  } else if (fromScale === "100") {
    standard4 = Math.min(4.0, Math.max(0.0, (oldScore / 100) * 4));
  } else if (fromScale === "4") {
    standard4 = Math.min(4.0, Math.max(0.0, oldScore));
  }

  // 2. Chuyển từ standard4 sang thang đích
  if (toScale === "10") {
    const s10 = Math.round((standard4 / 4) * 10 * 10) / 10;
    return { score: s10, rawScore: s10.toFixed(1) };
  }

  if (toScale === "100") {
    const s100 = Math.round((standard4 / 4) * 100);
    return { score: s100, rawScore: String(s100) };
  }

  if (toScale === "4") {
    const s4 = Math.round(standard4 * 100) / 100;
    return { score: s4, rawScore: s4.toFixed(2) };
  }

  if (toScale === "letter") {
    if (standard4 >= 3.85) return { score: 4.0, rawScore: "A+" };
    if (standard4 >= 3.65) return { score: 4.0, rawScore: "A" };
    if (standard4 >= 3.45) return { score: 3.7, rawScore: "A-" };
    if (standard4 >= 3.15) return { score: 3.3, rawScore: "B+" };
    if (standard4 >= 2.85) return { score: 3.0, rawScore: "B" };
    if (standard4 >= 2.5) return { score: 2.7, rawScore: "B-" };
    if (standard4 >= 2.15) return { score: 2.3, rawScore: "C+" };
    if (standard4 >= 1.85) return { score: 2.0, rawScore: "C" };
    if (standard4 >= 1.5) return { score: 1.7, rawScore: "C-" };
    if (standard4 >= 1.15) return { score: 1.3, rawScore: "D+" };
    if (standard4 >= 0.85) return { score: 1.0, rawScore: "D" };
    if (standard4 >= 0.5) return { score: 0.7, rawScore: "D-" };
    return { score: 0.0, rawScore: "F" };
  }

  return { score: oldScore, rawScore: oldRaw ?? undefined };
}

interface OtherTestItem {
  id: string;
  name: string;
  score: string;
}

interface Props {
  initialProfile?: AcademicProfileResponse | null;
  autoAnalyze?: boolean;
}

export function AcademicProfileForm({ initialProfile, autoAnalyze = false }: Props) {
  const router = useRouter();

  // Xác định xem hồ sơ đã được lưu trước đó hay chưa
  const hasSavedProfile = Boolean(
    initialProfile &&
      initialProfile.id &&
      initialProfile.id !== "00000000-0000-0000-0000-000000000000" &&
      initialProfile.currentSchool
  );

  // Chế độ: false = Xem (không sửa được, có nút Sửa), true = Chỉnh sửa
  const [isEditing, setIsEditing] = useState<boolean>(!hasSavedProfile);

  // Hiển thị khung phân tích năng lực (kết nối Story #4 - Xuân Đức)
  const [showAnalysis, setShowAnalysis] = useState<boolean>(autoAnalyze);
  const isAnalysisVisible = showAnalysis || autoAnalyze;

  // Basic Info State
  const [targetLevel, setTargetLevel] = useState<string>(initialProfile?.targetLevel ?? "undergraduate");
  const isGraduate = targetLevel === "master" || targetLevel === "phd";

  const [currentSchool, setCurrentSchool] = useState<string>(initialProfile?.currentSchool ?? "");
  const [educationSystem, setEducationSystem] = useState<string>(
    initialProfile?.educationSystem ?? (isGraduate ? "university" : "standard")
  );
  const [graduationYear, setGraduationYear] = useState<string>(
    initialProfile?.graduationYear ? String(initialProfile.graduationYear) : ""
  );
  const [currentGrade, setCurrentGrade] = useState<string>(
    initialProfile?.currentGrade ?? (isGraduate ? "Đại học năm 3" : "Lớp 11")
  );
  const [intendedMajor, setIntendedMajor] = useState<string>(initialProfile?.intendedMajor ?? "");

  // Scale State
  const [gradeScale, setGradeScale] = useState<GradeScaleType>(
    (initialProfile?.gradeScale as GradeScaleType) || (isGraduate ? "4" : "10")
  );

  // Học kỳ mặc định tùy theo bậc học
  const defaultHighSchoolTerms: TranscriptTerm[] = [
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

  const defaultGraduateTerms: TranscriptTerm[] = [
    {
      termName: "Đại học - Năm 1",
      termOrder: 1,
      scores: [
        { subject: "Giải tích đại học", score: 3.5, rawScore: "3.5", credits: 3 },
        { subject: "Nhập môn lập trình", score: 3.8, rawScore: "3.8", credits: 4 },
        { subject: "Tiếng Anh chuyên ngành", score: 3.7, rawScore: "3.7", credits: 3 },
      ],
    },
    {
      termName: "Đại học - Năm 2",
      termOrder: 2,
      scores: [
        { subject: "Cấu trúc dữ liệu & Thuật toán", score: 3.6, rawScore: "3.6", credits: 4 },
        { subject: "Hệ cơ sở dữ liệu", score: 3.8, rawScore: "3.8", credits: 3 },
        { subject: "Xác suất thống kê", score: 3.4, rawScore: "3.4", credits: 3 },
      ],
    },
  ];

  const [terms, setTerms] = useState<TranscriptTerm[]>(
    initialProfile?.terms && initialProfile.terms.length > 0
      ? initialProfile.terms
      : isGraduate
        ? defaultGraduateTerms
        : defaultHighSchoolTerms
  );

  // Certificates State (Cho phép để trống khi chưa thi)
  const [ielts, setIelts] = useState<string>(initialProfile?.ielts != null ? String(initialProfile.ielts) : "");
  const [toefl, setToefl] = useState<string>(initialProfile?.toefl != null ? String(initialProfile.toefl) : "");
  const [duolingo, setDuolingo] = useState<string>(
    initialProfile?.duolingo != null ? String(initialProfile.duolingo) : ""
  );
  const [sat, setSat] = useState<string>(initialProfile?.sat != null ? String(initialProfile.sat) : "");
  const [act, setAct] = useState<string>(initialProfile?.act != null ? String(initialProfile.act) : "");
  const [gre, setGre] = useState<string>(initialProfile?.gre != null ? String(initialProfile.gre) : "");
  const [gmat, setGmat] = useState<string>(initialProfile?.gmat != null ? String(initialProfile.gmat) : "");

  // Chứng chỉ khác (AP, IB, PTE...) từ otherTestsJson
  const parseOtherTests = (jsonStr?: string | null): OtherTestItem[] => {
    if (!jsonStr) return [];
    try {
      const parsed = JSON.parse(jsonStr);
      if (Array.isArray(parsed)) {
        return parsed.map((item, index) => ({
          id: String(index),
          name: item.name || "",
          score: String(item.score || ""),
        }));
      }
    } catch {
      // Ignored
    }
    return [];
  };

  const [otherTests, setOtherTests] = useState<OtherTestItem[]>(
    parseOtherTests(initialProfile?.otherTestsJson)
  );

  // Trạng thái các loại chứng chỉ đang hiển thị trong giao diện
  const [enabledTests, setEnabledTests] = useState<{ [key: string]: boolean }>(() => ({
    ielts: Boolean(initialProfile?.ielts != null || !hasSavedProfile),
    toefl: Boolean(initialProfile?.toefl != null),
    duolingo: Boolean(initialProfile?.duolingo != null),
    sat: Boolean(initialProfile?.sat != null || (!isGraduate && !hasSavedProfile)),
    act: Boolean(initialProfile?.act != null),
    gre: Boolean(initialProfile?.gre != null || (isGraduate && !hasSavedProfile)),
    gmat: Boolean(initialProfile?.gmat != null),
  }));

  // Status & Feedback State
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Xử lý đổi thang điểm và TỰ ĐỘNG QUY ĐỔI TOÀN BỘ ĐIỂM SANG THANG MỚI
  const handleGradeScaleChange = (newScale: GradeScaleType) => {
    if (newScale === gradeScale) return;
    const oldScale = gradeScale;
    setGradeScale(newScale);

    // Tự động map điểm toàn bộ các môn trong tất cả học kỳ
    const convertedTerms = terms.map((term) => ({
      ...term,
      scores: term.scores.map((s) => {
        const { score, rawScore } = convertScore(s.score, s.rawScore, oldScale, newScale);
        return {
          ...s,
          score,
          rawScore: rawScore ?? s.rawScore,
        };
      }),
    }));

    setTerms(convertedTerms);
  };

  // Xác định xem bảng điểm đang có lớp / năm nào
  const currentGradeProgress = useMemo(() => {
    const termNames = terms.map((t) => t.termName.toLowerCase());
    const hasGrade10 = termNames.some((n) => n.includes("lớp 10") || n.includes("10"));
    const hasGrade11 = termNames.some((n) => n.includes("lớp 11") || n.includes("11"));
    const hasGrade12 = termNames.some((n) => n.includes("lớp 12") || n.includes("12"));

    const hasUniYear1 = termNames.some((n) => n.includes("năm 1") || n.includes("year 1"));
    const hasUniYear2 = termNames.some((n) => n.includes("năm 2") || n.includes("year 2"));
    const hasUniYear3 = termNames.some((n) => n.includes("năm 3") || n.includes("year 3"));
    const hasUniYear4 = termNames.some((n) => n.includes("năm 4") || n.includes("year 4"));

    return {
      hasGrade10,
      hasGrade11,
      hasGrade12,
      hasUniYear1,
      hasUniYear2,
      hasUniYear3,
      hasUniYear4,
    };
  }, [terms]);

  // TÍNH NĂNG "LÊN LỚP" CHO CẤP 3 (Grade Promotion Helper)
  const handlePromoteGrade = (nextGrade: "11" | "12") => {
    if (terms.length >= 6) {
      alert("Hệ thống hỗ trợ tối đa 6 học kỳ gần nhất (tương đương 3 năm học).");
      return;
    }

    // Lấy danh sách tên môn học của kỳ gần nhất để tái sử dụng
    const lastTerm = terms[terms.length - 1];
    const templateScores: TranscriptScoreItem[] =
      lastTerm && lastTerm.scores.length > 0
        ? lastTerm.scores.map((s) => {
            const initScore =
              gradeScale === "100" ? 80 : gradeScale === "letter" ? 4.0 : gradeScale === "4" ? 3.5 : 8.0;
            const initRaw = gradeScale === "letter" ? "A" : String(initScore);
            return {
              subject: s.subject,
              score: initScore,
              rawScore: initRaw,
              credits: s.credits,
            };
          })
        : [
            { subject: "Toán học", score: 8.5, rawScore: "8.5", credits: null },
            { subject: "Ngữ văn", score: 8.0, rawScore: "8.0", credits: null },
            { subject: "Tiếng Anh", score: 8.5, rawScore: "8.5", credits: null },
          ];

    const currentMaxOrder = terms.length;
    const term1Order = currentMaxOrder + 1;
    const term2Order = currentMaxOrder + 2;

    const newTerms: TranscriptTerm[] = [
      ...terms,
      {
        termName: `Lớp ${nextGrade} - Học kỳ 1`,
        termOrder: term1Order,
        scores: templateScores.map((s) => ({ ...s })),
      },
      {
        termName: `Lớp ${nextGrade} - Học kỳ 2`,
        termOrder: term2Order,
        scores: templateScores.map((s) => ({ ...s })),
      },
    ];

    setTerms(newTerms);
    setCurrentGrade(`Lớp ${nextGrade}`);
  };

  // TÍNH NĂNG "THÊM NĂM ĐẠI HỌC" CHO THẠC SĨ / TIẾN SĨ
  const handlePromoteUniversityYear = (yearNum: number) => {
    if (terms.length >= 6) {
      alert("Hệ thống hỗ trợ tối đa 6 học kỳ gần nhất.");
      return;
    }

    const currentMaxOrder = terms.length;
    const nextOrder = currentMaxOrder + 1;
    const initScore = gradeScale === "4" ? 3.5 : gradeScale === "10" ? 8.0 : 80;

    const newTerm: TranscriptTerm = {
      termName: `Đại học - Năm ${yearNum}`,
      termOrder: nextOrder,
      scores: [
        { subject: `Môn chuyên ngành năm ${yearNum} (1)`, score: initScore, rawScore: String(initScore), credits: 3 },
        { subject: `Môn chuyên ngành năm ${yearNum} (2)`, score: initScore, rawScore: String(initScore), credits: 3 },
      ],
    };

    setTerms([...terms, newTerm]);
    setCurrentGrade(`Đại học năm ${yearNum}`);
  };

  // Term management helpers
  const handleAddTerm = () => {
    if (terms.length >= 6) {
      alert("Hệ thống hỗ trợ tối đa 6 học kỳ gần nhất (tương đương 3 năm học).");
      return;
    }
    const nextOrder = terms.length + 1;
    const initScore = gradeScale === "100" ? 80 : gradeScale === "letter" ? 4.0 : gradeScale === "4" ? 3.5 : 8.0;
    const initRaw = gradeScale === "letter" ? "A" : String(initScore);

    setTerms([
      ...terms,
      {
        termName: isGraduate ? `Đại học - Học kỳ ${nextOrder}` : `Học kỳ ${nextOrder}`,
        termOrder: nextOrder,
        scores: [
          {
            subject: isGraduate ? "Môn học đại học" : "Toán học",
            score: initScore,
            rawScore: initRaw,
            credits: isGraduate ? 3 : null,
          },
        ],
      },
    ]);
  };

  const handleRemoveTerm = (index: number) => {
    if (terms.length <= 1) {
      alert("Cần giữ lại ít nhất 1 học kỳ.");
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
    const initScore = gradeScale === "100" ? 80 : gradeScale === "letter" ? 4.0 : gradeScale === "4" ? 3.5 : 8.0;
    const initRaw = gradeScale === "letter" ? "A" : String(initScore);

    next[termIndex].scores.push({
      subject: "",
      score: initScore,
      rawScore: initRaw,
      credits: isGraduate ? 3 : null,
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

  // Realtime validation
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
        } else {
          errors[subKey] = "Tên môn học không được để trống";
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

  // Quy đổi GPA chuẩn Mỹ 4.0 để phục vụ Story #4 (Xuân Đức)
  const usGpaEstimate = useMemo(() => {
    if (gradeScale === "4" || gradeScale === "letter") {
      return overallGpaPreview;
    }
    if (gradeScale === "10") {
      return Math.round((overallGpaPreview / 10) * 4 * 100) / 100;
    }
    if (gradeScale === "100") {
      return Math.round((overallGpaPreview / 100) * 4 * 100) / 100;
    }
    return overallGpaPreview;
  }, [overallGpaPreview, gradeScale]);

  // Phân loại hồ sơ Reach / Match / Safety (Story #4 Integration)
  const profileClassification = useMemo(() => {
    const satScore = sat ? parseInt(sat, 10) : 0;
    const ieltsScore = ielts ? parseFloat(ielts) : 0;
    const greScore = gre ? parseInt(gre, 10) : 0;

    let tier = "Cần cải thiện (Needs Improvement)";
    let badgeColor = "bg-amber-100 text-amber-800 border-amber-200";
    let reachDescription = isGraduate ? "Top 50-80 US Graduate Schools" : "Top 100-150 US Universities";
    let matchDescription = isGraduate ? "Top 80-120 US Graduate Schools" : "Top 150-200 hoặc Đại học vùng";
    let safetyDescription = isGraduate ? "State Universities / Regional Masters" : "Community College 2+2";

    if (
      usGpaEstimate >= 3.65 ||
      (!isGraduate && satScore >= 1450) ||
      (isGraduate && greScore >= 325) ||
      ieltsScore >= 7.5
    ) {
      tier = "Ứng viên rất mạnh (Strong Candidate)";
      badgeColor = "bg-emerald-100 text-emerald-800 border-emerald-200";
      reachDescription = isGraduate
        ? "Top 10-25 US Graduate Programs (Stanford, MIT, Carnegie Mellon, Ivy League)"
        : "Top 20-40 National Universities (Ivy League / Top Tier)";
      matchDescription = isGraduate ? "Top 25-60 National Graduate Programs" : "Top 40-75 National Universities";
      safetyDescription = isGraduate ? "Top 60-100 National Graduate Programs" : "Top 75-120 National Universities";
    } else if (
      usGpaEstimate >= 3.2 ||
      (!isGraduate && satScore >= 1250) ||
      (isGraduate && greScore >= 310) ||
      ieltsScore >= 6.5
    ) {
      tier = "Ứng viên tiềm năng (Competitive Candidate)";
      badgeColor = "bg-blue-100 text-blue-800 border-blue-200";
      reachDescription = isGraduate ? "Top 30-60 US Graduate Programs" : "Top 50-80 National Universities";
      matchDescription = isGraduate ? "Top 60-100 US Graduate Programs" : "Top 80-130 National Universities";
      safetyDescription = isGraduate ? "Top 100-150 State Universities" : "Top 130-180 hoặc Public State Colleges";
    }

    return { tier, badgeColor, reachDescription, matchDescription, safetyDescription };
  }, [usGpaEstimate, sat, ielts, gre, isGraduate]);

  // Thêm chứng chỉ khác
  const handleAddOtherTest = () => {
    const newId = String(Date.now());
    setOtherTests([...otherTests, { id: newId, name: "", score: "" }]);
  };

  const handleRemoveOtherTest = (id: string) => {
    setOtherTests(otherTests.filter((t) => t.id !== id));
  };

  const handleUpdateOtherTest = (id: string, field: "name" | "score", value: string) => {
    setOtherTests(otherTests.map((t) => (t.id === id ? { ...t, [field]: value } : t)));
  };

  // Bật/tắt loại chứng chỉ
  const handleToggleTest = (key: string) => {
    setEnabledTests((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  // Hủy chỉnh sửa (Reset về dữ liệu ban đầu)
  const handleCancelEdit = () => {
    if (initialProfile) {
      setTargetLevel(initialProfile.targetLevel ?? "undergraduate");
      setCurrentSchool(initialProfile.currentSchool ?? "");
      setEducationSystem(initialProfile.educationSystem ?? "standard");
      setGraduationYear(initialProfile.graduationYear ? String(initialProfile.graduationYear) : "");
      setCurrentGrade(initialProfile.currentGrade ?? "Lớp 11");
      setIntendedMajor(initialProfile.intendedMajor ?? "");
      setGradeScale((initialProfile.gradeScale as GradeScaleType) || "10");
      setTerms(initialProfile.terms && initialProfile.terms.length > 0 ? initialProfile.terms : defaultHighSchoolTerms);
      setIelts(initialProfile.ielts != null ? String(initialProfile.ielts) : "");
      setToefl(initialProfile.toefl != null ? String(initialProfile.toefl) : "");
      setDuolingo(initialProfile.duolingo != null ? String(initialProfile.duolingo) : "");
      setSat(initialProfile.sat != null ? String(initialProfile.sat) : "");
      setAct(initialProfile.act != null ? String(initialProfile.act) : "");
      setGre(initialProfile.gre != null ? String(initialProfile.gre) : "");
      setGmat(initialProfile.gmat != null ? String(initialProfile.gmat) : "");
      setOtherTests(parseOtherTests(initialProfile.otherTestsJson));
    }
    setErrorMessage(null);
    setIsEditing(false);
  };

  // Lưu hồ sơ
  const handleSave = async (andAnalyze = false) => {
    setErrorMessage(null);
    setSuccessMessage(null);

    // 1. Kiểm tra các trường BẮT BUỘC (*)
    if (!currentSchool.trim()) {
      setErrorMessage(
        isGraduate
          ? "Vui lòng nhập 'Trường Đại học tốt nghiệp / đang học' (Trường bắt buộc có dấu *)."
          : "Vui lòng nhập 'Trường học hiện tại' (Trường bắt buộc có dấu *)."
      );
      return;
    }

    if (!graduationYear.trim()) {
      setErrorMessage("Vui lòng nhập 'Năm tốt nghiệp dự kiến' (Trường bắt buộc có dấu *).");
      return;
    }

    const gradYearNum = parseInt(graduationYear, 10);
    if (isNaN(gradYearNum) || gradYearNum < 2015 || gradYearNum > 2040) {
      setErrorMessage("Năm tốt nghiệp phải nằm trong khoảng từ 2015 đến 2040.");
      return;
    }

    if (!currentGrade.trim()) {
      setErrorMessage("Vui lòng nhập 'Khối / Lớp / Năm học hiện tại' (Trường bắt buộc có dấu *).");
      return;
    }

    if (terms.length === 0) {
      setErrorMessage("Vui lòng nhập ít nhất 1 học kỳ trong bảng điểm.");
      return;
    }

    // 2. Kiểm tra lỗi môn học và điểm số
    const hasFieldErrors = Object.keys(validationErrors).length > 0;
    if (hasFieldErrors) {
      setErrorMessage("Vui lòng sửa các điểm ngoài thang hoặc tên môn học bị trùng lặp/để trống trước khi lưu.");
      return;
    }

    // 3. Validate chứng chỉ (nếu có nhập)
    const ieltsVal = enabledTests.ielts && ielts ? parseFloat(ielts) : null;
    if (ieltsVal != null && (ieltsVal < 0 || ieltsVal > 9.0 || (ieltsVal * 10) % 5 !== 0)) {
      setErrorMessage("Điểm IELTS không hợp lệ. Phải từ 0.0 đến 9.0 với bước nhảy 0.5 (ví dụ: 6.5, 7.0, 7.5).");
      return;
    }

    const toeflVal = enabledTests.toefl && toefl ? parseInt(toefl, 10) : null;
    if (toeflVal != null && (toeflVal < 0 || toeflVal > 120)) {
      setErrorMessage("Điểm TOEFL iBT không hợp lệ. Phải từ 0 đến 120.");
      return;
    }

    const duolingoVal = enabledTests.duolingo && duolingo ? parseInt(duolingo, 10) : null;
    if (duolingoVal != null && (duolingoVal < 10 || duolingoVal > 160)) {
      setErrorMessage("Điểm Duolingo English Test không hợp lệ. Phải từ 10 đến 160.");
      return;
    }

    const satVal = enabledTests.sat && sat ? parseInt(sat, 10) : null;
    if (satVal != null && (satVal < 400 || satVal > 1600)) {
      setErrorMessage("Điểm SAT không hợp lệ. Phải nằm trong khoảng từ 400 đến 1600.");
      return;
    }

    const actVal = enabledTests.act && act ? parseInt(act, 10) : null;
    if (actVal != null && (actVal < 1 || actVal > 36)) {
      setErrorMessage("Điểm ACT không hợp lệ. Phải từ 1 đến 36.");
      return;
    }

    const greVal = enabledTests.gre && gre ? parseInt(gre, 10) : null;
    if (greVal != null && (greVal < 260 || greVal > 340)) {
      setErrorMessage("Điểm GRE General Test không hợp lệ. Phải nằm trong khoảng từ 260 đến 340.");
      return;
    }

    const gmatVal = enabledTests.gmat && gmat ? parseInt(gmat, 10) : null;
    if (gmatVal != null && (gmatVal < 200 || gmatVal > 800)) {
      setErrorMessage("Điểm GMAT không hợp lệ. Phải nằm trong khoảng từ 200 đến 800.");
      return;
    }

    // Lọc các chứng chỉ khác hợp lệ
    const validOtherTests = otherTests.filter((t) => t.name.trim() !== "");
    const otherTestsJsonStr = validOtherTests.length > 0 ? JSON.stringify(validOtherTests) : null;

    const payload: SaveAcademicProfileRequest = {
      targetLevel,
      currentSchool: currentSchool.trim(),
      educationSystem,
      graduationYear: gradYearNum,
      currentGrade: currentGrade.trim(),
      gradeScale,
      intendedMajor: intendedMajor.trim() || null,
      ielts: ieltsVal,
      toefl: toeflVal,
      duolingo: duolingoVal,
      sat: satVal,
      act: actVal,
      gre: greVal,
      gmat: gmatVal,
      otherTestsJson: otherTestsJsonStr,
      terms,
    };

    setIsSaving(true);
    try {
      await profileApi.saveAcademicProfile(payload);
      setSuccessMessage("Đã lưu hồ sơ học thuật thành công! Dữ liệu đã được ghi an toàn vào hệ thống.");
      setIsEditing(false); // Chuyển về chế độ Xem, khóa form để tránh sửa nhầm

      if (andAnalyze) {
        setShowAnalysis(true);
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
      {/* THANH TRẠNG THÁI: VIEW MODE VS EDIT MODE */}
      <div className="flex flex-col gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <div
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl font-bold ${
              !isEditing ? "bg-emerald-100 text-emerald-700" : "bg-blue-100 text-blue-700"
            }`}
          >
            {!isEditing ? "🔒" : "✏️"}
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-bold text-slate-900">
                {!isEditing ? "Chế độ xem hồ sơ (Đã lưu trữ)" : "Chế độ chỉnh sửa hồ sơ"}
              </h2>
              <span
                className={`rounded-full px-2 py-0.5 text-[11px] font-semibold border ${
                  !isEditing
                    ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                    : "bg-amber-50 text-amber-700 border-amber-200"
                }`}
              >
                {!isEditing ? "Đã khóa chỉnh sửa" : "Đang chỉnh sửa"}
              </span>
            </div>
            <p className="text-xs text-slate-500">
              {!isEditing
                ? "Dữ liệu được bảo vệ an toàn. Bấm nút 'Chỉnh sửa' bên cạnh nếu bạn muốn cập nhật lại điểm."
                : "Điền các trường bắt buộc (*) và nhấn 'Lưu hồ sơ' khi hoàn tất."}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {!isEditing ? (
            <>
              <button
                type="button"
                onClick={() => setIsEditing(true)}
                className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-xs font-bold text-white shadow-sm transition hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                </svg>
                Chỉnh sửa hồ sơ
              </button>
              <button
                type="button"
                onClick={() => {
                  setShowAnalysis(true);
                  router.push("/profile/academic?autoAnalyze=true");
                }}
                className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-4 py-2.5 text-xs font-bold text-white shadow-sm transition hover:opacity-95"
              >
                <span>🚀 Phân tích năng lực (Đức)</span>
              </button>
            </>
          ) : (
            <div className="flex items-center gap-2">
              {hasSavedProfile && (
                <button
                  type="button"
                  onClick={handleCancelEdit}
                  disabled={isSaving}
                  className="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-600 hover:bg-slate-50"
                >
                  Hủy
                </button>
              )}
              <button
                type="button"
                onClick={() => handleSave(false)}
                disabled={isSaving}
                className="rounded-xl bg-blue-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-blue-700 disabled:opacity-50"
              >
                {isSaving ? "Đang lưu..." : "💾 Lưu thay đổi"}
              </button>
            </div>
          )}
        </div>
      </div>

      {/* THÔNG BÁO LỖI HOẶC THÀNH CÔNG */}
      {errorMessage && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-xs font-semibold text-red-800 flex items-start gap-2">
          <svg className="h-4 w-4 shrink-0 text-red-600 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span>{errorMessage}</span>
        </div>
      )}

      {successMessage && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-xs font-semibold text-emerald-800 flex items-start gap-2">
          <svg className="h-4 w-4 shrink-0 text-emerald-600 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
          <span>{successMessage}</span>
        </div>
      )}

      {/* KHỐI 1: THÔNG TIN HỌC VẤN CƠ BẢN (PHÂN HÓA BẬC HỌC) */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l9-5-9-5-9 5 9 5z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z" />
            </svg>
          </div>
          <div>
            <h2 className="text-lg font-bold text-slate-900">1. Thông tin học vấn cơ bản</h2>
            <p className="text-xs text-slate-500">
              {isGraduate
                ? "Bậc Thạc sĩ / Tiến sĩ: Áp dụng bảng điểm bậc Đại học và chứng chỉ sau đại học (GRE, GMAT)"
                : "Bậc Cử nhân / THPT: Áp dụng bảng điểm THPT (Lớp 10, 11, 12) và chứng chỉ SAT / ACT"}
            </p>
          </div>
        </div>

        <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-2">
          {/* Bậc muốn học tại Mỹ */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Bậc muốn học tại Mỹ <span className="text-red-500 font-bold">*</span>
            </label>
            <select
              disabled={!isEditing}
              value={targetLevel}
              onChange={(e) => {
                const newLevel = e.target.value;
                setTargetLevel(newLevel);
                if (newLevel === "master" || newLevel === "phd") {
                  if (educationSystem === "standard") setEducationSystem("university");
                  if (currentGrade.includes("Lớp")) setCurrentGrade("Đại học năm 3");
                  setGradeScale("4");
                } else {
                  if (educationSystem === "university") setEducationSystem("standard");
                  if (currentGrade.includes("Đại học")) setCurrentGrade("Lớp 11");
                  setGradeScale("10");
                }
              }}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
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
              disabled={!isEditing}
              value={intendedMajor}
              onChange={(e) => setIntendedMajor(e.target.value)}
              placeholder={isGraduate ? "Ví dụ: Khoa học Dữ liệu, MBA, Tài chính..." : "Ví dụ: Khoa học Máy tính, Kinh doanh, Tâm lý học..."}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
            />
          </div>

          {/* Trường hiện tại (Đại học nếu học Thạc sĩ/Tiến sĩ, THPT nếu học Đại học) */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              {isGraduate ? "Trường Đại học tốt nghiệp / đang học" : "Trường học hiện tại"}{" "}
              <span className="text-red-500 font-bold">*</span>
            </label>
            <input
              type="text"
              disabled={!isEditing}
              value={currentSchool}
              onChange={(e) => setCurrentSchool(e.target.value)}
              placeholder={isGraduate ? "Ví dụ: Đại học Bách Khoa Hà Nội, ĐH Kinh tế Quốc dân..." : "Ví dụ: THPT Chuyên Hà Nội - Amsterdam..."}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
            />
          </div>

          {/* Hệ chương trình */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Hệ chương trình đào tạo
            </label>
            <select
              disabled={!isEditing}
              value={educationSystem}
              onChange={(e) => setEducationSystem(e.target.value)}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
            >
              {EDUCATION_SYSTEMS.map((sys) => (
                <option key={sys.value} value={sys.value}>
                  {sys.label}
                </option>
              ))}
            </select>
          </div>

          {/* Khối / Lớp / Năm học hiện tại */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              {isGraduate ? "Năm học đại học hiện tại" : "Khối / Lớp hiện tại"}{" "}
              <span className="text-red-500 font-bold">*</span>
            </label>
            <input
              type="text"
              disabled={!isEditing}
              value={currentGrade}
              onChange={(e) => setCurrentGrade(e.target.value)}
              placeholder={isGraduate ? "Ví dụ: Đại học năm 3, Đại học năm 4, Đã tốt nghiệp Cử nhân" : "Ví dụ: Lớp 10, Lớp 11, Lớp 12..."}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
            />
          </div>

          {/* Năm tốt nghiệp dự kiến */}
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              {isGraduate ? "Năm tốt nghiệp Đại học (hoặc dự kiến)" : "Năm tốt nghiệp THPT dự kiến"}{" "}
              <span className="text-red-500 font-bold">*</span>
            </label>
            <input
              type="number"
              disabled={!isEditing}
              value={graduationYear}
              onChange={(e) => setGraduationYear(e.target.value)}
              placeholder="Ví dụ: 2026, 2027..."
              min={2015}
              max={2040}
              className={`mt-1 block w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm transition ${
                !isEditing
                  ? "bg-slate-50 border-slate-200 text-slate-700 cursor-not-allowed"
                  : "bg-white border-slate-300 text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              }`}
            />
          </div>
        </div>
      </section>

      {/* KHỐI 2: THANG ĐIỂM & BẢNG ĐIỂM HỌC KỲ (CÓ NÚT LÊN LỚP) */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-4 border-b border-slate-100 pb-4 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-purple-50 text-purple-600">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">
                {isGraduate ? "2. Bảng điểm bậc Đại học (Cử nhân)" : "2. Bảng điểm học kỳ (3 năm gần nhất)"}
              </h2>
              <p className="text-xs text-slate-500">
                {isGraduate
                  ? "Nhập điểm các năm/kỳ Đại học. Điểm số được tính trung bình có trọng số theo số tín chỉ."
                  : "Khi chuyển đổi thang điểm, hệ thống tự động quy đổi toàn bộ điểm các môn sang thang mới."}
              </p>
            </div>
          </div>

          {/* Widget Xem Trước GPA */}
          <div className="flex items-center gap-3 rounded-xl bg-slate-50 px-4 py-2 border border-slate-200">
            <span className="text-xs font-medium text-slate-500">
              {isGraduate ? "GPA Cử nhân tích lũy:" : "GPA Ước tính:"}
            </span>
            <span className="text-base font-extrabold text-blue-600">{overallGpaPreview.toFixed(2)}</span>
            <span className="text-xs text-slate-400">({GRADE_SCALES.find((s) => s.id === gradeScale)?.name})</span>
            <span className="text-xs font-bold text-emerald-600">≈ {usGpaEstimate.toFixed(2)}/4.0</span>
          </div>
        </div>

        {/* BỘ CHỌN THANG ĐIỂM VÀ TỰ ĐỘNG QUY ĐỔI */}
        <div className="mt-5">
          <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
            Chọn thang điểm áp dụng <span className="text-red-500 font-bold">*</span>
            <span className="ml-2 font-normal text-slate-400">
              (Bấm chuyển thang điểm để tự động quy đổi toàn bộ điểm đã nhập)
            </span>
          </label>
          <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {GRADE_SCALES.map((scale) => {
              const selected = gradeScale === scale.id;
              return (
                <button
                  key={scale.id}
                  type="button"
                  disabled={!isEditing}
                  onClick={() => handleGradeScaleChange(scale.id)}
                  className={`rounded-xl border p-3.5 text-left transition ${
                    selected
                      ? "border-blue-600 bg-blue-50/60 text-blue-900 shadow-sm ring-1 ring-blue-500 font-bold"
                      : !isEditing
                        ? "border-slate-200 bg-slate-50 text-slate-400 cursor-not-allowed"
                        : "border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50"
                  }`}
                >
                  <div className="flex items-center justify-between text-sm">
                    <span>{scale.name}</span>
                    {selected && <span className="text-blue-600 text-xs">✓ Đang chọn</span>}
                  </div>
                  <div className="mt-1 text-[11px] text-slate-500 font-normal">{scale.desc}</div>
                </button>
              );
            })}
          </div>
        </div>

        {/* NÚT "LÊN LỚP" / TIẾN ĐỘ BẢNG ĐIỂM */}
        {isEditing && (
          <div className="mt-5 rounded-xl border border-blue-100 bg-blue-50/40 p-4">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold uppercase tracking-wider text-blue-900">
                    Tiến độ bảng điểm:
                  </span>
                  <div className="flex items-center gap-1.5">
                    {!isGraduate ? (
                      <>
                        <span
                          className={`rounded px-2 py-0.5 text-[11px] font-semibold border ${
                            currentGradeProgress.hasGrade10
                              ? "bg-emerald-100 text-emerald-800 border-emerald-200"
                              : "bg-slate-100 text-slate-500 border-slate-200"
                          }`}
                        >
                          Lớp 10 {currentGradeProgress.hasGrade10 ? "✓" : "○"}
                        </span>
                        <span
                          className={`rounded px-2 py-0.5 text-[11px] font-semibold border ${
                            currentGradeProgress.hasGrade11
                              ? "bg-emerald-100 text-emerald-800 border-emerald-200"
                              : "bg-slate-100 text-slate-500 border-slate-200"
                          }`}
                        >
                          Lớp 11 {currentGradeProgress.hasGrade11 ? "✓" : "○"}
                        </span>
                        <span
                          className={`rounded px-2 py-0.5 text-[11px] font-semibold border ${
                            currentGradeProgress.hasGrade12
                              ? "bg-emerald-100 text-emerald-800 border-emerald-200"
                              : "bg-slate-100 text-slate-500 border-slate-200"
                          }`}
                        >
                          Lớp 12 {currentGradeProgress.hasGrade12 ? "✓" : "○"}
                        </span>
                      </>
                    ) : (
                      <>
                        <span className="rounded bg-emerald-100 px-2 py-0.5 text-[11px] font-semibold text-emerald-800 border border-emerald-200">
                          Đại học ({terms.length} kỳ đã nhập)
                        </span>
                      </>
                    )}
                  </div>
                </div>
                <p className="mt-1 text-[11px] text-blue-800">
                  {!isGraduate
                    ? "Bấm nút 'Lên lớp' để hệ thống tự động sinh 2 học kỳ cho khối lớp tiếp theo với danh sách môn học sẵn có."
                    : "Bấm 'Thêm năm học tiếp theo' để nhập bảng điểm các năm đại học tiếp theo."}
                </p>
              </div>

              {/* CÁC NÚT LÊN LỚP */}
              <div className="flex items-center gap-2">
                {!isGraduate ? (
                  <>
                    {!currentGradeProgress.hasGrade11 && (
                      <button
                        type="button"
                        onClick={() => handlePromoteGrade("11")}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-blue-700"
                      >
                        <span>🎓 Lên Lớp 11</span>
                      </button>
                    )}
                    {currentGradeProgress.hasGrade11 && !currentGradeProgress.hasGrade12 && (
                      <button
                        type="button"
                        onClick={() => handlePromoteGrade("12")}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-blue-700"
                      >
                        <span>🎓 Lên Lớp 12</span>
                      </button>
                    )}
                  </>
                ) : (
                  <>
                    <button
                      type="button"
                      onClick={() => handlePromoteUniversityYear(terms.length + 1)}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-blue-700"
                    >
                      <span>🎓 Thêm Năm học tiếp theo</span>
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>
        )}

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
                      disabled={!isEditing}
                      value={term.termName}
                      onChange={(e) => handleTermNameChange(tIdx, e.target.value)}
                      className={`text-sm font-bold text-slate-800 rounded px-2 py-1 transition ${
                        !isEditing
                          ? "bg-transparent border-transparent"
                          : "border border-slate-300 bg-white hover:border-blue-400 focus:border-blue-500"
                      }`}
                    />
                  </div>

                  <div className="flex items-center gap-3">
                    <span className="text-xs font-medium text-slate-600">
                      GPA kỳ: <strong className="text-blue-600">{currentTermGpa.toFixed(2)}</strong>
                    </span>

                    {isEditing && terms.length > 1 && (
                      <button
                        type="button"
                        onClick={() => handleRemoveTerm(tIdx)}
                        className="text-xs font-semibold text-red-600 hover:text-red-700 hover:underline"
                      >
                        Xóa kỳ này
                      </button>
                    )}
                  </div>
                </div>

                {/* Bảng điểm các môn */}
                <div className="mt-3 space-y-2">
                  <div className="grid grid-cols-12 gap-2 text-[11px] font-bold uppercase tracking-wider text-slate-500 px-2">
                    <div className="col-span-6 sm:col-span-5">Tên môn học *</div>
                    <div className="col-span-4 sm:col-span-3">Điểm số *</div>
                    <div className="hidden sm:block sm:col-span-3">Số tín chỉ / Trọng số</div>
                    <div className="col-span-2 sm:col-span-1 text-center">Thao tác</div>
                  </div>

                  {term.scores.map((scoreItem, sIdx) => {
                    const subKey = `${tIdx}-${sIdx}`;
                    const hasSubError = validationErrors[subKey];
                    const hasScoreError = validationErrors[`${subKey}-score`];

                    return (
                      <div key={sIdx} className="space-y-1">
                        <div className="grid grid-cols-12 gap-2 items-center rounded-lg bg-white p-2 border border-slate-200">
                          {/* Tên môn học */}
                          <div className="col-span-6 sm:col-span-5">
                            <input
                              type="text"
                              disabled={!isEditing}
                              value={scoreItem.subject}
                              onChange={(e) => handleSubjectChange(tIdx, sIdx, "subject", e.target.value)}
                              placeholder="Nhập tên môn học..."
                              className={`w-full rounded-md border px-2.5 py-1.5 text-xs transition ${
                                !isEditing
                                  ? "bg-slate-50 border-slate-200 text-slate-700"
                                  : hasSubError
                                    ? "border-red-400 bg-red-50 text-red-900 focus:border-red-500"
                                    : "border-slate-300 text-slate-900 focus:border-blue-500"
                              }`}
                            />
                          </div>

                          {/* Điểm số */}
                          <div className="col-span-4 sm:col-span-3">
                            {gradeScale === "letter" ? (
                              <select
                                disabled={!isEditing}
                                value={scoreItem.rawScore || "A"}
                                onChange={(e) => handleSubjectChange(tIdx, sIdx, "rawScore", e.target.value)}
                                className={`w-full rounded-md border px-2 py-1.5 text-xs transition ${
                                  !isEditing
                                    ? "bg-slate-50 border-slate-200 text-slate-700"
                                    : hasScoreError
                                      ? "border-red-400 bg-red-50 text-red-900 focus:border-red-500"
                                      : "border-slate-300 text-slate-900 focus:border-blue-500"
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
                                disabled={!isEditing}
                                value={scoreItem.rawScore ?? scoreItem.score}
                                onChange={(e) => handleSubjectChange(tIdx, sIdx, "score", e.target.value)}
                                placeholder="Điểm..."
                                className={`w-full rounded-md border px-2.5 py-1.5 text-xs transition ${
                                  !isEditing
                                    ? "bg-slate-50 border-slate-200 text-slate-700"
                                    : hasScoreError
                                      ? "border-red-400 bg-red-50 text-red-900 focus:border-red-500"
                                      : "border-slate-300 text-slate-900 focus:border-blue-500"
                                }`}
                              />
                            )}
                          </div>

                          {/* Số tín chỉ / Trọng số */}
                          <div className="hidden sm:block sm:col-span-3">
                            <input
                              type="number"
                              disabled={!isEditing}
                              step="0.5"
                              min="0"
                              value={scoreItem.credits ?? ""}
                              onChange={(e) => handleSubjectChange(tIdx, sIdx, "credits", e.target.value)}
                              placeholder={isGraduate ? "Ví dụ: 3 (tín chỉ)" : "Không bắt buộc"}
                              className={`w-full rounded-md border px-2.5 py-1.5 text-xs transition ${
                                !isEditing
                                  ? "bg-slate-50 border-slate-200 text-slate-700"
                                  : "border-slate-300 text-slate-900 focus:border-blue-500"
                              }`}
                            />
                          </div>

                          {/* Xóa dòng */}
                          <div className="col-span-2 sm:col-span-1 text-center">
                            {isEditing && term.scores.length > 1 ? (
                              <button
                                type="button"
                                onClick={() => handleRemoveSubject(tIdx, sIdx)}
                                title="Xóa môn học này"
                                className="text-slate-400 hover:text-red-600 transition"
                              >
                                <svg className="h-4 w-4 mx-auto" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                              </button>
                            ) : (
                              <span className="text-slate-300 text-xs">-</span>
                            )}
                          </div>
                        </div>

                        {/* Báo lỗi validation dòng */}
                        {(hasSubError || hasScoreError) && (
                          <div className="px-2 text-[11px] font-semibold text-red-600">
                            {hasSubError && <div>• {hasSubError}</div>}
                            {hasScoreError && <div>• {hasScoreError}</div>}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>

                {/* Thêm môn học */}
                {isEditing && (
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
                )}
              </div>
            );
          })}
        </div>

        {/* Nút Thêm Học Kỳ */}
        {isEditing && (
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
        )}
      </section>

      {/* KHỐI 3: CHỨNG CHỈ NGOẠI NGỮ & BÀI THI CHUẨN HÓA (ĐỘNG TỪ CSDL) */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-b border-slate-100 pb-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 text-emerald-600">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z" />
              </svg>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">3. Chứng chỉ Ngoại ngữ & Bài thi chuẩn hóa</h2>
              <p className="text-xs text-slate-500">
                Lựa chọn các chứng chỉ bạn đang có trong CSDL (Có thể để trống nếu chưa thi)
              </p>
            </div>
          </div>

          {/* BẬT / TẮT HOẶC THÊM CHỨNG CHỈ */}
          {isEditing && (
            <div className="flex flex-wrap gap-1.5 items-center">
              <span className="text-[11px] font-semibold text-slate-500 mr-1">Thêm chứng chỉ:</span>
              <button
                type="button"
                onClick={() => handleToggleTest("ielts")}
                className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                  enabledTests.ielts ? "bg-red-50 text-red-700 border-red-200" : "bg-white text-slate-600 border-slate-200"
                }`}
              >
                + IELTS
              </button>
              <button
                type="button"
                onClick={() => handleToggleTest("toefl")}
                className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                  enabledTests.toefl ? "bg-blue-50 text-blue-700 border-blue-200" : "bg-white text-slate-600 border-slate-200"
                }`}
              >
                + TOEFL
              </button>
              <button
                type="button"
                onClick={() => handleToggleTest("duolingo")}
                className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                  enabledTests.duolingo ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-white text-slate-600 border-slate-200"
                }`}
              >
                + Duolingo
              </button>
              {!isGraduate ? (
                <>
                  <button
                    type="button"
                    onClick={() => handleToggleTest("sat")}
                    className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                      enabledTests.sat ? "bg-purple-50 text-purple-700 border-purple-200" : "bg-white text-slate-600 border-slate-200"
                    }`}
                  >
                    + SAT
                  </button>
                  <button
                    type="button"
                    onClick={() => handleToggleTest("act")}
                    className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                      enabledTests.act ? "bg-indigo-50 text-indigo-700 border-indigo-200" : "bg-white text-slate-600 border-slate-200"
                    }`}
                  >
                    + ACT
                  </button>
                </>
              ) : (
                <>
                  <button
                    type="button"
                    onClick={() => handleToggleTest("gre")}
                    className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                      enabledTests.gre ? "bg-teal-50 text-teal-700 border-teal-200" : "bg-white text-slate-600 border-slate-200"
                    }`}
                  >
                    + GRE
                  </button>
                  <button
                    type="button"
                    onClick={() => handleToggleTest("gmat")}
                    className={`rounded-lg px-2.5 py-1 text-xs font-semibold border transition ${
                      enabledTests.gmat ? "bg-cyan-50 text-cyan-700 border-cyan-200" : "bg-white text-slate-600 border-slate-200"
                    }`}
                  >
                    + GMAT
                  </button>
                </>
              )}
              <button
                type="button"
                onClick={handleAddOtherTest}
                className="rounded-lg px-2.5 py-1 text-xs font-semibold border border-dashed border-slate-300 bg-white text-slate-700 hover:border-slate-400"
              >
                + Khác (AP/IB/PTE)
              </button>
            </div>
          )}
        </div>

        {/* CÁC Ô NHẬP ĐIỂM CHỨNG CHỈ */}
        <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-3">
          {/* IELTS */}
          {enabledTests.ielts && (
            <div className="relative rounded-xl border border-red-100 bg-red-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-red-900">IELTS (0.0 - 9.0)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("ielts")}
                    className="text-[11px] text-slate-400 hover:text-red-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                step="0.5"
                min="0"
                max="9"
                value={ielts}
                onChange={(e) => setIelts(e.target.value)}
                placeholder="Ví dụ: 7.0 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-red-500 focus:ring-1 focus:ring-red-500"
                }`}
              />
            </div>
          )}

          {/* TOEFL */}
          {enabledTests.toefl && (
            <div className="relative rounded-xl border border-blue-100 bg-blue-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-blue-900">TOEFL iBT (0 - 120)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("toefl")}
                    className="text-[11px] text-slate-400 hover:text-blue-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="0"
                max="120"
                value={toefl}
                onChange={(e) => setToefl(e.target.value)}
                placeholder="Ví dụ: 95 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                }`}
              />
            </div>
          )}

          {/* Duolingo */}
          {enabledTests.duolingo && (
            <div className="relative rounded-xl border border-emerald-100 bg-emerald-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-emerald-900">Duolingo Test (10 - 160)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("duolingo")}
                    className="text-[11px] text-slate-400 hover:text-emerald-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="10"
                max="160"
                value={duolingo}
                onChange={(e) => setDuolingo(e.target.value)}
                placeholder="Ví dụ: 125 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
                }`}
              />
            </div>
          )}

          {/* SAT */}
          {enabledTests.sat && (
            <div className="relative rounded-xl border border-purple-100 bg-purple-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-purple-900">SAT (400 - 1600)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("sat")}
                    className="text-[11px] text-slate-400 hover:text-purple-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="400"
                max="1600"
                value={sat}
                onChange={(e) => setSat(e.target.value)}
                placeholder="Ví dụ: 1450 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-purple-500 focus:ring-1 focus:ring-purple-500"
                }`}
              />
            </div>
          )}

          {/* ACT */}
          {enabledTests.act && (
            <div className="relative rounded-xl border border-indigo-100 bg-indigo-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-indigo-900">ACT (1 - 36)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("act")}
                    className="text-[11px] text-slate-400 hover:text-indigo-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="1"
                max="36"
                value={act}
                onChange={(e) => setAct(e.target.value)}
                placeholder="Ví dụ: 32 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
                }`}
              />
            </div>
          )}

          {/* GRE (Sau đại học) */}
          {enabledTests.gre && (
            <div className="relative rounded-xl border border-teal-100 bg-teal-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-teal-900">GRE General (260 - 340)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("gre")}
                    className="text-[11px] text-slate-400 hover:text-teal-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="260"
                max="340"
                value={gre}
                onChange={(e) => setGre(e.target.value)}
                placeholder="Ví dụ: 322 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-teal-500 focus:ring-1 focus:ring-teal-500"
                }`}
              />
            </div>
          )}

          {/* GMAT (Sau đại học) */}
          {enabledTests.gmat && (
            <div className="relative rounded-xl border border-cyan-100 bg-cyan-50/20 p-3.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-cyan-900">GMAT (200 - 800)</label>
                {isEditing && (
                  <button
                    type="button"
                    onClick={() => handleToggleTest("gmat")}
                    className="text-[11px] text-slate-400 hover:text-cyan-600"
                  >
                    ✕ Ẩn
                  </button>
                )}
              </div>
              <input
                type="number"
                disabled={!isEditing}
                min="200"
                max="800"
                value={gmat}
                onChange={(e) => setGmat(e.target.value)}
                placeholder="Ví dụ: 685 hoặc để trống"
                className={`mt-1.5 block w-full rounded-lg border px-3 py-2 text-sm transition ${
                  !isEditing
                    ? "bg-slate-50 border-slate-200 text-slate-700"
                    : "bg-white border-slate-300 text-slate-900 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500"
                }`}
              />
            </div>
          )}
        </div>

        {/* DANH SÁCH CHỨNG CHỈ TÙY CHỌN KHÁC (AP, IB, PTE...) */}
        {otherTests.length > 0 && (
          <div className="mt-5 border-t border-slate-100 pt-4">
            <span className="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-2">
              Chứng chỉ khác (AP, IB, PTE Academic...):
            </span>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {otherTests.map((t) => (
                <div key={t.id} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50/50 p-2">
                  <input
                    type="text"
                    disabled={!isEditing}
                    value={t.name}
                    onChange={(e) => handleUpdateOtherTest(t.id, "name", e.target.value)}
                    placeholder="Tên chứng chỉ (vd: AP Calculus BC, IB Math)"
                    className="w-1/2 rounded border border-slate-300 bg-white px-2 py-1 text-xs"
                  />
                  <input
                    type="text"
                    disabled={!isEditing}
                    value={t.score}
                    onChange={(e) => handleUpdateOtherTest(t.id, "score", e.target.value)}
                    placeholder="Điểm (vd: 5, 42)"
                    className="w-1/3 rounded border border-slate-300 bg-white px-2 py-1 text-xs"
                  />
                  {isEditing && (
                    <button
                      type="button"
                      onClick={() => handleRemoveOtherTest(t.id)}
                      className="text-slate-400 hover:text-red-600"
                    >
                      ✕
                    </button>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </section>

      {/* KHỐI 4: KẾT NỐI STORY #4 (PHÂN TÍCH NĂNG LỰC HỌC THUẬT - XUÂN ĐỨC) */}
      {isAnalysisVisible && (
        <section className="rounded-2xl border-2 border-indigo-200 bg-gradient-to-br from-indigo-50/60 via-purple-50/40 to-white p-6 shadow-md animate-in fade-in">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-b border-indigo-100 pb-4">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-indigo-600 text-white shadow-sm">
                <span className="text-xl">🚀</span>
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-lg font-extrabold text-indigo-950">
                    Phân tích năng lực học thuật
                  </h2>
                  <span className="rounded-full bg-indigo-100 px-2.5 py-0.5 text-[11px] font-bold text-indigo-800 border border-indigo-200">
                    AI Recommendation Engine
                  </span>
                </div>
                <p className="text-xs text-indigo-700">
                  Phân tích dựa trên điểm chuẩn hóa CSDL và phân nhóm trường Reach / Match / Safety
                </p>
              </div>
            </div>

            <span className={`rounded-xl px-3 py-1.5 text-xs font-bold border ${profileClassification.badgeColor}`}>
              {profileClassification.tier}
            </span>
          </div>

          <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-3">
            {/* REACH */}
            <div className="rounded-xl border border-purple-200 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-purple-700 uppercase tracking-wider">🎯 Nhóm Vươn tới (Reach)</span>
                <span className="rounded bg-purple-100 px-2 py-0.5 text-[10px] font-bold text-purple-800">Cạnh tranh</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-slate-800">{profileClassification.reachDescription}</p>
              <p className="mt-1 text-[11px] text-slate-500">
                Các trường top đòi hỏi bài luận ấn tượng và hoạt động ngoại khóa xuất sắc để tạo đột phá.
              </p>
            </div>

            {/* MATCH */}
            <div className="rounded-xl border border-blue-200 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-blue-700 uppercase tracking-wider">⚖️ Nhóm Mục tiêu (Match)</span>
                <span className="rounded bg-blue-100 px-2 py-0.5 text-[10px] font-bold text-blue-800">Vừa sức</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-slate-800">{profileClassification.matchDescription}</p>
              <p className="mt-1 text-[11px] text-slate-500">
                Hồ sơ học thuật hiện tại nằm trong khoảng 50% ứng viên được nhận của nhóm trường này.
              </p>
            </div>

            {/* SAFETY */}
            <div className="rounded-xl border border-emerald-200 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-emerald-700 uppercase tracking-wider">🛡️ Nhóm An toàn (Safety)</span>
                <span className="rounded bg-emerald-100 px-2 py-0.5 text-[10px] font-bold text-emerald-800">Đảm bảo</span>
              </div>
              <p className="mt-2 text-xs font-semibold text-slate-800">{profileClassification.safetyDescription}</p>
              <p className="mt-1 text-[11px] text-slate-500">
                Tỷ lệ trúng tuyển trên 80% với cơ hội nhận học bổng tự động (Merit-based scholarships) cao.
              </p>
            </div>
          </div>

          <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between rounded-xl bg-indigo-950 p-4 text-white">
            <div className="flex items-center gap-3">
              <span className="text-2xl">💡</span>
              <div>
                <div className="text-xs font-bold text-indigo-200">Gợi ý lộ trình tiếp theo:</div>
                <div className="text-xs text-slate-200">
                  {ielts || toefl ? "Đã có chứng chỉ ngoại ngữ. Hãy tra cứu danh sách trường có hỗ trợ tài chính tốt." : "Nên thi bổ sung chứng chỉ IELTS hoặc Duolingo để mở rộng số lượng trường nộp hồ sơ."}
                </div>
              </div>
            </div>
            <Link
              href="/schools"
              className="inline-flex items-center justify-center rounded-lg bg-white px-4 py-2 text-xs font-bold text-indigo-950 hover:bg-indigo-50 transition whitespace-nowrap"
            >
              Khám phá danh sách trường phù hợp →
            </Link>
          </div>
        </section>
      )}

      {/* HÀNH ĐỘNG SUBMIT VÀ NÚT LƯU */}
      <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:items-center sm:justify-end">
        {isEditing ? (
          <>
            {hasSavedProfile && (
              <button
                type="button"
                onClick={handleCancelEdit}
                disabled={isSaving}
                className="rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-400 disabled:opacity-50"
              >
                Hủy thay đổi
              </button>
            )}

            <button
              type="button"
              onClick={() => handleSave(false)}
              disabled={isSaving}
              className="rounded-xl border border-slate-300 bg-white px-6 py-3 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
            >
              {isSaving ? "Đang lưu..." : "💾 Lưu hồ sơ"}
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
          </>
        ) : (
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => setIsEditing(true)}
              className="inline-flex items-center gap-2 rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
              </svg>
              Chỉnh sửa lại hồ sơ
            </button>

            <button
              type="button"
              onClick={() => {
                setShowAnalysis(true);
                router.push("/profile/academic?autoAnalyze=true");
              }}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-6 py-3 text-sm font-bold text-white shadow-md transition hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <span>🚀</span> Phân tích năng lực học thuật (Story #4)
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
