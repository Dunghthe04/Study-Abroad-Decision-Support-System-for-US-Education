"use client";

import { useId } from "react";
import { FilterIcon, RotateCcwIcon, SearchIcon, SparklesIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import type { UniversityFilterOptions, UniversitySearchParams } from "@/types/university";
import { cn } from "@/lib/utils";

interface UniversityFiltersProps {
  options: UniversityFilterOptions;
  values: UniversitySearchParams;
  onChange: (newValues: Partial<UniversitySearchParams>) => void;
  onReset: () => void;
  className?: string;
  isMobileModal?: boolean;
}

const IELTS_OPTIONS = [
  { value: "0", label: "Tất cả mức điểm" },
  { value: "6.0", label: "Tối đa 6.0" },
  { value: "6.5", label: "Tối đa 6.5" },
  { value: "7.0", label: "Tối đa 7.0" },
  { value: "7.5", label: "Tối đa 7.5" },
  { value: "8.0", label: "Tối đa 8.0" },
];

const MAX_COST_OPTIONS = [
  { value: "0", label: "Tất cả mức học phí" },
  { value: "25000", label: "Dưới $25,000 / năm" },
  { value: "40000", label: "Dưới $40,000 / năm" },
  { value: "55000", label: "Dưới $55,000 / năm" },
  { value: "70000", label: "Dưới $70,000 / năm" },
  { value: "85000", label: "Dưới $85,000 / năm" },
];

const ACCEPT_RATE_OPTIONS = [
  { value: "all", label: "Tất cả tỷ lệ" },
  { value: "very_high", label: "Trên 50% (Dễ trúng tuyển)" },
  { value: "high", label: "20% - 50% (Trung bình)" },
  { value: "selective", label: "10% - 20% (Chọn lọc cao)" },
  { value: "very_selective", label: "Dưới 10% (Cực kỳ cạnh tranh)" },
];

export function UniversityFilters({
  options,
  values,
  onChange,
  onReset,
  className,
  isMobileModal = false,
}: UniversityFiltersProps) {
  const ivySwitchId = useId();

  const handleAcceptRateChange = (val: string) => {
    if (val === "all") {
      onChange({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined });
    } else if (val === "very_high") {
      onChange({ minAcceptanceRate: 0.5, maxAcceptanceRate: undefined });
    } else if (val === "high") {
      onChange({ minAcceptanceRate: 0.2, maxAcceptanceRate: 0.5 });
    } else if (val === "selective") {
      onChange({ minAcceptanceRate: 0.1, maxAcceptanceRate: 0.2 });
    } else if (val === "very_selective") {
      onChange({ minAcceptanceRate: undefined, maxAcceptanceRate: 0.1 });
    }
  };

  const getAcceptRateValue = () => {
    const min = values.minAcceptanceRate;
    const max = values.maxAcceptanceRate;
    if (min === 0.5 && max === undefined) return "very_high";
    if (min === 0.2 && max === 0.5) return "high";
    if (min === 0.1 && max === 0.2) return "selective";
    if (min === undefined && max === 0.1) return "very_selective";
    return "all";
  };

  const activeFilterCount = [
    values.state && values.state !== "all",
    values.control && values.control !== "all",
    values.major && values.major !== "all",
    values.maxTuitionUsd && values.maxTuitionUsd > 0,
    values.maxMinIelts && values.maxMinIelts > 0,
    values.isIvy === true,
    values.minAcceptanceRate !== undefined || values.maxAcceptanceRate !== undefined,
  ].filter(Boolean).length;

  return (
    <div
      className={cn(
        "rounded-2xl border border-line bg-card p-5 shadow-sm space-y-6",
        className
      )}
    >
      <div className="flex items-center justify-between pb-1 border-b border-line">
        <div className="flex items-center gap-2 font-heading text-base font-bold text-foreground">
          <FilterIcon className="size-4 text-brand" />
          <span>Bộ lọc tìm kiếm</span>
          {activeFilterCount > 0 && (
            <Badge variant="secondary" className="bg-brand/10 text-brand font-semibold text-xs px-2 py-0.5">
              {activeFilterCount}
            </Badge>
          )}
        </div>
        {activeFilterCount > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onReset}
            className="h-8 px-2 text-xs text-muted-foreground hover:text-foreground gap-1"
          >
            <RotateCcwIcon className="size-3" />
            <span>Đặt lại</span>
          </Button>
        )}
      </div>

      <div className="space-y-4 text-sm">
        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Bang tại Mỹ (State)</Label>
          <Select
            value={values.state || "all"}
            onValueChange={(val) => {
              if (val !== null) onChange({ state: val === "all" ? undefined : val });
            }}
            items={[{ value: "all", label: "Tất cả các bang" }, ...options.states]}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Tất cả các bang" />
            </SelectTrigger>
            <SelectContent className="max-h-[260px]">
              <SelectItem value="all" className="text-xs">
                Tất cả các bang
              </SelectItem>
              {options.states.map((st) => (
                <SelectItem key={st.value} value={st.value} className="text-xs">
                  {st.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Loại hình trường</Label>
          <Select
            value={values.control || "all"}
            onValueChange={(val) => {
              if (val !== null) onChange({ control: val === "all" ? undefined : val });
            }}
            items={[{ value: "all", label: "Mọi loại hình" }, ...options.controls]}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Mọi loại hình" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all" className="text-xs">
                Mọi loại hình
              </SelectItem>
              {options.controls.map((ctl) => (
                <SelectItem key={ctl.value} value={ctl.value} className="text-xs">
                  {ctl.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Ngành đào tạo</Label>
          <Select
            value={values.major || "all"}
            onValueChange={(val) => {
              if (val !== null) onChange({ major: val === "all" ? undefined : val });
            }}
            items={[{ value: "all", label: "Tất cả ngành học" }, ...options.majors.map((m) => ({ value: m, label: m }))]}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Tất cả ngành học" />
            </SelectTrigger>
            <SelectContent className="max-h-[260px]">
              <SelectItem value="all" className="text-xs">
                Tất cả ngành học
              </SelectItem>
              {options.majors.map((major) => (
                <SelectItem key={major} value={major} className="text-xs">
                  {major}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <Separator className="my-2" />

        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Mức học phí tối đa</Label>
          <Select
            value={values.maxTuitionUsd ? values.maxTuitionUsd.toString() : "0"}
            onValueChange={(val) => {
              if (val !== null) {
                const num = parseInt(val, 10);
                onChange({ maxTuitionUsd: num > 0 ? num : undefined });
              }
            }}
            items={MAX_COST_OPTIONS}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Tất cả mức học phí" />
            </SelectTrigger>
            <SelectContent>
              {MAX_COST_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value} className="text-xs">
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Yêu cầu IELTS trường đòi hỏi</Label>
          <Select
            value={values.maxMinIelts ? values.maxMinIelts.toString() : "0"}
            onValueChange={(val) => {
              if (val !== null) {
                const num = parseFloat(val);
                onChange({ maxMinIelts: num > 0 ? num : undefined });
              }
            }}
            items={IELTS_OPTIONS}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Tất cả mức điểm" />
            </SelectTrigger>
            <SelectContent>
              {IELTS_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value} className="text-xs">
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">
            Lọc các trường có điểm sàn IELTS không vượt quá mức này.
          </p>
        </div>

        <div className="space-y-1.5">
          <Label className="text-xs font-semibold text-foreground">Tỷ lệ trúng tuyển</Label>
          <Select
            value={getAcceptRateValue()}
            onValueChange={(val) => {
              if (val !== null) handleAcceptRateChange(val);
            }}
            items={ACCEPT_RATE_OPTIONS}
          >
            <SelectTrigger className="w-full text-xs bg-background">
              <SelectValue placeholder="Tất cả tỷ lệ" />
            </SelectTrigger>
            <SelectContent>
              {ACCEPT_RATE_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value} className="text-xs">
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <Separator className="my-2" />

        <div className="flex items-center justify-between rounded-xl bg-muted/40 p-3">
          <div className="space-y-0.5">
            <Label htmlFor={ivySwitchId} className="text-xs font-semibold cursor-pointer">
              Ivy League
            </Label>
            <p className="text-xs text-muted-foreground">
              Chỉ 8 trường Ivy League danh giá
            </p>
          </div>
          <Switch
            id={ivySwitchId}
            checked={values.isIvy === true}
            onCheckedChange={(checked) => onChange({ isIvy: checked ? true : undefined })}
          />
        </div>
      </div>
    </div>
  );
}
