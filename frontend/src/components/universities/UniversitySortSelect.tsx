"use client";

import { ArrowUpDownIcon } from "lucide-react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

interface UniversitySortSelectProps {
  value: string;
  onValueChange: (value: string) => void;
}

export const SORT_OPTIONS = [
  { value: "rank", label: "Thứ hạng tốt nhất" },
  { value: "name", label: "Tên trường (A → Z)" },
  { value: "tuition_asc", label: "Học phí: Thấp đến cao" },
  { value: "tuition_desc", label: "Học phí: Cao đến thấp" },
  { value: "acceptance_asc", label: "Cạnh tranh nhất (Tỷ lệ nhận thấp)" },
  { value: "acceptance_desc", label: "Dễ trúng tuyển (Tỷ lệ nhận cao)" },
];

export function UniversitySortSelect({ value, onValueChange }: UniversitySortSelectProps) {
  return (
    <div className="flex items-center gap-2">
      <span className="text-xs font-medium text-muted-foreground whitespace-nowrap hidden sm:inline-flex items-center gap-1">
        <ArrowUpDownIcon className="size-3.5" />
        Sắp xếp:
      </span>
      <Select
        value={value || "rank"}
        onValueChange={(val) => {
          if (val !== null) onValueChange(val);
        }}
        items={SORT_OPTIONS}
      >
        <SelectTrigger className="h-9 w-[200px] text-xs font-medium bg-background">
          <SelectValue placeholder="Sắp xếp theo..." />
        </SelectTrigger>
        <SelectContent align="end">
          {SORT_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value} className="text-xs">
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
