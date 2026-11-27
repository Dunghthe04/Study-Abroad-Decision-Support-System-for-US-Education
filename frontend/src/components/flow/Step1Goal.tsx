"use client";

import { useId, useState, useRef, useEffect } from "react";
import { AlertCircleIcon, ArrowRightIcon, CheckIcon, ChevronDownIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { StepHeader, StepNav } from "./StepChrome";
import {
  ActionRow,
  ChoicePicker,
  FormBody,
  FormGroup,
  FormSection,
  PickerPanel,
  CHOICE_CHIP,
  PrefixInput,
} from "./FormKit";
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

// Năm kết thúc của năm học hiện tại. Năm học Việt Nam kết thúc tháng 6, từ tháng 8 tính là năm học mới
function currentSchoolYearEnd() {
  const now = new Date();
  return now.getMonth() >= 7 ? now.getFullYear() + 1 : now.getFullYear();
}

// Năm tốt nghiệp dự kiến theo lớp đang học. "Đã tốt nghiệp" hay lớp không đọc được thì trả null (không tự điền)
function guessGraduationYear(level: CurrentEducationLevel | null | undefined, grade: string | null | undefined) {
  const last = level ? LAST_GRADE[level] : undefined;
  if (!last || !grade) return null;
  const schoolYearEnd = currentSchoolYearEnd();
  if (grade === "Năm cuối") return schoolYearEnd;
  const n = Number(/^(?:Lớp|Năm) (\d+)$/.exec(grade)?.[1]);
  return n ? schoolYearEnd + Math.max(0, last - n) : null;
}
// Kỳ nhập học: chọn kỳ rồi chọn năm, lưu ghép "Fall 2027" — khớp IntakeRegex phía BE và câu B1.4 bên chat
const INTAKE_TERM_OPTIONS = [
  { value: "Fall", label: "Mùa thu", description: "Fall" },
  { value: "Spring", label: "Mùa xuân", description: "Spring" },
  { value: "Summer", label: "Mùa hè", description: "Summer" },
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

// Loại trường ghép vào đầu tên: lưu một chuỗi `${loại} ${tên}`, "Khác" thì không có tiền tố.
// Xếp dài trước để "Trường THPT chuyên" khớp trước "Trường THPT".
const SCHOOL_PREFIX_NONE = "other";
const SCHOOL_PREFIX_OPTIONS = [
  { value: "Trường THPT chuyên", label: "Trường THPT chuyên" },
  { value: "Trường THPT", label: "Trường THPT" },
  { value: "Trường THCS", label: "Trường THCS" },
  { value: "Trường quốc tế", label: "Trường quốc tế" },
  { value: SCHOOL_PREFIX_NONE, label: "Khác" },
];

function parseSchool(school: string): { prefix: string; name: string } {
  const hit = SCHOOL_PREFIX_OPTIONS.find(
    (p) => p.value !== SCHOOL_PREFIX_NONE && (school === p.value || school.startsWith(`${p.value} `))
  );
  if (hit) return { prefix: hit.value, name: school.slice(hit.value.length + 1) };
  return { prefix: school ? SCHOOL_PREFIX_NONE : "", name: school };
}

function composeSchool(prefix: string, name: string): string {
  if (!name) return "";
  return prefix && prefix !== SCHOOL_PREFIX_NONE ? `${prefix} ${name}` : name;
}

export function Step1Goal({ data, metadata, onUpdate, onNext, goals, onEditNeeds }: Step1GoalProps) {
  const [majorError, setMajorError] = useState<string | null>(null);
  // Học sinh chọn "Tôi cần thêm tư vấn về ngành học" (ở form hoặc chat): ngành lưu null kèm cờ majorUndecided
  const majorUnknown = !data.targetMajor && data.majorUndecided === true;
  const [levelError, setLevelError] = useState<string | null>(null);
  // Đánh giá hồ sơ thì cho qua mà không cần ngành; lộ trình, gợi ý trường lọc theo ngành nên cần ngành thật
  const needMajor = goalsNeedMajor(goals);
  // Mới chọn kỳ hoặc năm thì giữ tạm ở đây; đủ cả hai mới ghi vào nháp
  const [intakePending, setIntakePending] = useState({ term: "", year: "" });
  const savedIntake = parseIntake(data.targetIntake);
  const intakeTerm = savedIntake?.term ?? intakePending.term;
  const intakeYear = savedIntake?.year ?? intakePending.year;
  const updateIntake = (term: string, year: string) => {
    setIntakePending({ term, year });
    onUpdate({ targetIntake: term && year ? `${term} ${year}` : null });
  };

  // Loại trường chọn trước khi gõ tên: chưa có tên thì chưa lưu gì, nên giữ tạm ở đây
  const [schoolPrefixPending, setSchoolPrefixPending] = useState("");
  const school = parseSchool(data.currentSchool ?? "");
  const schoolPrefix = data.currentSchool ? school.prefix : schoolPrefixPending;

  const [stateSearch, setStateSearch] = useState("");
  const [showStateDropdown, setShowStateDropdown] = useState(false);

  const majorInputId = useId();
  const stateDropdownRef = useRef<HTMLDivElement>(null);

  // Đóng danh sách bang khi bấm ra ngoài
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (stateDropdownRef.current && !stateDropdownRef.current.contains(event.target as Node)) {
        setShowStateDropdown(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Lớp đã lưu có thể nằm ngoài danh sách (từ chat, hồ sơ cũ): vẫn hiện thành một lựa chọn để không mất
  const levelGrades = getGradeOptionsForLevel(data.currentEducationLevel);
  const gradeOptions = (data.currentGrade && !levelGrades.includes(data.currentGrade) ? [...levelGrades, data.currentGrade] : levelGrades).map(
    (g) => ({ value: g, label: g })
  );
  const yearOptions = intakeYearOptions(data.expectedGraduationYear);
  const isHighSchool = data.currentEducationLevel === "high_school";
  // Còn đang học thì không thể tốt nghiệp ở năm học đã qua (cùng quy tắc EffectiveGraduationYear của backend)
  const earliestGraduationYear = guessGraduationYear(data.currentEducationLevel, data.currentGrade)
    ? currentSchoolYearEnd()
    : THIS_YEAR;
  const graduationYearOptions = GRADUATION_YEAR_OPTIONS.filter((y) => y >= earliestGraduationYear);
  // Năm đã lưu có thể nằm ngoài danh sách (từ chat, hồ sơ cũ): vẫn hiện để không mất
  const graduationYearItems = (
    data.expectedGraduationYear && !graduationYearOptions.includes(data.expectedGraduationYear)
      ? [data.expectedGraduationYear, ...graduationYearOptions]
      : graduationYearOptions
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
  // Bang hay chọn, cộng các bang đã chọn nhưng không phổ biến, để bỏ chọn được bằng chính nút bấm
  const stateChips = [
    ...popularStates,
    ...selectedStates
      .filter((code) => !popularStates.some((s) => s.code === code))
      .map((code) => allStates.find((x) => x.code === code) ?? { code, name: code }),
  ];
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

  // Ngành trong bảng majors: hiện label (tiếng Việt), lưu value (tiếng Anh) để lọc trường khớp
  const allMajors = (metadata?.majors ?? []).map((m) => ({ ...m, keywords: m.aliases }));
  const popularMajors = allMajors.filter((m) => m.isPopular);
  const selectedMajor = data.targetMajor
    ? allMajors.find((m) => m.value.toLowerCase() === data.targetMajor!.toLowerCase())
    : undefined;
  // Hồ sơ cũ có thể lưu ngành không còn trong danh sách (vd "Computer Science")
  const majorNotInList = !!data.targetMajor && allMajors.length > 0 && !selectedMajor;
  // Đã chọn "cần tư vấn" mà nhu cầu lại cần ngành thật (vd vừa thêm Gợi ý trường): báo ngay, kèm đường sang đổi nhu cầu
  const guidanceConflict = needMajor && majorUnknown;

  const handleSelectMajor = (major: string) => {
    onUpdate({ targetMajor: major, majorUndecided: false });
    setMajorError(null);
  };

  const handleMajorUnknown = () => {
    if (needMajor) return; // lựa chọn này đang bị khóa, đã có chú thích bên cạnh
    // Bấm lại khi đang chọn thì bỏ chọn
    onUpdate({ targetMajor: null, majorUndecided: !majorUnknown });
    setMajorError(null);
  };

  const handleNextClick = () => {
    if (!data.currentEducationLevel) {
      setLevelError("Chọn cấp học bạn đang theo.");
      if (typeof window !== "undefined") {
        window.scrollTo({ top: 0, behavior: "smooth" });
        document.getElementById("flow-scroll")?.scrollTo({ top: 0, behavior: "smooth" });
      }
      return;
    }
    setLevelError(null);
    const noMajor = !data.targetMajor || !data.targetMajor.trim();
    if (majorNotInList || (noMajor && (needMajor || !majorUnknown))) {
      setMajorError(
        majorNotInList
          ? "Ngành đã lưu không còn trong danh sách. Bạn chọn lại nhé."
          : needMajor
            ? "Lộ trình và gợi ý trường cần một ngành cụ thể."
            : `Chọn một ngành, hoặc chọn “${MAJOR_GUIDANCE}”.`
      );
      document.getElementById(majorInputId)?.focus();
      return;
    }
    setMajorError(null);
    onNext();
  };

  const majorErrorText = guidanceConflict
    ? null
    : (majorError ?? (majorNotInList ? "Ngành đã lưu không còn trong danh sách. Bạn chọn lại nhé." : null));

  return (
    <>
      <StepHeader title="Mục tiêu của bạn" />
      <FormBody>
        <FormGroup title="Tình trạng học vấn">
        <FormSection title="Bạn đang học cấp nào?" required error={levelError}>
          <ChoicePicker
            options={EDUCATION_LEVEL_OPTIONS}
            value={data.currentEducationLevel}
            onChange={(v) => v && handleSelectEducationLevel(v as CurrentEducationLevel)}
            ariaLabel="Cấp học đang theo"
          />
        </FormSection>

          {/* Chọn cấp học xong mới hiện lớp, trường, năm tốt nghiệp; cấp 3 thêm hệ chương trình (giống câu D1 bên chat) */}
          {data.currentEducationLevel && (
            <>
              <FormSection title="Lớp / năm đang học">
                <ChoicePicker
                  id="currentGrade"
                  options={gradeOptions}
                  value={data.currentGrade}
                  ariaLabel="Lớp / năm đang học"
                  onChange={(v) => {
                    // Chọn lớp thì tự điền năm tốt nghiệp, học sinh vẫn sửa lại được
                    const grade = v || null;
                    const graduationYear = guessGraduationYear(data.currentEducationLevel, grade);
                    onUpdate({ currentGrade: grade, ...(graduationYear && { expectedGraduationYear: graduationYear }) });
                  }}
                />
              </FormSection>

              <FormSection title="Trường đang học">
                <PrefixInput
                  id="currentSchool"
                  ariaLabel="Trường đang học"
                  prefixes={SCHOOL_PREFIX_OPTIONS}
                  prefix={schoolPrefix}
                  placeholder="Tên trường, ví dụ Chu Văn An"
                  value={school.name}
                  onPrefixChange={(p) => {
                    setSchoolPrefixPending(p);
                    onUpdate({ currentSchool: composeSchool(p, school.name) });
                  }}
                  onChange={(name) => onUpdate({ currentSchool: composeSchool(schoolPrefix, name) })}
                />
              </FormSection>

              {/* Năm tốt nghiệp dự kiến: tự điền khi chọn lớp, năm nay → 6 năm tới */}
              <FormSection title="Năm tốt nghiệp dự kiến" description="Tự điền theo lớp, bạn sửa lại được.">
                <ChoicePicker
                  id="expectedGraduationYear"
                  options={graduationYearItems}
                  value={data.expectedGraduationYear != null ? String(data.expectedGraduationYear) : null}
                  placeholder="Chưa xác định"
                  ariaLabel="Năm tốt nghiệp dự kiến"
                  onChange={(v) => onUpdate({ expectedGraduationYear: v ? Number(v) : null })}
                />
              </FormSection>

              {/* Hệ chương trình chỉ có ở cấp 3 */}
              {isHighSchool && (
                <FormSection title="Hệ chương trình">
                  <ChoicePicker
                    id="educationSystem"
                    options={EDUCATION_SYSTEM_OPTIONS}
                    value={data.educationSystem}
                    placeholder="Chưa chọn"
                    ariaLabel="Hệ chương trình"
                    onChange={(v) => onUpdate({ educationSystem: (v || null) as EducationSystem | null })}
                  />
                </FormSection>
              )}
            </>
          )}
        </FormGroup>

        <FormGroup title="Mục tiêu du học">
        {/* Kỳ nhập học — chọn kỳ rồi chọn năm, giống câu B1.4 bên chat */}
        <FormSection title="Bạn muốn nhập học kỳ nào?">
            <ChoicePicker
              id="targetIntakeTerm"
              options={INTAKE_TERM_OPTIONS}
              value={intakeTerm || null}
              ariaLabel="Kỳ nhập học"
              onChange={(v) => updateIntake(v ?? "", intakeYear)}
            />
        </FormSection>
        <FormSection title="Năm nào?">
            <ChoicePicker
              id="targetIntakeYear"
              options={intakeYearItems}
              value={intakeYear || null}
              ariaLabel="Năm nhập học"
              onChange={(v) => updateIntake(intakeTerm, v ?? "")}
            />
        </FormSection>

        {/* Câu 3: ngành học mong muốn (bắt buộc, trừ khi chọn cần tư vấn) */}
        <FormSection title="Bạn muốn học ngành gì?" required error={majorErrorText} wide>
          <PickerPanel
            id={majorInputId}
            ariaLabel="Ngành học"
            searchPlaceholder="Tìm ngành, ví dụ: máy tính, kinh tế"
            loading={!metadata}
            value={selectedMajor?.value}
            onChange={handleSelectMajor}
            groups={[
              { label: "Mọi người hay chọn", options: popularMajors },
              { label: "Toàn bộ các ngành", options: allMajors },
            ]}
            footer={
              <ActionRow checked={majorUnknown} disabled={needMajor} onClick={handleMajorUnknown}>
                <span className="block">{MAJOR_GUIDANCE}</span>
                {needMajor && (
                  <span className="block text-xs font-normal text-ink-2">
                    Cần bỏ Lộ trình và Gợi ý trường ở Nhu cầu của bạn
                  </span>
                )}
              </ActionRow>
            }
          />
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
        </FormSection>

        </FormGroup>

        {/* Thu hẹp gợi ý — không bắt buộc */}
        <FormGroup title="Thu hẹp gợi ý trường">

              {/* Bang mong muốn: chọn nhiều, bấm để bật/tắt; ngoài các bang hay chọn thì tìm trong cả 51 bang */}
              <FormSection title="Bang mong muốn" description="Chọn một hoặc nhiều bang, bỏ trống nếu bang nào cũng được.">
                <div ref={stateDropdownRef} className="relative">
                  <div role="group" aria-label="Bang mong muốn" className="flex flex-wrap items-center gap-2">
                    {stateChips.map((s) => (
                      <button
                        key={s.code}
                        type="button"
                        role="checkbox"
                        aria-checked={selectedStates.includes(s.code)}
                        onClick={() => toggleState(s.code)}
                        className={CHOICE_CHIP}
                      >
                        {s.name}
                      </button>
                    ))}
                    <button
                      type="button"
                      onClick={() => setShowStateDropdown(!showStateDropdown)}
                      aria-expanded={showStateDropdown}
                      className={cn(CHOICE_CHIP, "gap-1.5 text-brand")}
                    >
                      Tìm trong 51 bang
                      <ChevronDownIcon className={cn("size-3.5 transition-transform", showStateDropdown && "rotate-180")} />
                    </button>
                  </div>

                  {showStateDropdown && (
                    <div className="absolute z-30 mt-2 flex max-h-72 w-full max-w-md flex-col overflow-hidden rounded-xl border border-line bg-surface shadow-md">
                      <div className="border-b border-line p-2">
                        <div className="relative">
                          <SearchIcon className="pointer-events-none absolute top-2.5 left-3 size-4 text-ink-2" />
                          <Input
                            placeholder="Tên bang hoặc mã, ví dụ CA"
                            aria-label="Tìm bang"
                            value={stateSearch}
                            onChange={(e) => setStateSearch(e.target.value)}
                            className="h-9 bg-surface pr-2 pl-9"
                            autoFocus
                          />
                        </div>
                      </div>

                      <div className="max-h-56 space-y-2 overflow-y-auto p-1.5">
                        {stateGroups.map((group) => (
                          <div key={group.value || "other"}>
                            <p className="px-2.5 pb-1 text-xs font-medium text-ink-2">{group.label}</p>
                            <div className="grid grid-cols-2 gap-1">
                              {group.states.map((s) => {
                                const isSelected = selectedStates.includes(s.code);
                                return (
                                  <button
                                    key={s.code}
                                    type="button"
                                    role="checkbox"
                                    aria-checked={isSelected}
                                    onClick={() => toggleState(s.code)}
                                    className={cn(
                                      "flex cursor-pointer items-center justify-between rounded-md px-2.5 py-1.5 text-left text-xs focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
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
              </FormSection>

              {/* Loại trường (Công lập / Tư thục) */}
              <FormSection title="Loại hình trường">
                <ChoicePicker
                  options={SCHOOL_TYPE_OPTIONS}
                  value={data.targetSchoolType ?? "all"}
                  ariaLabel="Loại hình trường"
                  onChange={(v) => v && onUpdate({ targetSchoolType: v as (typeof SCHOOL_TYPE_OPTIONS)[number]["value"] })}
                />
              </FormSection>
        </FormGroup>
      </FormBody>

      <StepNav>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </>
  );
}
