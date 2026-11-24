"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { UniversityFilterOptionItem } from "@/types/university";

interface UniversitySortSelectProps {
  value: string;
  onValueChange: (value: string) => void;
  rankingLists?: UniversityFilterOptionItem[];
  rankListId?: string;
  onRankListChange?: (rankListId: string) => void;
}

export const SORT_OPTIONS = [
  { value: "name", label: "Tên trường (A → Z)" },
  { value: "rank", label: "Thứ hạng tốt nhất" },
  { value: "tuition_asc", label: "Học phí: Thấp đến cao" },
  { value: "tuition_desc", label: "Học phí: Cao đến thấp" },
  { value: "total_cost_asc", label: "Tổng chi phí: Thấp đến cao" },
  { value: "total_cost_desc", label: "Tổng chi phí: Cao đến thấp" },
  { value: "acceptance_asc", label: "Cạnh tranh nhất (Tỷ lệ nhận thấp)" },
  { value: "acceptance_desc", label: "Dễ trúng tuyển (Tỷ lệ nhận cao)" },
];

export function UniversitySortSelect({
  value,
  onValueChange,
  rankingLists = [],
  rankListId,
  onRankListChange,
}: UniversitySortSelectProps) {
  const activeSort = value || "name";
  const showRankList = activeSort === "rank" && rankingLists.length > 0;
  const activeRankList =
    rankListId && rankingLists.some((r) => r.value === rankListId)
      ? rankListId
      : rankingLists[0]?.value;

  return (
    <div className="flex flex-wrap items-center gap-2.5">
      <span className="text-sm font-medium text-muted-foreground whitespace-nowrap">
        Sắp xếp theo
      </span>
      <Select
        value={activeSort}
        onValueChange={(val) => {
          if (val !== null) onValueChange(val);
        }}
        items={SORT_OPTIONS}
      >
        <SelectTrigger className="h-9! w-[240px] border-line bg-surface px-3 text-sm font-medium shadow-xs">
          <SelectValue placeholder="Sắp xếp theo..." />
        </SelectTrigger>
        <SelectContent align="end" className="min-w-64 w-max">
          {SORT_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value} className="text-sm">
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {showRankList && (
        <>
          <span className="text-sm font-medium text-muted-foreground whitespace-nowrap">
            trong BXH
          </span>
          <Select
            value={activeRankList}
            onValueChange={(val) => {
              if (val !== null) onRankListChange?.(val);
            }}
            items={rankingLists}
          >
            <SelectTrigger className="h-9! w-[240px] border-line bg-surface px-3 text-sm font-medium shadow-xs sm:w-[270px]">
              <SelectValue placeholder="Chọn bảng xếp hạng..." />
            </SelectTrigger>
            <SelectContent align="end" className="min-w-64 max-w-[90vw] w-max">
              {rankingLists.map((opt) => (
                <SelectItem key={opt.value} value={opt.value} className="text-sm">
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </>
      )}
    </div>
  );
}
