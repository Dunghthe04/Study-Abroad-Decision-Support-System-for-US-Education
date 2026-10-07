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
  { id: "10", name: "Thang 10", desc: "0.0 - 10.0 (Phổ biến tại Việt Nam)", placeholder: "Ví dụ: 8.5" },
  { id: "100", name: "Thang 100", desc: "0 - 100 (Hệ phần trăm)", placeholder: "Ví dụ: 85" },
  { id: "4", name: "Thang 4", desc: "0.0 - 4.0 (Hệ tín chỉ Đại học)", placeholder: "Ví dụ: 3.6" },
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
  { value: "middle_school", label: "Trung học cơ sở / Cấp 2 (Middle School)" },
  { value: "secondary", label: "Trung học phổ thông / Cấp 3 (High School)" },
  { value: "community_college", label: "Cao đẳng cộng đồng 2+2 (Community College)" },
  { value: "undergraduate", label: "Đại học 4 năm (Undergraduate)" },
  { value: "master", label: "Thạc sĩ (Master)" },
  { value: "phd", label: "Tiến sĩ (PhD)" },
];

/**
 * Quy đổi điểm số giữa các thang điểm khi người dùng chủ động chuyển thang
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

export function AcademicProfileForm({ initialProfile }: Props) {
  const router = useRouter();

  // Kiểm tra xem đã có hồ sơ lưu trước đó hay chưa
  const hasSavedProfile = Boolean(
    initialProfile &&
      initialProfile.id &&
      initialProfile.id !== "00000000-0000-0000-0000-000000000000" &&
      initialProfile.currentSchool
  );

  // Chế độ: false = Xem (không sửa được, có nút Sửa), true = Chỉnh sửa
  const [isEditing, setIsEditing] = useState<boolean>(!hasSavedProfile);

  // Thông tin học vấn
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
    initialProfile?.currentGrade ?? (isGraduate ? "Đại học năm 3" : targetLevel === "middle_school" ? "Lớp 8" : "Lớp 11")
  );
  const [intendedMajor, setIntendedMajor] = useState<string>(initialProfile?.intendedMajor ?? "");

  // Thang điểm áp dụng
  const [gradeScale, setGradeScale] = useState<GradeScaleType>(
    (initialProfile?.gradeScale as GradeScaleType) || (isGraduate ? "4" : "10")
  );

  // Danh sách học kỳ mặc định
  const defaultHighSchoolTerms: TranscriptTerm[] = [
    {
      termName: targetLevel === "middle_school" ? "Lớp 8 - Học kỳ 1" : "Lớp 10 - Học kỳ 1",
      termOrder: 1,
      scores: [
        { subject: "Toán học", score: 8.5, rawScore: "8.5", credits: null },
        { subject: "Ngữ văn", score: 8.0, rawScore: "8.0", credits: null },
        { subject: "Tiếng Anh", score: 9.0, rawScore: "9.0", credits: null },
      ],
    },
    {
      termName: targetLevel === "middle_school" ? "Lớp 8 - Học kỳ 2" : "Lớp 10 - Học kỳ 2",
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
      termName: "Đại học - Kỳ 1",
      termOrder: 1,
      scores: [
        { subject: "Giải tích đại học", score: 3.5, rawScore: "3.5", credits: 3 },
        { subject: "Nhập môn lập trình", score: 3.8, rawScore: "3.8", credits: 4 },
        { subject: "Tiếng Anh chuyên ngành", score: 3.7, rawScore: "3.7", credits: 3 },
      ],
    },
    {
      termName: "Đại học - Kỳ 2",
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

  // Chứng chỉ Ngoại ngữ & Chuẩn hóa
  const [ielts, setIelts] = useState<string>(initialProfile?.ielts != null ? String(initialProfile.ielts) : "");
  const [toefl, setToefl] = useState<string>(initialProfile?.toefl != null ? String(initialProfile.toefl) : "");
  const [duolingo, setDuolingo] = useState<string>(
    initialProfile?.duolingo != null ? String(initialProfile.duolingo) : ""
  );
  const [sat, setSat] = useState<string>(initialProfile?.sat != null ? String(initialProfile.sat) : "");
  const [act, setAct] = useState<string>(initialProfile?.act != null ? String(initialProfile.act) : "");
  const [gre, setGre] = useState<string>(initialProfile?.gre != null ? String(initialProfile.gre) : "");
  const [gmat, setGmat] = useState<string>(initialProfile?.gmat != null ? String(initialProfile.gmat) : "");

  // Chứng chỉ khác (AP, IB, PTE...)
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
      // Bỏ qua nếu json lỗi
    }
    return [];
  };

  const [otherTests, setOtherTests] = useState<OtherTestItem[]>(
    parseOtherTests(initialProfile?.otherTestsJson)
  );

  // Trạng thái các loại chứng chỉ đang hiển thị
  const [enabledTests, setEnabledTests] = useState<{ [key: string]: boolean }>(() => ({
    ielts: Boolean(initialProfile?.ielts != null || !hasSavedProfile),
    toefl: Boolean(initialProfile?.toefl != null),
    duolingo: Boolean(initialProfile?.duolingo != null),
    sat: Boolean(initialProfile?.sat != null || (!isGraduate && !hasSavedProfile && targetLevel !== "middle_school")),
    act: Boolean(initialProfile?.act != null),
    gre: Boolean(initialProfile?.gre != null || (isGraduate && !hasSavedProfile)),
    gmat: Boolean(initialProfile?.gmat != null),
  }));

  // Status & Feedback State
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Đổi thang điểm và tự động quy đổi điểm các môn
  const handleGradeScaleChange = (newScale: GradeScaleType) => {
    if (newScale === gradeScale) return;
    const oldScale = gradeScale;
    setGradeScale(newScale);

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

  // Quản lý học kỳ (không giới hạn tối đa 6 học kỳ, hỗ trợ đại học 8-12 kỳ)
  const handleAddTerm = () => {
    if (terms.length >= 24) {
      alert("Hệ thống hỗ trợ tối đa 24 học kỳ.");
      return;
    }
    const nextOrder = terms.length + 1;
    const initScore = gradeScale === "100" ? 80 : gradeScale === "letter" ? 4.0 : gradeScale === "4" ? 3.5 : 8.0;
    const initRaw = gradeScale === "letter" ? "A" : String(initScore);

    // Tự động gợi ý tên kỳ tiếp theo
    const defaultTermName = isGraduate
      ? `Đại học - Kỳ ${nextOrder}`
      : targetLevel === "middle_school"
        ? `Lớp THCS - Học kỳ ${nextOrder}`
        : `Học kỳ ${nextOrder}`;

    setTerms([
      ...terms,
      {
        termName: defaultTermName,
        termOrder: nextOrder,
        scores: [
          {
            subject: isGraduate ? "Môn học chuyên ngành" : "Toán học",
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

  // Quản lý môn học trong từng học kỳ
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

  // Realtime validation môn học và điểm số
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

        // Kiểm tra điểm theo thang điểm đã chọn
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

  // Tính GPA theo đúng thang điểm hiện tại của người dùng (không ép quy đổi thang 4)
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
  const handleSave = async (andGoToAdvisor = false) => {
    setErrorMessage(null);
    setSuccessMessage(null);

    const notifyError = (msg: string) => {
      setErrorMessage(msg);
      setTimeout(() => {
        document.getElementById("form-feedback-section")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }, 50);
    };

    // 1. Kiểm tra các trường BẮT BUỘC (*)
    if (!currentSchool.trim()) {
      notifyError(
        isGraduate
          ? "Vui lòng nhập 'Trường Đại học tốt nghiệp / đang học' (Trường bắt buộc có dấu *)."
          : "Vui lòng nhập 'Trường học hiện tại' (Trường bắt buộc có dấu *)."
      );
      return;
    }

    if (!graduationYear.trim()) {
      notifyError("Vui lòng nhập 'Năm tốt nghiệp dự kiến' (Trường bắt buộc có dấu *).");
      return;
    }

    const gradYearNum = parseInt(graduationYear, 10);
    if (isNaN(gradYearNum) || gradYearNum < 2015 || gradYearNum > 2040) {
      notifyError("Năm tốt nghiệp phải nằm trong khoảng từ 2015 đến 2040.");
      return;
    }

    if (!currentGrade.trim()) {
      notifyError("Vui lòng nhập 'Khối / Lớp / Năm học hiện tại' (Trường bắt buộc có dấu *).");
      return;
    }

    if (terms.length === 0) {
      notifyError("Vui lòng nhập ít nhất 1 học kỳ trong bảng điểm.");
      return;
    }

    // 2. Kiểm tra lỗi môn học và điểm số
    const hasFieldErrors = Object.keys(validationErrors).length > 0;
    if (hasFieldErrors) {
      notifyError("Vui lòng sửa các điểm ngoài thang hoặc tên môn học bị trùng lặp/để trống trước khi lưu.");
      return;
    }

    // 3. Validate chứng chỉ (nếu có nhập)
    const ieltsVal = enabledTests.ielts && ielts ? parseFloat(ielts) : null;
    if (ieltsVal != null && (ieltsVal < 0 || ieltsVal > 9.0 || (ieltsVal * 10) % 5 !== 0)) {
      notifyError("Điểm IELTS không hợp lệ. Phải từ 0.0 đến 9.0 với bước nhảy 0.5 (ví dụ: 6.5, 7.0, 7.5).");
      return;
    }

    const toeflVal = enabledTests.toefl && toefl ? parseInt(toefl, 10) : null;
    if (toeflVal != null && (toeflVal < 0 || toeflVal > 120)) {
      notifyError("Điểm TOEFL iBT không hợp lệ. Phải từ 0 đến 120.");
      return;
    }

    const duolingoVal = enabledTests.duolingo && duolingo ? parseInt(duolingo, 10) : null;
    if (duolingoVal != null && (duolingoVal < 10 || duolingoVal > 160)) {
      notifyError("Điểm Duolingo English Test không hợp lệ. Phải từ 10 đến 160.");
      return;
    }

    const satVal = enabledTests.sat && sat ? parseInt(sat, 10) : null;
    if (satVal != null && (satVal < 400 || satVal > 1600)) {
      notifyError("Điểm SAT không hợp lệ. Phải nằm trong khoảng từ 400 đến 1600.");
      return;
    }

    const actVal = enabledTests.act && act ? parseInt(act, 10) : null;
    if (actVal != null && (actVal < 1 || actVal > 36)) {
      notifyError("Điểm ACT không hợp lệ. Phải từ 1 đến 36.");
      return;
    }

    const greVal = enabledTests.gre && gre ? parseInt(gre, 10) : null;
    if (greVal != null && (greVal < 260 || greVal > 340)) {
      notifyError("Điểm GRE General Test không hợp lệ. Phải nằm trong khoảng từ 260 đến 340.");
      return;
    }

    const gmatVal = enabledTests.gmat && gmat ? parseInt(gmat, 10) : null;
    if (gmatVal != null && (gmatVal < 200 || gmatVal > 800)) {
      notifyError("Điểm GMAT không hợp lệ. Phải nằm trong khoảng từ 200 đến 800.");
      return;
    }

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
      setIsEditing(false);
      setTimeout(() => {
        document.getElementById("form-feedback-section")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }, 50);

      // Nếu bấm nút phân tích: chuyển sang route phân tích GPA (USAS-365)
      if (andGoToAdvisor) {
        router.push("/profile/academic/analysis");
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        notifyError(err.message);
      } else {
        notifyError("Đã xảy ra lỗi khi lưu hồ sơ. Vui lòng thử lại.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-8">
      {/* THANH TRẠNG THÁI: VIEW MODE VS EDIT MODE (KHÔNG CÓ NÚT HỦY/LƯU Ở TRÊN NÀY) */}
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
                ? "Dữ liệu được bảo vệ an toàn. Bấm nút 'Chỉnh sửa hồ sơ' nếu bạn muốn cập nhật lại điểm."
                : "Điền các thông tin bắt buộc (*). Các nút Lưu và Hủy nằm ở cuối trang sau khi bạn điền xong."}
            </p>
          </div>
        </div>

        <div>
          {!isEditing && (
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
          )}
        </div>
      </div>

      {/* KHỐI 1: THÔNG TIN HỌC VẤN CƠ BẢN */}
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
                : targetLevel === "middle_school"
                  ? "Bậc THCS / Cấp 2: Áp dụng bảng điểm các lớp THCS (Lớp 6, 7, 8, 9)"
                  : "Bậc Cử nhân / THPT: Áp dụng bảng điểm các lớp THPT (Lớp 10, 11, 12)"}
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
                } else if (newLevel === "middle_school") {
                  if (educationSystem === "university") setEducationSystem("standard");
                  setCurrentGrade("Lớp 8");
                  setGradeScale("10");
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

          {/* Trường hiện tại */}
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
              placeholder={
                isGraduate
                  ? "Ví dụ: Đại học Bách Khoa Hà Nội, ĐH Kinh tế Quốc dân..."
                  : targetLevel === "middle_school"
                    ? "Ví dụ: THCS Giảng Võ, THCS Cầu Giấy..."
                    : "Ví dụ: THPT Chuyên Hà Nội - Amsterdam..."
              }
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
              placeholder={
                isGraduate
                  ? "Ví dụ: Đại học năm 3, Đại học năm 4, Đã tốt nghiệp Cử nhân"
                  : targetLevel === "middle_school"
                    ? "Ví dụ: Lớp 6, Lớp 7, Lớp 8, Lớp 9"
                    : "Ví dụ: Lớp 10, Lớp 11, Lớp 12..."
              }
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
              {isGraduate ? "Năm tốt nghiệp Đại học (hoặc dự kiến)" : "Năm tốt nghiệp dự kiến"}{" "}
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

      {/* KHỐI 2: THANG ĐIỂM & BẢNG ĐIỂM HỌC KỲ (KHÔNG ÉP THANG 4, KHÔNG GIỚI HẠN 6 KỲ) */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-4 border-b border-slate-100 pb-4 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-purple-50 text-purple-600">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900">2. Bảng điểm học kỳ</h2>
              <p className="text-xs text-slate-500">
                Nhập danh sách học kỳ và môn học. Thang điểm sẽ tính theo đúng thang điểm trường bạn áp dụng.
              </p>
            </div>
          </div>

          {/* Widget Xem Trước GPA theo đúng thang điểm của trường */}
          <div className="flex items-center gap-3 rounded-xl bg-slate-50 px-4 py-2 border border-slate-200">
            <span className="text-xs font-medium text-slate-500">GPA Trung bình:</span>
            <span className="text-base font-extrabold text-blue-600">{overallGpaPreview.toFixed(2)}</span>
            <span className="text-xs text-slate-400">({GRADE_SCALES.find((s) => s.id === gradeScale)?.name})</span>
          </div>
        </div>

        {/* BỘ CHỌN THANG ĐIỂM */}
        <div className="mt-5">
          <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
            Chọn thang điểm áp dụng <span className="text-red-500 font-bold">*</span>
            <span className="ml-2 font-normal text-slate-400">
              (Bấm chuyển thang điểm để tự động quy đổi toàn bộ điểm các môn sang thang mới)
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

        {/* DANH SÁCH CÁC HỌC KỲ (LINH HOẠT TẬN 9, 10, 12 KỲ) */}
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
                      placeholder="Ví dụ: Lớp 10 - Học kỳ 1, Đại học - Kỳ 5..."
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

        {/* Nút Thêm Học Kỳ (Không giới hạn tối đa 6 học kỳ) */}
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
              + Thêm học kỳ mới
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
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138z" />
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
                    placeholder="Tên chứng chỉ (vd: AP Calculus, IB Math)"
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

      {/* THÔNG BÁO LỖI HOẶC THÀNH CÔNG (HIỂN THỊ Ở CUỐI GẦN NÚT BẤM ĐỂ DỄ ĐỌC) */}
      {(errorMessage || successMessage) && (
        <div id="form-feedback-section" className="space-y-3">
          {errorMessage && (
            <div className="rounded-2xl border-2 border-red-200 bg-red-50 p-4 text-xs font-semibold text-red-900 shadow-sm flex items-start justify-between gap-3 animate-in fade-in">
              <div className="flex items-start gap-2.5">
                <svg className="h-5 w-5 shrink-0 text-red-600 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <div>
                  <div className="font-bold text-sm text-red-900">Vui lòng kiểm tra lại thông tin:</div>
                  <p className="mt-1 font-normal text-red-800 leading-relaxed">{errorMessage}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setErrorMessage(null)}
                className="text-red-400 hover:text-red-700 p-1 rounded-lg"
                title="Đóng thông báo"
              >
                ✕
              </button>
            </div>
          )}

          {successMessage && (
            <div className="rounded-2xl border-2 border-emerald-200 bg-emerald-50 p-4 text-xs font-semibold text-emerald-900 shadow-sm flex items-start justify-between gap-3 animate-in fade-in">
              <div className="flex items-start gap-2.5">
                <svg className="h-5 w-5 shrink-0 text-emerald-600 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                <div>
                  <div className="font-bold text-sm text-emerald-900">Lưu thành công!</div>
                  <p className="mt-1 font-normal text-emerald-800 leading-relaxed">{successMessage}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSuccessMessage(null)}
                className="text-emerald-400 hover:text-emerald-700 p-1 rounded-lg"
                title="Đóng thông báo"
              >
                ✕
              </button>
            </div>
          )}
        </div>
      )}

      {/* HÀNH ĐỘNG SUBMIT VÀ NÚT PHÂN TÍCH Ở CUỐI TRANG */}
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
              {isSaving ? "Đang xử lý..." : "Lưu & Phân tích năng lực 🚀"}
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
              onClick={() => router.push("/profile/academic/analysis")}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-6 py-3 text-sm font-bold text-white shadow-md transition hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <span>🚀</span> Phân tích năng lực học thuật
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
