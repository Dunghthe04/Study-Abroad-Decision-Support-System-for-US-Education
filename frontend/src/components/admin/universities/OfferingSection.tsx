"use client";

import { useEffect, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, getAdminMajors, updateAdminOffering } from "@/lib/admin-university-api";
import {
  SAT_POLICY_OPTIONS,
  SOURCE_RELIABILITY_OPTIONS,
  formatLastEdited,
  formatNumber,
  formatRetrievedAt,
  formatSatRange,
  formatUsd,
  parseDeadlines,
  satPolicyLabel,
  sourceReliabilityLabel,
} from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import type {
  AdminFieldErrors,
  AdminMajorOption,
  AdminUniversityOffering,
} from "@/types/admin-university";
import { BlockBody, EditableTable, TableFrame, type TableEditState, type TableRow } from "./EditableTable";
import { OptionSelect } from "./OptionSelect";

export type OfferingTable = "cost" | "requirements" | "majors" | "source";

interface OfferingSectionProps {
  universityId: string;
  offering: AdminUniversityOffering;
  /** Bảng đang sửa của bậc học này; null nếu không sửa bảng nào của bậc học này. */
  editingTable: OfferingTable | null;
  /** false khi đang sửa một bảng khác trong panel. */
  canEdit: boolean;
  onStartEdit: (table: OfferingTable) => void;
  onStopEdit: () => void;
  onSaved: (offering: AdminUniversityOffering) => void;
}

/** Các ô số, khóa trùng tên trường dữ liệu gửi lên API. */
const NUMBER_FIELDS = [
  "tuitionUsd",
  "livingUsd",
  "feesUsd",
  "otherExpensesUsd",
  "minGpa4",
  "avgGpa4",
  "minIelts",
  "minToefl",
  "minDuolingo",
  "sat25",
  "sat75",
  "act25",
  "act75",
] as const;

type NumberField = (typeof NUMBER_FIELDS)[number];

const NUMBER_LABELS: Record<NumberField, string> = {
  tuitionUsd: "Học phí (USD/năm)",
  livingUsd: "Sinh hoạt (USD/năm)",
  feesUsd: "Các khoản phí (USD/năm)",
  otherExpensesUsd: "Chi phí khác (USD/năm)",
  minGpa4: "GPA tối thiểu (thang 4)",
  avgGpa4: "GPA TB trúng tuyển",
  minIelts: "IELTS tối thiểu",
  minToefl: "TOEFL iBT tối thiểu",
  minDuolingo: "Duolingo tối thiểu",
  sat25: "SAT mốc 25%",
  sat75: "SAT mốc 75%",
  act25: "ACT mốc 25%",
  act75: "ACT mốc 75%",
};

const COST_FIELDS: NumberField[] = ["tuitionUsd", "livingUsd", "feesUsd", "otherExpensesUsd"];
const REQUIREMENT_FIELDS: NumberField[] = ["minGpa4", "avgGpa4", "minIelts", "minToefl", "minDuolingo"];
const TEST_FIELDS: NumberField[] = ["sat25", "sat75", "act25", "act75"];

interface FormState {
  numbers: Record<NumberField, string>;
  academicYear: string;
  satPolicy: string;
  sourceUrl: string;
  retrievedAt: string;
  sourceReliability: string;
  notes: string;
  /** majorId -> isStem; ngành không có trong map là trường không đào tạo. */
  majors: Record<string, boolean>;
}

function toFormState(offering: AdminUniversityOffering): FormState {
  const numbers = {} as Record<NumberField, string>;
  for (const field of NUMBER_FIELDS) {
    numbers[field] = toInputValue(offering[field]);
  }
  const majors: Record<string, boolean> = {};
  for (const item of offering.majorItems) {
    majors[item.majorId] = item.isStem;
  }
  return {
    numbers,
    academicYear: toInputValue(offering.academicYear),
    satPolicy: toInputValue(offering.satPolicy),
    sourceUrl: toInputValue(offering.sourceUrl),
    retrievedAt: toInputValue(offering.retrievedAt),
    sourceReliability: toInputValue(offering.sourceReliability),
    notes: toInputValue(offering.notes),
    majors,
  };
}

const INVALID_MESSAGE = "Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.";

export function OfferingSection({
  universityId,
  offering,
  editingTable,
  canEdit,
  onStartEdit,
  onStopEdit,
  onSaved,
}: OfferingSectionProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(offering));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [majorOptions, setMajorOptions] = useState<AdminMajorOption[] | null>(null);
  const [majorLoadError, setMajorLoadError] = useState<string | null>(null);

  const isEditingMajors = editingTable === "majors";

  useEffect(() => {
    if (!isEditingMajors || majorOptions) {
      return;
    }
    let cancelled = false;
    getAdminMajors()
      .then((loaded) => {
        if (!cancelled) setMajorOptions(loaded);
      })
      .catch((err: unknown) => {
        if (!cancelled) setMajorLoadError(err instanceof Error ? err.message : "Không thể tải danh mục ngành.");
      });
    return () => {
      cancelled = true;
    };
  }, [isEditingMajors, majorOptions]);

  const hasChanges = JSON.stringify(form) !== JSON.stringify(toFormState(offering));

  const handleEdit = (table: OfferingTable) => {
    setForm(toFormState(offering));
    setFieldErrors({});
    setError(null);
    onStartEdit(table);
  };

  const setNumber = (field: NumberField, value: string) => {
    setForm({ ...form, numbers: { ...form.numbers, [field]: value } });
  };

  const toggleMajor = (majorId: string, included: boolean) => {
    const majors = { ...form.majors };
    if (included) {
      majors[majorId] = false;
    } else {
      delete majors[majorId];
    }
    setForm({ ...form, majors });
  };

  const toggleStem = (majorId: string, isStem: boolean) => {
    setForm({ ...form, majors: { ...form.majors, [majorId]: isStem } });
  };

  const handleSave = async () => {
    const numbers = {} as Record<NumberField, number | null>;
    for (const field of NUMBER_FIELDS) {
      numbers[field] = numberOrNull(form.numbers[field]);
    }
    const notNumbers = findNotNumbers(numbers);
    if (notNumbers.length > 0) {
      const errors: AdminFieldErrors = {};
      for (const field of notNumbers) {
        errors[field] = "Vui lòng nhập số.";
      }
      setFieldErrors(errors);
      setError(INVALID_MESSAGE);
      return;
    }

    // SAT cần đủ cả 2 mốc (công thức gợi ý dùng cả hai); báo sớm trước khi gửi.
    if ((numbers.sat25 === null) !== (numbers.sat75 === null)) {
      setFieldErrors({ sat25: "Nhập đủ cả SAT 25% và 75%, hoặc để trống cả hai." });
      setError(INVALID_MESSAGE);
      return;
    }

    const majors = Object.entries(form.majors).map(([majorId, isStem]) => ({ majorId, isStem }));

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = await updateAdminOffering(universityId, offering.id, {
        ...numbers,
        academicYear: textOrNull(form.academicYear),
        satPolicy: textOrNull(form.satPolicy),
        sourceUrl: textOrNull(form.sourceUrl),
        retrievedAt: textOrNull(form.retrievedAt),
        sourceReliability: textOrNull(form.sourceReliability),
        notes: textOrNull(form.notes),
        majors,
      });
      toast.add({ type: "success", title: "Đã lưu bậc học", description: studyLevelLabel(saved.studyLevel) });
      onSaved(saved);
    } catch (err) {
      if (err instanceof AdminFormError) {
        setError(err.message);
        setFieldErrors(err.fieldErrors);
      } else {
        setError("Không thể lưu. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const editStateFor = (table: OfferingTable): TableEditState => ({
    isEditing: editingTable === table,
    canEdit,
    isSaving,
    hasChanges,
    error: editingTable === table ? error : null,
    onEdit: () => handleEdit(table),
    onCancel: onStopEdit,
    onSave: handleSave,
  });

  const numberRow = (field: NumberField, autoFocus = false): TableRow => ({
    label: NUMBER_LABELS[field],
    inputId: `offering-${offering.id}-${field}`,
    error: fieldErrors[field],
    value: (
      <Input
        id={`offering-${offering.id}-${field}`}
        inputMode="decimal"
        value={form.numbers[field]}
        onChange={(e) => setNumber(field, e.target.value)}
        aria-invalid={Boolean(fieldErrors[field]) || undefined}
        disabled={isSaving}
        autoFocus={autoFocus}
      />
    ),
  });

  const costRows = (): TableRow[] => {
    if (editingTable !== "cost") {
      const hasCost =
        offering.tuitionUsd !== null ||
        offering.livingUsd !== null ||
        offering.feesUsd !== null ||
        offering.otherExpensesUsd !== null;
      const totalCost =
        (offering.tuitionUsd ?? 0) +
        (offering.livingUsd ?? 0) +
        (offering.feesUsd ?? 0) +
        (offering.otherExpensesUsd ?? 0);
      return [
        { label: "Năm học", value: offering.academicYear ?? "—" },
        { label: "Học phí", value: formatUsd(offering.tuitionUsd) },
        { label: "Sinh hoạt", value: formatUsd(offering.livingUsd) },
        { label: "Các khoản phí", value: formatUsd(offering.feesUsd) },
        { label: "Chi phí khác", value: formatUsd(offering.otherExpensesUsd) },
        { label: "Tổng", value: hasCost ? formatUsd(totalCost) : "—" },
      ];
    }

    const yearInputId = `offering-${offering.id}-year`;
    const yearRow: TableRow = {
      label: "Năm học",
      inputId: yearInputId,
      error: fieldErrors.academicYear,
      value: (
        <Input
          id={yearInputId}
          value={form.academicYear}
          onChange={(e) => setForm({ ...form, academicYear: e.target.value })}
          aria-invalid={Boolean(fieldErrors.academicYear) || undefined}
          placeholder="2026-27"
          disabled={isSaving}
          autoFocus
        />
      ),
    };
    const rows = [yearRow];
    for (const field of COST_FIELDS) {
      rows.push(numberRow(field));
    }
    return rows;
  };

  const requirementRows = (): TableRow[] => {
    if (editingTable !== "requirements") {
      return [
        { label: "GPA tối thiểu (thang 4)", value: formatNumber(offering.minGpa4) },
        { label: "GPA TB trúng tuyển", value: formatNumber(offering.avgGpa4) },
        { label: "IELTS tối thiểu", value: formatNumber(offering.minIelts) },
        { label: "TOEFL tối thiểu", value: formatNumber(offering.minToefl) },
        { label: "Duolingo tối thiểu", value: formatNumber(offering.minDuolingo) },
        { label: "SAT 25% – 75%", value: formatSatRange(offering.sat25, offering.sat75) },
        { label: "ACT 25% – 75%", value: formatSatRange(offering.act25, offering.act75) },
        { label: "Chính sách SAT", value: satPolicyLabel(offering.satPolicy) },
      ];
    }

    const rows: TableRow[] = [];
    REQUIREMENT_FIELDS.forEach((field, index) => {
      rows.push(numberRow(field, index === 0));
    });
    for (const field of TEST_FIELDS) {
      rows.push(numberRow(field));
    }
    const policyInputId = `offering-${offering.id}-sat-policy`;
    rows.push({
      label: "Chính sách SAT/ACT",
      inputId: policyInputId,
      error: fieldErrors.satPolicy,
      value: (
        <OptionSelect
          id={policyInputId}
          value={form.satPolicy}
          options={SAT_POLICY_OPTIONS}
          onChange={(value) => setForm({ ...form, satPolicy: value })}
          invalid={Boolean(fieldErrors.satPolicy)}
          disabled={isSaving}
        />
      ),
    });
    return rows;
  };

  const sourceRows = (): TableRow[] => {
    const lastEditedRow: TableRow = {
      label: "Sửa lần cuối",
      value: formatLastEdited(offering.updatedAt, offering.lastEditedBy),
    };

    if (editingTable !== "source") {
      const sourceLink = offering.sourceUrl ? (
        <a
          href={offering.sourceUrl}
          target="_blank"
          rel="noreferrer"
          className="text-brand underline-offset-2 hover:underline"
        >
          Mở liên kết
        </a>
      ) : (
        "—"
      );
      return [
        { label: "Nguồn", value: sourceLink },
        { label: "Ngày lấy", value: formatRetrievedAt(offering.retrievedAt) },
        { label: "Mức tin cậy", value: sourceReliabilityLabel(offering.sourceReliability) },
        { label: "Ghi chú", value: offering.notes ?? "—" },
        lastEditedRow,
      ];
    }

    const idPrefix = `offering-${offering.id}`;
    return [
      {
        label: "Liên kết nguồn",
        inputId: `${idPrefix}-source`,
        error: fieldErrors.sourceUrl,
        value: (
          <Input
            id={`${idPrefix}-source`}
            value={form.sourceUrl}
            onChange={(e) => setForm({ ...form, sourceUrl: e.target.value })}
            aria-invalid={Boolean(fieldErrors.sourceUrl) || undefined}
            placeholder="https://"
            disabled={isSaving}
            autoFocus
          />
        ),
      },
      {
        label: "Ngày lấy dữ liệu",
        inputId: `${idPrefix}-retrieved`,
        error: fieldErrors.retrievedAt,
        value: (
          <Input
            id={`${idPrefix}-retrieved`}
            type="date"
            value={form.retrievedAt}
            onChange={(e) => setForm({ ...form, retrievedAt: e.target.value })}
            aria-invalid={Boolean(fieldErrors.retrievedAt) || undefined}
            disabled={isSaving}
          />
        ),
      },
      {
        label: "Mức tin cậy",
        inputId: `${idPrefix}-reliability`,
        error: fieldErrors.sourceReliability,
        value: (
          <OptionSelect
            id={`${idPrefix}-reliability`}
            value={form.sourceReliability}
            options={SOURCE_RELIABILITY_OPTIONS}
            onChange={(value) => setForm({ ...form, sourceReliability: value })}
            invalid={Boolean(fieldErrors.sourceReliability)}
            disabled={isSaving}
          />
        ),
      },
      {
        label: "Ghi chú",
        inputId: `${idPrefix}-notes`,
        error: fieldErrors.notes,
        value: (
          <Textarea
            id={`${idPrefix}-notes`}
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            aria-invalid={Boolean(fieldErrors.notes) || undefined}
            maxLength={2000}
            disabled={isSaving}
          />
        ),
      },
      lastEditedRow,
    ];
  };

  const deadlines = parseDeadlines(offering.deadlinesJson);
  let deadlineRows: TableRow[] = [{ label: "Chưa có dữ liệu", value: "—" }];
  if (deadlines.length > 0) {
    deadlineRows = deadlines.map((deadline) => ({ label: deadline.round, value: deadline.date }));
  }

  const majorCount = isEditingMajors ? Object.keys(form.majors).length : offering.majors.length;

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-base font-semibold">{studyLevelLabel(offering.studyLevel)}</h3>
        {offering.academicYear && <Badge variant="outline">{offering.academicYear}</Badge>}
      </div>

      <EditableTable title="Chi phí mỗi năm" rows={costRows()} edit={editStateFor("cost")} />
      <EditableTable title="Điều kiện" rows={requirementRows()} edit={editStateFor("requirements")} />
      <EditableTable title="Hạn nộp hồ sơ" rows={deadlineRows} />

      <TableFrame title={`Ngành (${majorCount})`} edit={editStateFor("majors")}>
        <BlockBody>
          {isEditingMajors ? (
            <MajorChecklist
              offering={offering}
              options={majorOptions}
              loadError={majorLoadError}
              selected={form.majors}
              fieldError={fieldErrors.majors}
              disabled={isSaving}
              onToggleMajor={toggleMajor}
              onToggleStem={toggleStem}
            />
          ) : (
            <MajorBadges offering={offering} />
          )}
        </BlockBody>
      </TableFrame>

      <EditableTable title="Nguồn dữ liệu" rows={sourceRows()} edit={editStateFor("source")} />
    </section>
  );
}

function MajorBadges({ offering }: { offering: AdminUniversityOffering }) {
  const unverifiedCount = offering.majorItems.filter((item) => !item.isVerified).length;

  return (
    <>
      {offering.majors.length === 0 ? (
        <span className="text-muted-foreground">—</span>
      ) : (
        <div className="flex flex-wrap gap-1">
          {offering.majors.map((major) => (
            <Badge key={major} variant={offering.stemMajors.includes(major) ? "brand" : "outline"}>
              {major}
            </Badge>
          ))}
        </div>
      )}
      {offering.stemMajors.length > 0 && (
        <p className="mt-2 text-xs text-muted-foreground">Ngành tô đậm là ngành STEM.</p>
      )}
      {unverifiedCount > 0 && (
        <p className="mt-1 text-xs text-muted-foreground">
          {unverifiedCount} ngành lấy từ College Scorecard, chưa xác minh với CDS.
        </p>
      )}
    </>
  );
}

function MajorChecklist({
  offering,
  options,
  loadError,
  selected,
  fieldError,
  disabled,
  onToggleMajor,
  onToggleStem,
}: {
  offering: AdminUniversityOffering;
  options: AdminMajorOption[] | null;
  loadError: string | null;
  selected: Record<string, boolean>;
  fieldError: string | undefined;
  disabled: boolean;
  onToggleMajor: (majorId: string, included: boolean) => void;
  onToggleStem: (majorId: string, isStem: boolean) => void;
}) {
  const unverifiedMajorIds = new Set(
    offering.majorItems.filter((item) => !item.isVerified).map((item) => item.majorId),
  );

  if (loadError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{loadError}</AlertDescription>
      </Alert>
    );
  }

  if (!options) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Spinner />
        Đang tải danh mục ngành...
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {fieldError && <p className="text-xs text-destructive">{fieldError}</p>}
      <div className="-mx-3 max-h-80 overflow-y-auto">
        {options.map((major) => {
          const included = major.id in selected;
          return (
            <div
              key={major.id}
              className="flex items-center gap-3 border-b border-border px-3 py-1.5 last:border-b-0"
            >
              <label className="flex min-w-0 flex-1 items-center gap-2">
                <Checkbox
                  checked={included}
                  onCheckedChange={(checked) => onToggleMajor(major.id, checked === true)}
                  disabled={disabled}
                />
                <span className="min-w-0">
                  {major.name}
                  {major.cipCode && <span className="ml-1 text-xs text-muted-foreground">CIP {major.cipCode}</span>}
                </span>
                {included && unverifiedMajorIds.has(major.id) && <Badge variant="outline">Chưa xác minh</Badge>}
              </label>
              <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <Checkbox
                  checked={selected[major.id] === true}
                  onCheckedChange={(checked) => onToggleStem(major.id, checked === true)}
                  disabled={disabled || !included}
                />
                STEM
              </label>
            </div>
          );
        })}
      </div>
    </div>
  );
}
