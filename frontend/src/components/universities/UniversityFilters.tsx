"use client";

import { useId, useState } from "react";
import { ChevronDownIcon, RotateCcwIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Slider } from "@/components/ui/slider";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import type {
  UniversityFilterOptions,
  UniversitySearchParams,
} from "@/types/university";

interface UniversityFiltersProps {
  options: UniversityFilterOptions;
  values: UniversitySearchParams;
  onChange: (newValues: Partial<UniversitySearchParams>) => void;
  onReset: () => void;
  hideTitle?: boolean;
  className?: string;
}

const CONTROL_OPTIONS = [
  { value: "public", label: "Công lập" },
  { value: "private", label: "Tư thục" },
];

const CAMPUS_OPTIONS = [
  { value: "city", label: "Thành phố" },
  { value: "suburb", label: "Ngoại ô" },
  { value: "town", label: "Thị trấn" },
  { value: "rural", label: "Nông thôn" },
];

const IELTS_OPTIONS = [
  { value: "5.5", label: "5.5" },
  { value: "6", label: "6.0" },
  { value: "6.5", label: "6.5" },
  { value: "7", label: "7.0" },
  { value: "7.5", label: "7.5" },
];

const floorThousand = (n: number) => Math.floor(n / 1000) * 1000;
const ceilThousand = (n: number) => Math.ceil(n / 1000) * 1000;
const formatUsd = (n: number) => `$${n.toLocaleString("en-US")}`;
const formatNumber = (n: number) => n.toLocaleString("en-US");

function formatRangeSummary(
  min: number | undefined,
  max: number | undefined,
  format: (n: number) => string,
): string | null {
  if (min !== undefined && max !== undefined) return `${format(min)} – ${format(max)}`;
  if (min !== undefined) return `Từ ${format(min)}`;
  if (max !== undefined) return `Đến ${format(max)}`;
  return null;
}

function FilterCategory({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <section className={cn("space-y-1", className)}>{children}</section>;
}

interface CollapsibleFilterProps {
  title: string;
  activeSummary?: string | null;
  onClear?: () => void;
  defaultOpen?: boolean;
  children: React.ReactNode | ((collapse: () => void) => React.ReactNode);
}

function CollapsibleFilter({
  title,
  activeSummary,
  onClear,
  defaultOpen = false,
  children,
}: CollapsibleFilterProps) {
  const [open, setOpen] = useState(defaultOpen || Boolean(activeSummary));

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="py-1.5">
      <div className="flex items-center justify-between gap-2">
        <CollapsibleTrigger className="flex min-w-0 flex-1 flex-col items-start rounded-sm py-0.5 text-left outline-none focus-visible:ring-2 focus-visible:ring-ring">
          <span className="text-sm font-semibold leading-snug text-foreground">{title}</span>
          {!open && activeSummary && (
            <span className="mt-0.5 max-w-full truncate text-xs font-medium text-brand">
              {activeSummary}
            </span>
          )}
        </CollapsibleTrigger>
        <div className="flex shrink-0 items-center gap-0.5">
          {activeSummary && onClear && (
            <button
              type="button"
              onClick={onClear}
              aria-label={`Xóa bộ lọc ${title}`}
              className="flex size-6 cursor-pointer items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            >
              <XIcon className="size-3.5" />
            </button>
          )}
          <CollapsibleTrigger
            aria-label={open ? `Thu gọn ${title}` : `Mở rộng ${title}`}
            className="flex size-6 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          >
            <ChevronDownIcon
              className={cn("size-4 transition-transform duration-150", open && "rotate-180")}
            />
          </CollapsibleTrigger>
        </div>
      </div>
      <CollapsibleContent className="h-(--collapsible-panel-height) overflow-hidden transition-[height] duration-150 ease-out data-ending-style:h-0 data-starting-style:h-0">
        <div className="px-0.5 pt-2 pb-1">
          {typeof children === "function" ? children(() => setOpen(false)) : children}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

interface CheckboxChoiceFilterProps {
  label: string;
  options: { value: string; label: string }[];
  value: string | undefined;
  onValueChange: (value: string | undefined) => void;
}

function CheckboxChoiceFilter({ label, options, value, onValueChange }: CheckboxChoiceFilterProps) {
  const baseId = useId();
  return (
    <div role="group" aria-label={label} className="flex flex-col gap-2">
      {options.map((option) => {
        const id = `${baseId}-${option.value}`;
        const checked = value === option.value;
        return (
          <div key={option.value} className="flex min-w-0 items-start gap-2.5">
            <Checkbox
              id={id}
              checked={checked}
              className="mt-0.5 border-ink-3/65 bg-surface"
              onCheckedChange={(next) => onValueChange(next ? option.value : undefined)}
            />
            <Label
              htmlFor={id}
              className="min-w-0 cursor-pointer text-sm leading-snug font-normal text-foreground"
            >
              {option.label}
            </Label>
          </div>
        );
      })}
    </div>
  );
}

interface RangeFilterProps {
  label: string;
  minBound: number;
  maxBound: number;
  step: number;
  minValue: number | undefined;
  maxValue: number | undefined;
  formatInput?: (n: number) => string;
  onCommit: (min: number | undefined, max: number | undefined) => void;
}

function RangeFilter({
  label,
  minBound,
  maxBound,
  step,
  minValue,
  maxValue,
  formatInput = formatNumber,
  onCommit,
}: RangeFilterProps) {
  const minId = useId();
  const maxId = useId();
  const safeMax = maxBound > minBound ? maxBound : minBound + step;
  const clamp = (v: number) => Math.min(safeMax, Math.max(minBound, v));

  const committed: [number, number] = [
    minValue !== undefined ? clamp(minValue) : minBound,
    maxValue !== undefined ? clamp(maxValue) : safeMax,
  ];

  const [dragRange, setDragRange] = useState<[number, number] | null>(null);
  const [minDraft, setMinDraft] = useState<string | null>(null);
  const [maxDraft, setMaxDraft] = useState<string | null>(null);

  const current = dragRange ?? committed;

  const commitRange = (nextMin: number, nextMax: number) => {
    const lo = clamp(Math.min(nextMin, nextMax));
    const hi = clamp(Math.max(nextMin, nextMax));
    onCommit(lo <= minBound ? undefined : lo, hi >= safeMax ? undefined : hi);
  };

  const commitMinDraft = () => {
    if (minDraft === null) return;
    const digits = minDraft.replace(/[^\d]/g, "");
    setMinDraft(null);
    if (!digits) {
      commitRange(minBound, current[1]);
      return;
    }
    const parsed = clamp(Number(digits));
    commitRange(Math.min(parsed, current[1]), current[1]);
  };

  const commitMaxDraft = () => {
    if (maxDraft === null) return;
    const digits = maxDraft.replace(/[^\d]/g, "");
    setMaxDraft(null);
    if (!digits) {
      commitRange(current[0], safeMax);
      return;
    }
    const parsed = clamp(Number(digits));
    commitRange(current[0], Math.max(parsed, current[0]));
  };

  return (
    <div className="space-y-3 pt-1">
      <div className="px-2">
        <Slider
          aria-label={label}
          min={minBound}
          max={safeMax}
          step={step}
          minStepsBetweenValues={1}
          thumbCollisionBehavior="none"
          value={current}
          onValueChange={(val) => {
            if (Array.isArray(val)) setDragRange([val[0], val[1]]);
          }}
          onValueCommitted={(val) => {
            setDragRange(null);
            if (Array.isArray(val)) commitRange(val[0], val[1]);
          }}
        />
      </div>
      <div className="flex items-end gap-2">
        <div className="min-w-0 flex-1 space-y-1">
          <Label htmlFor={minId} className="block text-xs font-normal text-muted-foreground">
            Tối thiểu
          </Label>
          <Input
            id={minId}
            inputMode="numeric"
            className="h-8 border-ink-3/45 bg-surface px-2 text-xs tabular-nums sm:text-sm"
            value={minDraft ?? formatInput(current[0])}
            onFocus={(e) => {
              setMinDraft(String(current[0]));
              e.currentTarget.select();
            }}
            onChange={(e) => setMinDraft(e.target.value)}
            onBlur={commitMinDraft}
            onKeyDown={(e) => {
              if (e.key === "Enter") e.currentTarget.blur();
              if (e.key === "Escape") {
                setMinDraft(null);
                e.currentTarget.blur();
              }
            }}
          />
        </div>
        <span className="pb-1.5 text-sm text-muted-foreground select-none" aria-hidden>
          —
        </span>
        <div className="min-w-0 flex-1 space-y-1">
          <Label htmlFor={maxId} className="block text-right text-xs font-normal text-muted-foreground">
            Tối đa
          </Label>
          <Input
            id={maxId}
            inputMode="numeric"
            className="h-8 border-ink-3/45 bg-surface px-2 text-xs tabular-nums sm:text-sm"
            value={maxDraft ?? formatInput(current[1])}
            onFocus={(e) => {
              setMaxDraft(String(current[1]));
              e.currentTarget.select();
            }}
            onChange={(e) => setMaxDraft(e.target.value)}
            onBlur={commitMaxDraft}
            onKeyDown={(e) => {
              if (e.key === "Enter") e.currentTarget.blur();
              if (e.key === "Escape") {
                setMaxDraft(null);
                e.currentTarget.blur();
              }
            }}
          />
        </div>
      </div>
    </div>
  );
}

function BooleanFilterRow({
  label,
  checked,
  onCheckedChange,
}: {
  label: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  const id = useId();
  return (
    <div className="flex items-center justify-between gap-3 pt-3 pb-1">
      <Label htmlFor={id} className="min-w-0 cursor-pointer text-sm font-medium text-foreground">
        {label}
      </Label>
      <Switch
        id={id}
        checked={checked}
        className="shrink-0 cursor-pointer data-unchecked:bg-ink-3/35 **:data-[slot=switch-thumb]:bg-white"
        onCheckedChange={(next) => onCheckedChange(Boolean(next))}
      />
    </div>
  );
}

export function UniversityFilters({
  options,
  values,
  onChange,
  onReset,
  hideTitle = false,
  className,
}: UniversityFiltersProps) {
  const tuitionMinBound = floorThousand(options.minTuitionUsd);
  const tuitionMaxBound = ceilThousand(options.maxTuitionUsd);
  const totalCostMinBound = floorThousand(options.minTotalCostUsd);
  const totalCostMaxBound = ceilThousand(options.maxTotalCostUsd);

  const stateSummary = values.state
    ? (options.states.find((s) => s.value === values.state)?.label ?? values.state)
    : null;
  const majorSummary = values.major
    ? (options.majors.find((m) => m.value === values.major)?.label ?? values.major)
    : null;
  const controlSummary = values.control
    ? (CONTROL_OPTIONS.find((o) => o.value === values.control)?.label ?? values.control)
    : null;
  const campusSummary = values.campusSetting
    ? (CAMPUS_OPTIONS.find((o) => o.value === values.campusSetting)?.label ?? values.campusSetting)
    : null;
  const ieltsSummary = values.maxMinIelts ? `IELTS ≤ ${Number(values.maxMinIelts).toFixed(1)}` : null;

  const acceptMinPct =
    values.minAcceptanceRate !== undefined ? Math.round(values.minAcceptanceRate * 100) : undefined;
  const acceptMaxPct =
    values.maxAcceptanceRate !== undefined ? Math.round(values.maxAcceptanceRate * 100) : undefined;
  const acceptSummary = formatRangeSummary(acceptMinPct, acceptMaxPct, (n) => `${n}%`);

  const tuitionSummary = formatRangeSummary(values.minTuitionUsd, values.maxTuitionUsd, formatUsd);
  const totalCostSummary = formatRangeSummary(values.minTotalCostUsd, values.maxTotalCostUsd, formatUsd);

  const hasActiveFilter =
    Boolean(values.state) ||
    Boolean(values.control) ||
    Boolean(values.major) ||
    values.minTuitionUsd !== undefined ||
    values.maxTuitionUsd !== undefined ||
    values.minTotalCostUsd !== undefined ||
    values.maxTotalCostUsd !== undefined ||
    Boolean(values.maxMinIelts) ||
    Boolean(values.maxRank) ||
    Boolean(values.campusSetting) ||
    values.testOptional === true ||
    values.hasIntlAid === true ||
    values.isIvy === true ||
    values.minAcceptanceRate !== undefined ||
    values.maxAcceptanceRate !== undefined;

  return (
    <div className={cn("min-w-0 space-y-4 overflow-x-hidden", className)}>
      {!hideTitle && (
        <h2 className="font-heading text-2xl font-bold text-foreground">Bộ lọc</h2>
      )}

      <FilterCategory>
        <CollapsibleFilter
          title="Bang"
          activeSummary={stateSummary}
          onClear={() => onChange({ state: undefined })}
        >
          {(collapse) => (
            <CheckboxChoiceFilter
              label="Bang"
              options={options.states}
              value={values.state}
              onValueChange={(state) => {
                onChange({ state });
                if (state) collapse();
              }}
            />
          )}
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Loại trường"
          activeSummary={controlSummary}
          onClear={() => onChange({ control: undefined })}
          defaultOpen
        >
          {(collapse) => (
            <CheckboxChoiceFilter
              label="Loại trường"
              options={CONTROL_OPTIONS}
              value={values.control}
              onValueChange={(control) => {
                onChange({ control });
                if (control) collapse();
              }}
            />
          )}
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Môi trường campus"
          activeSummary={campusSummary}
          onClear={() => onChange({ campusSetting: undefined })}
        >
          {(collapse) => (
            <CheckboxChoiceFilter
              label="Môi trường campus"
              options={CAMPUS_OPTIONS}
              value={values.campusSetting}
              onValueChange={(campusSetting) => {
                onChange({ campusSetting });
                if (campusSetting) collapse();
              }}
            />
          )}
        </CollapsibleFilter>

        <BooleanFilterRow
          label="Chỉ trường Ivy League"
          checked={values.isIvy === true}
          onCheckedChange={(checked) => onChange({ isIvy: checked ? true : undefined })}
        />
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Ngành đào tạo"
          activeSummary={majorSummary}
          onClear={() => onChange({ major: undefined })}
        >
          {(collapse) => (
            <CheckboxChoiceFilter
              label="Ngành đào tạo"
              options={options.majors}
              value={values.major}
              onValueChange={(major) => {
                onChange({ major });
                if (major) collapse();
              }}
            />
          )}
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Tỷ lệ nhận (%)"
          activeSummary={acceptSummary}
          onClear={() => onChange({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined })}
          defaultOpen
        >
          <RangeFilter
            label="Tỷ lệ nhận (%)"
            minBound={0}
            maxBound={100}
            step={1}
            minValue={acceptMinPct}
            maxValue={acceptMaxPct}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({
                minAcceptanceRate: minPct !== undefined ? minPct / 100 : undefined,
                maxAcceptanceRate: maxPct !== undefined ? maxPct / 100 : undefined,
              })
            }
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="IELTS của bạn"
          activeSummary={ieltsSummary}
          onClear={() => onChange({ maxMinIelts: undefined })}
        >
          {(collapse) => (
            <CheckboxChoiceFilter
              label="IELTS của bạn"
              options={IELTS_OPTIONS}
              value={values.maxMinIelts ? String(values.maxMinIelts) : undefined}
              onValueChange={(next) => {
                onChange({ maxMinIelts: next ? Number(next) : undefined });
                if (next) collapse();
              }}
            />
          )}
        </CollapsibleFilter>

        <BooleanFilterRow
          label="Không bắt buộc SAT/ACT"
          checked={values.testOptional === true}
          onCheckedChange={(checked) => onChange({ testOptional: checked ? true : undefined })}
        />
      </FilterCategory>

      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Học phí mỗi năm (USD)"
          activeSummary={tuitionSummary}
          onClear={() => onChange({ minTuitionUsd: undefined, maxTuitionUsd: undefined })}
          defaultOpen
        >
          <RangeFilter
            label="Học phí mỗi năm (USD)"
            minBound={tuitionMinBound}
            maxBound={tuitionMaxBound}
            step={1000}
            minValue={values.minTuitionUsd}
            maxValue={values.maxTuitionUsd}
            onCommit={(minTuitionUsd, maxTuitionUsd) => onChange({ minTuitionUsd, maxTuitionUsd })}
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Tổng chi phí mỗi năm (USD)"
          activeSummary={totalCostSummary}
          onClear={() => onChange({ minTotalCostUsd: undefined, maxTotalCostUsd: undefined })}
        >
          <RangeFilter
            label="Tổng chi phí mỗi năm (USD)"
            minBound={totalCostMinBound}
            maxBound={totalCostMaxBound}
            step={1000}
            minValue={values.minTotalCostUsd}
            maxValue={values.maxTotalCostUsd}
            onCommit={(minTotalCostUsd, maxTotalCostUsd) =>
              onChange({ minTotalCostUsd, maxTotalCostUsd })
            }
          />
        </CollapsibleFilter>

        <BooleanFilterRow
          label="Có hỗ trợ tài chính SV quốc tế"
          checked={values.hasIntlAid === true}
          onCheckedChange={(checked) => onChange({ hasIntlAid: checked ? true : undefined })}
        />
      </FilterCategory>

      <div className="border-t border-line pt-4">
        <Button
          type="button"
          variant="outline"
          disabled={!hasActiveFilter}
          onClick={onReset}
          className="w-full border-ink-3/45 bg-surface text-foreground hover:bg-muted"
        >
          <RotateCcwIcon className="size-4" />
          Đặt lại bộ lọc
        </Button>
      </div>
    </div>
  );
}
