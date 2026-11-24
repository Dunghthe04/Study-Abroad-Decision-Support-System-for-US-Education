"use client";

import { useId, useState } from "react";
import { RotateCcwIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from "@/components/ui/combobox";
import type {
  UniversityFilterOptionItem,
  UniversityFilterOptions,
  UniversitySearchParams,
} from "@/types/university";
import { cn } from "@/lib/utils";

interface UniversityFiltersProps {
  options: UniversityFilterOptions;
  values: UniversitySearchParams;
  onChange: (newValues: Partial<UniversitySearchParams>) => void;
  onReset: () => void;
  className?: string;
}

const ALL = "all";

const CONTROL_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "public", label: "Công lập" },
  { value: "private", label: "Tư thục" },
];

const TUITION_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "25000", label: "Dưới $25,000" },
  { value: "40000", label: "Dưới $40,000" },
  { value: "55000", label: "Dưới $55,000" },
  { value: "70000", label: "Dưới $70,000" },
];

const IELTS_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "5.5", label: "5.5" },
  { value: "6", label: "6.0" },
  { value: "6.5", label: "6.5" },
  { value: "7", label: "7.0" },
  { value: "7.5", label: "7.5" },
];

const TOTAL_COST_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "40000", label: "Dưới $40,000" },
  { value: "60000", label: "Dưới $60,000" },
  { value: "80000", label: "Dưới $80,000" },
  { value: "100000", label: "Dưới $100,000" },
];

const RANK_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "20", label: "Top 20" },
  { value: "50", label: "Top 50" },
  { value: "100", label: "Top 100" },
];

const CAMPUS_OPTIONS = [
  { value: ALL, label: "Tất cả" },
  { value: "city", label: "Thành phố" },
  { value: "suburb", label: "Ngoại ô" },
  { value: "town", label: "Thị trấn" },
  { value: "rural", label: "Nông thôn" },
];

const ACCEPT_RATE_OPTIONS = [
  { value: ALL, label: "Tất cả", min: undefined, max: undefined },
  { value: "very_high", label: "Trên 50%", min: 0.5, max: undefined },
  { value: "high", label: "20% – 50%", min: 0.2, max: 0.5 },
  { value: "selective", label: "10% – 20%", min: 0.1, max: 0.2 },
  { value: "very_selective", label: "Dưới 10%", min: undefined, max: 0.1 },
];

function FilterSection({ title, hint, children }: { title: string; hint?: string; children: React.ReactNode }) {
  return (
    <fieldset>
      <legend className="mb-1.5 text-sm font-semibold text-foreground">{title}</legend>
      {hint && <p className="-mt-1 mb-2 text-xs text-muted-foreground">{hint}</p>}
      {children}
    </fieldset>
  );
}

interface CheckboxChoiceFilterProps {
  options: { value: string; label: string }[];
  value: string;
  onValueChange: (value: string) => void;
}

// Single-choice list rendered as checkboxes: checking sets it, unchecking clears (back to ALL).
function CheckboxChoiceFilter({ options, value, onValueChange }: CheckboxChoiceFilterProps) {
  const baseId = useId();
  return (
    <div className={cn("flex flex-col gap-2", options.length > 6 && "max-h-56 overflow-y-auto pr-1")}>
      {options.map((option) => {
        const id = `${baseId}-${option.value}`;
        return (
          <div key={option.value} className="flex items-center gap-2.5">
            <Checkbox
              id={id}
              checked={value === option.value}
              onCheckedChange={(checked) => {
                if (option.value === ALL) {
                  if (checked) onValueChange(ALL); // already-checked "Tất cả" does nothing
                } else {
                  onValueChange(checked ? option.value : ALL);
                }
              }}
            />
            <Label htmlFor={id} className="cursor-pointer text-sm font-normal">
              {option.label}
            </Label>
          </div>
        );
      })}
    </div>
  );
}

interface SearchableFilterProps {
  items: UniversityFilterOptionItem[];
  value: string | undefined;
  placeholder: string;
  onValueChange: (value: string | undefined) => void;
}

function SearchableFilter({ items, value, placeholder, onValueChange }: SearchableFilterProps) {
  const selected = items.find((item) => item.value === value) ?? null;
  const [inputValue, setInputValue] = useState(selected?.label ?? "");

  return (
    <Combobox<UniversityFilterOptionItem>
      items={items}
      value={selected}
      onValueChange={(next) => {
        onValueChange(next?.value);
        setInputValue(next?.label ?? "");
      }}
      inputValue={inputValue}
      onInputValueChange={setInputValue}
      itemToStringLabel={(item) => item.label}
      isItemEqualToValue={(a, b) => a.value === b.value}
      autoHighlight
    >
      <ComboboxInput placeholder={placeholder} showClear={selected !== null} className="h-9 w-full bg-surface" />
      <ComboboxContent>
        <ComboboxEmpty>Không tìm thấy</ComboboxEmpty>
        <ComboboxList className="max-h-64 [scrollbar-width:thin]!">
          {(item: UniversityFilterOptionItem) => (
            <ComboboxItem key={item.value} value={item}>
              {item.label}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}

function acceptRateValue(values: UniversitySearchParams): string {
  const match = ACCEPT_RATE_OPTIONS.find(
    (option) => option.min === values.minAcceptanceRate && option.max === values.maxAcceptanceRate
  );
  return match ? match.value : ALL;
}

export function UniversityFilters({ options, values, onChange, onReset, className }: UniversityFiltersProps) {
  const ivyId = useId();
  const testOptionalId = useId();
  const intlAidId = useId();
  const majorItems = options.majors.map((major) => ({ value: major, label: major }));

  const hasActiveFilter =
    Boolean(values.state) ||
    Boolean(values.control) ||
    Boolean(values.major) ||
    Boolean(values.maxTuitionUsd) ||
    Boolean(values.maxMinIelts) ||
    Boolean(values.maxTotalCostUsd) ||
    Boolean(values.maxRank) ||
    Boolean(values.campusSetting) ||
    values.testOptional === true ||
    values.hasIntlAid === true ||
    values.isIvy === true ||
    values.minAcceptanceRate !== undefined ||
    values.maxAcceptanceRate !== undefined;

  return (
    <div className={cn("space-y-5", className)}>
      <div className="flex h-8 items-center justify-between">
        <h2 className="font-heading text-xl font-bold text-foreground">Bộ lọc</h2>
        {hasActiveFilter && (
          <Button variant="ghost" size="sm" onClick={onReset} className="text-muted-foreground">
            <RotateCcwIcon />
            Đặt lại
          </Button>
        )}
      </div>

      <FilterSection title="Bang">
        <SearchableFilter
          key={values.state ?? ALL}
          items={options.states}
          value={values.state}
          placeholder="Gõ để tìm bang"
          onValueChange={(state) => onChange({ state })}
        />
      </FilterSection>

      <FilterSection title="Ngành đào tạo">
        <SearchableFilter
          key={values.major ?? ALL}
          items={majorItems}
          value={values.major}
          placeholder="Gõ để tìm ngành"
          onValueChange={(major) => onChange({ major })}
        />
      </FilterSection>

      <FilterSection title="Loại trường">
        <CheckboxChoiceFilter
          options={CONTROL_OPTIONS}
          value={values.control ?? ALL}
          onValueChange={(next) => onChange({ control: next === ALL ? undefined : next })}
        />
      </FilterSection>

      <FilterSection title="Học phí tối đa mỗi năm">
        <CheckboxChoiceFilter
          options={TUITION_OPTIONS}
          value={values.maxTuitionUsd ? String(values.maxTuitionUsd) : ALL}
          onValueChange={(next) => onChange({ maxTuitionUsd: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="IELTS của bạn">
        <CheckboxChoiceFilter
          options={IELTS_OPTIONS}
          value={values.maxMinIelts ? String(values.maxMinIelts) : ALL}
          onValueChange={(next) => onChange({ maxMinIelts: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="Tỷ lệ nhận">
        <CheckboxChoiceFilter
          options={ACCEPT_RATE_OPTIONS}
          value={acceptRateValue(values)}
          onValueChange={(next) => {
            const option = ACCEPT_RATE_OPTIONS.find((item) => item.value === next);
            onChange({ minAcceptanceRate: option?.min, maxAcceptanceRate: option?.max });
          }}
        />
      </FilterSection>

      <FilterSection title="Tổng chi phí tối đa mỗi năm">
        <CheckboxChoiceFilter
          options={TOTAL_COST_OPTIONS}
          value={values.maxTotalCostUsd ? String(values.maxTotalCostUsd) : ALL}
          onValueChange={(next) => onChange({ maxTotalCostUsd: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="Xếp hạng">
        <CheckboxChoiceFilter
          options={RANK_OPTIONS}
          value={values.maxRank ? String(values.maxRank) : ALL}
          onValueChange={(next) => onChange({ maxRank: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="Môi trường campus">
        <CheckboxChoiceFilter
          options={CAMPUS_OPTIONS}
          value={values.campusSetting ?? ALL}
          onValueChange={(next) => onChange({ campusSetting: next === ALL ? undefined : next })}
        />
      </FilterSection>

      <div className="flex items-center gap-2.5">
        <Checkbox
          id={ivyId}
          checked={values.isIvy === true}
          onCheckedChange={(checked) => onChange({ isIvy: checked ? true : undefined })}
        />
        <Label htmlFor={ivyId} className="cursor-pointer text-sm font-normal">
          Chỉ trường Ivy League
        </Label>
      </div>

      <div className="flex items-center gap-2.5">
        <Checkbox
          id={testOptionalId}
          checked={values.testOptional === true}
          onCheckedChange={(checked) => onChange({ testOptional: checked ? true : undefined })}
        />
        <Label htmlFor={testOptionalId} className="cursor-pointer text-sm font-normal">
          Không bắt buộc SAT/ACT
        </Label>
      </div>

      <div className="flex items-center gap-2.5">
        <Checkbox
          id={intlAidId}
          checked={values.hasIntlAid === true}
          onCheckedChange={(checked) => onChange({ hasIntlAid: checked ? true : undefined })}
        />
        <Label htmlFor={intlAidId} className="cursor-pointer text-sm font-normal">
          Có hỗ trợ tài chính cho SV quốc tế
        </Label>
      </div>
    </div>
  );
}
