"use client";

import { useState } from "react";
import { cn } from "cn";
import { Spinner } from "@/components/ui/spinner";
import type { LabeledOption } from "@/lib/admin-university-display";
import { OptionSelect } from "../universities/OptionSelect";

const BORDERLESS =
  "border-transparent bg-transparent shadow-none hover:border-border hover:bg-card focus-visible:bg-card aria-expanded:border-border aria-expanded:bg-card dark:bg-transparent dark:hover:bg-input/30";

interface InlineSelectCellProps {
  value: string;
  options: LabeledOption[];
  label: string;
  onChange: (value: string) => Promise<void>;
  className?: string;
  renderOption?: (option: LabeledOption) => React.ReactNode;
}

/** Ô bảng mở dropdown ngay tại dòng; bấm vào ô không làm chọn dòng. Lỗi do `onChange` tự xử lý. */
export function InlineSelectCell({ value, options, label, onChange, className, renderOption }: InlineSelectCellProps) {
  const [isPending, setIsPending] = useState(false);

  const handleChange = async (next: string) => {
    if (next === value || isPending) {
      return;
    }
    setIsPending(true);
    try {
      await onChange(next);
    } finally {
      setIsPending(false);
    }
  };

  return (
    <div
      className="flex items-center gap-1.5"
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => event.stopPropagation()}
    >
      <OptionSelect
        size="sm"
        value={value}
        options={options}
        onChange={(next) => void handleChange(next)}
        disabled={isPending}
        aria-label={label}
        className={cn(BORDERLESS, className)}
        renderOption={renderOption}
      />
      {isPending && <Spinner />}
    </div>
  );
}
