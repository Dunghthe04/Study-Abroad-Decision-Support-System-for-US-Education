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
  /** Cách hiển thị một lựa chọn (cả trong danh sách lẫn trên ô chọn), ví dụ nhãn màu theo trạng thái. */
  renderOption?: (option: LabeledOption) => React.ReactNode;
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
  renderOption,
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
        <SelectValue>
          {renderOption
            ? (selected: string) => {
                const option = options.find((item) => item.value === selected);
                return option ? renderOption(option) : null;
              }
            : undefined}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {renderOption ? renderOption(option) : option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
