"use client";

import { useId, useState, useRef, useEffect } from "react";
import {
  ArrowRightIcon,
  CheckIcon,
  ChevronDownIcon,
  MapPinIcon,
  SearchIcon,
  XIcon,
  AlertCircleIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RequiredMark } from "./RequiredMark";
import { cn } from "@/lib/utils";
import { normalizeSearch } from "@/lib/certification-utils";
import {
  EDUCATION_LEVEL_OPTIONS,
  EDUCATION_SYSTEM_OPTIONS,
  SCHOOL_TYPE_OPTIONS,
  getGradeOptionsForLevel,
} from "@/lib/profile-flow-options";
import type { CurrentEducationLevel, EducationSystem, GoalDraft, ProfileMetadataResponse } from "@/types/profile-flow";
import { FORM_GOAL_OPTIONS, goalsNeedMajor, type FormGoal } from "@/lib/form-goals";

interface Step1GoalProps {
  data: GoalDraft;
  metadata: ProfileMetadataResponse | null;
  onUpdate: (updated: Partial<GoalDraft>) => void;
  onNext: () => void;
  /** Mục tiêu đã tích. Mở từ chat thì lấy theo câu hỏi trong chat và khóa lại */
  goals: FormGoal[];
  goalsLocked: boolean;
  /** Mở từ chat mà chat không gửi mục tiêu: ẩn ô tích, chat tự biết câu đang chờ */
  showGoals: boolean;
  onGoalsChange: (goals: FormGoal[]) => void;
}

// Năm tốt nghiệp dự kiến: năm nay → 6 năm tới
const THIS_YEAR = new Date().getFullYear();
const GRADUATION_YEAR_OPTIONS = Array.from({ length: 7 }, (_, i) => THIS_YEAR + i);
// Lớp / năm cuối của từng cấp học, để tính còn mấy năm nữa tốt nghiệp
const LAST_GRADE: Partial<Record<CurrentEducationLevel, number>> = {
  middle_school: 9,
  high_school: 12,
  college_university: 4,
  master: 2,
};

// Năm tốt nghiệp dự kiến theo lớp đang học. Năm học Việt Nam kết thúc tháng 6, từ tháng 8 tính là năm học mới.
// "Đã tốt nghiệp" hay lớp không đọc được thì trả null (không tự điền)
function guessGraduationYear(level: CurrentEducationLevel | null | undefined, grade: string | null | undefined) {
  const last = level ? LAST_GRADE[level] : undefined;
  if (!last || !grade) return null;
  const now = new Date();
  const schoolYearEnd = now.getMonth() >= 7 ? now.getFullYear() + 1 : now.getFullYear();
  if (grade === "Năm cuối") return schoolYearEnd;
  const n = Number(/^(?:Lớp|Năm) (\d+)$/.exec(grade)?.[1]);
  return n ? schoolYearEnd + Math.max(0, last - n) : null;
}
// Kỳ nhập học: chọn kỳ rồi chọn năm, lưu ghép "Fall 2027" — khớp IntakeRegex phía BE và câu B1.4 bên chat
const INTAKE_TERM_OPTIONS = [
  { value: "Fall", label: "Kỳ mùa thu (Fall)" },
  { value: "Spring", label: "Kỳ mùa xuân (Spring)" },
  { value: "Summer", label: "Kỳ mùa hè (Summer)" },
];
// Năm nhập học: từ năm tốt nghiệp dự kiến (chưa có thì từ năm nay) tới 5 năm sau
function intakeYearOptions(graduationYear?: number | null): string[] {
  const from = Math.max(THIS_YEAR, graduationYear ?? THIS_YEAR);
  return Array.from({ length: 6 }, (_, i) => String(from + i));
}
// 4 vùng của Cục Thống kê Mỹ, khớp cột region của bảng us_states
const STATE_REGIONS = [
  { value: "northeast", label: "Đông Bắc" },
  { value: "midwest", label: "Trung Tây" },
  { value: "south", label: "Miền Nam" },
  { value: "west", label: "Miền Tây" },
];

function parseIntake(intake?: string | null): { term: string; year: string } | null {
  const m = /^(Fall|Spring|Summer) (20\d\d)$/.exec(intake ?? "");
  return m ? { term: m[1], year: m[2] } : null;
}

export function Step1Goal({
  data,
  metadata,
  onUpdate,
  onNext,
  goals,
  goalsLocked,
  showGoals,
  onGoalsChange,
}: Step1GoalProps) {
  // Chữ đang gõ để tìm ngành; null = không gõ, ô hiện tên Việt của ngành đã chọn
  const [majorQuery, setMajorQuery] = useState<string | null>(null);
  const [showMajorDropdown, setShowMajorDropdown] = useState(false);
  const [majorError, setMajorError] = useState<string | null>(null);
  // Học sinh chọn "Chưa rõ" (ở form hoặc chat): ngành lưu null kèm cờ majorUndecided, vẫn cho qua bước này
  const majorUnknown = !data.targetMajor && data.majorUndecided === true;
  const [levelError, setLevelError] = useState<string | null>(null);
  const [goalError, setGoalError] = useState<string | null>(null);
  const goalSectionId = useId();
  // Lộ trình, gợi ý trường lọc theo ngành nên không cho "Chưa chọn ngành"
  const needMajor = goalsNeedMajor(goals);
  const toggleGoal = (goal: FormGoal) => {
    if (goalsLocked) return;
    onGoalsChange(goals.includes(goal) ? goals.filter((g) => g !== goal) : [...goals, goal]);
    setGoalError(null);
  };
  const [degreeError, setDegreeError] = useState<string | null>(null);
  // Mới chọn kỳ hoặc năm thì giữ tạm ở đây; đủ cả hai mới ghi vào nháp
  const [intakePending, setIntakePending] = useState({ term: "", year: "" });
  const savedIntake = parseIntake(data.targetIntake);
  const intakeTerm = savedIntake?.term ?? intakePending.term;
  const intakeYear = savedIntake?.year ?? intakePending.year;
  const updateIntake = (term: string, year: string) => {
    setIntakePending({ term, year });
    onUpdate({ targetIntake: term && year ? `${term} ${year}` : null });
  };

  const [stateSearch, setStateSearch] = useState("");
  // Khối "Thu hẹp gợi ý" thu gọn sẵn; đã chọn bang hoặc loại trường thì mở để thấy lựa chọn cũ
  const [showNarrow, setShowNarrow] = useState(
    (data.targetStates?.length ?? 0) > 0 || (data.targetSchoolType ?? "all") !== "all"
  );
  const [showStateDropdown, setShowStateDropdown] = useState(false);

  const majorInputId = useId();
  const degreeSectionId = useId();
  const majorDropdownRef = useRef<HTMLDivElement>(null);
  const stateDropdownRef = useRef<HTMLDivElement>(null);

  // Close dropdowns when clicking outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        majorDropdownRef.current &&
        !majorDropdownRef.current.contains(event.target as Node)
      ) {
        setShowMajorDropdown(false);
        setMajorQuery(null);
      }
      if (
        stateDropdownRef.current &&
        !stateDropdownRef.current.contains(event.target as Node)
      ) {
        setShowStateDropdown(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Sync internal state when external data changes
  const [prevTargetMajor, setPrevTargetMajor] = useState(data.targetMajor);
  if (data.targetMajor !== prevTargetMajor) {
    setPrevTargetMajor(data.targetMajor);
    setMajorQuery(null);
  }

  // Grade options based on current level
  const gradeOptions = getGradeOptionsForLevel(data.currentEducationLevel);
  const yearOptions = intakeYearOptions(data.expectedGraduationYear);
  const isHighSchool = data.currentEducationLevel === "high_school";

  const handleSelectEducationLevel = (level: CurrentEducationLevel) => {
    // Giữ lớp đã chọn nếu vẫn hợp lệ với cấp học mới, không thì để học sinh chọn lại
    const stillValid = !!data.currentGrade && getGradeOptionsForLevel(level).includes(data.currentGrade);
    const graduationYear = stillValid ? guessGraduationYear(level, data.currentGrade) : null;
    onUpdate({
      currentEducationLevel: level,
      currentGrade: stillValid ? data.currentGrade : null,
      // "Năm 1" đại học và thạc sĩ ra năm tốt nghiệp khác nhau, nên tính lại
      ...(graduationYear && { expectedGraduationYear: graduationYear }),
      // Hệ chương trình chỉ có nghĩa với cấp 3 (BE cũng bỏ khi lưu)
      educationSystem: level === "high_school" ? data.educationSystem : null,
    });
    setLevelError(null);
  };

  // 51 States from metadata
  const allStates = metadata?.states ?? [];
  const popularStates = allStates.filter((s) => s.isPopular);
  const selectedStates = data.targetStates ?? [];
  // Bang khớp chữ đang tìm (không dấu, theo tên hoặc mã), chia theo vùng; bang chưa có vùng xếp vào "Khác"
  const stateQuery = normalizeSearch(stateSearch);
  const foundStates = allStates.filter(
    (s) => normalizeSearch(s.name).includes(stateQuery) || normalizeSearch(s.code).includes(stateQuery)
  );
  const stateGroups = [...STATE_REGIONS, { value: "", label: "Khác" }]
    .map((r) => ({
      ...r,
      states: foundStates.filter((s) =>
        r.value ? s.region === r.value : !STATE_REGIONS.some((x) => x.value === s.region)
      ),
    }))
    .filter((g) => g.states.length > 0);

  const toggleState = (code: string) => {
    const next = selectedStates.includes(code)
      ? selectedStates.filter((c) => c !== code)
      : [...selectedStates, code];
    onUpdate({ targetStates: next });
  };

  const removeState = (code: string) => {
    onUpdate({ targetStates: selectedStates.filter((c) => c !== code) });
  };

  // Ngành trong bảng majors: hiện label (tiếng Việt), lưu value (tiếng Anh) để lọc trường khớp
  const allMajors = metadata?.majors ?? [];
  const popularMajors = allMajors.filter((m) => m.isPopular);
  const selectedMajor = data.targetMajor
    ? allMajors.find((m) => m.value.toLowerCase() === data.targetMajor!.toLowerCase())
    : undefined;
  const selectedMajorLabel = selectedMajor?.label ?? data.targetMajor ?? "";
  // Hồ sơ cũ có thể lưu ngành không còn trong danh sách (vd "Computer Science")
  const majorNotInList = !!data.targetMajor && allMajors.length > 0 && !selectedMajor;
  // Tìm không dấu theo tên Việt, tên Anh và tên gọi khác ("may tinh", "cntt")
  const majorSearch = normalizeSearch(majorQuery ?? "");
  const filteredMajors = majorSearch
    ? allMajors.filter((m) => [m.label, m.value, ...m.aliases].some((s) => normalizeSearch(s).includes(majorSearch)))
    : allMajors;

  const handleSelectMajor = (major: string) => {
    setMajorQuery(null);
    onUpdate({ targetMajor: major, majorUndecided: false });
    setMajorError(null);
    setShowMajorDropdown(false);
  };

  const handleMajorUnknown = () => {
    setMajorQuery(null);
    onUpdate({ targetMajor: null, majorUndecided: true });
    setMajorError(null);
    setShowMajorDropdown(false);
  };

  const handleNextClick = () => {
    if (showGoals && goals.length === 0) {
      setGoalError("Vui lòng tích ít nhất một việc bạn muốn hệ thống làm.");
      document.getElementById(goalSectionId)?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    setGoalError(null);
    if (!data.currentEducationLevel) {
      setLevelError("Vui lòng chọn cấp học hiện tại.");
      if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }
    setLevelError(null);
    if (!data.targetDegreeLevel) {
      setDegreeError("Vui lòng chọn bậc học muốn theo học ở Mỹ.");
      document.getElementById(degreeSectionId)?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    setDegreeError(null);
    const noMajor = !data.targetMajor || !data.targetMajor.trim();
    if (majorNotInList || (noMajor && (needMajor || !majorUnknown))) {
      setMajorError(
        majorNotInList
          ? "Ngành đã lưu không còn trong danh sách, vui lòng chọn lại."
          : needMajor
            ? "Lộ trình và gợi ý trường cần biết ngành, vui lòng chọn một ngành trong danh sách."
            : "Vui lòng chọn một ngành trong danh sách, hoặc bấm \"Chưa chọn ngành\" nếu bạn chưa quyết định."
      );
      setShowMajorDropdown(true);
      const el = document.getElementById(majorInputId);
      if (el) el.focus();
      return;
    }
    setMajorError(null);
    onNext();
  };

  const studyLevels = metadata?.studyLevels ?? [
    {
      key: "community_college",
      label: "Cao đẳng",
      description: "Community college · 2 năm",
      durationYears: 2,
      defaultGradingScale: "scale_10",
      allowedGradingScales: [],
    },
    {
      key: "undergraduate",
      label: "Đại học",
      description: "Bachelor · 4 năm",
      durationYears: 4,
      defaultGradingScale: "scale_10",
      allowedGradingScales: [],
    },
    {
      key: "master",
      label: "Thạc sĩ",
      description: "Master · 1–2 năm",
      durationYears: 2,
      defaultGradingScale: "scale_10",
      allowedGradingScales: [],
    },
    {
      key: "phd",
      label: "Tiến sĩ",
      description: "PhD · 4–6 năm",
      durationYears: 5,
      defaultGradingScale: "scale_10",
      allowedGradingScales: [],
    },
  ];

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 1/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">
          Bạn đang ở đâu và muốn đi đến đâu?
        </h1>
        <p className="text-body text-muted-foreground">
          Vài câu hỏi nhanh để chúng tôi gợi ý trường phù hợp nhất với mục tiêu học tập của bạn.
        </p>
      </div>

      {/* Mục tiêu: quyết định ô nào bắt buộc và nút cuối đi đâu */}
      {showGoals && (
        <section id={goalSectionId} className="space-y-4">
          <div>
            <Label className="text-sm font-semibold text-foreground">
              Bạn muốn hệ thống làm gì? <RequiredMark />
            </Label>
            <p className="text-xs text-muted-foreground mt-0.5">
              {goalsLocked
                ? "Theo câu bạn hỏi trong chat. Lưu hồ sơ xong chat sẽ trả lời tiếp."
                : "Tích một hoặc nhiều việc."}
            </p>
          </div>
          <div className="grid gap-2.5 sm:grid-cols-3">
            {FORM_GOAL_OPTIONS.map((opt) => {
              const isSelected = goals.includes(opt.value);
              return (
                <button
                  key={opt.value}
                  type="button"
                  role="checkbox"
                  aria-checked={isSelected}
                  disabled={goalsLocked}
                  onClick={() => toggleGoal(opt.value)}
                  className={cn(
                    "flex items-start gap-2.5 rounded-lg border p-3 text-left transition-all",
                    isSelected ? "border-foreground bg-foreground/5" : "border-line bg-surface",
                    goalsLocked ? "cursor-not-allowed opacity-80" : "cursor-pointer hover:border-foreground/40"
                  )}
                >
                  <span
                    className={cn(
                      "mt-0.5 flex size-4 shrink-0 items-center justify-center rounded border",
                      isSelected ? "border-foreground bg-foreground text-background" : "border-line"
                    )}
                  >
                    {isSelected && <CheckIcon className="size-3" />}
                  </span>
                  <span>
                    <span className="block text-sm font-medium text-foreground">{opt.label}</span>
                    <span className="block text-xs text-muted-foreground">{opt.description}</span>
                  </span>
                </button>
              );
            })}
          </div>
          {goalError && <p className="text-xs font-medium text-destructive">{goalError}</p>}
        </section>
      )}

      {/* Question 1: Current Education */}
      <section className="space-y-4">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Hiện tại bạn đang học ở đâu? <RequiredMark />
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Chọn bậc học hiện tại để hệ thống điều chỉnh lớp học và năm học tương ứng.
          </p>
        </div>

        {/* Level pills with explicit single-select radio dots */}
        <div className="flex flex-wrap gap-2.5">
          {EDUCATION_LEVEL_OPTIONS.map((opt) => {
            const isSelected = data.currentEducationLevel === opt.value;
            return (
              <button
                key={opt.value}
                type="button"
                onClick={() => handleSelectEducationLevel(opt.value)}
                aria-pressed={isSelected}
                className={cn(
                  "flex items-center gap-1.5 rounded-full border px-4 py-2 text-sm font-medium transition-all cursor-pointer",
                  isSelected
                    ? "border-foreground bg-foreground text-background"
                    : "border-line bg-surface text-foreground hover:border-foreground/40"
                )}
              >
                {isSelected && <CheckIcon className="size-3.5" />}
                {opt.label}
              </button>
            );
          })}
        </div>
        {levelError && <p className="text-xs font-medium text-destructive">{levelError}</p>}

        {/* Chọn cấp học xong mới hiện lớp, trường, năm tốt nghiệp; cấp 3 thêm hệ chương trình (giống câu D1 bên chat) */}
        {data.currentEducationLevel && (
          <div className={cn("grid gap-4 pt-2", isHighSchool ? "sm:grid-cols-2" : "sm:grid-cols-3")}>
            {/* Lớp / Năm học (Dropdown động theo bậc học) */}
            <div className="space-y-1.5">
              <Label htmlFor="currentGrade" className="text-xs font-medium text-foreground">
                Lớp / Năm học hiện tại
              </Label>
              <div className="relative">
                <select
                  id="currentGrade"
                  value={data.currentGrade ?? ""}
                  onChange={(e) => {
                    // Chọn lớp thì tự điền năm tốt nghiệp, học sinh vẫn sửa lại được
                    const grade = e.target.value || null;
                    const graduationYear = guessGraduationYear(data.currentEducationLevel, grade);
                    onUpdate({ currentGrade: grade, ...(graduationYear && { expectedGraduationYear: graduationYear }) });
                  }}
                  className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
                >
                  <option value="">Chọn lớp / năm</option>
                  {gradeOptions.map((g) => (
                    <option key={g} value={g}>
                      {g}
                    </option>
                  ))}
                </select>
                <ChevronDownIcon className="absolute right-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
              </div>
            </div>

            {/* Trường đang học */}
            <div className="space-y-1.5">
              <Label htmlFor="currentSchool" className="text-xs font-medium text-foreground">
                Trường đang học
              </Label>
              <Input
                id="currentSchool"
                placeholder="VD: THPT Chuyên Hà Nội – Amsterdam"
                value={data.currentSchool ?? ""}
                onChange={(e) => onUpdate({ currentSchool: e.target.value })}
              />
            </div>

            {/* Năm tốt nghiệp dự kiến: tự điền khi chọn lớp, năm nay → 6 năm tới */}
            <div className="space-y-1.5">
              <Label htmlFor="expectedGraduationYear" className="text-xs font-medium text-foreground">
                Năm tốt nghiệp dự kiến
              </Label>
              <div className="relative">
                <select
                  id="expectedGraduationYear"
                  value={data.expectedGraduationYear ?? ""}
                  onChange={(e) =>
                    onUpdate({
                      expectedGraduationYear: e.target.value
                        ? Number(e.target.value)
                        : null,
                    })
                  }
                  className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
                >
                  <option value="">Chưa xác định</option>
                  {GRADUATION_YEAR_OPTIONS.map((yr) => (
                    <option key={yr} value={yr}>
                      {yr}
                    </option>
                  ))}
                </select>
                <ChevronDownIcon className="absolute right-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
              </div>
            </div>

            {/* Hệ chương trình chỉ có ở cấp 3 */}
            {isHighSchool && (
              <div className="space-y-1.5">
                <Label htmlFor="educationSystem" className="text-xs font-medium text-foreground">
                  Hệ chương trình đang học
                </Label>
                <div className="relative">
                  <select
                    id="educationSystem"
                    value={data.educationSystem ?? ""}
                    onChange={(e) => onUpdate({ educationSystem: (e.target.value || null) as EducationSystem | null })}
                    className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
                  >
                    <option value="">Chưa chọn</option>
                    {EDUCATION_SYSTEM_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDownIcon className="absolute right-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
                </div>
              </div>
            )}
          </div>
        )}
      </section>

      {/* Question 2: Target Degree Level */}
      <section id={degreeSectionId} className="space-y-4 pt-2">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Bạn muốn học bậc nào ở Mỹ? <RequiredMark />
          </Label>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          {studyLevels.map((lvl) => {
            const isSelected = data.targetDegreeLevel === lvl.key;
            return (
              <button
                key={lvl.key}
                type="button"
                onClick={() => {
                  onUpdate({ targetDegreeLevel: lvl.key });
                  setDegreeError(null);
                }}
                className={cn(
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full shadow-2xs",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="flex items-center justify-between w-full">
                  <div className="font-semibold text-sm text-foreground">
                    {lvl.label}
                  </div>
                  <div
                    className={cn(
                      "size-4 rounded-full border flex items-center justify-center transition-all",
                      isSelected
                        ? "border-brand bg-brand"
                        : "border-muted-foreground/40 bg-surface"
                    )}
                  >
                    {isSelected && <div className="size-1.5 rounded-full bg-white" />}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground mt-2">
                  {lvl.description}
                </div>
              </button>
            );
          })}
        </div>
        {degreeError && <p className="text-xs font-medium text-destructive">{degreeError}</p>}
        {data.targetDegreeLevel === "master" || data.targetDegreeLevel === "phd" ? (
          <p className="flex items-center gap-1.5 text-xs font-medium text-warn">
            <AlertCircleIcon className="size-3.5 shrink-0" />
            Bậc này đang làm sau. Bản hiện tại gợi ý trường cho Cao đẳng và Đại học.
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">Chưa chắc? Chọn bậc gần nhất, bạn có thể đổi lại sau.</p>
        )}

        {/* Kỳ nhập học — chọn kỳ rồi chọn năm, giống câu B1.4 bên chat */}
        <div className="space-y-1.5">
          <Label htmlFor="targetIntakeTerm" className="text-xs font-medium text-foreground">
            Kỳ muốn nhập học
          </Label>
          <div className="grid gap-3 sm:max-w-md sm:grid-cols-2">
            <div className="relative">
              <select
                id="targetIntakeTerm"
                aria-label="Kỳ nhập học"
                value={intakeTerm}
                onChange={(e) => updateIntake(e.target.value, intakeYear)}
                className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
              >
                <option value="">Chọn kỳ</option>
                {INTAKE_TERM_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
              <ChevronDownIcon className="absolute right-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
            </div>
            <div className="relative">
              <select
                id="targetIntakeYear"
                aria-label="Năm nhập học"
                value={intakeYear}
                onChange={(e) => updateIntake(intakeTerm, e.target.value)}
                className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
              >
                <option value="">Chọn năm</option>
                {/* Năm đã lưu từ chat có thể nằm ngoài danh sách — vẫn hiện để không mất */}
                {intakeYear && !yearOptions.includes(intakeYear) && (
                  <option value={intakeYear}>{intakeYear}</option>
                )}
                {yearOptions.map((yr) => (
                  <option key={yr} value={yr}>
                    {yr}
                  </option>
                ))}
              </select>
              <ChevronDownIcon className="absolute right-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
            </div>
          </div>
        </div>
      </section>

      {/* Question 3: Ngành học mong muốn (bắt buộc) */}
      <section className="space-y-4 pt-2">
        {/* Ngành học mong muốn (BẮT BUỘC & Searchable Dropdown) */}
        <div ref={majorDropdownRef} className="space-y-1.5 relative">
          <div className="flex items-center justify-between">
            <Label
              htmlFor={majorInputId}
              className="text-xs font-semibold text-foreground flex items-center gap-1"
            >
              Ngành học mong muốn <RequiredMark />
              <span className="text-muted-foreground font-normal">
                ({allMajors.length > 0 ? `${allMajors.length} ngành` : "Đang tải danh sách..."})
              </span>
            </Label>
            {data.targetMajor ? (
              <span className="text-xs text-brand font-medium">
                Đã chọn: <strong className="text-foreground">{selectedMajorLabel}</strong>
              </span>
            ) : majorUnknown ? (
              <span className="text-xs text-brand font-medium">
                Đã chọn: <strong className="text-foreground">Chưa chọn ngành</strong>
              </span>
            ) : needMajor ? null : (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={handleMajorUnknown}
                className="text-xs h-7 cursor-pointer"
              >
                Chưa chọn ngành
              </Button>
            )}
          </div>

          <div className="relative">
            <SearchIcon className="absolute left-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
            <Input
              id={majorInputId}
              placeholder="Gõ để tìm ngành, ví dụ: máy tính, kinh tế, y..."
              value={majorQuery ?? selectedMajorLabel}
              onFocus={() => setShowMajorDropdown(true)}
              onChange={(e) => {
                setMajorQuery(e.target.value);
                if (data.majorUndecided) onUpdate({ majorUndecided: false });
                setShowMajorDropdown(true);
                if (majorError) setMajorError(null);
              }}
              className={cn(
                "pl-9 pr-16 bg-surface",
                majorError && "border-destructive ring-1 ring-destructive"
              )}
            />
            <div className="absolute right-2 top-2 flex items-center gap-1">
              {(majorQuery || data.targetMajor) && (
                <button
                  type="button"
                  onClick={() => {
                    setMajorQuery(null);
                    onUpdate({ targetMajor: null });
                    setShowMajorDropdown(true);
                  }}
                  className="p-0.5 text-muted-foreground hover:text-foreground cursor-pointer rounded"
                  title="Xóa lựa chọn"
                >
                  <XIcon className="size-4" />
                </button>
              )}
              <button
                type="button"
                onClick={() => setShowMajorDropdown(!showMajorDropdown)}
                className="p-0.5 text-muted-foreground hover:text-foreground cursor-pointer rounded"
                title="Mở danh sách ngành"
              >
                <ChevronDownIcon
                  className={cn(
                    "size-4 transition-transform",
                    showMajorDropdown && "rotate-180 text-brand"
                  )}
                />
              </button>
            </div>
          </div>

          {(majorError || majorNotInList) && (
            <div className="flex items-center gap-1 text-xs text-destructive pt-0.5">
              <AlertCircleIcon className="size-3.5 shrink-0" />
              <span>{majorError ?? "Ngành đã lưu không còn trong danh sách, vui lòng chọn lại."}</span>
            </div>
          )}

          {/* Ngành phổ biến (cột is_popular trong bảng majors), không có thì ẩn */}
          {popularMajors.length > 0 && (
            <div className="flex flex-wrap gap-1.5 pt-1">
              {popularMajors.map((m) => {
                const isSelected = selectedMajor?.value === m.value;
                return (
                  <button
                    key={m.value}
                    type="button"
                    onClick={() => handleSelectMajor(m.value)}
                    className={cn(
                      "rounded-md border px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer",
                      isSelected
                        ? "border-brand bg-brand text-white shadow-xs"
                        : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                    )}
                  >
                    {m.label}
                  </button>
                );
              })}
            </div>
          )}

          {/* Searchable Dropdown List */}
          {showMajorDropdown && (
            <div className="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-xl border border-line bg-surface p-1.5 shadow-xl">
              <div className="px-2 py-1 text-[11px] font-semibold text-muted-foreground uppercase border-b border-line/60 mb-1">
                {filteredMajors.length > 0
                  ? `Tìm thấy ${filteredMajors.length} ngành`
                  : "Không tìm thấy ngành phù hợp"}
              </div>
              {filteredMajors.length === 0 ? (
                <div className="p-3 text-center text-xs text-muted-foreground">
                  Không tìm thấy ngành nào khớp với &quot;{majorQuery}&quot;. Thử gõ tên khác hoặc bấm
                  &quot;Chưa chọn ngành&quot; nếu bạn chưa quyết định.
                </div>
              ) : (
                filteredMajors.map((m) => {
                  const isSelected = selectedMajor?.value === m.value;
                  return (
                    <button
                      key={m.value}
                      type="button"
                      onClick={() => handleSelectMajor(m.value)}
                      className={cn(
                        "w-full text-left rounded-lg px-3 py-2 text-xs transition-colors flex items-center justify-between cursor-pointer",
                        isSelected
                          ? "bg-brand text-white font-medium"
                          : "text-foreground hover:bg-brand-wash hover:text-brand"
                      )}
                    >
                      <span>{m.label}</span>
                      {isSelected && <CheckIcon className="size-3.5 shrink-0" />}
                    </button>
                  );
                })
              )}
            </div>
          )}
        </div>
      </section>

      {/* Question 4: Thu hẹp gợi ý — không bắt buộc */}
      <section className="space-y-5 rounded-2xl border border-line bg-ground/50 p-5">
        <button
          type="button"
          onClick={() => setShowNarrow(!showNarrow)}
          aria-expanded={showNarrow}
          className="flex w-full items-center gap-2 text-left cursor-pointer"
        >
          <span className="text-sm font-semibold text-foreground">Thu hẹp gợi ý</span>
          <span className="rounded-full bg-surface px-2 py-0.5 text-[11px] font-medium text-muted-foreground">
            Không bắt buộc
          </span>
          {!showNarrow && (
            <span className="text-xs text-muted-foreground">Bang mong muốn, loại hình trường</span>
          )}
          <ChevronDownIcon
            className={cn("ml-auto size-4 text-muted-foreground transition-transform", showNarrow && "rotate-180")}
          />
        </button>

        {showNarrow && (
        <>
        {/* Bang mong muốn (Searchable Dropdown + Popular Chips) */}
        <div ref={stateDropdownRef} className="space-y-2 relative">
          <div className="flex items-center justify-between">
            <Label className="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <MapPinIcon className="size-3.5 text-brand" />
              Bang mong muốn (Mỹ) · Chọn được nhiều bang
            </Label>
          </div>

          {/* Selected chips display */}
          {selectedStates.length > 0 && (
            <div className="flex flex-wrap gap-1.5 pb-1">
              {selectedStates.map((code) => {
                const s = allStates.find((x) => x.code === code);
                return (
                  <span
                    key={code}
                    className="inline-flex items-center gap-1 rounded-md bg-brand-wash px-2.5 py-1 text-xs font-medium text-brand-ink border border-brand/20 shadow-2xs"
                  >
                    {s?.name ?? code} ({code})
                    <button
                      type="button"
                      onClick={() => removeState(code)}
                      className="hover:text-destructive cursor-pointer ml-0.5"
                      title="Bỏ chọn"
                    >
                      <XIcon className="size-3" />
                    </button>
                  </span>
                );
              })}
            </div>
          )}

          {/* Popular State Chips & Dropdown trigger */}
          <div className="flex flex-wrap gap-1.5 items-center">
            {popularStates.map((s) => {
              const isSelected = selectedStates.includes(s.code);
              return (
                <button
                  key={s.code}
                  type="button"
                  onClick={() => toggleState(s.code)}
                  className={cn(
                    "rounded-md border px-2.5 py-1 text-xs font-medium transition-colors cursor-pointer",
                    isSelected
                      ? "border-brand bg-brand text-white shadow-xs"
                      : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                  )}
                >
                  {s.name}
                </button>
              );
            })}

            <button
              type="button"
              onClick={() => setShowStateDropdown(!showStateDropdown)}
              className={cn(
                "rounded-md border border-line bg-surface px-3 py-1 text-xs font-medium flex items-center gap-1.5 transition-colors cursor-pointer",
                showStateDropdown
                  ? "border-brand text-brand ring-1 ring-brand"
                  : "text-brand hover:border-brand/60"
              )}
            >
              <span>+ Tìm trong 51 bang</span>
              <ChevronDownIcon
                className={cn(
                  "size-3.5 transition-transform",
                  showStateDropdown && "rotate-180"
                )}
              />
            </button>
          </div>

          <p className="text-xs text-muted-foreground">
            {selectedStates.length > 0
              ? `Đã chọn ${selectedStates.length} bang.`
              : "Chưa chọn bang nào, không lọc theo bang."}
          </p>

          {/* Searchable State Dropdown List */}
          {showStateDropdown && (
            <div className="absolute z-30 mt-1 max-h-60 w-full max-w-md overflow-hidden rounded-xl border border-line bg-surface shadow-xl flex flex-col">
              <div className="p-2 border-b border-line bg-muted/20">
                <div className="relative">
                  <SearchIcon className="absolute left-2.5 top-2 size-3.5 text-muted-foreground pointer-events-none" />
                  <Input
                    placeholder="Tìm tên bang hoặc mã viết tắt (VD: CA, Texas)..."
                    value={stateSearch}
                    onChange={(e) => setStateSearch(e.target.value)}
                    className="h-8 text-xs pl-8 pr-2 bg-surface"
                    autoFocus
                  />
                </div>
              </div>

              <div className="overflow-y-auto max-h-48 p-1.5 space-y-2">
                {stateGroups.map((group) => (
                  <div key={group.value || "other"}>
                    <p className="px-2.5 pb-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                      {group.label}
                    </p>
                    <div className="grid grid-cols-2 gap-1">
                      {group.states.map((s) => {
                        const isSelected = selectedStates.includes(s.code);
                        return (
                          <button
                            key={s.code}
                            type="button"
                            onClick={() => toggleState(s.code)}
                            className={cn(
                              "flex items-center justify-between rounded-md px-2.5 py-1.5 text-xs text-left cursor-pointer transition-colors",
                              isSelected
                                ? "bg-brand text-white font-medium"
                                : "hover:bg-muted text-foreground"
                            )}
                          >
                            <span className="truncate">{s.name} ({s.code})</span>
                            {isSelected && <CheckIcon className="size-3.5 shrink-0 ml-1" />}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                ))}
                {stateGroups.length === 0 && (
                  <p className="px-2.5 py-2 text-xs text-muted-foreground">Không tìm thấy bang nào.</p>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Loại trường (Công lập / Tư thục) */}
        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Loại hình trường</Label>
          <div className="flex gap-2">
            {SCHOOL_TYPE_OPTIONS.map((opt) => {
              const current = data.targetSchoolType ?? "all";
              const isSelected = current === opt.value;
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => onUpdate({ targetSchoolType: opt.value })}
                  className={cn(
                    "flex items-center rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
                    isSelected
                      ? "border-brand bg-brand-wash/60 text-brand ring-1 ring-brand font-semibold"
                      : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                  )}
                >
                  <span
                    className={cn(
                      "mr-1.5 flex size-3 items-center justify-center rounded-full border transition-all",
                      isSelected
                        ? "border-brand bg-brand"
                        : "border-muted-foreground/50 bg-surface"
                    )}
                  >
                    {isSelected && <span className="size-1 rounded-full bg-white" />}
                  </span>
                  {opt.label}
                </button>
              );
            })}
          </div>
        </div>
        </>
        )}
      </section>

      {/* Bottom Actions */}
      <div className="flex justify-end pt-4 border-t border-line">
        <Button onClick={handleNextClick} size="lg" className="gap-1.5 cursor-pointer">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
