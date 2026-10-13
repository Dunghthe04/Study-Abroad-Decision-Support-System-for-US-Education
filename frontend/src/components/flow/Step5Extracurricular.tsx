"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, AwardIcon, PlusIcon, Trash2Icon, UsersIcon, CalendarIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MonthPicker, type MonthValue } from "@/components/ui/month-picker";
import { cn } from "@/lib/utils";
import {
  ACTIVITY_ROLE_OPTIONS,
  IMPACT_LEVEL_OPTIONS,
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
function getItemProblem(item: ActivityDraft): string | null {
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
  const [formError, setFormError] = useState<string | null>(null);
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
    setFormError(null);
  };

  const handleSaveItem = () => {
    if (!name.trim()) return;
    if (!impactLevel) {
      setFormError("Vui lòng chọn phạm vi ảnh hưởng.");
      return;
    }

    const id =
      typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString();
    let newItem: ActivityDraft;

    if (category === "achievement") {
      const parsedYear = Number(year);
      if (!Number.isInteger(parsedYear) || parsedYear > currentYear) {
        setFormError(`Năm đạt giải không được lớn hơn năm hiện tại (${currentYear}).`);
        return;
      }
      if (parsedYear < 2000) {
        setFormError("Năm đạt giải không hợp lệ (từ năm 2000 trở lại đây).");
        return;
      }
      newItem = { id, name: name.trim(), category, impactLevel, year: parsedYear };
    } else {
      if (!role) {
        setFormError("Vui lòng chọn vai trò của bạn.");
        return;
      }
      const start = startDate;
      if (!start) {
        setFormError("Vui lòng chọn tháng bắt đầu.");
        return;
      }
      if (start.year * 12 + start.month > currentMonthIndex) {
        setFormError("Tháng bắt đầu không được sau tháng hiện tại.");
        return;
      }
      const end = isOngoing ? null : endDate;
      if (!isOngoing) {
        if (!end) {
          setFormError("Vui lòng chọn tháng kết thúc, hoặc tick “Đến nay”.");
          return;
        }
        if (end.year * 12 + end.month < start.year * 12 + start.month) {
          setFormError("Tháng kết thúc phải sau hoặc bằng tháng bắt đầu.");
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

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 5/6 · khoảng 2 phút
        </div>
        <h1 className="text-h1 text-foreground">Bạn đã làm gì ngoài giờ học?</h1>
        <p className="text-body text-muted-foreground">
          Thêm lần lượt từng hoạt động, câu lạc bộ hoặc giải thưởng. Chọn mốc thời gian từ lúc nào đến lúc nào (có tùy chọn đến nay).
        </p>
      </div>

      {/* List of Added Items */}
      {activities.length > 0 && (
        <section className="space-y-3">
          <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Đã thêm ({activities.length} mục)
          </Label>

          <div className="space-y-2.5">
            {activities.map((item, idx) => {
              const problem = getItemProblem(item);
              return (
                <div
                  key={item.id ?? idx}
                  className={cn(
                    "flex items-center justify-between rounded-xl border bg-surface p-4 shadow-2xs hover:border-brand/40 transition-colors",
                    problem ? "border-destructive/50" : "border-line"
                  )}
                >
                  <div className="flex items-start gap-3">
                    <div
                      className={cn(
                        "mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg text-xs font-bold",
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
                        {item.impactLevel && (
                          <Badge variant="outline" className="text-[10px]">
                            {labelOf(IMPACT_LEVEL_OPTIONS, item.impactLevel)}
                          </Badge>
                        )}
                        {problem && (
                          <Badge variant="destructive" className="text-[10px]">
                            Thiếu: {problem}
                          </Badge>
                        )}
                      </div>

                      <div className="text-xs text-muted-foreground mt-1 flex items-center gap-2">
                        {item.category === "activity" ? (
                          <>
                            <span>{labelOf(ACTIVITY_ROLE_OPTIONS, item.role) || "Chưa chọn vai trò"}</span>
                            <span>•</span>
                            <span className="inline-flex items-center gap-1 font-medium text-foreground">
                              <CalendarIcon className="size-3 text-brand" />
                              {item.startDate ?? "?"} – {item.isOngoing ? "Đến nay" : item.endDate || "?"}
                              {item.durationMonths ? ` · ${item.durationMonths} tháng` : ""}
                            </span>
                          </>
                        ) : (
                          <span>Năm {item.year ?? "?"}</span>
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
        <section className="rounded-2xl border border-brand/40 bg-surface p-5 sm:p-6 space-y-5 shadow-xs">
          <div className="flex items-center justify-between border-b border-line pb-3">
            <div className="font-semibold text-sm text-foreground">Bạn muốn thêm gì?</div>
            <div className="flex gap-1 rounded-lg bg-ground p-1 border border-line">
              {(["activity", "achievement"] as const).map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => {
                    setCategory(c);
                    setFormError(null);
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
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="itemName" className="text-xs font-medium text-foreground">
                Tên {category === "activity" ? "hoạt động / câu lạc bộ / dự án" : "giải thưởng"}
              </Label>
              <Input
                id="itemName"
                placeholder={
                  category === "activity"
                    ? "VD: CLB Robotics, Chiến dịch Mùa hè xanh, Ban tổ chức Prom..."
                    : "VD: Giải Nhì Olympic Tin học, Huy chương Đồng Bơi lội..."
                }
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>

            {category === "activity" ? (
              <div className="space-y-3">
                <div className="space-y-1.5">
                  <Label className="text-xs font-medium text-foreground">Vai trò của bạn · Chọn 1</Label>
                  <div className="flex flex-wrap gap-2">
                    {ACTIVITY_ROLE_OPTIONS.map((r) => (
                      <button
                        key={r.value}
                        type="button"
                        onClick={() => {
                          setRole(r.value);
                          setFormError(null);
                        }}
                        className={cn(
                          "rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
                          role === r.value
                            ? "border-brand bg-brand-wash/60 text-brand ring-1 ring-brand font-semibold"
                            : "border-line bg-surface text-muted-foreground hover:border-brand/40"
                        )}
                      >
                        {r.label}
                      </button>
                    ))}
                  </div>
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
                        // Tháng kết thúc cũ trước tháng bắt đầu mới thì bỏ, để chọn lại
                        if (endDate && endDate.year * 12 + endDate.month < m.year * 12 + m.month) setEndDate(null);
                        setFormError(null);
                      }}
                    />
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
                          onChange={(e) => setIsOngoing(e.target.checked)}
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
                        setFormError(null);
                      }}
                    />
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
                    setFormError(null);
                  }}
                />
              </div>
            )}

            {/* Phạm vi ảnh hưởng — 5 mức, DB lưu impact_level 1..5 */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium text-foreground">Phạm vi ảnh hưởng · Chọn 1</Label>
              <div className="flex flex-wrap gap-2">
                {IMPACT_LEVEL_OPTIONS.map((lvl) => {
                  const isSelected = impactLevel === lvl.value;
                  return (
                    <button
                      key={lvl.value}
                      type="button"
                      onClick={() => {
                        setImpactLevel(lvl.value);
                        setFormError(null);
                      }}
                      className={cn(
                        "flex items-center rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
                        isSelected
                          ? "border-brand bg-brand-wash/60 text-brand ring-1 ring-brand font-semibold"
                          : "border-line bg-surface text-muted-foreground hover:border-brand/40"
                      )}
                    >
                      <span
                        className={cn(
                          "mr-1.5 flex size-3 items-center justify-center rounded-full border transition-all",
                          isSelected ? "border-brand bg-brand" : "border-muted-foreground/50 bg-surface"
                        )}
                      >
                        {isSelected && <span className="size-1 rounded-full bg-white" />}
                      </span>
                      {lvl.label}
                    </button>
                  );
                })}
              </div>
            </div>
          </div>

          {formError && <div className="text-xs text-destructive font-medium">{formError}</div>}

          {/* Form Actions */}
          <div className="flex justify-end gap-2 border-t border-line pt-3">
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
              disabled={!name.trim()}
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
