"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { LabeledOption } from "@/lib/admin-university-display";
import { cn } from "cn";

interface OptionSelectProps {
  id?: string;
  value: string;
  options: LabeledOption[];
  onChange: (value: string) => void;
  disabled?: boolean;
  invalid?: boolean;
  size?: "sm" | "default";
  className?: string;
  "aria-label"?: string;
}

/** Select của shadcn với danh sách lựa chọn dạng { value, label }; value "" dùng cho "Chưa rõ"/"Tất cả". */
export function OptionSelect({
  id,
  value,
  options,
  onChange,
  disabled,
  invalid,
  size = "default",
  className,
  "aria-label": ariaLabel,
}: OptionSelectProps) {
  const handleValueChange = (next: string | null) => {
    onChange(next ?? "");
  };

  return (
    <Select items={options} value={value} onValueChange={handleValueChange} disabled={disabled}>
      <SelectTrigger
        id={id}
        size={size}
        aria-label={ariaLabel}
        aria-invalid={invalid || undefined}
        className={cn("w-full", className)}
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
