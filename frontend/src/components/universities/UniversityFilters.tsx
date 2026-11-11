"use client";

import { useId, useState } from "react";
import { RotateCcwIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
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
  { value: "6", label: "Tối đa 6.0" },
  { value: "6.5", label: "Tối đa 6.5" },
  { value: "7", label: "Tối đa 7.0" },
  { value: "7.5", label: "Tối đa 7.5" },
];

const ACCEPT_RATE_OPTIONS = [
  { value: ALL, label: "Tất cả", min: undefined, max: undefined },
  { value: "very_high", label: "Trên 50%", min: 0.5, max: undefined },
  { value: "high", label: "20% – 50%", min: 0.2, max: 0.5 },
  { value: "selective", label: "10% – 20%", min: 0.1, max: 0.2 },
  { value: "very_selective", label: "Dưới 10%", min: undefined, max: 0.1 },
];

function FilterSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <fieldset>
      <legend className="mb-1.5 text-sm font-semibold text-foreground">{title}</legend>
      {children}
    </fieldset>
  );
}

interface RadioFilterProps {
  name: string;
  options: { value: string; label: string }[];
  value: string;
  onValueChange: (value: string) => void;
}

function RadioFilter({ name, options, value, onValueChange }: RadioFilterProps) {
  const baseId = useId();
  return (
    <RadioGroup
      name={name}
      value={value}
      onValueChange={(next) => onValueChange(String(next))}
      className="gap-2"
    >
      {options.map((option) => {
        const id = `${baseId}-${option.value}`;
        return (
          <div key={option.value} className="flex items-center gap-2.5">
            <RadioGroupItem id={id} value={option.value} />
            <Label htmlFor={id} className="cursor-pointer text-sm font-normal">
              {option.label}
            </Label>
          </div>
        );
      })}
    </RadioGroup>
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
        <ComboboxList>
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
  const majorItems = options.majors.map((major) => ({ value: major, label: major }));

  const hasActiveFilter =
    Boolean(values.state) ||
    Boolean(values.control) ||
    Boolean(values.major) ||
    Boolean(values.maxTuitionUsd) ||
    Boolean(values.maxMinIelts) ||
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
        <RadioFilter
          name="control"
          options={CONTROL_OPTIONS}
          value={values.control ?? ALL}
          onValueChange={(next) => onChange({ control: next === ALL ? undefined : next })}
        />
      </FilterSection>

      <FilterSection title="Học phí tối đa mỗi năm">
        <RadioFilter
          name="maxTuitionUsd"
          options={TUITION_OPTIONS}
          value={values.maxTuitionUsd ? String(values.maxTuitionUsd) : ALL}
          onValueChange={(next) => onChange({ maxTuitionUsd: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="Yêu cầu IELTS của trường">
        <RadioFilter
          name="maxMinIelts"
          options={IELTS_OPTIONS}
          value={values.maxMinIelts ? String(values.maxMinIelts) : ALL}
          onValueChange={(next) => onChange({ maxMinIelts: next === ALL ? undefined : Number(next) })}
        />
      </FilterSection>

      <FilterSection title="Tỷ lệ nhận">
        <RadioFilter
          name="acceptanceRate"
          options={ACCEPT_RATE_OPTIONS}
          value={acceptRateValue(values)}
          onValueChange={(next) => {
            const option = ACCEPT_RATE_OPTIONS.find((item) => item.value === next);
            onChange({ minAcceptanceRate: option?.min, maxAcceptanceRate: option?.max });
          }}
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
    </div>
  );
}
