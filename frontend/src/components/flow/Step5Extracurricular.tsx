"use client";

import { useState } from "react";
import {
  ArrowLeftIcon,
  ArrowRightIcon,
  AwardIcon,
  CalendarIcon,
  CheckCircle2Icon,
  ChevronDownIcon,
  ChevronUpIcon,
  InfoIcon,
  PlusIcon,
  SparklesIcon,
  Trash2Icon,
  UsersIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { MonthPicker, type MonthValue } from "@/components/ui/month-picker";
import { cn } from "@/lib/utils";
import {
  ACTIVITY_ROLE_OPTIONS,
  IMPACT_LEVEL_OPTIONS,
  IMPACT_PYRAMID_LEVELS,
  IMPACT_TIER_DETAILS,
  countMonths,
  labelOf,
  parseMonthYear,
} from "@/lib/profile-flow-options";
import type { ActivityDraft, ActivityRole, ImpactLevel } from "@/types/profile-flow";

interface Step5ExtracurricularProps {
  activities: ActivityDraft[];
  onUpdate: (activities: ActivityDraft[]) => void;
  onPrev: () => void;
  onNext: () => void;
}

/** Thiếu gì so với điều kiện commit phía BE (ProfileFlowService.ValidateForCommit). */
export function getItemProblem(item: ActivityDraft): string | null {
  if (!item.impactLevel) return "chưa chọn phạm vi";
  if (item.category === "achievement") return item.year ? null : "chưa có năm đạt giải";
  if (!item.role) return "chưa chọn vai trò";
  if (!parseMonthYear(item.startDate)) return "chưa có tháng bắt đầu";
  if (!item.isOngoing && !parseMonthYear(item.endDate)) return "chưa có tháng kết thúc";
  return null;
}

const formatMonthYear = (m: { year: number; month: number }) =>
  `${String(m.month).padStart(2, "0")}/${m.year}`;

export function Step5Extracurricular({
  activities,
  onUpdate,
  onPrev,
  onNext,
}: Step5ExtracurricularProps) {
  const [showAddForm, setShowAddForm] = useState(false);
  const [category, setCategory] = useState<"activity" | "achievement">("activity");
  const [name, setName] = useState("");
  const [role, setRole] = useState<ActivityRole | null>(null);
  const [startDate, setStartDate] = useState<MonthValue | null>(null);
  const [endDate, setEndDate] = useState<MonthValue | null>(null);
  const [isOngoing, setIsOngoing] = useState(false);
  const [impactLevel, setImpactLevel] = useState<ImpactLevel | null>(null);
  const [year, setYear] = useState(new Date().getFullYear().toString());
  const [description, setDescription] = useState("");

  // Lỗi theo từng ô của form thêm mục, hiện ngay dưới ô đó
  const [formErrors, setFormErrors] = useState<
    Partial<Record<"impact" | "year" | "role" | "start" | "end" | "desc", string>>
  >({});
  const [listError, setListError] = useState<string | null>(null);

  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonthIndex = currentYear * 12 + now.getMonth() + 1;
  const thisMonth: MonthValue = { year: currentYear, month: now.getMonth() + 1 };

  const resetForm = () => {
    setName("");
    setRole(null);
    setStartDate(null);
    setEndDate(null);
    setIsOngoing(false);
    setImpactLevel(null);
    setYear(currentYear.toString());
    setDescription("");
    setFormErrors({});
  };

  // Đủ các trường bắt buộc mới cho bấm "Thêm vào hồ sơ"; lỗi khoảng thời gian vẫn báo khi bấm
  const canSave =
    Boolean(name.trim()) &&
    Boolean(impactLevel) &&
    (category === "achievement"
      ? Boolean(year)
      : Boolean(role) && Boolean(startDate) && (isOngoing || Boolean(endDate)));

  const handleSaveItem = () => {
    if (!canSave) return;
    if (!impactLevel) {
      setFormErrors({ impact: "Vui lòng chọn phạm vi ảnh hưởng trên kim tự tháp." });
      return;
    }

    if (description.trim().length > 500) {
      setFormErrors({ desc: "Mô tả tối đa 500 ký tự." });
      return;
    }

    const id =
      typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString();
    let newItem: ActivityDraft;

    const trimmedDesc = description.trim() ? description.trim() : null;

    if (category === "achievement") {
      const parsedYear = Number(year);
      if (!Number.isInteger(parsedYear) || parsedYear > currentYear) {
        setFormErrors({ year: `Năm đạt giải không được lớn hơn năm hiện tại (${currentYear}).` });
        return;
      }
      if (parsedYear < 2000) {
        setFormErrors({ year: "Năm đạt giải không hợp lệ (từ năm 2000 trở lại đây)." });
        return;
      }
      newItem = {
        id,
        name: name.trim(),
        category,
        impactLevel,
        year: parsedYear,
        description: trimmedDesc,
      };
    } else {
      if (!role) {
        setFormErrors({ role: "Vui lòng chọn vai trò của bạn." });
        return;
      }
      const start = startDate;
      if (!start) {
        setFormErrors({ start: "Vui lòng chọn tháng bắt đầu." });
        return;
      }
      if (start.year * 12 + start.month > currentMonthIndex) {
        setFormErrors({ start: "Tháng bắt đầu không được sau tháng hiện tại." });
        return;
      }
      const end = isOngoing ? null : endDate;
      if (!isOngoing) {
        if (!end) {
          setFormErrors({ end: "Vui lòng chọn tháng kết thúc, hoặc tick “Đến nay”." });
          return;
        }
        if (end.year * 12 + end.month < start.year * 12 + start.month) {
          setFormErrors({ end: "Tháng kết thúc phải sau hoặc bằng tháng bắt đầu." });
          return;
        }
      }
      const startStr = formatMonthYear(start);
      const endStr = end ? formatMonthYear(end) : null;
      newItem = {
        id,
        name: name.trim(),
        category,
        impactLevel,
        role,
        startDate: startStr,
        endDate: endStr,
        isOngoing,
        durationMonths: countMonths(startStr, endStr, isOngoing),
        description: trimmedDesc,
      };
    }

    onUpdate([...activities, newItem]);
    setListError(null);
    resetForm();
    setShowAddForm(false);
  };

  const handleDeleteItem = (index: number) => {
    onUpdate(activities.filter((_, i) => i !== index));
    setListError(null);
  };

  const handleNextClick = () => {
    const bad = activities.find((a) => getItemProblem(a));
    if (bad) {
      setListError(`“${bad.name}” ${getItemProblem(bad)}. Hãy xoá mục này rồi thêm lại cho đủ thông tin.`);
      return;
    }
    setListError(null);
    onNext();
  };

  // Điều khiển lên/xuống bậc kim tự tháp giống CollegeVine
  const handleStepUp = () => {
    if (!impactLevel) {
      setImpactLevel(IMPACT_PYRAMID_LEVELS[IMPACT_PYRAMID_LEVELS.length - 1]); // mặc định đáy
      return;
    }
    const idx = IMPACT_PYRAMID_LEVELS.indexOf(impactLevel);
    if (idx > 0) {
      setImpactLevel(IMPACT_PYRAMID_LEVELS[idx - 1]);
      setFormErrors({});
    }
  };

  const handleStepDown = () => {
    if (!impactLevel) {
      setImpactLevel(IMPACT_PYRAMID_LEVELS[IMPACT_PYRAMID_LEVELS.length - 1]);
      return;
    }
    const idx = IMPACT_PYRAMID_LEVELS.indexOf(impactLevel);
    if (idx < IMPACT_PYRAMID_LEVELS.length - 1) {
      setImpactLevel(IMPACT_PYRAMID_LEVELS[idx + 1]);
      setFormErrors({});
    }
  };

  const selectedTierDetail = impactLevel ? IMPACT_TIER_DETAILS[impactLevel] : null;

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 5/6 · khoảng 2 phút
        </div>
        <h1 className="text-h1 text-foreground">Bạn đã làm gì ngoài giờ học?</h1>
        <p className="text-body text-muted-foreground">
          Thêm lần lượt từng hoạt động, câu lạc bộ hoặc giải thưởng. Đánh giá phạm vi ảnh hưởng theo cấu trúc kim tự tháp và mô tả đóng góp nổi bật của bạn.
        </p>
      </div>

      {/* List of Added Items */}
      {activities.length > 0 && (
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Đã thêm ({activities.length} mục)
            </Label>
            <span className="text-[11px] text-muted-foreground">
              Tối đa 10 mục tiêu biểu
            </span>
          </div>

          <div className="space-y-3">
            {activities.map((item, idx) => {
              const problem = getItemProblem(item);
              const tierInfo = item.impactLevel ? IMPACT_TIER_DETAILS[item.impactLevel] : null;
              return (
                <div
                  key={item.id ?? idx}
                  className={cn(
                    "rounded-xl border bg-surface p-4 shadow-2xs hover:border-brand/40 transition-all space-y-2.5",
                    problem ? "border-destructive/50" : "border-line"
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-3">
                      <div
                        className={cn(
                          "mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-xl text-xs font-bold shadow-2xs",
                          item.category === "activity" ? "bg-brand-wash text-brand" : "bg-warn-wash text-warn"
                        )}
                      >
                        {item.category === "activity" ? (
                          <UsersIcon className="size-4" />
                        ) : (
                          <AwardIcon className="size-4" />
                        )}
                      </div>

                      <div>
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-semibold text-sm text-foreground">{item.name}</span>
                          <Badge variant="secondary" className="text-[10px] font-medium">
                            {item.category === "activity" ? "Hoạt động" : "Giải thưởng"}
                          </Badge>
                          {tierInfo && (
                            <Badge
                              variant="outline"
                              className={cn(
                                "text-[10px] font-semibold gap-1",
                                item.impactLevel === "international" && "border-purple-300 bg-purple-50 text-purple-700 dark:bg-purple-950/40 dark:text-purple-300",
                                item.impactLevel === "national" && "border-amber-300 bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300",
                                item.impactLevel === "province" && "border-sky-300 bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-300",
                                item.impactLevel === "district" && "border-teal-300 bg-teal-50 text-teal-700 dark:bg-teal-950/40 dark:text-teal-300",
                                item.impactLevel === "school" && "border-slate-300 bg-slate-50 text-slate-700 dark:bg-slate-900 dark:text-slate-300"
                              )}
                            >
                              <span>{tierInfo.icon}</span>
                              <span>{tierInfo.shortLabel}</span>
                            </Badge>
                          )}
                          {problem && (
                            <Badge variant="destructive" className="text-[10px]">
                              Thiếu: {problem}
                            </Badge>
                          )}
                        </div>

                        <div className="text-xs text-muted-foreground mt-1 flex items-center gap-2 flex-wrap">
                          {item.category === "activity" ? (
                            <>
                              <span className="font-medium text-foreground">
                                {labelOf(ACTIVITY_ROLE_OPTIONS, item.role) || "Chưa chọn vai trò"}
                              </span>
                              <span>·</span>
                              <span className="inline-flex items-center gap-1 font-medium text-foreground">
                                <CalendarIcon className="size-3 text-brand" />
                                {item.startDate ?? "?"} – {item.isOngoing ? "nay" : item.endDate || "?"}
                                {item.durationMonths ? ` (${item.durationMonths} tháng)` : ""}
                              </span>
                            </>
                          ) : (
                            <span className="font-medium text-foreground">Năm {item.year ?? "?"}</span>
                          )}
                          {tierInfo && (
                            <>
                              <span>·</span>
                              <span className="text-muted-foreground">{tierInfo.badge}</span>
                            </>
                          )}
                        </div>
                      </div>
                    </div>

                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleDeleteItem(idx)}
                      className="text-muted-foreground hover:text-destructive h-8 px-2 cursor-pointer"
                    >
                      <Trash2Icon className="size-4" />
                      <span className="hidden sm:inline text-xs ml-1">Xóa</span>
                    </Button>
                  </div>

                  {/* Hiển thị description nếu có */}
                  {item.description && (
                    <div className="rounded-lg bg-ground/70 border border-line/60 p-2.5 text-xs text-muted-foreground italic border-l-2 border-l-brand/50 pl-3">
                      &ldquo;{item.description}&rdquo;
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </section>
      )}

      {/* Add New Item Form / Toggle */}
      {!showAddForm ? (
        <Button
          type="button"
          variant="outline"
          onClick={() => setShowAddForm(true)}
          className="w-full py-6 border-dashed border-2 hover:border-brand hover:bg-brand-wash/20 text-brand font-semibold text-sm gap-2 cursor-pointer shadow-2xs"
        >
          <PlusIcon className="size-4" />
          Thêm hoạt động hoặc giải thưởng mới
        </Button>
      ) : (
        <section className="rounded-2xl border border-brand/40 bg-surface p-5 sm:p-6 space-y-6 shadow-xs">
          <div className="flex items-center justify-between border-b border-line pb-3">
            <div className="font-semibold text-sm text-foreground">Bạn muốn thêm gì?</div>
            <div className="flex gap-1 rounded-lg bg-ground p-1 border border-line">
              {(["activity", "achievement"] as const).map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => {
                    setCategory(c);
                    setFormErrors({});
                  }}
                  className={cn(
                    "rounded-md px-3 py-1 text-xs font-semibold transition-all cursor-pointer",
                    category === c ? "bg-brand text-white shadow-xs" : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  {c === "activity" ? "Hoạt động" : "Giải thưởng"}
                </button>
              ))}
            </div>
          </div>

          {/* Form Fields */}
          <div className="space-y-5">
            {/* Tên hoạt động / giải thưởng */}
            <div className="space-y-1.5">
              <Label htmlFor="itemName" className="text-xs font-medium text-foreground">
                Tên {category === "activity" ? "hoạt động / câu lạc bộ / dự án" : "giải thưởng"}
              </Label>
              <Input
                id="itemName"
                placeholder={
                  category === "activity"
                    ? "VD: CLB Robotics, Dự án Mùa Hè Xanh, Ban tổ chức Prom, Model UN..."
                    : "VD: Giải Nhì HSG môn Tin học, Huy chương Đồng Bơi lội, Giải Ba Sáng tạo KHKT..."
                }
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>

            {/* Thuộc tính theo Category */}
            {category === "activity" ? (
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label className="text-xs font-medium text-foreground">Vai trò của bạn · Chọn 1</Label>
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                    {ACTIVITY_ROLE_OPTIONS.map((r) => (
                      <button
                        key={r.value}
                        type="button"
                        onClick={() => {
                          setRole(r.value);
                          setFormErrors({});
                        }}
                        className={cn(
                          "rounded-lg border px-3 py-2 text-xs font-medium transition-all cursor-pointer text-center",
                          role === r.value
                            ? "border-brand bg-brand-wash text-brand ring-1 ring-brand font-semibold shadow-2xs"
                            : "border-line bg-surface text-muted-foreground hover:border-brand/40"
                        )}
                      >
                        {r.label}
                      </button>
                    ))}
                  </div>
                  <FieldError message={formErrors.role} />
                </div>

                {/* Khoảng thời gian: Từ lúc nào đến lúc nào + Đến nay */}
                <div className="grid gap-3 sm:grid-cols-2 pt-1">
                  <div className="space-y-1.5">
                    <Label htmlFor="itemStartDate" className="text-xs font-medium text-foreground">
                      Từ (Tháng/Năm)
                    </Label>
                    <MonthPicker
                      id="itemStartDate"
                      placeholder="Chọn tháng bắt đầu"
                      value={startDate}
                      max={thisMonth}
                      onChange={(m) => {
                        setStartDate(m);
                        if (endDate && endDate.year * 12 + endDate.month < m.year * 12 + m.month) setEndDate(null);
                        setFormErrors({});
                      }}
                    />
                    <FieldError message={formErrors.start} />
                  </div>

                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <Label htmlFor="itemEndDate" className="text-xs font-medium text-foreground">
                        Đến (Tháng/Năm)
                      </Label>
                      <label className="flex items-center gap-1.5 cursor-pointer text-xs font-medium text-brand">
                        <input
                          type="checkbox"
                          checked={isOngoing}
                          onChange={(e) => {
                            setIsOngoing(e.target.checked);
                            setFormErrors({});
                          }}
                          className="rounded border-line text-brand focus:ring-brand size-3.5 cursor-pointer"
                        />
                        <span>Đến nay</span>
                      </label>
                    </div>
                    <MonthPicker
                      id="itemEndDate"
                      placeholder="Chọn tháng kết thúc"
                      value={endDate}
                      min={startDate}
                      max={thisMonth}
                      disabled={isOngoing}
                      disabledText="Đến nay (Hiện tại)"
                      onChange={(m) => {
                        setEndDate(m);
                        setFormErrors({});
                      }}
                    />
                    <FieldError message={formErrors.end} />
                  </div>
                </div>
              </div>
            ) : (
              <div className="space-y-1.5 max-w-xs">
                <Label htmlFor="itemYear" className="text-xs font-medium text-foreground">
                  Năm đạt giải (tối đa {currentYear})
                </Label>
                <Input
                  id="itemYear"
                  type="number"
                  min={2000}
                  max={currentYear}
                  value={year}
                  onChange={(e) => {
                    setYear(e.target.value);
                    setFormErrors({});
                  }}
                />
                <FieldError message={formErrors.year} />
              </div>
            )}

            {/* ==================================================== */}
            {/* KIM TỰ THÁP PHẠM VI ẢNH HƯỞNG (COLLEGEVINE PYRAMID) */}
            {/* ==================================================== */}
            <div className="space-y-3 pt-2">
              <div className="flex items-center justify-between">
                <div>
                  <Label className="text-xs font-semibold text-foreground flex items-center gap-1.5">
                    <span>Kim tự tháp phạm vi ảnh hưởng</span>
                    <span className="text-brand font-normal text-[11px]">(Mô hình CollegeVine)</span>
                  </Label>
                  <p className="text-[11px] text-muted-foreground mt-0.5">
                    Bấm vào tầng phù hợp nhất với tầm vóc của mục này, hoặc dùng phím mũi tên.
                  </p>
                </div>

                {/* Nút điều hướng nấc ↑ ↓ kiểu CollegeVine */}
                <div className="flex items-center gap-1 bg-ground border border-line rounded-lg p-0.5">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-7 text-muted-foreground hover:text-foreground cursor-pointer"
                    onClick={handleStepUp}
                    title="Lên nấc cao hơn"
                  >
                    <ChevronUpIcon className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-7 text-muted-foreground hover:text-foreground cursor-pointer"
                    onClick={handleStepDown}
                    title="Xuống nấc thấp hơn"
                  >
                    <ChevronDownIcon className="size-4" />
                  </Button>
                </div>
              </div>

              {/* Graphic Pyramid */}
              <div className="rounded-2xl border border-line/80 bg-gradient-to-b from-surface to-ground/50 p-4 space-y-2">
                <div className="flex flex-col items-center gap-1.5 w-full max-w-xl mx-auto py-1">
                  {IMPACT_PYRAMID_LEVELS.map((levelKey, tierIdx) => {
                    const detail = IMPACT_TIER_DETAILS[levelKey];
                    const isSelected = impactLevel === levelKey;

                    // Chiều rộng theo tầng: Bậc 5 (Quốc tế) nhỏ nhất ở đỉnh -> Bậc 1 (Trường) rộng nhất ở đáy
                    const widthClasses = [
                      "w-[66%] sm:w-[62%]", // Quốc tế (đỉnh)
                      "w-[74%] sm:w-[71%]", // Quốc gia
                      "w-[82%] sm:w-[80%]", // Tỉnh / TP
                      "w-[91%] sm:w-[90%]", // Quận / Huyện
                      "w-full",              // Trường (đáy)
                    ];

                    return (
                      <button
                        key={levelKey}
                        type="button"
                        onClick={() => {
                          setImpactLevel(levelKey);
                          setFormErrors({});
                        }}
                        className={cn(
                          "group relative flex items-center justify-between rounded-xl px-3 py-2 text-xs font-medium transition-all duration-150 cursor-pointer border shadow-2xs",
                          widthClasses[tierIdx],
                          isSelected
                            ? "border-brand bg-brand-wash/80 text-foreground ring-2 ring-brand shadow-xs scale-[1.01]"
                            : "border-line bg-surface/90 text-muted-foreground hover:border-brand/40 hover:bg-surface hover:text-foreground"
                        )}
                      >
                        <div className="flex items-center gap-2">
                          <span className="text-base select-none">{detail.icon}</span>
                          <span className={cn("font-medium", isSelected && "font-bold text-brand-ink")}>
                            {detail.label}
                          </span>
                        </div>

                        <div className="flex items-center gap-2">
                          <span
                            className={cn(
                              "text-[10px] px-1.5 py-0.5 rounded-md font-semibold",
                              isSelected
                                ? "bg-brand text-white"
                                : "bg-ground text-muted-foreground group-hover:text-foreground"
                            )}
                          >
                            {detail.badge}
                          </span>
                          {isSelected && (
                            <CheckCircle2Icon className="size-3.5 text-brand shrink-0" />
                          )}
                        </div>
                      </button>
                    );
                  })}
                </div>

                {/* Box hướng dẫn chi tiết theo tầng đang chọn (Criterion details) */}
                {selectedTierDetail ? (
                  <div className="rounded-xl border border-brand/20 bg-brand-wash/40 p-3.5 space-y-2 text-xs mt-3 animate-in fade-in-50 duration-150">
                    <div className="flex items-center justify-between">
                      <div className="font-semibold text-brand flex items-center gap-1.5">
                        <SparklesIcon className="size-3.5 text-brand" />
                        <span>Tiêu chí tham chiếu: {selectedTierDetail.label}</span>
                      </div>
                      <Badge variant="outline" className="text-[10px] font-semibold border-brand/30 bg-surface">
                        {selectedTierDetail.badge}
                      </Badge>
                    </div>

                    <p className="text-muted-foreground text-[11px] leading-relaxed">
                      {selectedTierDetail.summary}
                    </p>

                    <div className="space-y-1.5 pt-1">
                      <div className="font-medium text-foreground text-[11px]">
                        Ví dụ {category === "activity" ? "hoạt động phù hợp" : "giải thưởng phù hợp"}:
                      </div>
                      <ul className="list-disc list-inside space-y-1 text-muted-foreground text-[11px] pl-1">
                        {(category === "activity"
                          ? selectedTierDetail.activityCriteria
                          : selectedTierDetail.awardCriteria
                        ).map((criterion, i) => (
                          <li key={i} className="leading-snug">{criterion}</li>
                        ))}
                      </ul>
                    </div>

                    <div className="text-[10px] text-muted-foreground/90 border-t border-brand/10 pt-2 flex items-center gap-1">
                      <InfoIcon className="size-3 text-brand shrink-0" />
                      <span>
                        Mẹo: Nếu phân vân giữa 2 mức (ví dụ Cấp Trường hay Cấp Quận), bạn có thể tự tin chọn mức cao hơn.
                      </span>
                    </div>
                  </div>
                ) : (
                  <div className="rounded-xl border border-dashed border-line bg-surface/50 p-3 text-center text-xs text-muted-foreground">
                    👆 Hãy bấm chọn một tầng trên kim tự tháp để xem tiêu chí và ví dụ chi tiết.
                  </div>
                )}

                <FieldError message={formErrors.impact} />
              </div>
            </div>

            {/* ==================================================== */}
            {/* MÔ TẢ THÊM (DESCRIPTION - TÙY CHỌN THEO COLLEGEVINE) */}
            {/* ==================================================== */}
            <div className="space-y-1.5 pt-1">
              <div className="flex items-center justify-between">
                <Label htmlFor="itemDesc" className="text-xs font-medium text-foreground flex items-center gap-1.5">
                  <span>Mô tả thêm (không bắt buộc)</span>
                  <span className="text-[11px] text-muted-foreground font-normal">
                    · Giúp tăng độ tin cậy và điểm hồ sơ
                  </span>
                </Label>
                <span className="text-[10px] text-muted-foreground">
                  {description.length}/500 ký tự
                </span>
              </div>
              <Textarea
                id="itemDesc"
                rows={3}
                maxLength={500}
                placeholder={
                  category === "activity"
                    ? "Tóm tắt vai trò cụ thể, thành tích nổi bật hoặc số liệu (VD: dẫn dắt 20 thành viên, tổ chức 2 sự kiện từ thiện, quyên góp 15 triệu VNĐ, quản lý trang mạng xã hội 5.000 người theo dõi...)"
                    : "Tóm tắt thông tin giải (VD: Đạt giải Nhì môn Tin học kỳ thi chọn HSG cấp Tỉnh năm 2025 với số điểm 18.5/20, lọt vào top 5 học sinh có điểm cao nhất tỉnh...)"
                }
                value={description}
                onChange={(e) => {
                  setDescription(e.target.value);
                  setFormErrors({});
                }}
                className="text-xs resize-none"
              />
              <p className="text-[10px] text-muted-foreground">
                Gợi ý: Nêu rõ kết quả có số liệu (quantifiable results), vai trò và kỹ năng đạt được.
              </p>
              <FieldError message={formErrors.desc} />
            </div>
          </div>

          {/* Form Actions */}
          <div className="flex justify-end gap-2 border-t border-line pt-4">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                setShowAddForm(false);
                resetForm();
              }}
              className="cursor-pointer"
            >
              Hủy
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={handleSaveItem}
              disabled={!canSave}
              className="cursor-pointer font-semibold"
            >
              Thêm vào hồ sơ
            </Button>
          </div>
        </section>
      )}

      {listError && (
        <div className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive font-medium border border-destructive/20">
          {listError}
        </div>
      )}

      {activities.length === 0 && !showAddForm && (
        <p className="text-center text-xs text-muted-foreground">
          Chưa có gì để điền? Bạn có thể bấm Tiếp tục và bổ sung sau.
        </p>
      )}

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5 cursor-pointer">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5 cursor-pointer">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs font-medium text-destructive">{message}</p>;
}
