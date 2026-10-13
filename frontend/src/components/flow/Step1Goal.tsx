"use client";

import { useId, useState } from "react";
import { ArrowRightIcon, CheckIcon, MapPinIcon, SearchIcon, XIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
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

export function Step1Goal({ data, metadata, onUpdate, onNext }: Step1GoalProps) {
  const [majorQuery, setMajorQuery] = useState(data.targetMajor ?? "");
  const [showMajorDropdown, setShowMajorDropdown] = useState(false);
  const [stateSearch, setStateSearch] = useState("");
  const [showAllStates, setShowAllStates] = useState(false);
  const majorInputId = useId();

  // 51 States from metadata (with fallback popular states)
  const allStates = metadata?.states ?? [];
  const popularStates = allStates.filter((s) => s.isPopular);
  const selectedStates = data.targetStates ?? [];

  // 366 majors filtered
  const allMajors = metadata?.offeringMajors ?? [];
  const filteredMajors = majorQuery.trim()
    ? allMajors
        .filter((m) => m.toLowerCase().includes(majorQuery.toLowerCase()))
        .slice(0, 15)
    : allMajors.slice(0, 15);

  const toggleState = (code: string) => {
    const next = selectedStates.includes(code)
      ? selectedStates.filter((c) => c !== code)
      : [...selectedStates, code];
    onUpdate({ targetStates: next });
  };

  const removeState = (code: string) => {
    onUpdate({ targetStates: selectedStates.filter((c) => c !== code) });
  };

  const handleSelectMajor = (major: string) => {
    setMajorQuery(major);
    onUpdate({ targetMajor: major });
    setShowMajorDropdown(false);
  };

  const studyLevels = metadata?.studyLevels ?? [
    { key: "community_college", label: "Cao đẳng", description: "Community college · 2 năm", durationYears: 2, defaultGradingScale: "scale_10", allowedGradingScales: [] },
    { key: "undergraduate", label: "Đại học", description: "Bachelor · 4 năm", durationYears: 4, defaultGradingScale: "scale_10", allowedGradingScales: [] },
    { key: "master", label: "Thạc sĩ", description: "Master · 1–2 năm", durationYears: 2, defaultGradingScale: "scale_10", allowedGradingScales: [] },
    { key: "phd", label: "Tiến sĩ", description: "PhD · 4–6 năm", durationYears: 5, defaultGradingScale: "scale_10", allowedGradingScales: [] },
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
          Vài câu hỏi nhanh để chúng tôi biết nên gợi ý trường nào phù hợp nhất với bạn.
        </p>
      </div>

      {/* Question 1: Current Education */}
      <section className="space-y-4">
        <Label className="text-sm font-semibold text-foreground">
          Hiện tại bạn đang học ở đâu?
        </Label>
        <div className="flex flex-wrap gap-2">
          {currentLevelOptions.map((opt) => {
            const isSelected = data.currentEducationLevel === opt;
            return (
              <button
                key={opt}
                type="button"
                onClick={() => onUpdate({ currentEducationLevel: opt })}
                className={cn(
                  "rounded-full border px-4 py-2 text-sm font-medium transition-all cursor-pointer",
                  isSelected
                    ? "border-brand bg-brand text-white shadow-xs"
                    : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                )}
              >
                {opt}
              </button>
            );
          })}
        </div>

        {/* Grade, School Name & Expected Graduation Year */}
        <div className="grid gap-4 sm:grid-cols-3 pt-2">
          <div className="space-y-1.5">
            <Label htmlFor="currentGrade" className="text-xs text-muted-foreground">
              Lớp / Năm học
            </Label>
            <Input
              id="currentGrade"
              placeholder="VD: Lớp 11, Năm 2"
              value={data.currentGrade ?? ""}
              onChange={(e) => onUpdate({ currentGrade: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="currentSchool" className="text-xs text-muted-foreground">
              Trường đang học
            </Label>
            <Input
              id="currentSchool"
              placeholder="VD: THPT Chuyên Hà Nội – Amsterdam"
              value={data.currentSchool ?? ""}
              onChange={(e) => onUpdate({ currentSchool: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="expectedGraduationYear" className="text-xs text-muted-foreground">
              Năm tốt nghiệp dự kiến
            </Label>
            <Input
              id="expectedGraduationYear"
              type="number"
              min={2020}
              max={2035}
              placeholder="VD: 2026"
              value={data.expectedGraduationYear ?? ""}
              onChange={(e) =>
                onUpdate({
                  expectedGraduationYear: e.target.value ? Number(e.target.value) : undefined,
                })
              }
            />
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
                  "flex flex-col items-start justify-between rounded-xl border p-4 text-left transition-all cursor-pointer h-full",
                  isSelected
                    ? "border-brand bg-brand-wash/40 ring-2 ring-brand text-brand-ink"
                    : "border-line bg-surface hover:border-brand/40 hover:bg-muted/30"
                )}
              >
                <div className="font-semibold text-sm text-foreground">{lvl.label}</div>
                <div className="text-xs text-muted-foreground mt-1">{lvl.description}</div>
              </button>
            );
          })}
        </div>
      </section>

      {/* Question 3: Narrowing Down Filters (Optional) */}
      <section className="space-y-5 rounded-2xl border border-line bg-ground/50 p-5">
        <div>
          <div className="flex items-center gap-2">
            <Label className="text-sm font-semibold text-foreground">
              Thu hẹp gợi ý
            </Label>
            <Badge variant="outline" className="text-xs font-normal">
              Không bắt buộc
            </Badge>
          </div>
          <p className="text-xs text-muted-foreground mt-0.5">
            Bỏ trống mục nào thì hệ thống không lọc theo mục đó.
          </p>
        </div>

        {/* Ngành học (Searchable) */}
        <div className="space-y-1.5 relative">
          <Label htmlFor={majorInputId} className="text-xs font-medium text-foreground">
            Ngành học mong muốn ({allMajors.length > 0 ? `${allMajors.length} ngành` : "Đang tải..."})
          </Label>
          <div className="relative">
            <SearchIcon className="absolute left-3 top-2.5 size-4 text-muted-foreground pointer-events-none" />
            <Input
              id={majorInputId}
              placeholder="VD: Khoa học máy tính (Computer Science), Quản trị kinh doanh..."
              value={majorQuery}
              onFocus={() => setShowMajorDropdown(true)}
              onChange={(e) => {
                setMajorQuery(e.target.value);
                onUpdate({ targetMajor: e.target.value });
                setShowMajorDropdown(true);
              }}
              className="pl-9 pr-8"
            />
            {majorQuery && (
              <button
                type="button"
                onClick={() => {
                  setMajorQuery("");
                  onUpdate({ targetMajor: undefined });
                }}
                className="absolute right-2.5 top-2.5 text-muted-foreground hover:text-foreground cursor-pointer"
              >
                <XIcon className="size-4" />
              </button>
            )}
          </div>

          {/* Autocomplete suggestions */}
          {showMajorDropdown && filteredMajors.length > 0 && (
            <div className="absolute z-20 mt-1 max-h-56 w-full overflow-auto rounded-lg border border-line bg-surface p-1 shadow-lg">
              {filteredMajors.map((m) => (
                <button
                  key={m}
                  type="button"
                  onClick={() => handleSelectMajor(m)}
                  className="w-full text-left rounded-md px-3 py-1.5 text-xs text-foreground hover:bg-brand-wash hover:text-brand cursor-pointer"
                >
                  {m}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Bang (Popular chips + Full 51 States) */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label className="text-xs font-medium text-foreground flex items-center gap-1.5">
              <MapPinIcon className="size-3.5 text-brand" />
              Bang mong muốn · Chọn được nhiều bang
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
                    className="inline-flex items-center gap-1 rounded-md bg-brand-wash px-2 py-0.5 text-xs font-medium text-brand-ink"
                  >
                    {s?.name ?? code}
                    <button
                      type="button"
                      onClick={() => removeState(code)}
                      className="hover:text-destructive cursor-pointer"
                    >
                      <XIcon className="size-3" />
                    </button>
                  </span>
                );
              })}
            </div>
          )}

          {/* Popular State Chips */}
          <div className="flex flex-wrap gap-1.5">
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
              onClick={() => setShowAllStates(!showAllStates)}
              className="rounded-md border border-dashed border-line px-2.5 py-1 text-xs text-brand hover:border-brand cursor-pointer"
            >
              {showAllStates ? "Thu gọn bang" : "+ Xem tất cả 51 bang"}
            </button>
          </div>

          {/* Expanded 51 states picker */}
          {showAllStates && (
            <div className="mt-2 space-y-2 rounded-xl border border-line bg-surface p-3">
              <Input
                placeholder="Tìm tên bang..."
                value={stateSearch}
                onChange={(e) => setStateSearch(e.target.value)}
                className="h-8 text-xs"
              />
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-1.5 max-h-48 overflow-y-auto pt-1">
                {allStates
                  .filter((s) =>
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
                          "flex items-center justify-between rounded px-2 py-1 text-xs text-left cursor-pointer",
                          isSelected
                            ? "bg-brand text-white font-medium"
                            : "hover:bg-muted text-muted-foreground hover:text-foreground"
                        )}
                      >
                        <span className="truncate">{s.name}</span>
                        {isSelected && <CheckIcon className="size-3 shrink-0 ml-1" />}
                      </button>
                    );
                  })}
              </div>
            </div>
          )}
        </div>

        {/* Loại trường (Công lập / Tư thục) */}
        <div className="space-y-1.5">
          <Label className="text-xs font-medium text-foreground">Loại trường</Label>
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
                    "rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
                    isSelected
                      ? "border-brand bg-brand text-white"
                      : "border-line bg-surface text-muted-foreground hover:border-brand/40"
                  )}
                >
                  {opt.label}
                </button>
              );
            })}
          </div>
        </div>
      </section>

      {/* Bottom Actions */}
      <div className="flex justify-end pt-4">
        <Button onClick={onNext} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
