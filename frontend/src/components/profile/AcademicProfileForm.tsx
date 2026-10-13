"use client";

import React, { useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import {
  CheckIcon,
  CircleAlertIcon,
  LockIcon,
  PencilIcon,
  PlusIcon,
  RocketIcon,
  SaveIcon,
  Trash2Icon,
  XIcon,
  ZapIcon,
} from "lucide-react";
import { profileApi } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Alert, AlertAction, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";
import { Toggle } from "@/components/ui/toggle";
import type {
  AcademicProfileResponse,
  GradeScaleType,
  SaveAcademicProfileRequest,
  TranscriptTerm,
  TranscriptScoreItem,
} from "@/types/api";

const VIEW_MODE_INPUT_CLASS = "disabled:opacity-100";
const SELECT_WRAPPER_CLASS = "w-full has-[select:disabled]:opacity-100";

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
      <Card>
        <CardContent className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-h3">
                {!isEditing ? "Chế độ xem hồ sơ (Đã lưu trữ)" : "Chế độ chỉnh sửa hồ sơ"}
              </h2>
              <Badge variant={!isEditing ? "ok" : "warn"}>
                {!isEditing ? <LockIcon aria-hidden /> : <PencilIcon aria-hidden />}
                {!isEditing ? "Đã khóa chỉnh sửa" : "Đang chỉnh sửa"}
              </Badge>
            </div>
            <p className="text-body-s">
              {!isEditing
                ? "Dữ liệu được bảo vệ an toàn. Bấm nút 'Chỉnh sửa hồ sơ' nếu bạn muốn cập nhật lại điểm."
                : "Điền các thông tin bắt buộc (*). Các nút Lưu và Hủy nằm ở cuối trang sau khi bạn điền xong."}
            </p>
          </div>

          <div>
            {!isEditing && (
              <Button type="button" size="lg" onClick={() => setIsEditing(true)}>
                <PencilIcon />
                Chỉnh sửa hồ sơ
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* KHỐI 1: THÔNG TIN HỌC VẤN CƠ BẢN */}
      <Card>
        <CardHeader>
          <CardTitle>1. Thông tin học vấn cơ bản</CardTitle>
          <CardDescription>
            {isGraduate
              ? "Bậc Thạc sĩ / Tiến sĩ: Áp dụng bảng điểm bậc Đại học và chứng chỉ sau đại học (GRE, GMAT)"
              : targetLevel === "middle_school"
                ? "Bậc THCS / Cấp 2: Áp dụng bảng điểm các lớp THCS (Lớp 6, 7, 8, 9)"
                : "Bậc Cử nhân / THPT: Áp dụng bảng điểm các lớp THPT (Lớp 10, 11, 12)"}
          </CardDescription>
        </CardHeader>

        <CardContent className="grid grid-cols-1 gap-5 md:grid-cols-2">
          {/* Bậc muốn học tại Mỹ */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-target-level">
              Bậc muốn học tại Mỹ <span className="text-destructive">*</span>
            </Label>
            <NativeSelect
              id="academic-target-level"
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
              className={SELECT_WRAPPER_CLASS}
            >
              {TARGET_LEVELS.map((lvl) => (
                <NativeSelectOption key={lvl.value} value={lvl.value}>
                  {lvl.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>

          {/* Ngành muốn học */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-intended-major">
              Ngành muốn học (Intended Major)
            </Label>
            <Input
              id="academic-intended-major"
              type="text"
              disabled={!isEditing}
              value={intendedMajor}
              onChange={(e) => setIntendedMajor(e.target.value)}
              placeholder={isGraduate ? "Ví dụ: Khoa học Dữ liệu, MBA, Tài chính..." : "Ví dụ: Khoa học Máy tính, Kinh doanh, Tâm lý học..."}
              className={VIEW_MODE_INPUT_CLASS}
            />
          </div>

          {/* Trường hiện tại */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-current-school">
              {isGraduate ? "Trường Đại học tốt nghiệp / đang học" : "Trường học hiện tại"}{" "}
              <span className="text-destructive">*</span>
            </Label>
            <Input
              id="academic-current-school"
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
              className={VIEW_MODE_INPUT_CLASS}
            />
          </div>

          {/* Hệ chương trình */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-education-system">
              Hệ chương trình đào tạo
            </Label>
            <NativeSelect
              id="academic-education-system"
              disabled={!isEditing}
              value={educationSystem}
              onChange={(e) => setEducationSystem(e.target.value)}
              className={SELECT_WRAPPER_CLASS}
            >
              {EDUCATION_SYSTEMS.map((sys) => (
                <NativeSelectOption key={sys.value} value={sys.value}>
                  {sys.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>

          {/* Khối / Lớp / Năm học hiện tại */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-current-grade">
              {isGraduate ? "Năm học đại học hiện tại" : "Khối / Lớp hiện tại"}{" "}
              <span className="text-destructive">*</span>
            </Label>
            <Input
              id="academic-current-grade"
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
              className={VIEW_MODE_INPUT_CLASS}
            />
          </div>

          {/* Năm tốt nghiệp dự kiến */}
          <div className="space-y-1.5">
            <Label htmlFor="academic-graduation-year">
              {isGraduate ? "Năm tốt nghiệp Đại học (hoặc dự kiến)" : "Năm tốt nghiệp dự kiến"}{" "}
              <span className="text-destructive">*</span>
            </Label>
            <Input
              id="academic-graduation-year"
              type="number"
              disabled={!isEditing}
              value={graduationYear}
              onChange={(e) => setGraduationYear(e.target.value)}
              placeholder="Ví dụ: 2026, 2027..."
              min={2015}
              max={2040}
              className={VIEW_MODE_INPUT_CLASS}
            />
          </div>
        </CardContent>
      </Card>

      {/* KHỐI 2: THANG ĐIỂM & BẢNG ĐIỂM HỌC KỲ (KHÔNG ÉP THANG 4, KHÔNG GIỚI HẠN 6 KỲ) */}
      <Card>
        <CardHeader>
          <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
            <div>
              <CardTitle>2. Bảng điểm học kỳ</CardTitle>
              <CardDescription>
                Nhập danh sách học kỳ và môn học. Thang điểm sẽ tính theo đúng thang điểm trường bạn áp dụng.
              </CardDescription>
            </div>

            {/* Widget Xem Trước GPA theo đúng thang điểm của trường */}
            <div className="flex items-center gap-3">
              <span className="text-label">GPA Trung bình:</span>
              <span className="text-numeric">{overallGpaPreview.toFixed(2)}</span>
              <span className="text-body-s">({GRADE_SCALES.find((s) => s.id === gradeScale)?.name})</span>
            </div>
          </div>
        </CardHeader>

        <CardContent>
          {/* BỘ CHỌN THANG ĐIỂM */}
          <div>
            <Label>
              Chọn thang điểm áp dụng <span className="text-destructive">*</span>
              <span className="text-muted-foreground">
                (Bấm chuyển thang điểm để tự động quy đổi toàn bộ điểm các môn sang thang mới)
              </span>
            </Label>
            <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
              {GRADE_SCALES.map((scale) => {
                const selected = gradeScale === scale.id;
                return (
                  <Button
                    key={scale.id}
                    type="button"
                    variant={selected ? "default" : "outline"}
                    disabled={!isEditing}
                    aria-pressed={selected}
                    onClick={() => handleGradeScaleChange(scale.id)}
                    className={cn(
                      "h-auto flex-col items-stretch py-2 text-left whitespace-normal",
                      selected && !isEditing && "disabled:opacity-100"
                    )}
                  >
                    <div className="flex items-center justify-between">
                      <span>{scale.name}</span>
                      {selected && (
                        <span className="flex items-center gap-1">
                          <CheckIcon aria-hidden />
                          Đang chọn
                        </span>
                      )}
                    </div>
                    <div className="font-normal">{scale.desc}</div>
                  </Button>
                );
              })}
            </div>
          </div>

          {/* DANH SÁCH CÁC HỌC KỲ (LINH HOẠT TẬN 9, 10, 12 KỲ) */}
          <div className="mt-6 space-y-6">
            {terms.map((term, tIdx) => {
              const currentTermGpa = termGpaList[tIdx]?.gpa ?? 0;
              return (
                <Card key={tIdx} size="sm">
                  {/* Header Học Kỳ */}
                  <CardHeader>
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                      <div className="flex items-center gap-2">
                        <Badge>{term.termOrder}</Badge>
                        <Input
                          type="text"
                          disabled={!isEditing}
                          value={term.termName}
                          onChange={(e) => handleTermNameChange(tIdx, e.target.value)}
                          placeholder="Ví dụ: Lớp 10 - Học kỳ 1, Đại học - Kỳ 5..."
                          className={cn("w-auto", VIEW_MODE_INPUT_CLASS)}
                        />
                      </div>

                      <div className="flex items-center gap-3">
                        <span className="text-body-s">
                          GPA kỳ: <strong className="text-numeric">{currentTermGpa.toFixed(2)}</strong>
                        </span>

                        {isEditing && terms.length > 1 && (
                          <Button
                            type="button"
                            variant="destructive"
                            size="xs"
                            onClick={() => handleRemoveTerm(tIdx)}
                          >
                            Xóa kỳ này
                          </Button>
                        )}
                      </div>
                    </div>
                  </CardHeader>

                  <CardContent>
                    {/* Bảng điểm các môn */}
                    <div className="space-y-2">
                      <div className="grid grid-cols-12 gap-2 text-label">
                        <div className="col-span-6 sm:col-span-5">Tên môn học *</div>
                        <div className="col-span-4 sm:col-span-3">Điểm số *</div>
                        <div className="hidden sm:col-span-3 sm:block">Số tín chỉ / Trọng số</div>
                        <div className="col-span-2 text-center sm:col-span-1">Thao tác</div>
                      </div>

                      {term.scores.map((scoreItem, sIdx) => {
                        const subKey = `${tIdx}-${sIdx}`;
                        const hasSubError = validationErrors[subKey];
                        const hasScoreError = validationErrors[`${subKey}-score`];

                        return (
                          <div key={sIdx} className="space-y-1">
                            <div className="grid grid-cols-12 items-center gap-2">
                              {/* Tên môn học */}
                              <div className="col-span-6 sm:col-span-5">
                                <Input
                                  type="text"
                                  disabled={!isEditing}
                                  value={scoreItem.subject}
                                  onChange={(e) => handleSubjectChange(tIdx, sIdx, "subject", e.target.value)}
                                  placeholder="Nhập tên môn học..."
                                  aria-invalid={isEditing && Boolean(hasSubError)}
                                  className={VIEW_MODE_INPUT_CLASS}
                                />
                              </div>

                              {/* Điểm số */}
                              <div className="col-span-4 sm:col-span-3">
                                {gradeScale === "letter" ? (
                                  <NativeSelect
                                    disabled={!isEditing}
                                    value={scoreItem.rawScore || "A"}
                                    onChange={(e) => handleSubjectChange(tIdx, sIdx, "rawScore", e.target.value)}
                                    aria-invalid={isEditing && Boolean(hasScoreError)}
                                    className={SELECT_WRAPPER_CLASS}
                                  >
                                    {LETTER_OPTIONS.map((opt) => (
                                      <NativeSelectOption key={opt.value} value={opt.value}>
                                        {opt.label}
                                      </NativeSelectOption>
                                    ))}
                                  </NativeSelect>
                                ) : (
                                  <Input
                                    type="number"
                                    step={gradeScale === "100" ? "1" : "0.1"}
                                    disabled={!isEditing}
                                    value={scoreItem.rawScore ?? scoreItem.score}
                                    onChange={(e) => handleSubjectChange(tIdx, sIdx, "score", e.target.value)}
                                    placeholder="Điểm..."
                                    aria-invalid={isEditing && Boolean(hasScoreError)}
                                    className={VIEW_MODE_INPUT_CLASS}
                                  />
                                )}
                              </div>

                              {/* Số tín chỉ / Trọng số */}
                              <div className="hidden sm:col-span-3 sm:block">
                                <Input
                                  type="number"
                                  disabled={!isEditing}
                                  step="0.5"
                                  min="0"
                                  value={scoreItem.credits ?? ""}
                                  onChange={(e) => handleSubjectChange(tIdx, sIdx, "credits", e.target.value)}
                                  placeholder={isGraduate ? "Ví dụ: 3 (tín chỉ)" : "Không bắt buộc"}
                                  className={VIEW_MODE_INPUT_CLASS}
                                />
                              </div>

                              {/* Xóa dòng */}
                              <div className="col-span-2 text-center sm:col-span-1">
                                {isEditing && term.scores.length > 1 ? (
                                  <Button
                                    type="button"
                                    variant="ghost"
                                    size="icon-sm"
                                    onClick={() => handleRemoveSubject(tIdx, sIdx)}
                                    title="Xóa môn học này"
                                    aria-label="Xóa môn học này"
                                  >
                                    <Trash2Icon />
                                  </Button>
                                ) : (
                                  <span className="text-muted-foreground">-</span>
                                )}
                              </div>
                            </div>

                            {/* Báo lỗi validation dòng */}
                            {(hasSubError || hasScoreError) && (
                              <div className="text-destructive">
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
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => handleAddSubject(tIdx)}
                        >
                          <PlusIcon />
                          Thêm môn học vào {term.termName}
                        </Button>
                      </div>
                    )}
                  </CardContent>
                </Card>
              );
            })}
          </div>

          {/* Nút Thêm Học Kỳ (Không giới hạn tối đa 6 học kỳ) */}
          {isEditing && (
            <div className="mt-4">
              <Button
                type="button"
                variant="secondary"
                size="lg"
                onClick={handleAddTerm}
              >
                <PlusIcon />
                + Thêm học kỳ mới
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* KHỐI 3: CHỨNG CHỈ NGOẠI NGỮ & BÀI THI CHUẨN HÓA (ĐỘNG TỪ CSDL) */}
      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <CardTitle>3. Chứng chỉ Ngoại ngữ & Bài thi chuẩn hóa</CardTitle>
              <CardDescription>
                Lựa chọn các chứng chỉ bạn đang có trong CSDL (Có thể để trống nếu chưa thi)
              </CardDescription>
            </div>

            {/* BẬT / TẮT HOẶC THÊM CHỨNG CHỈ */}
            {isEditing && (
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="text-label">Thêm chứng chỉ:</span>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.ielts}
                  onPressedChange={() => handleToggleTest("ielts")}
                >
                  + IELTS
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.toefl}
                  onPressedChange={() => handleToggleTest("toefl")}
                >
                  + TOEFL
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.duolingo}
                  onPressedChange={() => handleToggleTest("duolingo")}
                >
                  + Duolingo
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.sat}
                  onPressedChange={() => handleToggleTest("sat")}
                >
                  + SAT
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.act}
                  onPressedChange={() => handleToggleTest("act")}
                >
                  + ACT
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.gre}
                  onPressedChange={() => handleToggleTest("gre")}
                >
                  + GRE
                </Toggle>
                <Toggle
                  variant="outline"
                  size="sm"
                  pressed={enabledTests.gmat}
                  onPressedChange={() => handleToggleTest("gmat")}
                >
                  + GMAT
                </Toggle>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleAddOtherTest}
                >
                  + Khác (AP/IB/PTE)
                </Button>
              </div>
            )}
          </div>
        </CardHeader>

        <CardContent>
          {/* CÁC Ô NHẬP ĐIỂM CHỨNG CHỈ */}
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
            {/* IELTS */}
            {enabledTests.ielts && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-ielts">IELTS (0.0 - 9.0)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("ielts")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-ielts"
                    type="number"
                    disabled={!isEditing}
                    step="0.5"
                    min="0"
                    max="9"
                    value={ielts}
                    onChange={(e) => setIelts(e.target.value)}
                    placeholder="Ví dụ: 7.0 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* TOEFL */}
            {enabledTests.toefl && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-toefl">TOEFL iBT (0 - 120)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("toefl")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-toefl"
                    type="number"
                    disabled={!isEditing}
                    min="0"
                    max="120"
                    value={toefl}
                    onChange={(e) => setToefl(e.target.value)}
                    placeholder="Ví dụ: 95 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* Duolingo */}
            {enabledTests.duolingo && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-duolingo">Duolingo Test (10 - 160)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("duolingo")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-duolingo"
                    type="number"
                    disabled={!isEditing}
                    min="10"
                    max="160"
                    value={duolingo}
                    onChange={(e) => setDuolingo(e.target.value)}
                    placeholder="Ví dụ: 125 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* SAT */}
            {enabledTests.sat && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-sat">SAT (400 - 1600)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("sat")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-sat"
                    type="number"
                    disabled={!isEditing}
                    min="400"
                    max="1600"
                    value={sat}
                    onChange={(e) => setSat(e.target.value)}
                    placeholder="Ví dụ: 1450 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* ACT */}
            {enabledTests.act && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-act">ACT (1 - 36)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("act")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-act"
                    type="number"
                    disabled={!isEditing}
                    min="1"
                    max="36"
                    value={act}
                    onChange={(e) => setAct(e.target.value)}
                    placeholder="Ví dụ: 32 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* GRE (Sau đại học) */}
            {enabledTests.gre && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-gre">GRE General (260 - 340)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("gre")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-gre"
                    type="number"
                    disabled={!isEditing}
                    min="260"
                    max="340"
                    value={gre}
                    onChange={(e) => setGre(e.target.value)}
                    placeholder="Ví dụ: 322 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}

            {/* GMAT (Sau đại học) */}
            {enabledTests.gmat && (
              <Card size="sm">
                <CardContent className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="academic-test-gmat">GMAT (200 - 800)</Label>
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => handleToggleTest("gmat")}
                      >
                        <XIcon aria-hidden />
                        Ẩn
                      </Button>
                    )}
                  </div>
                  <Input
                    id="academic-test-gmat"
                    type="number"
                    disabled={!isEditing}
                    min="200"
                    max="800"
                    value={gmat}
                    onChange={(e) => setGmat(e.target.value)}
                    placeholder="Ví dụ: 685 hoặc để trống"
                    className={VIEW_MODE_INPUT_CLASS}
                  />
                </CardContent>
              </Card>
            )}
          </div>

          {/* DANH SÁCH CHỨNG CHỈ TÙY CHỌN KHÁC (AP, IB, PTE...) */}
          {otherTests.length > 0 && (
            <div className="mt-5">
              <Separator className="mb-4" />
              <span className="mb-2 block text-label">
                Chứng chỉ khác (AP, IB, PTE Academic...):
              </span>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {otherTests.map((t) => (
                  <div key={t.id} className="flex items-center gap-2">
                    <Input
                      type="text"
                      disabled={!isEditing}
                      value={t.name}
                      onChange={(e) => handleUpdateOtherTest(t.id, "name", e.target.value)}
                      placeholder="Tên chứng chỉ (vd: AP Calculus, IB Math)"
                      className={cn("w-1/2", VIEW_MODE_INPUT_CLASS)}
                    />
                    <Input
                      type="text"
                      disabled={!isEditing}
                      value={t.score}
                      onChange={(e) => handleUpdateOtherTest(t.id, "score", e.target.value)}
                      placeholder="Điểm (vd: 5, 42)"
                      className={cn("w-1/3", VIEW_MODE_INPUT_CLASS)}
                    />
                    {isEditing && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-xs"
                        onClick={() => handleRemoveOtherTest(t.id)}
                        aria-label="Xóa chứng chỉ"
                      >
                        <XIcon />
                      </Button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* THÔNG BÁO LỖI HOẶC THÀNH CÔNG (HIỂN THỊ Ở CUỐI GẦN NÚT BẤM ĐỂ DỄ ĐỌC) */}
      {(errorMessage || successMessage) && (
        <div id="form-feedback-section" className="space-y-3">
          {errorMessage && (
            <Alert variant="destructive">
              <CircleAlertIcon />
              <AlertTitle>Vui lòng kiểm tra lại thông tin:</AlertTitle>
              <AlertDescription>{errorMessage}</AlertDescription>
              <AlertAction>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setErrorMessage(null)}
                  title="Đóng thông báo"
                  aria-label="Đóng thông báo"
                >
                  <XIcon />
                </Button>
              </AlertAction>
            </Alert>
          )}

          {successMessage && (
            <Alert variant="success">
              <CheckIcon />
              <AlertTitle>Lưu thành công!</AlertTitle>
              <AlertDescription>{successMessage}</AlertDescription>
              <AlertAction>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setSuccessMessage(null)}
                  title="Đóng thông báo"
                  aria-label="Đóng thông báo"
                >
                  <XIcon />
                </Button>
              </AlertAction>
            </Alert>
          )}
        </div>
      )}

      {/* HÀNH ĐỘNG SUBMIT VÀ NÚT PHÂN TÍCH Ở CUỐI TRANG */}
      <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:items-center sm:justify-end">
        {isEditing ? (
          <>
            {hasSavedProfile && (
              <Button
                type="button"
                variant="outline"
                size="lg"
                onClick={handleCancelEdit}
                disabled={isSaving}
              >
                Hủy thay đổi
              </Button>
            )}

            <Button
              type="button"
              variant="outline"
              size="lg"
              onClick={() => handleSave(false)}
              disabled={isSaving}
            >
              {isSaving ? <Spinner /> : <SaveIcon aria-hidden />}
              {isSaving ? "Đang lưu..." : "Lưu hồ sơ"}
            </Button>

            <Button
              type="button"
              size="lg"
              onClick={() => handleSave(true)}
              disabled={isSaving}
            >
              {isSaving ? <Spinner /> : <ZapIcon />}
              {isSaving ? "Đang xử lý..." : "Lưu & Phân tích năng lực"}
            </Button>
          </>
        ) : (
          <div className="flex items-center gap-3">
            <Button
              type="button"
              variant="outline"
              size="lg"
              onClick={() => setIsEditing(true)}
            >
              <PencilIcon />
              Chỉnh sửa lại hồ sơ
            </Button>

            <Button
              type="button"
              size="lg"
              onClick={() => router.push("/profile/academic/analysis")}
            >
              <RocketIcon aria-hidden />
              Phân tích năng lực học thuật
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
