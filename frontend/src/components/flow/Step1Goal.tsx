"use client";

import { useId, useState, useRef, useEffect } from "react";
import {
  ArrowRightIcon,
  CheckIcon,
  ChevronDownIcon,
  SearchIcon,
  XIcon,
  AlertCircleIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { RequiredMark } from "./RequiredMark";
import { CHOICE_CARD, CHOICE_PILL, StepHeader, StepNav } from "./StepChrome";
import { cn } from "@/lib/utils";
import { normalizeSearch } from "@/lib/certification-utils";
import {
  EDUCATION_LEVEL_OPTIONS,
  EDUCATION_SYSTEM_OPTIONS,
  SCHOOL_TYPE_OPTIONS,
  getGradeOptionsForLevel,
} from "@/lib/profile-flow-options";
import type { CurrentEducationLevel, EducationSystem, GoalDraft, ProfileMetadataResponse } from "@/types/profile-flow";
import { goalsNeedMajor, type FormGoal } from "@/lib/form-goals";

interface Step1GoalProps {
  data: GoalDraft;
  metadata: ProfileMetadataResponse | null;
  onUpdate: (updated: Partial<GoalDraft>) => void;
  onNext: () => void;
  /** Nhu cầu đã chọn (quyết định ngành phải là ngành thật hay được chọn "cần tư vấn") */
  goals: FormGoal[];
  /** Mở màn sửa nhu cầu; không có khi nhu cầu bị khóa theo chat */
  onEditNeeds?: () => void;
}

// Lựa chọn "chưa biết ngành": lưu targetMajor = null kèm majorUndecided, không lưu chữ này vào hồ sơ (BE từ chối ngành lạ)
const MAJOR_GUIDANCE = "Tôi cần thêm tư vấn về ngành học";

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
  { value: "Fall", label: "Mùa thu (Fall)" },
  { value: "Spring", label: "Mùa xuân (Spring)" },
  { value: "Summer", label: "Mùa hè (Summer)" },
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

export function Step1Goal({ data, metadata, onUpdate, onNext, goals, onEditNeeds }: Step1GoalProps) {
  // Chữ đang gõ để tìm ngành; null = không gõ, ô hiện tên Việt của ngành đã chọn
  const [majorQuery, setMajorQuery] = useState<string | null>(null);
  const [showMajorDropdown, setShowMajorDropdown] = useState(false);
  const [majorError, setMajorError] = useState<string | null>(null);
  // Học sinh chọn "Tôi cần thêm tư vấn về ngành học" (ở form hoặc chat): ngành lưu null kèm cờ majorUndecided
  const majorUnknown = !data.targetMajor && data.majorUndecided === true;
  const [levelError, setLevelError] = useState<string | null>(null);
  // Đánh giá hồ sơ thì cho qua mà không cần ngành; lộ trình, gợi ý trường lọc theo ngành nên cần ngành thật
  const needMajor = goalsNeedMajor(goals);
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
  // Khối "Thu hẹp gợi ý trường" thu gọn sẵn; đã chọn bang hoặc loại trường thì mở để thấy lựa chọn cũ
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
  // Lớp hoặc năm đã lưu có thể nằm ngoài danh sách (từ chat, đổi cấp học): vẫn hiện để không mất
  const gradeItems = (data.currentGrade && !gradeOptions.includes(data.currentGrade) ? [data.currentGrade, ...gradeOptions] : gradeOptions).map(
    (g) => ({ value: g, label: g })
  );
  const graduationYearItems = (
    data.expectedGraduationYear && !GRADUATION_YEAR_OPTIONS.includes(data.expectedGraduationYear)
      ? [data.expectedGraduationYear, ...GRADUATION_YEAR_OPTIONS]
      : GRADUATION_YEAR_OPTIONS
  ).map((y) => ({ value: String(y), label: String(y) }));
  const intakeYearItems = (intakeYear && !yearOptions.includes(intakeYear) ? [intakeYear, ...yearOptions] : yearOptions).map(
    (y) => ({ value: y, label: y })
  );

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
  // Đã chọn "cần tư vấn" mà nhu cầu lại cần ngành thật (vd vừa thêm Gợi ý trường): báo ngay, kèm đường sang đổi nhu cầu
  const guidanceConflict = needMajor && majorUnknown;

  const handleSelectMajor = (major: string) => {
    setMajorQuery(null);
    onUpdate({ targetMajor: major, majorUndecided: false });
    setMajorError(null);
    setShowMajorDropdown(false);
  };

  const handleMajorUnknown = () => {
    if (needMajor) return; // lựa chọn này đang bị khóa, đã có chú thích bên cạnh
    setMajorQuery(null);
    onUpdate({ targetMajor: null, majorUndecided: true });
    setMajorError(null);
    setShowMajorDropdown(false);
  };

  const handleNextClick = () => {
    if (!data.currentEducationLevel) {
      setLevelError("Chọn cấp học bạn đang theo.");
      if (typeof window !== "undefined") window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }
    setLevelError(null);
    if (!data.targetDegreeLevel) {
      setDegreeError("Chọn bậc học bạn muốn theo.");
      document.getElementById(degreeSectionId)?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    setDegreeError(null);
    const noMajor = !data.targetMajor || !data.targetMajor.trim();
    if (majorNotInList || (noMajor && (needMajor || !majorUnknown))) {
      setMajorError(
        majorNotInList
          ? "Ngành đã lưu không còn trong danh sách. Bạn chọn lại nhé."
          : needMajor
            ? "Lộ trình và gợi ý trường cần một ngành cụ thể."
            : `Chọn một ngành, hoặc chọn “${MAJOR_GUIDANCE}”.`
      );
      setShowMajorDropdown(!guidanceConflict);
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

  const fieldLabel = "text-xs font-medium text-foreground";
  const selectTrigger = "h-9 w-full bg-surface";
  const majorErrorText = guidanceConflict
    ? null
    : (majorError ?? (majorNotInList ? "Ngành đã lưu không còn trong danh sách. Bạn chọn lại nhé." : null));

  return (
    <div className="flow-stagger space-y-10">
      <StepHeader title="Mục tiêu của bạn" />

      {/* Question 1: Current Education */}
      <fieldset role="radiogroup" className="min-w-0 space-y-4">
        <legend className="text-h3 mb-4 text-foreground">
          Bạn đang học cấp nào? <RequiredMark />
        </legend>

        <div className="flex flex-wrap gap-2.5">
          {EDUCATION_LEVEL_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              type="button"
              role="radio"
              aria-checked={data.currentEducationLevel === opt.value}
              onClick={() => handleSelectEducationLevel(opt.value)}
              className={CHOICE_PILL}
            >
              {opt.label}
            </button>
          ))}
        </div>
        {levelError && <p className="text-xs font-medium text-destructive">{levelError}</p>}

        {/* Chọn cấp học xong mới hiện lớp, trường, năm tốt nghiệp; cấp 3 thêm hệ chương trình (giống câu D1 bên chat) */}
        {data.currentEducationLevel && (
          <div className={cn("grid gap-4 pt-2", isHighSchool ? "sm:grid-cols-2" : "sm:grid-cols-3")}>
            {/* Lớp / Năm học (động theo cấp học) */}
            <div className="space-y-1.5">
              <Label htmlFor="currentGrade" className={fieldLabel}>
                Lớp / năm đang học
              </Label>
              <Select
                value={data.currentGrade || null}
                onValueChange={(v) => {
                  // Chọn lớp thì tự điền năm tốt nghiệp, học sinh vẫn sửa lại được
                  const grade = v || null;
                  const graduationYear = guessGraduationYear(data.currentEducationLevel, grade);
                  onUpdate({ currentGrade: grade, ...(graduationYear && { expectedGraduationYear: graduationYear }) });
                }}
                items={gradeItems}
              >
                <SelectTrigger id="currentGrade" className={selectTrigger}>
                  <SelectValue placeholder="Chọn lớp / năm" />
                </SelectTrigger>
                <SelectContent>
                  {gradeItems.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Trường đang học */}
            <div className="space-y-1.5">
              <Label htmlFor="currentSchool" className={fieldLabel}>
                Trường đang học
              </Label>
              <Input
                id="currentSchool"
                placeholder="VD: THPT Chuyên Hà Nội – Amsterdam"
                value={data.currentSchool ?? ""}
                onChange={(e) => onUpdate({ currentSchool: e.target.value })}
                className="h-9 w-full bg-surface"
              />
            </div>

            {/* Năm tốt nghiệp dự kiến: tự điền khi chọn lớp, năm nay → 6 năm tới */}
            <div className="space-y-1.5">
              <Label htmlFor="expectedGraduationYear" className={fieldLabel}>
                Năm tốt nghiệp dự kiến
              </Label>
              <Select
                value={data.expectedGraduationYear != null ? String(data.expectedGraduationYear) : null}
                onValueChange={(v) => onUpdate({ expectedGraduationYear: v ? Number(v) : null })}
                items={graduationYearItems}
              >
                <SelectTrigger id="expectedGraduationYear" className={selectTrigger}>
                  <SelectValue placeholder="Chưa xác định" />
                </SelectTrigger>
                <SelectContent>
                  {graduationYearItems.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Hệ chương trình chỉ có ở cấp 3 */}
            {isHighSchool && (
              <div className="space-y-1.5">
                <Label htmlFor="educationSystem" className={fieldLabel}>
                  Hệ chương trình
                </Label>
                <Select
                  value={data.educationSystem || null}
                  onValueChange={(v) => onUpdate({ educationSystem: (v || null) as EducationSystem | null })}
                  items={EDUCATION_SYSTEM_OPTIONS}
                >
                  <SelectTrigger id="educationSystem" className={selectTrigger}>
                    <SelectValue placeholder="Chưa chọn" />
                  </SelectTrigger>
                  <SelectContent className="w-max min-w-(--anchor-width)">
                    {EDUCATION_SYSTEM_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>
        )}
      </fieldset>

      {/* Question 2: Target Degree Level + kỳ nhập học */}
      <div id={degreeSectionId} className="space-y-6">
        <fieldset role="radiogroup" className="min-w-0 space-y-4">
          <legend className="text-h3 mb-4 text-foreground">
            Bạn muốn học bậc nào ở Mỹ? <RequiredMark />
          </legend>

          <div className="grid gap-3 sm:grid-cols-2">
            {studyLevels.map((lvl) => (
              <button
                key={lvl.key}
                type="button"
                role="radio"
                aria-checked={data.targetDegreeLevel === lvl.key}
                onClick={() => {
                  onUpdate({ targetDegreeLevel: lvl.key });
                  setDegreeError(null);
                }}
                className={cn(CHOICE_CARD, "flex h-full flex-col items-start")}
              >
                <span className="text-sm font-semibold text-foreground">{lvl.label}</span>
                <span className="mt-1 text-xs text-ink-2">{lvl.description}</span>
              </button>
            ))}
          </div>
          {degreeError && <p className="text-xs font-medium text-destructive">{degreeError}</p>}
          {(data.targetDegreeLevel === "master" || data.targetDegreeLevel === "phd") && (
            <p className="flex items-center gap-1.5 text-xs font-medium text-warn">
              <AlertCircleIcon className="size-3.5 shrink-0" />
              Hiện hệ thống mới gợi ý trường cho Cao đẳng và Đại học.
            </p>
          )}
        </fieldset>

        {/* Kỳ nhập học — chọn kỳ rồi chọn năm, giống câu B1.4 bên chat */}
        <fieldset className="min-w-0 space-y-3">
          <legend className="text-h3 mb-3 text-foreground">Bạn muốn nhập học khi nào?</legend>
          <div className="grid gap-3 sm:max-w-md sm:grid-cols-2">
            <Select
              value={intakeTerm || null}
              onValueChange={(v) => updateIntake(v ?? "", intakeYear)}
              items={INTAKE_TERM_OPTIONS}
            >
              <SelectTrigger id="targetIntakeTerm" aria-label="Kỳ nhập học" className={selectTrigger}>
                <SelectValue placeholder="Chọn kỳ" />
              </SelectTrigger>
              <SelectContent>
                {INTAKE_TERM_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={intakeYear || null}
              onValueChange={(v) => updateIntake(intakeTerm, v ?? "")}
              items={intakeYearItems}
            >
              <SelectTrigger id="targetIntakeYear" aria-label="Năm nhập học" className={selectTrigger}>
                <SelectValue placeholder="Chọn năm" />
              </SelectTrigger>
              <SelectContent>
                {intakeYearItems.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </fieldset>
      </div>

      {/* Question 3: Ngành học mong muốn (bắt buộc, trừ khi chọn cần tư vấn) */}
      <div ref={majorDropdownRef} className="relative space-y-3">
        <Label htmlFor={majorInputId} className="text-h3 text-foreground">
          Bạn muốn học ngành gì? <RequiredMark />
        </Label>

        <div className="relative">
          <SearchIcon className="pointer-events-none absolute top-2.5 left-3 size-4 text-ink-2" />
          <Input
            id={majorInputId}
            placeholder={allMajors.length > 0 ? "Tìm ngành, ví dụ: máy tính, kinh tế" : "Đang tải danh sách ngành…"}
            value={majorQuery ?? (majorUnknown ? MAJOR_GUIDANCE : selectedMajorLabel)}
            onFocus={(e) => {
              setShowMajorDropdown(true);
              if (majorUnknown && majorQuery === null) e.target.select(); // gõ là thay luôn lựa chọn "cần tư vấn"
            }}
            onChange={(e) => {
              setMajorQuery(e.target.value);
              if (data.majorUndecided) onUpdate({ majorUndecided: false });
              setShowMajorDropdown(true);
              if (majorError) setMajorError(null);
            }}
            aria-invalid={majorErrorText || guidanceConflict ? true : undefined}
            className="h-9 w-full bg-surface pr-16 pl-9"
          />
          <div className="absolute top-1.5 right-2 flex items-center gap-1">
            {(majorQuery || data.targetMajor || majorUnknown) && (
              <button
                type="button"
                onClick={() => {
                  setMajorQuery(null);
                  onUpdate({ targetMajor: null, majorUndecided: false });
                  setShowMajorDropdown(true);
                }}
                className="cursor-pointer rounded p-0.5 text-ink-2 hover:text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
                aria-label="Xóa lựa chọn"
              >
                <XIcon className="size-4" />
              </button>
            )}
            <button
              type="button"
              onClick={() => setShowMajorDropdown(!showMajorDropdown)}
              className="cursor-pointer rounded p-0.5 text-ink-2 hover:text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
              aria-label="Mở danh sách ngành"
              aria-expanded={showMajorDropdown}
            >
              <ChevronDownIcon className={cn("size-4 transition-transform", showMajorDropdown && "rotate-180 text-brand")} />
            </button>
          </div>
        </div>

        {majorErrorText && (
          <div role="alert" className="flex items-center gap-1 pt-0.5 text-xs text-destructive">
            <AlertCircleIcon className="size-3.5 shrink-0" />
            <span>{majorErrorText}</span>
          </div>
        )}
        {guidanceConflict && (
          <p role="alert" className="flex flex-wrap items-center gap-x-1.5 text-xs text-destructive">
            <AlertCircleIcon className="size-3.5 shrink-0" />
            <span>Lộ trình và gợi ý trường cần một ngành cụ thể.</span>
            {onEditNeeds && (
              <button
                type="button"
                onClick={onEditNeeds}
                className="cursor-pointer font-medium underline underline-offset-2 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
              >
                Đổi nhu cầu
              </button>
            )}
          </p>
        )}

        {/* Ngành phổ biến (cột is_popular trong bảng majors) và lựa chọn "cần tư vấn" */}
          <div role="radiogroup" aria-label="Ngành phổ biến" className="flex flex-wrap gap-1.5 pt-1">
            {popularMajors.map((m) => (
              <button
                key={m.value}
                type="button"
                role="radio"
                aria-checked={selectedMajor?.value === m.value}
                onClick={() => handleSelectMajor(m.value)}
                className={cn(CHOICE_PILL, "px-3 py-1 text-xs")}
              >
                {m.label}
              </button>
            ))}
            <button
              type="button"
              role="radio"
              aria-checked={majorUnknown}
              aria-disabled={needMajor || undefined}
              onClick={handleMajorUnknown}
              className={cn(CHOICE_PILL, "border-dashed px-3 py-1 text-xs aria-disabled:opacity-(--cds-opacity-disabled) aria-checked:border-solid")}
            >
              {MAJOR_GUIDANCE}
            </button>
          </div>

        {/* Searchable Dropdown List */}
        {showMajorDropdown && (
          <div className="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-xl border border-line bg-surface p-1.5 shadow-md">
            {/* Luôn nằm trên cùng, kể cả khi đang lọc */}
            <button
              type="button"
              aria-disabled={needMajor || undefined}
              onClick={handleMajorUnknown}
              className={cn(
                "sticky top-0 z-10 mb-1 flex w-full cursor-pointer flex-col rounded-lg border-b border-line bg-surface px-3 py-2 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                needMajor ? "cursor-default text-ink-2" : "text-foreground hover:bg-brand-wash hover:text-brand",
                majorUnknown && "font-medium text-brand"
              )}
            >
              <span className="flex items-center justify-between">
                {MAJOR_GUIDANCE}
                {majorUnknown && <CheckIcon className="size-3.5 shrink-0" />}
              </span>
              {needMajor && <span className="mt-0.5 font-normal">Cần bỏ Lộ trình và Gợi ý trường ở Nhu cầu của bạn</span>}
            </button>
            {filteredMajors.length === 0 ? (
              <div className="p-3 text-center text-xs text-ink-2">Không có ngành khớp “{majorQuery}”.</div>
            ) : (
              filteredMajors.map((m) => {
                const isSelected = selectedMajor?.value === m.value;
                return (
                  <button
                    key={m.value}
                    type="button"
                    onClick={() => handleSelectMajor(m.value)}
                    className={cn(
                      "flex w-full cursor-pointer items-center justify-between rounded-lg px-3 py-2 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                      isSelected ? "bg-brand font-medium text-brand-foreground" : "text-foreground hover:bg-brand-wash hover:text-brand"
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

      {/* Thu hẹp gợi ý — không bắt buộc */}
      <section className="space-y-5">
        <button
          type="button"
          onClick={() => setShowNarrow(!showNarrow)}
          aria-expanded={showNarrow}
          className="flex w-full cursor-pointer items-center gap-2 rounded-lg text-left focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
        >
          <span className="text-h3 text-foreground">Thu hẹp gợi ý trường (không bắt buộc)</span>
          <ChevronDownIcon
            className={cn("ml-auto size-4 shrink-0 text-ink-2 transition-transform", showNarrow && "rotate-180")}
          />
        </button>

        {showNarrow && (
          <>
            {/* Bang mong muốn (Searchable Dropdown + Popular Chips) */}
            <div ref={stateDropdownRef} className="relative space-y-2">
              <Label className="text-xs font-semibold text-foreground">Bang mong muốn</Label>

              {/* Selected chips display */}
              {selectedStates.length > 0 && (
                <div className="flex flex-wrap gap-1.5 pb-1">
                  {selectedStates.map((code) => {
                    const s = allStates.find((x) => x.code === code);
                    return (
                      <span
                        key={code}
                        className="inline-flex items-center gap-1 rounded-md border border-brand/20 bg-brand-wash px-2.5 py-1 text-xs font-medium text-brand-ink"
                      >
                        {s?.name ?? code} ({code})
                        <button
                          type="button"
                          onClick={() => removeState(code)}
                          className="ml-0.5 cursor-pointer rounded hover:text-destructive focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
                          aria-label={`Bỏ chọn ${s?.name ?? code}`}
                        >
                          <XIcon className="size-3" />
                        </button>
                      </span>
                    );
                  })}
                </div>
              )}

              {/* Popular State Chips & Dropdown trigger */}
              <div role="group" aria-label="Bang phổ biến" className="flex flex-wrap items-center gap-1.5">
                {popularStates.map((s) => (
                  <button
                    key={s.code}
                    type="button"
                    role="checkbox"
                    aria-checked={selectedStates.includes(s.code)}
                    onClick={() => toggleState(s.code)}
                    className={cn(CHOICE_PILL, "px-3 py-1 text-xs")}
                  >
                    {s.name}
                  </button>
                ))}

                <button
                  type="button"
                  onClick={() => setShowStateDropdown(!showStateDropdown)}
                  aria-expanded={showStateDropdown}
                  className={cn(
                    "flex cursor-pointer items-center gap-1.5 rounded-full border border-dashed border-line bg-surface px-3 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                    showStateDropdown ? "border-brand text-brand" : "text-brand hover:border-brand/60"
                  )}
                >
                  Tìm trong 51 bang
                  <ChevronDownIcon className={cn("size-3.5 transition-transform", showStateDropdown && "rotate-180")} />
                </button>
              </div>

              {/* Searchable State Dropdown List */}
              {showStateDropdown && (
                <div className="absolute z-30 mt-1 flex max-h-60 w-full max-w-md flex-col overflow-hidden rounded-xl border border-line bg-surface shadow-md">
                  <div className="border-b border-line p-2">
                    <div className="relative">
                      <SearchIcon className="pointer-events-none absolute top-2 left-2.5 size-3.5 text-ink-2" />
                      <Input
                        placeholder="Tên bang hoặc mã, ví dụ CA"
                        value={stateSearch}
                        onChange={(e) => setStateSearch(e.target.value)}
                        className="h-8 bg-surface pr-2 pl-8 text-xs"
                        autoFocus
                      />
                    </div>
                  </div>

                  <div className="max-h-48 space-y-2 overflow-y-auto p-1.5">
                    {stateGroups.map((group) => (
                      <div key={group.value || "other"}>
                        <p className="px-2.5 pb-1 text-xs font-semibold text-ink-2">{group.label}</p>
                        <div className="grid grid-cols-2 gap-1">
                          {group.states.map((s) => {
                            const isSelected = selectedStates.includes(s.code);
                            return (
                              <button
                                key={s.code}
                                type="button"
                                onClick={() => toggleState(s.code)}
                                className={cn(
                                  "flex cursor-pointer items-center justify-between rounded-md px-2.5 py-1.5 text-left text-xs transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
                                  isSelected ? "bg-brand font-medium text-brand-foreground" : "text-foreground hover:bg-muted"
                                )}
                              >
                                <span className="truncate">
                                  {s.name} ({s.code})
                                </span>
                                {isSelected && <CheckIcon className="ml-1 size-3.5 shrink-0" />}
                              </button>
                            );
                          })}
                        </div>
                      </div>
                    ))}
                    {stateGroups.length === 0 && (
                      <p className="px-2.5 py-2 text-xs text-ink-2">Không tìm thấy bang nào.</p>
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* Loại trường (Công lập / Tư thục) */}
            <fieldset role="radiogroup" className="min-w-0 space-y-1.5">
              <legend className="mb-1.5 text-xs font-semibold text-foreground">Loại hình trường</legend>
              <div className="flex gap-2">
                {SCHOOL_TYPE_OPTIONS.map((opt) => (
                  <button
                    key={opt.value}
                    type="button"
                    role="radio"
                    aria-checked={(data.targetSchoolType ?? "all") === opt.value}
                    onClick={() => onUpdate({ targetSchoolType: opt.value })}
                    className={cn(CHOICE_PILL, "px-3 py-1 text-xs")}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </fieldset>
          </>
        )}
      </section>

      <StepNav>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </div>
  );
}
