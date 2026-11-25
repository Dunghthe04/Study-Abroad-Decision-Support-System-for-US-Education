"use client";

import { Input } from "@/components/ui/input";
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

// Bỏ mục "Chưa rõ" (value rỗng) của form quản trị; KIND_OPTIONS đã loại "external"
export const KIND_FILTER_OPTIONS = KIND_OPTIONS.filter((o) => o.value);
export const COVERAGE_FILTER_OPTIONS = COVERAGE_OPTIONS.filter((o) => o.value);

// Điểm của người dùng: lọc học bổng có yêu cầu tối thiểu không vượt quá điểm này
export const MY_GPA_OPTIONS = ["2.5", "3", "3.3", "3.5", "3.7", "4"].map((v) => ({ value: v, label: `GPA ${Number(v).toFixed(1)}` }));
export const MY_IELTS_OPTIONS = ["5.5", "6", "6.5", "7", "7.5", "8"].map((v) => ({ value: v, label: `IELTS ${Number(v).toFixed(1)}` }));
export const MY_TOEFL_OPTIONS = ["60", "70", "80", "90", "100", "110"].map((v) => ({ value: v, label: `TOEFL ${v}` }));
export const MY_DUOLINGO_OPTIONS = ["90", "100", "110", "120", "130", "140"].map((v) => ({ value: v, label: `Duolingo ${v}` }));

const AMOUNT_MAX = 100_000;
const usd = (n: number) => `$${n.toLocaleString("en-US")}`;

interface ScholarshipFiltersProps {
  values: ScholarshipSearchParams;
  hasCustomDeadline?: boolean;
  onChange: (patch: Partial<ScholarshipSearchParams>) => void;
  className?: string;
}

export function ScholarshipFilters({ values, hasCustomDeadline = false, onChange, className }: ScholarshipFiltersProps) {
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
  const toefl = values.maxMinToefl !== undefined ? String(values.maxMinToefl) : undefined;
  const duolingo = values.maxMinDuolingo !== undefined ? String(values.maxMinDuolingo) : undefined;

  return (
    <div className={cn("min-w-0 space-y-4 overflow-x-hidden", className)}>
      <FilterCategory>
        <BooleanFilterRow
          label="Chỉ học bổng mở cho người Việt"
          checked={values.vnEligible !== false}
          onCheckedChange={(checked) => onChange({ vnEligible: checked ? true : false })}
        />
        {choice("Loại học bổng", KIND_FILTER_OPTIONS, values.kind, (kind) => onChange({ kind }), true)}
        {choice("Phần được hỗ trợ", COVERAGE_FILTER_OPTIONS, values.coverageType, (coverageType) => onChange({ coverageType }))}
        <BooleanFilterRow
          label="Hiện cả học bổng đã hết hạn"
          checked={values.includeExpired === true}
          onCheckedChange={(checked) => onChange({ includeExpired: checked ? true : undefined, deadlineFrom: undefined })}
        />
        <div className="space-y-1.5 pt-1">
          <label htmlFor="scholarship-deadline-from" className="block text-sm font-medium text-foreground">
            Hạn nộp từ ngày
          </label>
          <Input
            id="scholarship-deadline-from"
            type="date"
            value={hasCustomDeadline ? (values.deadlineFrom ?? "") : ""}
            onChange={(e) => {
              const next = e.target.value || undefined;
              onChange({ deadlineFrom: next, includeExpired: next ? undefined : values.includeExpired });
            }}
            className="bg-surface text-sm"
          />
        </div>
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
        {choice(
          "TOEFL iBT của bạn",
          MY_TOEFL_OPTIONS,
          toefl,
          (next) => onChange({ maxMinToefl: next === undefined ? undefined : Number(next) }),
          false,
          toefl ? `Đủ TOEFL ${toefl}` : null,
        )}
        {choice(
          "Duolingo của bạn",
          MY_DUOLINGO_OPTIONS,
          duolingo,
          (next) => onChange({ maxMinDuolingo: next === undefined ? undefined : Number(next) }),
          false,
          duolingo ? `Đủ Duolingo ${duolingo}` : null,
        )}
      </FilterCategory>
    </div>
  );
}
