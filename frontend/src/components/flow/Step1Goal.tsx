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
import { cn } from "@/lib/utils";
import type { GoalDraft, ProfileMetadataResponse } from "@/types/profile-flow";

interface Step1GoalProps {
  data: GoalDraft;
  metadata: ProfileMetadataResponse | null;
  onUpdate: (updated: Partial<GoalDraft>) => void;
  onNext: () => void;
}

const currentLevelOptions = [
  "Trung học cơ sở",
  "Trung học phổ thông",
  "Cao đẳng / Đại học",
  "Thạc sĩ",
];

const schoolTypeOptions = [
  { value: "all", label: "Không lọc" },
  { value: "public", label: "Công lập" },
  { value: "private", label: "Tư thục" },
];

const GRADUATION_YEAR_OPTIONS = [2026, 2027, 2028, 2029, 2030, 2031];

function getGradeOptionsForLevel(level?: string): string[] {
  switch (level) {
    case "Trung học cơ sở":
      return ["Lớp 6", "Lớp 7", "Lớp 8", "Lớp 9"];
    case "Trung học phổ thông":
      return ["Lớp 10", "Lớp 11", "Lớp 12"];
    case "Cao đẳng / Đại học":
      return ["Năm 1", "Năm 2", "Năm 3", "Năm 4", "Năm cuối", "Đã tốt nghiệp"];
    case "Thạc sĩ":
      return ["Năm 1", "Năm 2", "Đã tốt nghiệp"];
    default:
      return ["Lớp 10", "Lớp 11", "Lớp 12"];
  }
}

export function Step1Goal({ data, metadata, onUpdate, onNext }: Step1GoalProps) {
  const [majorQuery, setMajorQuery] = useState(data.targetMajor ?? "");
  const [showMajorDropdown, setShowMajorDropdown] = useState(false);
  const [majorError, setMajorError] = useState<string | null>(null);

  const [stateSearch, setStateSearch] = useState("");
  const [showStateDropdown, setShowStateDropdown] = useState(false);

  const majorInputId = useId();
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
    setMajorQuery(data.targetMajor ?? "");
  }

  // Grade options based on current level
  const gradeOptions = getGradeOptionsForLevel(data.currentEducationLevel);

  const handleSelectEducationLevel = (opt: string) => {
    const newGradeOptions = getGradeOptionsForLevel(opt);
    const defaultGrade =
      opt === "Trung học cơ sở"
        ? "Lớp 9"
        : opt === "Cao đẳng / Đại học"
        ? "Năm 2"
        : opt === "Thạc sĩ"
        ? "Năm 1"
        : "Lớp 11";
    const currentGradeStillValid =
      data.currentGrade && newGradeOptions.includes(data.currentGrade);
    onUpdate({
      currentEducationLevel: opt,
      currentGrade: currentGradeStillValid ? data.currentGrade : defaultGrade,
    });
  };

  // 51 States from metadata
  const allStates = metadata?.states ?? [];
  const popularStates = allStates.filter((s) => s.isPopular);
  const selectedStates = data.targetStates ?? [];

  const toggleState = (code: string) => {
    const next = selectedStates.includes(code)
      ? selectedStates.filter((c) => c !== code)
      : [...selectedStates, code];
    onUpdate({ targetStates: next });
  };

  const removeState = (code: string) => {
    onUpdate({ targetStates: selectedStates.filter((c) => c !== code) });
  };

  // 366 majors filtered
  const allMajors = metadata?.offeringMajors ?? [];
  const filteredMajors = majorQuery.trim()
    ? allMajors.filter((m) =>
        m.toLowerCase().includes(majorQuery.toLowerCase())
      )
    : allMajors;

  const handleSelectMajor = (major: string) => {
    setMajorQuery(major);
    onUpdate({ targetMajor: major });
    setMajorError(null);
    setShowMajorDropdown(false);
  };

  const handleNextClick = () => {
    if (!data.targetMajor || !data.targetMajor.trim()) {
      setMajorError("Vui lòng chọn hoặc nhập ngành học mong muốn trước khi tiếp tục.");
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

      {/* Question 1: Current Education */}
      <section className="space-y-4">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Hiện tại bạn đang học ở đâu?
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Chọn bậc học hiện tại để hệ thống điều chỉnh lớp học và năm học tương ứng.
          </p>
        </div>

        {/* Level pills with explicit single-select radio dots */}
        <div className="flex flex-wrap gap-2.5">
          {currentLevelOptions.map((opt) => {
            const isSelected = data.currentEducationLevel === opt;
            return (
              <button
                key={opt}
                type="button"
                onClick={() => handleSelectEducationLevel(opt)}
                className={cn(
                  "flex items-center rounded-full border px-4 py-2 text-sm font-medium transition-all cursor-pointer shadow-2xs",
                  isSelected
                    ? "border-brand bg-brand-wash/60 text-brand ring-1 ring-brand"
                    : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                )}
              >
                <span
                  className={cn(
                    "mr-2 flex size-3.5 items-center justify-center rounded-full border transition-all",
                    isSelected
                      ? "border-brand bg-brand"
                      : "border-muted-foreground/50 bg-surface"
                  )}
                >
                  {isSelected && <span className="size-1.5 rounded-full bg-white" />}
                </span>
                {opt}
              </button>
            );
          })}
        </div>

        {/* Dynamic Grade, School Name & Expected Graduation Year */}
        <div className="grid gap-4 sm:grid-cols-3 pt-2">
          {/* Lớp / Năm học (Dropdown động theo bậc học) */}
          <div className="space-y-1.5">
            <Label htmlFor="currentGrade" className="text-xs font-medium text-foreground">
              Lớp / Năm học hiện tại
            </Label>
            <div className="relative">
              <select
                id="currentGrade"
                value={data.currentGrade ?? gradeOptions[0]}
                onChange={(e) => onUpdate({ currentGrade: e.target.value })}
                className="w-full appearance-none rounded-lg border border-line bg-surface px-3 py-2 text-sm text-foreground shadow-2xs focus:border-brand focus:outline-hidden focus:ring-1 focus:ring-brand cursor-pointer"
              >
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

          {/* Năm tốt nghiệp dự kiến (Dropdown 5 năm từ năm hiện tại) */}
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
                      : undefined,
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
        </div>
      </section>

      {/* Question 2: Target Degree Level */}
      <section className="space-y-4 pt-2">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Bạn muốn học bậc nào ở Mỹ?
          </Label>
          <p className="text-xs text-muted-foreground mt-0.5">
            Chưa chắc? Chọn bậc gần nhất, bạn có thể đổi lại sau.
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {studyLevels.map((lvl) => {
            const isSelected = data.targetDegreeLevel === lvl.key;
            return (
              <button
                key={lvl.key}
                type="button"
                onClick={() => onUpdate({ targetDegreeLevel: lvl.key })}
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
      </section>

      {/* Question 3: Narrowing Down Filters (Ngành bắt buộc & Bang mong muốn) */}
      <section className="space-y-5 rounded-2xl border border-line bg-ground/50 p-5">
        <div>
          <div className="flex items-center gap-2">
            <Label className="text-sm font-semibold text-foreground">
              Ngành học & Tiêu chí trường
            </Label>
          </div>
          <p className="text-xs text-muted-foreground mt-0.5">
            Ngành học là yêu cầu bắt buộc để AI tìm kiếm và xếp hạng trường tương thích.
          </p>
        </div>

        {/* Ngành học mong muốn (BẮT BUỘC & Searchable Dropdown) */}
        <div ref={majorDropdownRef} className="space-y-1.5 relative">
          <div className="flex items-center justify-between">
            <Label
              htmlFor={majorInputId}
              className="text-xs font-semibold text-foreground flex items-center gap-1"
            >
              Ngành học mong muốn <span className="text-destructive font-bold">*</span>
              <span className="text-muted-foreground font-normal">
                ({allMajors.length > 0 ? `${allMajors.length} ngành` : "Đang tải danh sách..."})
              </span>
            </Label>
            {data.targetMajor && (
              <span className="text-xs text-brand font-medium">
                Đã chọn: <strong className="text-foreground">{data.targetMajor}</strong>
              </span>
            )}
          </div>

          <div className="relative">
            <SearchIcon className="absolute left-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
            <Input
              id={majorInputId}
              placeholder="Gõ để tìm kiếm hoặc nhấn mũi tên để chọn ngành..."
              value={majorQuery}
              onFocus={() => setShowMajorDropdown(true)}
              onChange={(e) => {
                setMajorQuery(e.target.value);
                setShowMajorDropdown(true);
                if (majorError) setMajorError(null);
              }}
              className={cn(
                "pl-9 pr-16 bg-surface",
                majorError && "border-destructive ring-1 ring-destructive"
              )}
            />
            <div className="absolute right-2 top-2 flex items-center gap-1">
              {majorQuery && (
                <button
                  type="button"
                  onClick={() => {
                    setMajorQuery("");
                    onUpdate({ targetMajor: undefined });
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

          {majorError && (
            <div className="flex items-center gap-1 text-xs text-destructive pt-0.5">
              <AlertCircleIcon className="size-3.5 shrink-0" />
              <span>{majorError}</span>
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
                  Không tìm thấy ngành nào khớp với &quot;{majorQuery}&quot;. Bạn có thể tiếp tục gõ để dùng tên ngành này.
                  <div className="mt-2">
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => handleSelectMajor(majorQuery.trim())}
                      className="text-xs h-7"
                    >
                      Dùng &quot;{majorQuery.trim()}&quot; làm ngành học
                    </Button>
                  </div>
                </div>
              ) : (
                filteredMajors.map((m) => {
                  const isSelected = data.targetMajor === m;
                  return (
                    <button
                      key={m}
                      type="button"
                      onClick={() => handleSelectMajor(m)}
                      className={cn(
                        "w-full text-left rounded-lg px-3 py-2 text-xs transition-colors flex items-center justify-between cursor-pointer",
                        isSelected
                          ? "bg-brand text-white font-medium"
                          : "text-foreground hover:bg-brand-wash hover:text-brand"
                      )}
                    >
                      <span>{m}</span>
                      {isSelected && <CheckIcon className="size-3.5 shrink-0" />}
                    </button>
                  );
                })
              )}
            </div>
          )}
        </div>

        {/* Bang mong muốn (Searchable Dropdown + Popular Chips) */}
        <div ref={stateDropdownRef} className="space-y-2 relative">
          <div className="flex items-center justify-between">
            <Label className="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <MapPinIcon className="size-3.5 text-brand" />
              Bang mong muốn (Mỹ) · Chọn được nhiều bang
            </Label>
            {selectedStates.length > 0 && (
              <span className="text-xs font-medium text-brand">
                Đã chọn {selectedStates.length} bang
              </span>
            )}
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

              <div className="overflow-y-auto max-h-48 p-1.5 grid grid-cols-2 gap-1">
                {allStates
                  .filter(
                    (s) =>
                      s.name.toLowerCase().includes(stateSearch.toLowerCase()) ||
                      s.code.toLowerCase().includes(stateSearch.toLowerCase())
                  )
                  .map((s) => {
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
          )}
        </div>

        {/* Loại trường (Công lập / Tư thục) */}
        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Loại hình trường</Label>
          <div className="flex gap-2">
            {schoolTypeOptions.map((opt) => {
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
