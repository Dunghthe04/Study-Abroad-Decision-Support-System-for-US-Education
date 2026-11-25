"use client";

import {
  BooleanFilterRow,
  CollapsibleFilter,
  CheckboxChoiceFilter,
  FilterCategory,
  RangeFilter,
  findLabel,
  formatRangeSummary,
} from "@/components/universities/UniversityFilters";
import { COVERAGE_OPTIONS, KIND_OPTIONS } from "@/lib/admin-scholarship-display";
import { cn } from "@/lib/utils";
import type { ScholarshipSearchParams } from "@/types/scholarship";

export const LEVEL_OPTIONS = [
  { value: "undergraduate", label: "Đại học" },
  { value: "graduate", label: "Sau đại học" },
];
// Bỏ mục "Chưa rõ" (value rỗng) của form quản trị
export const KIND_FILTER_OPTIONS = KIND_OPTIONS.filter((o) => o.value);
export const COVERAGE_FILTER_OPTIONS = COVERAGE_OPTIONS.filter((o) => o.value);

// Điểm của người dùng: lọc học bổng có yêu cầu tối thiểu không vượt quá điểm này
export const MY_GPA_OPTIONS = ["2.5", "3", "3.3", "3.5", "3.7", "4"].map((v) => ({ value: v, label: `GPA ${Number(v).toFixed(1)}` }));
export const MY_IELTS_OPTIONS = ["5.5", "6", "6.5", "7", "7.5", "8"].map((v) => ({ value: v, label: `IELTS ${Number(v).toFixed(1)}` }));

const AMOUNT_MAX = 100_000;
const usd = (n: number) => `$${n.toLocaleString("en-US")}`;

interface ScholarshipFiltersProps {
  values: ScholarshipSearchParams;
  onChange: (patch: Partial<ScholarshipSearchParams>) => void;
  className?: string;
}

export function ScholarshipFilters({ values, onChange, className }: ScholarshipFiltersProps) {
  const choice = (
    title: string,
    options: { value: string; label: string }[],
    value: string | undefined,
    onSelect: (next: string | undefined) => void,
    defaultOpen = false,
    summary?: string | null,
  ) => (
    <CollapsibleFilter
      title={title}
      activeSummary={summary !== undefined ? summary : findLabel(options, value)}
      onClear={() => onSelect(undefined)}
      defaultOpen={defaultOpen}
    >
      {(collapse) => (
        <CheckboxChoiceFilter
          label={title}
          options={options}
          value={value}
          onValueChange={(next) => {
            onSelect(next);
            if (next !== undefined) collapse();
          }}
        />
      )}
    </CollapsibleFilter>
  );

  const gpa = values.maxMinGpa4 !== undefined ? String(values.maxMinGpa4) : undefined;
  const ielts = values.maxMinIelts !== undefined ? String(values.maxMinIelts) : undefined;

  return (
    <div className={cn("min-w-0 space-y-4 overflow-x-hidden", className)}>
      <FilterCategory>
        {choice("Bậc học", LEVEL_OPTIONS, values.studyLevel, (studyLevel) => onChange({ studyLevel }), true)}
        {choice("Loại học bổng", KIND_FILTER_OPTIONS, values.kind, (kind) => onChange({ kind }))}
        {choice("Phần được hỗ trợ", COVERAGE_FILTER_OPTIONS, values.coverageType, (coverageType) => onChange({ coverageType }))}
        <BooleanFilterRow
          label="Hiện cả học bổng đã hết hạn"
          checked={values.includeExpired === true}
          onCheckedChange={(checked) => onChange({ includeExpired: checked ? true : undefined })}
        />
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Giá trị học bổng (USD)"
          activeSummary={formatRangeSummary(values.minAmountUsd, values.maxAmountUsd, usd)}
          onClear={() => onChange({ minAmountUsd: undefined, maxAmountUsd: undefined })}
          defaultOpen
        >
          <RangeFilter
            label="Giá trị học bổng (USD)"
            minBound={0}
            maxBound={AMOUNT_MAX}
            step={1000}
            minValue={values.minAmountUsd}
            maxValue={values.maxAmountUsd}
            formatInput={usd}
            onCommit={(minAmountUsd, maxAmountUsd) => onChange({ minAmountUsd, maxAmountUsd })}
          />
        </CollapsibleFilter>
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        {choice(
          "GPA của bạn (thang 4)",
          MY_GPA_OPTIONS,
          gpa,
          (next) => onChange({ maxMinGpa4: next === undefined ? undefined : Number(next) }),
          false,
          gpa ? `Đủ GPA ${Number(gpa).toFixed(1)}` : null,
        )}
        {choice(
          "IELTS của bạn",
          MY_IELTS_OPTIONS,
          ielts,
          (next) => onChange({ maxMinIelts: next === undefined ? undefined : Number(next) }),
          false,
          ielts ? `Đủ IELTS ${Number(ielts).toFixed(1)}` : null,
        )}
      </FilterCategory>
    </div>
  );
}
