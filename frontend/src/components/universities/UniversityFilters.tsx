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
import { activityLabel, housingTypeLabel } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import type {
  UniversityFilterOptions,
  UniversityFilterRanges,
  UniversitySearchParams,
} from "@/types/university";

const DEFAULT_RANGES: UniversityFilterRanges = {
  minLivingUsd: 0,
  maxLivingUsd: 30000,
  minSat: 800,
  maxSat: 1600,
  minAct: 15,
  maxAct: 36,
  minStudentFacultyRatio: 5,
  maxStudentFacultyRatio: 30,
  minIntlStudents: 0,
  maxIntlStudents: 8000,
  maxIntlAidAvgUsd: 100000,
};

interface UniversityFiltersProps {
  options: UniversityFilterOptions;
  values: UniversitySearchParams;
  onChange: (newValues: Partial<UniversitySearchParams>) => void;
  onReset: () => void;
  onClose?: () => void;
  hideTitle?: boolean;
  className?: string;
}

export const CONTROL_OPTIONS = [
  { value: "public", label: "Công lập" },
  { value: "private", label: "Tư thục" },
];

export const CAMPUS_OPTIONS = [
  { value: "city", label: "Thành phố" },
  { value: "suburb", label: "Ngoại ô" },
  { value: "town", label: "Thị trấn" },
  { value: "rural", label: "Nông thôn" },
];

export const TEST_POLICY_OPTIONS = [
  { value: "optional", label: "Không bắt buộc" },
  { value: "required", label: "Bắt buộc" },
  { value: "not_accepted", label: "Không nhận điểm" },
];

export const DEADLINE_ROUND_OPTIONS = [
  { value: "ED", label: "Early Decision (ED)" },
  { value: "EA", label: "Early Action (EA)" },
  { value: "RD", label: "Regular Decision (RD)" },
  { value: "RollingFinal", label: "Rolling Admission" },
];

export const IMPORTANCE_OPTIONS = [
  { value: "very_important", label: "Rất quan trọng" },
  { value: "important", label: "Quan trọng" },
  { value: "considered", label: "Có xem xét" },
  { value: "not_considered", label: "Không xem xét" },
];

export const AID_POLICY_OPTIONS = [
  { value: "need_and_merit", label: "Theo nhu cầu & thành tích" },
  { value: "merit_only", label: "Chỉ theo thành tích" },
  { value: "need_only", label: "Chỉ theo nhu cầu" },
  { value: "none", label: "Không hỗ trợ" },
];

export const APP_FEE_OPTIONS = [
  { value: "0", label: "Miễn phí ($0)" },
  { value: "50", label: "≤ $50" },
  { value: "75", label: "≤ $75" },
  { value: "100", label: "≤ $100" },
];

export const GPA_MIN_OPTIONS = [
  { value: "2.5", label: "≤ 2.5" },
  { value: "3", label: "≤ 3.0" },
  { value: "3.2", label: "≤ 3.2" },
  { value: "3.5", label: "≤ 3.5" },
];

export const IELTS_OPTIONS = [
  { value: "5.5", label: "5.5+" },
  { value: "6", label: "6.0+" },
  { value: "6.5", label: "6.5+" },
  { value: "7", label: "7.0+" },
  { value: "7.5", label: "7.5+" },
];

export const TOEFL_OPTIONS = [
  { value: "60", label: "60+" },
  { value: "70", label: "70+" },
  { value: "80", label: "80+" },
  { value: "90", label: "90+" },
  { value: "100", label: "100+" },
];

export const DUOLINGO_OPTIONS = [
  { value: "95", label: "95+" },
  { value: "105", label: "105+" },
  { value: "115", label: "115+" },
  { value: "125", label: "125+" },
  { value: "135", label: "135+" },
];

const floorStep = (n: number, step: number) => Math.floor(n / step) * step;
const ceilStep = (n: number, step: number) => Math.ceil(n / step) * step;
const formatUsd = (n: number) => `$${n.toLocaleString("en-US")}`;
const formatNumber = (n: number) => n.toLocaleString("en-US");
const toPct = (v: number | undefined) => (v !== undefined ? Math.round(v * 100) : undefined);
const fromPct = (v: number | undefined) => (v !== undefined ? v / 100 : undefined);

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

function findLabel(options: { value: string; label: string }[], value: string | undefined): string | null {
  if (!value) return null;
  return options.find((o) => o.value === value)?.label ?? value;
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
  const decimals = step < 1 ? Math.max(1, Math.ceil(-Math.log10(step))) : 0;
  const roundStep = (v: number) => Number((Math.round(v / step) * step).toFixed(decimals));
  const clamp = (v: number) => roundStep(Math.min(safeMax, Math.max(minBound, v)));

  const committed: [number, number] = [
    minValue !== undefined ? clamp(minValue) : minBound,
    maxValue !== undefined ? clamp(maxValue) : safeMax,
  ];
  const committedKey = `${committed[0]}:${committed[1]}`;

  const [dragRange, setDragRange] = useState<[number, number] | null>(null);
  const [pending, setPending] = useState<{ range: [number, number]; baseKey: string } | null>(null);
  const [minDraft, setMinDraft] = useState<string | null>(null);
  const [maxDraft, setMaxDraft] = useState<string | null>(null);

  const activePending = pending && pending.baseKey === committedKey ? pending.range : null;
  const current = dragRange ?? activePending ?? committed;

  const commitRange = (nextMin: number, nextMax: number) => {
    const lo = clamp(Math.min(nextMin, nextMax));
    const hi = clamp(Math.max(nextMin, nextMax));
    if (lo === committed[0] && hi === committed[1]) {
      setPending(null);
      return;
    }
    setPending({ range: [lo, hi], baseKey: committedKey });
    onCommit(lo <= minBound ? undefined : lo, hi >= safeMax ? undefined : hi);
  };

  const parseDraft = (draft: string): number | null => {
    const cleaned = draft.replace(",", ".").replace(/[^\d.]/g, "");
    if (!cleaned) return null;
    const n = Number(cleaned);
    return Number.isFinite(n) ? clamp(n) : null;
  };

  const commitMinDraft = () => {
    if (minDraft === null) return;
    const parsed = parseDraft(minDraft);
    setMinDraft(null);
    if (parsed === null) {
      commitRange(minBound, current[1]);
      return;
    }
    commitRange(Math.min(parsed, current[1]), current[1]);
  };

  const commitMaxDraft = () => {
    if (maxDraft === null) return;
    const parsed = parseDraft(maxDraft);
    setMaxDraft(null);
    if (parsed === null) {
      commitRange(current[0], safeMax);
      return;
    }
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
            if (Array.isArray(val)) setDragRange([roundStep(val[0]), roundStep(val[1])]);
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
            inputMode={step < 1 ? "decimal" : "numeric"}
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
            inputMode={step < 1 ? "decimal" : "numeric"}
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

const NON_FILTER_KEYS = new Set(["search", "sortBy", "rankListId", "studyLevel", "page", "pageSize"]);

export function UniversityFilters({
  options,
  values,
  onChange,
  onReset,
  onClose,
  hideTitle = false,
  className,
}: UniversityFiltersProps) {
  const r = options.ranges ?? DEFAULT_RANGES;
  const tuitionMinBound = floorStep(options.minTuitionUsd, 1000);
  const tuitionMaxBound = ceilStep(options.maxTuitionUsd, 1000);
  const totalCostMinBound = floorStep(options.minTotalCostUsd, 1000);
  const totalCostMaxBound = ceilStep(options.maxTotalCostUsd, 1000);
  const livingMinBound = floorStep(r.minLivingUsd, 500);
  const livingMaxBound = ceilStep(r.maxLivingUsd, 500);
  const satMinBound = floorStep(r.minSat, 10);
  const satMaxBound = ceilStep(r.maxSat, 10);
  const intlMinBound = floorStep(r.minIntlStudents, 100);
  const intlMaxBound = ceilStep(r.maxIntlStudents, 100);
  const aidMaxBound = ceilStep(r.maxIntlAidAvgUsd, 1000);

  const housingOptions = (options.housingTypes ?? []).map((h) => ({ value: h, label: housingTypeLabel(h) }));
  const activityOptions = (options.activities ?? []).map((a) => ({ value: a, label: activityLabel(a) }));

  const hasActiveFilter = Object.entries(values).some(
    ([k, v]) => !NON_FILTER_KEYS.has(k) && v !== undefined && v !== "" && v !== false,
  );

  const renderChoice = (
    title: string,
    choiceOptions: { value: string; label: string }[],
    value: string | undefined,
    onSelect: (next: string | undefined) => void,
    summaryOverride?: string | null,
    defaultOpen = false,
  ) => (
    <CollapsibleFilter
      title={title}
      activeSummary={summaryOverride !== undefined ? summaryOverride : findLabel(choiceOptions, value)}
      onClear={() => onSelect(undefined)}
      defaultOpen={defaultOpen}
    >
      {(collapse) => (
        <CheckboxChoiceFilter
          label={title}
          options={choiceOptions}
          value={value}
          onValueChange={(next) => {
            onSelect(next);
            if (next !== undefined) collapse();
          }}
        />
      )}
    </CollapsibleFilter>
  );

  return (
    <div className={cn("min-w-0 space-y-4 overflow-x-hidden", className)}>
      {!hideTitle && (
        <div className="flex items-center justify-between gap-2">
          <h2 className="font-heading text-2xl font-bold text-foreground">Bộ lọc</h2>
          {onClose && (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={onClose}
              aria-label="Đóng bộ lọc"
              className="text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <XIcon className="size-4" />
            </Button>
          )}
        </div>
      )}

      {/* Vị trí & Đặc điểm trường */}
      <FilterCategory>
        {renderChoice("Bang", options.states, values.state, (state) => onChange({ state }))}
        {renderChoice(
          "Môi trường campus",
          CAMPUS_OPTIONS,
          values.campusSetting,
          (campusSetting) => onChange({ campusSetting }),
        )}
        {renderChoice(
          "Loại trường",
          CONTROL_OPTIONS,
          values.control,
          (control) => onChange({ control }),
          undefined,
          true,
        )}
        <BooleanFilterRow
          label="Chỉ trường Ivy League"
          checked={values.isIvy === true}
          onCheckedChange={(checked) => onChange({ isIvy: checked ? true : undefined })}
        />
      </FilterCategory>

      {/* Tuyển sinh & Yêu cầu đầu vào */}
      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Tỷ lệ nhận chung (%)"
          activeSummary={formatRangeSummary(toPct(values.minAcceptanceRate), toPct(values.maxAcceptanceRate), (n) => `${n}%`)}
          onClear={() => onChange({ minAcceptanceRate: undefined, maxAcceptanceRate: undefined })}
          defaultOpen
        >
          <RangeFilter
            label="Tỷ lệ nhận chung (%)"
            minBound={0}
            maxBound={100}
            step={1}
            minValue={toPct(values.minAcceptanceRate)}
            maxValue={toPct(values.maxAcceptanceRate)}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({ minAcceptanceRate: fromPct(minPct), maxAcceptanceRate: fromPct(maxPct) })
            }
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Tỷ lệ nhận SV quốc tế (%)"
          activeSummary={formatRangeSummary(
            toPct(values.minIntlAcceptanceRate),
            toPct(values.maxIntlAcceptanceRate),
            (n) => `${n}%`,
          )}
          onClear={() => onChange({ minIntlAcceptanceRate: undefined, maxIntlAcceptanceRate: undefined })}
        >
          <RangeFilter
            label="Tỷ lệ nhận SV quốc tế (%)"
            minBound={0}
            maxBound={100}
            step={1}
            minValue={toPct(values.minIntlAcceptanceRate)}
            maxValue={toPct(values.maxIntlAcceptanceRate)}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({ minIntlAcceptanceRate: fromPct(minPct), maxIntlAcceptanceRate: fromPct(maxPct) })
            }
          />
        </CollapsibleFilter>

        {renderChoice(
          "GPA tối thiểu yêu cầu",
          GPA_MIN_OPTIONS,
          values.maxMinGpa4 !== undefined ? String(values.maxMinGpa4) : undefined,
          (next) => onChange({ maxMinGpa4: next ? Number(next) : undefined }),
          values.maxMinGpa4 !== undefined ? `GPA ≤ ${values.maxMinGpa4.toFixed(1)}` : null,
        )}

        <CollapsibleFilter
          title="GPA trung bình trúng tuyển"
          activeSummary={formatRangeSummary(values.minAvgGpa4, values.maxAvgGpa4, (n) => n.toFixed(1))}
          onClear={() => onChange({ minAvgGpa4: undefined, maxAvgGpa4: undefined })}
        >
          <RangeFilter
            label="GPA trung bình trúng tuyển"
            minBound={3.0}
            maxBound={4.0}
            step={0.1}
            minValue={values.minAvgGpa4}
            maxValue={values.maxAvgGpa4}
            formatInput={(n) => n.toFixed(1)}
            onCommit={(minAvgGpa4, maxAvgGpa4) => onChange({ minAvgGpa4, maxAvgGpa4 })}
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Điểm SAT (mốc 25%–75%)"
          activeSummary={formatRangeSummary(values.minSat, values.maxSat, formatNumber)}
          onClear={() => onChange({ minSat: undefined, maxSat: undefined })}
        >
          <RangeFilter
            label="Điểm SAT (mốc 25%–75%)"
            minBound={satMinBound}
            maxBound={satMaxBound}
            step={10}
            minValue={values.minSat}
            maxValue={values.maxSat}
            onCommit={(minSat, maxSat) => onChange({ minSat, maxSat })}
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Điểm ACT (mốc 25%–75%)"
          activeSummary={formatRangeSummary(values.minAct, values.maxAct, (n) => String(n))}
          onClear={() => onChange({ minAct: undefined, maxAct: undefined })}
        >
          <RangeFilter
            label="Điểm ACT (mốc 25%–75%)"
            minBound={r.minAct}
            maxBound={r.maxAct}
            step={1}
            minValue={values.minAct}
            maxValue={values.maxAct}
            formatInput={(n) => String(n)}
            onCommit={(minAct, maxAct) => onChange({ minAct, maxAct })}
          />
        </CollapsibleFilter>

        {renderChoice("Chính sách SAT", TEST_POLICY_OPTIONS, values.satPolicy, (satPolicy) =>
          onChange({ satPolicy }),
        )}
        {renderChoice("Chính sách ACT", TEST_POLICY_OPTIONS, values.actPolicy, (actPolicy) =>
          onChange({ actPolicy }),
        )}

        {renderChoice(
          "IELTS",
          IELTS_OPTIONS,
          values.maxMinIelts ? String(values.maxMinIelts) : undefined,
          (next) => onChange({ maxMinIelts: next ? Number(next) : undefined }),
          values.maxMinIelts ? `IELTS ${Number(values.maxMinIelts).toFixed(1)}+` : null,
        )}
        {renderChoice(
          "TOEFL",
          TOEFL_OPTIONS,
          values.maxMinToefl ? String(values.maxMinToefl) : undefined,
          (next) => onChange({ maxMinToefl: next ? Number(next) : undefined }),
          values.maxMinToefl ? `TOEFL ${values.maxMinToefl}+` : null,
        )}
        {renderChoice(
          "Duolingo (DET)",
          DUOLINGO_OPTIONS,
          values.maxMinDuolingo ? String(values.maxMinDuolingo) : undefined,
          (next) => onChange({ maxMinDuolingo: next ? Number(next) : undefined }),
          values.maxMinDuolingo ? `DET ${values.maxMinDuolingo}+` : null,
        )}

        {renderChoice("Vòng nộp hồ sơ", DEADLINE_ROUND_OPTIONS, values.deadlineRound, (deadlineRound) =>
          onChange({ deadlineRound }),
        )}
        {renderChoice(
          "Mức coi trọng điểm thi (C7)",
          IMPORTANCE_OPTIONS,
          values.c7TestImportance,
          (c7TestImportance) => onChange({ c7TestImportance }),
        )}
        {renderChoice(
          "Mức coi trọng ngoại khóa (C7)",
          IMPORTANCE_OPTIONS,
          values.c7EcImportance,
          (c7EcImportance) => onChange({ c7EcImportance }),
        )}
      </FilterCategory>

      {/* Học thuật & Sinh viên */}
      <FilterCategory className="border-t border-line pt-4">
        {renderChoice("Ngành đào tạo", options.majors, values.major, (major) => onChange({ major }))}

        <CollapsibleFilter
          title="Tỷ lệ giữ chân năm nhất (%)"
          activeSummary={formatRangeSummary(
            toPct(values.minRetentionRate),
            toPct(values.maxRetentionRate),
            (n) => `${n}%`,
          )}
          onClear={() => onChange({ minRetentionRate: undefined, maxRetentionRate: undefined })}
        >
          <RangeFilter
            label="Tỷ lệ giữ chân năm nhất (%)"
            minBound={70}
            maxBound={100}
            step={1}
            minValue={toPct(values.minRetentionRate)}
            maxValue={toPct(values.maxRetentionRate)}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({ minRetentionRate: fromPct(minPct), maxRetentionRate: fromPct(maxPct) })
            }
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Tỷ lệ tốt nghiệp 6 năm (%)"
          activeSummary={formatRangeSummary(
            toPct(values.minGradRate6Yr),
            toPct(values.maxGradRate6Yr),
            (n) => `${n}%`,
          )}
          onClear={() => onChange({ minGradRate6Yr: undefined, maxGradRate6Yr: undefined })}
        >
          <RangeFilter
            label="Tỷ lệ tốt nghiệp 6 năm (%)"
            minBound={50}
            maxBound={100}
            step={1}
            minValue={toPct(values.minGradRate6Yr)}
            maxValue={toPct(values.maxGradRate6Yr)}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({ minGradRate6Yr: fromPct(minPct), maxGradRate6Yr: fromPct(maxPct) })
            }
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Số sinh viên quốc tế"
          activeSummary={formatRangeSummary(values.minIntlStudents, values.maxIntlStudents, formatNumber)}
          onClear={() => onChange({ minIntlStudents: undefined, maxIntlStudents: undefined })}
        >
          <RangeFilter
            label="Số sinh viên quốc tế"
            minBound={intlMinBound}
            maxBound={intlMaxBound}
            step={100}
            minValue={values.minIntlStudents}
            maxValue={values.maxIntlStudents}
            onCommit={(minIntlStudents, maxIntlStudents) =>
              onChange({ minIntlStudents, maxIntlStudents })
            }
          />
        </CollapsibleFilter>
      </FilterCategory>

      {/* Chi phí, Học bổng & Đời sống */}
      <FilterCategory className="border-t border-line pt-4">
        <CollapsibleFilter
          title="Học phí mỗi năm (USD)"
          activeSummary={formatRangeSummary(values.minTuitionUsd, values.maxTuitionUsd, formatUsd)}
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
          activeSummary={formatRangeSummary(values.minTotalCostUsd, values.maxTotalCostUsd, formatUsd)}
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

        <CollapsibleFilter
          title="Chi phí sinh hoạt mỗi năm (USD)"
          activeSummary={formatRangeSummary(values.minLivingUsd, values.maxLivingUsd, formatUsd)}
          onClear={() => onChange({ minLivingUsd: undefined, maxLivingUsd: undefined })}
        >
          <RangeFilter
            label="Chi phí sinh hoạt mỗi năm (USD)"
            minBound={livingMinBound}
            maxBound={livingMaxBound}
            step={500}
            minValue={values.minLivingUsd}
            maxValue={values.maxLivingUsd}
            onCommit={(minLivingUsd, maxLivingUsd) => onChange({ minLivingUsd, maxLivingUsd })}
          />
        </CollapsibleFilter>

        {renderChoice(
          "Phí nộp hồ sơ tối đa (USD)",
          APP_FEE_OPTIONS,
          values.maxApplicationFeeUsd !== undefined ? String(values.maxApplicationFeeUsd) : undefined,
          (next) => onChange({ maxApplicationFeeUsd: next !== undefined ? Number(next) : undefined }),
          values.maxApplicationFeeUsd !== undefined
            ? values.maxApplicationFeeUsd === 0
              ? "Miễn phí ($0)"
              : `≤ $${values.maxApplicationFeeUsd}`
            : null,
        )}

        {renderChoice(
          "Chính sách hỗ trợ tài chính SV quốc tế",
          AID_POLICY_OPTIONS,
          values.intlAidPolicy,
          (intlAidPolicy) => onChange({ intlAidPolicy }),
        )}

        <CollapsibleFilter
          title="Học bổng SV quốc tế TB/năm (USD)"
          activeSummary={formatRangeSummary(values.minIntlAidAvgUsd, values.maxIntlAidAvgUsd, formatUsd)}
          onClear={() => onChange({ minIntlAidAvgUsd: undefined, maxIntlAidAvgUsd: undefined })}
        >
          <RangeFilter
            label="Học bổng SV quốc tế TB/năm (USD)"
            minBound={0}
            maxBound={aidMaxBound}
            step={1000}
            minValue={values.minIntlAidAvgUsd}
            maxValue={values.maxIntlAidAvgUsd}
            onCommit={(minIntlAidAvgUsd, maxIntlAidAvgUsd) =>
              onChange({ minIntlAidAvgUsd, maxIntlAidAvgUsd })
            }
          />
        </CollapsibleFilter>

        <CollapsibleFilter
          title="Tỷ lệ SV năm nhất ở KTX (%)"
          activeSummary={formatRangeSummary(
            toPct(values.minFyOnCampusPct),
            toPct(values.maxFyOnCampusPct),
            (n) => `${n}%`,
          )}
          onClear={() => onChange({ minFyOnCampusPct: undefined, maxFyOnCampusPct: undefined })}
        >
          <RangeFilter
            label="Tỷ lệ SV năm nhất ở KTX (%)"
            minBound={0}
            maxBound={100}
            step={5}
            minValue={toPct(values.minFyOnCampusPct)}
            maxValue={toPct(values.maxFyOnCampusPct)}
            formatInput={(n) => String(n)}
            onCommit={(minPct, maxPct) =>
              onChange({ minFyOnCampusPct: fromPct(minPct), maxFyOnCampusPct: fromPct(maxPct) })
            }
          />
        </CollapsibleFilter>

        {renderChoice("Loại hình nhà ở", housingOptions, values.housingType, (housingType) =>
          onChange({ housingType }),
        )}
        {renderChoice("Hoạt động sinh viên", activityOptions, values.activity, (activity) =>
          onChange({ activity }),
        )}

        <BooleanFilterRow
          label="Có hỗ trợ tài chính SV quốc tế"
          checked={values.hasIntlAid === true}
          onCheckedChange={(checked) => onChange({ hasIntlAid: checked ? true : undefined })}
        />
        <BooleanFilterRow
          label="Có học bổng riêng trên hệ thống"
          checked={values.hasScholarship === true}
          onCheckedChange={(checked) => onChange({ hasScholarship: checked ? true : undefined })}
        />
        <BooleanFilterRow
          label="Có khu nhà ở cho SV quốc tế"
          checked={values.intlHousing === true}
          onCheckedChange={(checked) => onChange({ intlHousing: checked ? true : undefined })}
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
