"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, AwardIcon, PlusIcon, Trash2Icon, UsersIcon, CalendarIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import type { ActivityDraft } from "@/types/profile-flow";

interface Step5ExtracurricularProps {
  activities: ActivityDraft[];
  onUpdate: (activities: ActivityDraft[]) => void;
  onPrev: () => void;
  onNext: () => void;
}

const levelOptions = [
  { key: "school", label: "Cấp trường" },
  { key: "province", label: "Tỉnh / Thành" },
  { key: "national", label: "Quốc gia" },
  { key: "international", label: "Quốc tế" },
];

export function Step5Extracurricular({
  activities,
  onUpdate,
  onPrev,
  onNext,
}: Step5ExtracurricularProps) {
  const [showAddForm, setShowAddForm] = useState(false);
  const [category, setCategory] = useState<"activity" | "achievement">("activity");
  const [name, setName] = useState("");
  const [role, setRole] = useState("");
  const [startDate, setStartDate] = useState("09/2024");
  const [endDate, setEndDate] = useState("05/2026");
  const [isOngoing, setIsOngoing] = useState(false);
  const [impactLevel, setImpactLevel] = useState("school");
  const [year, setYear] = useState(new Date().getFullYear().toString());

  // Helper tính số năm từ khoảng thời gian
  const estimateDurationYears = (start: string, end: string, ongoing: boolean): number => {
    try {
      const startParts = start.split("/");
      const startYr = Number(startParts[startParts.length - 1]);
      const currentYear = new Date().getFullYear();
      const endYr = ongoing
        ? currentYear
        : Number(end.split("/")[end.split("/").length - 1]) || currentYear;
      const diff = endYr - startYr;
      return diff > 0 ? diff : 1;
    } catch {
      return 1;
    }
  };

  const handleSaveItem = () => {
    if (!name.trim()) return;

    const currentYearStr = `${new Date().getMonth() + 1}/${new Date().getFullYear()}`;
    const calculatedDuration = estimateDurationYears(startDate, endDate, isOngoing);

    const newItem: ActivityDraft = {
      id: typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(),
      name: name.trim(),
      category,
      role: category === "activity" ? (role.trim() || "Thành viên") : undefined,
      startDate: category === "activity" ? startDate.trim() : undefined,
      endDate: category === "activity" ? (isOngoing ? currentYearStr : endDate.trim()) : undefined,
      isOngoing: category === "activity" ? isOngoing : undefined,
      durationYears: category === "activity" ? calculatedDuration : undefined,
      impactLevel,
      year: category === "achievement" ? Number(year) || new Date().getFullYear() : undefined,
    };

    onUpdate([...activities, newItem]);

    // Reset form
    setName("");
    setRole("");
    setIsOngoing(false);
    setShowAddForm(false);
  };

  const handleDeleteItem = (index: number) => {
    onUpdate(activities.filter((_, i) => i !== index));
  };

  const getLevelLabel = (key: string) => {
    return levelOptions.find((l) => l.key === key)?.label ?? key;
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
            {activities.map((item, idx) => (
              <div
                key={item.id ?? idx}
                className="flex items-center justify-between rounded-xl border border-line bg-surface p-4 shadow-2xs hover:border-brand/40 transition-colors"
              >
                <div className="flex items-start gap-3">
                  <div
                    className={cn(
                      "mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg text-xs font-bold",
                      item.category === "activity"
                        ? "bg-brand-wash text-brand"
                        : "bg-warn-wash text-warn"
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
                      <Badge variant="outline" className="text-[10px]">
                        {getLevelLabel(item.impactLevel)}
                      </Badge>
                    </div>

                    <div className="text-xs text-muted-foreground mt-1 flex items-center gap-2">
                      {item.category === "activity" ? (
                        <>
                          <span>{item.role || "Thành viên"}</span>
                          <span>•</span>
                          <span className="inline-flex items-center gap-1 font-medium text-foreground">
                            <CalendarIcon className="size-3 text-brand" />
                            {item.startDate ? (
                              <>
                                {item.startDate} – {item.isOngoing ? "Đến nay" : item.endDate || "Nay"}
                              </>
                            ) : (
                              <>{item.durationYears || 1} năm</>
                            )}
                          </span>
                        </>
                      ) : (
                        <span>Năm {item.year}</span>
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
            ))}
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
              <button
                type="button"
                onClick={() => setCategory("activity")}
                className={cn(
                  "rounded-md px-3 py-1 text-xs font-semibold transition-all cursor-pointer",
                  category === "activity"
                    ? "bg-brand text-white shadow-xs"
                    : "text-muted-foreground hover:text-foreground"
                )}
              >
                Hoạt động
              </button>
              <button
                type="button"
                onClick={() => setCategory("achievement")}
                className={cn(
                  "rounded-md px-3 py-1 text-xs font-semibold transition-all cursor-pointer",
                  category === "achievement"
                    ? "bg-brand text-white shadow-xs"
                    : "text-muted-foreground hover:text-foreground"
                )}
              >
                Giải thưởng
              </button>
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
                  <Label htmlFor="itemRole" className="text-xs font-medium text-foreground">
                    Vai trò của bạn
                  </Label>
                  <Input
                    id="itemRole"
                    placeholder="VD: Đội trưởng, Trưởng ban Truyền thông, Thành viên"
                    value={role}
                    onChange={(e) => setRole(e.target.value)}
                  />
                </div>

                {/* Khoảng thời gian: Từ lúc nào đến lúc nào + Đến nay */}
                <div className="grid gap-3 sm:grid-cols-2 pt-1">
                  <div className="space-y-1.5">
                    <Label htmlFor="itemStartDate" className="text-xs font-medium text-foreground">
                      Từ (Tháng/Năm)
                    </Label>
                    <Input
                      id="itemStartDate"
                      placeholder="VD: 09/2023"
                      value={startDate}
                      onChange={(e) => setStartDate(e.target.value)}
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
                    <Input
                      id="itemEndDate"
                      placeholder="VD: 05/2025"
                      value={isOngoing ? "Đến nay (Hiện tại)" : endDate}
                      disabled={isOngoing}
                      onChange={(e) => setEndDate(e.target.value)}
                      className={cn(isOngoing && "bg-muted text-muted-foreground")}
                    />
                  </div>
                </div>
              </div>
            ) : (
              <div className="space-y-1.5 max-w-xs">
                <Label htmlFor="itemYear" className="text-xs font-medium text-foreground">
                  Năm đạt giải
                </Label>
                <Input
                  id="itemYear"
                  type="number"
                  min={2018}
                  max={2030}
                  value={year}
                  onChange={(e) => setYear(e.target.value)}
                />
              </div>
            )}

            {/* Impact Level Chips with Radio Dots */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium text-foreground">Cấp độ · Chọn 1 cấp độ</Label>
              <div className="flex flex-wrap gap-2">
                {levelOptions.map((lvl) => {
                  const isSelected = impactLevel === lvl.key;
                  return (
                    <button
                      key={lvl.key}
                      type="button"
                      onClick={() => setImpactLevel(lvl.key)}
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
                          isSelected
                            ? "border-brand bg-brand"
                            : "border-muted-foreground/50 bg-surface"
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

          {/* Form Actions */}
          <div className="flex justify-end gap-2 border-t border-line pt-3">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowAddForm(false)}
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

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5 cursor-pointer">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={onNext} size="lg" className="gap-1.5 cursor-pointer">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
