"use client";

import { useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { createAdminScholarship, updateAdminScholarship } from "@/lib/admin-scholarship-api";
import {
  AMOUNT_TYPE_LABELS,
  AMOUNT_TYPE_OPTIONS,
  APPLICATION_METHOD_LABELS,
  APPLICATION_METHOD_OPTIONS,
  COVERAGE_OPTIONS,
  KIND_LABELS,
  KIND_OPTIONS,
  scholarshipValueText,
  TRI_STATE_OPTIONS,
  triStateLabel,
} from "@/lib/admin-scholarship-display";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue, todayIso } from "@/lib/admin-form";
import { AdminFormError } from "@/lib/admin-university-api";
import {
  formatNumber,
  formatRetrievedAt,
  formatUsd,
  SOURCE_RELIABILITY_OPTIONS,
  sourceReliabilityLabel,
} from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import type { AdminScholarship, SaveScholarshipRequest } from "@/types/admin-scholarship";
import type { AdminFieldErrors } from "@/types/admin-university";
import { EditableTable, type TableRow } from "../universities/EditableTable";
import { OptionSelect } from "../universities/OptionSelect";
import { ScholarshipVisibilityToggle } from "./ScholarshipVisibilityToggle";
import { UniversityPicker } from "./UniversityPicker";

// UC-103: chỉ hỗ trợ học bổng bậc đại học (undergraduate)
const LEVEL_OPTIONS = [{ value: "undergraduate", label: studyLevelLabel("undergraduate") }];

// [USAS-2067] Cột bộ dữ liệu học bổng: ô số, ô Có/Không/Chưa rõ, ô chọn, ô chữ
const DATASET_NUMBER_LABELS = {
  amountMinUsd: "Giá trị thấp nhất (USD)",
  amountMaxUsd: "Giá trị cao nhất (USD)",
  durationYears: "Thời hạn (năm)",
  minSat: "SAT tối thiểu",
  minAct: "ACT tối thiểu",
  minToefl: "TOEFL iBT tối thiểu",
  minDuolingo: "Duolingo tối thiểu (10–160)",
} as const;
type DatasetNumberKey = keyof typeof DATASET_NUMBER_LABELS;
const DATASET_NUMBER_KEYS = Object.keys(DATASET_NUMBER_LABELS) as DatasetNumberKey[];

const TRI_STATE_LABELS = {
  renewable: "Được gia hạn",
  intlEligible: "Mở cho du học sinh",
  vnEligible: "Mở cho người Việt Nam",
  needBased: "Xét theo hoàn cảnh tài chính",
  meritBased: "Xét theo thành tích",
} as const;
type TriStateKey = keyof typeof TRI_STATE_LABELS;
const TRI_STATE_KEYS = Object.keys(TRI_STATE_LABELS) as TriStateKey[];

const triToBool = (value: string): boolean | null => (value === "" ? null : value === "true");
const boolToTri = (value: boolean | null | undefined): string => (value == null ? "" : String(value));

interface FormState {
  universityId: string;
  universityName: string;
  name: string;
  provider: string;
  studyLevel: string;
  amountUsd: string;
  coverageType: string;
  minGpa4: string;
  minIelts: string;
  deadline: string;
  eligibilityNotes: string;
  sourceUrl: string;
  retrievedAt: string;
  sourceReliability: string;
  kind: string;
  amountType: string;
  applicationMethod: string;
  renewalCriteria: string;
  majorRestriction: string;
  applyUrl: string;
  numbers: Record<DatasetNumberKey, string>;
  flags: Record<TriStateKey, string>;
}

function toForm(s: AdminScholarship | null): FormState {
  return {
    universityId: toInputValue(s?.universityId),
    universityName: toInputValue(s?.universityName),
    name: toInputValue(s?.name),
    provider: toInputValue(s?.provider),
    studyLevel: s?.studyLevel ?? "undergraduate",
    amountUsd: toInputValue(s?.amountUsd),
    coverageType: toInputValue(s?.coverageType),
    minGpa4: toInputValue(s?.minGpa4),
    minIelts: toInputValue(s?.minIelts),
    deadline: toInputValue(s?.deadline),
    eligibilityNotes: toInputValue(s?.eligibilityNotes),
    sourceUrl: toInputValue(s?.sourceUrl),
    retrievedAt: toInputValue(s?.retrievedAt),
    sourceReliability: toInputValue(s?.sourceReliability),
    kind: toInputValue(s?.kind),
    amountType: toInputValue(s?.amountType),
    applicationMethod: toInputValue(s?.applicationMethod),
    renewalCriteria: toInputValue(s?.renewalCriteria),
    majorRestriction: toInputValue(s?.majorRestriction),
    applyUrl: toInputValue(s?.applyUrl),
    numbers: Object.fromEntries(DATASET_NUMBER_KEYS.map((key) => [key, toInputValue(s?.[key])])) as Record<DatasetNumberKey, string>,
    flags: Object.fromEntries(TRI_STATE_KEYS.map((key) => [key, boolToTri(s?.[key])])) as Record<TriStateKey, string>,
  };
}

interface ScholarshipTableProps {
  /** null = học bổng mới. */
  scholarship: AdminScholarship | null;
  /** Trong panel của một trường: học bổng luôn thuộc trường này, không hiện ô chọn trường. */
  lockedUniversityId?: string;
  isEditing: boolean;
  canEdit: boolean;
  onStartEdit: () => void;
  onCancel: () => void;
  onSaved: (scholarship: AdminScholarship) => void;
  onVisibilityChanged: (scholarship: AdminScholarship) => void;
}

function sourceLink(url: string | null, text = "Mở nguồn") {
  if (!url) return "—";
  return (
    <a href={url} target="_blank" rel="noreferrer" className="text-brand underline-offset-2 hover:underline">
      {text}
    </a>
  );
}

function rangeText(min: number | null, max: number | null): string {
  if (min === null && max === null) return "—";
  return `${min === null ? "?" : formatUsd(min)} – ${max === null ? "?" : formatUsd(max)}`;
}

export function ScholarshipTable({
  scholarship,
  lockedUniversityId,
  isEditing,
  canEdit,
  onStartEdit,
  onCancel,
  onSaved,
  onVisibilityChanged,
}: ScholarshipTableProps) {
  // [UC-109] Chỉ Moderator nhập và sửa học bổng; Admin chỉ xem
  const { user } = useAuth();
  const isModerator = user?.role === "moderator";
  const [form, setForm] = useState<FormState>(() => toForm(scholarship));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const hasChanges = scholarship === null || JSON.stringify(form) !== JSON.stringify(toForm(scholarship));
  const title = scholarship ? "Học bổng" : "Học bổng mới";
  const idPrefix = `scholarship-${scholarship?.id ?? "new"}`;

  const setField = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((prev) => ({ ...prev, [key]: value }));

  const handleEdit = () => {
    setForm(toForm(scholarship));
    setFieldErrors({});
    setError(null);
    onStartEdit();
  };

  const handleSave = async () => {
    const amountUsd = numberOrNull(form.amountUsd);
    const minGpa4 = numberOrNull(form.minGpa4);
    const minIelts = numberOrNull(form.minIelts);
    const datasetNumbers = Object.fromEntries(
      DATASET_NUMBER_KEYS.map((key) => [key, numberOrNull(form.numbers[key])]),
    ) as Record<DatasetNumberKey, number | null>;
    const notNumbers = findNotNumbers({ amountUsd, minGpa4, minIelts, ...datasetNumbers });
    if (notNumbers.length > 0) {
      setFieldErrors(Object.fromEntries(notNumbers.map((field) => [field, "Vui lòng nhập số."])));
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    const body: SaveScholarshipRequest = {
      ...datasetNumbers,
      ...(Object.fromEntries(TRI_STATE_KEYS.map((key) => [key, triToBool(form.flags[key])])) as Record<TriStateKey, boolean | null>),
      kind: textOrNull(form.kind),
      amountType: textOrNull(form.amountType),
      applicationMethod: textOrNull(form.applicationMethod),
      renewalCriteria: textOrNull(form.renewalCriteria),
      majorRestriction: textOrNull(form.majorRestriction),
      applyUrl: textOrNull(form.applyUrl),
      universityId: lockedUniversityId ?? textOrNull(form.universityId),
      name: textOrNull(form.name),
      provider: textOrNull(form.provider),
      studyLevel: form.studyLevel,
      amountUsd,
      coverageType: textOrNull(form.coverageType),
      minGpa4,
      minIelts,
      deadline: textOrNull(form.deadline),
      eligibilityNotes: textOrNull(form.eligibilityNotes),
      sourceUrl: textOrNull(form.sourceUrl),
      retrievedAt: textOrNull(form.retrievedAt),
      sourceReliability: textOrNull(form.sourceReliability),
    };

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = scholarship ? await updateAdminScholarship(scholarship.id, body) : await createAdminScholarship(body);
      toast.add({ type: "success", title: scholarship ? "Đã lưu học bổng" : "Đã thêm học bổng", description: saved.name });
      onSaved(saved);
    } catch (err) {
      if (err instanceof AdminFormError) {
        setError(err.fieldErrors.universityId ?? err.message);
        setFieldErrors(err.fieldErrors);
      } else {
        setError("Không thể lưu. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const edit = {
    isEditing,
    canEdit,
    readOnly: !isModerator,
    isSaving,
    hasChanges,
    error,
    onEdit: handleEdit,
    onCancel,
    onSave: handleSave,
  };

  if (!isEditing && scholarship) {
    const rows: TableRow[] = [
      { label: "Tên học bổng", value: scholarship.name },
      ...(lockedUniversityId ? [] : [{ label: "Trường", value: scholarship.universityName ?? "—" }]),
      { label: "Đơn vị cấp", value: scholarship.provider ?? "—" },
      { label: "Bậc học", value: studyLevelLabel(scholarship.studyLevel) },
      { label: "Giá trị", value: scholarshipValueText(scholarship) },
      { label: "Phân loại", value: scholarship.kind ? (KIND_LABELS[scholarship.kind] ?? scholarship.kind) : "—" },
      { label: "Kiểu giá trị", value: scholarship.amountType ? (AMOUNT_TYPE_LABELS[scholarship.amountType] ?? scholarship.amountType) : "—" },
      { label: "Khoảng giá trị", value: rangeText(scholarship.amountMinUsd, scholarship.amountMaxUsd) },
      { label: "Thời hạn (năm)", value: formatNumber(scholarship.durationYears) },
      { label: "Được gia hạn", value: triStateLabel(scholarship.renewable) },
      { label: "Điều kiện gia hạn", value: scholarship.renewalCriteria ?? "—" },
      { label: "GPA tối thiểu (thang 4)", value: formatNumber(scholarship.minGpa4) },
      { label: "IELTS tối thiểu", value: formatNumber(scholarship.minIelts) },
      { label: "TOEFL iBT tối thiểu", value: formatNumber(scholarship.minToefl) },
      { label: "Duolingo tối thiểu", value: formatNumber(scholarship.minDuolingo) },
      { label: "SAT tối thiểu", value: formatNumber(scholarship.minSat) },
      { label: "ACT tối thiểu", value: formatNumber(scholarship.minAct) },
      { label: "Mở cho du học sinh", value: triStateLabel(scholarship.intlEligible) },
      { label: "Mở cho người Việt Nam", value: triStateLabel(scholarship.vnEligible) },
      { label: "Xét theo hoàn cảnh tài chính", value: triStateLabel(scholarship.needBased) },
      { label: "Xét theo thành tích", value: triStateLabel(scholarship.meritBased) },
      { label: "Giới hạn ngành", value: scholarship.majorRestriction ?? "—" },
      { label: "Cách nộp", value: scholarship.applicationMethod ? (APPLICATION_METHOD_LABELS[scholarship.applicationMethod] ?? scholarship.applicationMethod) : "—" },
      { label: "Link nộp", value: sourceLink(scholarship.applyUrl, "Mở link nộp") },
      { label: "Hạn nộp", value: formatRetrievedAt(scholarship.deadline) },
      { label: "Điều kiện", value: scholarship.eligibilityNotes ?? "—" },
      { label: "Nguồn", value: sourceLink(scholarship.sourceUrl) },
      { label: "Ngày lấy dữ liệu", value: formatRetrievedAt(scholarship.retrievedAt) },
      { label: "Mức tin cậy", value: sourceReliabilityLabel(scholarship.sourceReliability) },
      {
        label: "Hiển thị với học sinh",
        value: <ScholarshipVisibilityToggle scholarship={scholarship} canChange={isModerator && canEdit} onChanged={onVisibilityChanged} />,
      },
    ];
    return <EditableTable title={title} rows={rows} edit={edit} />;
  }

  const numberRow = (key: DatasetNumberKey): TableRow => ({
    label: DATASET_NUMBER_LABELS[key],
    inputId: `${idPrefix}-${key}`,
    error: fieldErrors[key],
    value: (
      <Input
        id={`${idPrefix}-${key}`}
        inputMode="decimal"
        value={form.numbers[key]}
        onChange={(e) => setForm((prev) => ({ ...prev, numbers: { ...prev.numbers, [key]: e.target.value } }))}
        aria-invalid={Boolean(fieldErrors[key]) || undefined}
        disabled={isSaving}
      />
    ),
  });
  const triRow = (key: TriStateKey): TableRow => ({
    label: TRI_STATE_LABELS[key],
    inputId: `${idPrefix}-${key}`,
    error: fieldErrors[key],
    value: (
      <OptionSelect
        id={`${idPrefix}-${key}`}
        value={form.flags[key]}
        options={TRI_STATE_OPTIONS}
        onChange={(value) => setForm((prev) => ({ ...prev, flags: { ...prev.flags, [key]: value } }))}
        invalid={Boolean(fieldErrors[key])}
        disabled={isSaving}
      />
    ),
  });
  const selectRow = (
    key: "kind" | "amountType" | "applicationMethod",
    label: string,
    options: { value: string; label: string }[],
  ): TableRow => ({
    label,
    inputId: `${idPrefix}-${key}`,
    error: fieldErrors[key],
    value: (
      <OptionSelect
        id={`${idPrefix}-${key}`}
        value={form[key]}
        options={options}
        onChange={(value) => setField(key, value)}
        invalid={Boolean(fieldErrors[key])}
        disabled={isSaving}
      />
    ),
  });
  const textRow = (
    key: "renewalCriteria" | "majorRestriction" | "applyUrl",
    label: string,
    maxLength: number,
    placeholder?: string,
  ): TableRow => ({
    label,
    inputId: `${idPrefix}-${key}`,
    error: fieldErrors[key],
    value: (
      <Input
        id={`${idPrefix}-${key}`}
        value={form[key]}
        onChange={(e) => setField(key, e.target.value)}
        aria-invalid={Boolean(fieldErrors[key]) || undefined}
        maxLength={maxLength}
        placeholder={placeholder}
        disabled={isSaving}
      />
    ),
  });

  const rows: TableRow[] = [
    {
      label: "Tên học bổng *",
      inputId: `${idPrefix}-name`,
      error: fieldErrors.name,
      value: (
        <Input
          id={`${idPrefix}-name`}
          value={form.name}
          onChange={(e) => setField("name", e.target.value)}
          aria-invalid={Boolean(fieldErrors.name) || undefined}
          maxLength={256}
          disabled={isSaving}
          autoFocus
        />
      ),
    },
    ...(lockedUniversityId
      ? []
      : [
          {
            label: "Trường *",
            inputId: `${idPrefix}-university`,
            error: fieldErrors.universityId,
            value: (
              <UniversityPicker
                id={`${idPrefix}-university`}
                value={form.universityId}
                valueName={form.universityName}
                onChange={(id, name) => setForm((prev) => ({ ...prev, universityId: id, universityName: name }))}
                invalid={Boolean(fieldErrors.universityId)}
                disabled={isSaving}
              />
            ),
          },
        ]),
    {
      label: "Đơn vị cấp",
      inputId: `${idPrefix}-provider`,
      error: fieldErrors.provider,
      value: (
        <Input
          id={`${idPrefix}-provider`}
          value={form.provider}
          onChange={(e) => setField("provider", e.target.value)}
          aria-invalid={Boolean(fieldErrors.provider) || undefined}
          maxLength={256}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Bậc học *",
      inputId: `${idPrefix}-level`,
      error: fieldErrors.studyLevel,
      value: (
        <OptionSelect
          id={`${idPrefix}-level`}
          value={form.studyLevel}
          options={LEVEL_OPTIONS}
          onChange={(value) => setField("studyLevel", value)}
          invalid={Boolean(fieldErrors.studyLevel)}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Giá trị (USD)",
      inputId: `${idPrefix}-amount`,
      error: fieldErrors.amountUsd,
      value: (
        <Input
          id={`${idPrefix}-amount`}
          inputMode="decimal"
          value={form.amountUsd}
          onChange={(e) => setField("amountUsd", e.target.value)}
          aria-invalid={Boolean(fieldErrors.amountUsd) || undefined}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Loại học bổng",
      inputId: `${idPrefix}-coverage`,
      error: fieldErrors.coverageType,
      value: (
        <OptionSelect
          id={`${idPrefix}-coverage`}
          value={form.coverageType}
          options={COVERAGE_OPTIONS}
          onChange={(value) => setField("coverageType", value)}
          invalid={Boolean(fieldErrors.coverageType)}
          disabled={isSaving}
        />
      ),
    },
    selectRow("kind", "Phân loại", KIND_OPTIONS),
    selectRow("amountType", "Kiểu giá trị", AMOUNT_TYPE_OPTIONS),
    numberRow("amountMinUsd"),
    numberRow("amountMaxUsd"),
    numberRow("durationYears"),
    triRow("renewable"),
    textRow("renewalCriteria", "Điều kiện gia hạn", 1000),
    {
      label: "GPA tối thiểu (thang 4)",
      inputId: `${idPrefix}-gpa`,
      error: fieldErrors.minGpa4,
      value: (
        <Input
          id={`${idPrefix}-gpa`}
          inputMode="decimal"
          value={form.minGpa4}
          onChange={(e) => setField("minGpa4", e.target.value)}
          aria-invalid={Boolean(fieldErrors.minGpa4) || undefined}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "IELTS tối thiểu",
      inputId: `${idPrefix}-ielts`,
      error: fieldErrors.minIelts,
      value: (
        <Input
          id={`${idPrefix}-ielts`}
          inputMode="decimal"
          value={form.minIelts}
          onChange={(e) => setField("minIelts", e.target.value)}
          aria-invalid={Boolean(fieldErrors.minIelts) || undefined}
          disabled={isSaving}
        />
      ),
    },
    numberRow("minToefl"),
    numberRow("minDuolingo"),
    numberRow("minSat"),
    numberRow("minAct"),
    triRow("intlEligible"),
    triRow("vnEligible"),
    triRow("needBased"),
    triRow("meritBased"),
    textRow("majorRestriction", "Giới hạn ngành", 500),
    selectRow("applicationMethod", "Cách nộp", APPLICATION_METHOD_OPTIONS),
    textRow("applyUrl", "Link nộp", 512, "https://"),
    {
      label: "Hạn nộp",
      inputId: `${idPrefix}-deadline`,
      error: fieldErrors.deadline,
      value: (
        <Input
          id={`${idPrefix}-deadline`}
          type="date"
          value={form.deadline}
          onChange={(e) => setField("deadline", e.target.value)}
          aria-invalid={Boolean(fieldErrors.deadline) || undefined}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Điều kiện",
      inputId: `${idPrefix}-notes`,
      error: fieldErrors.eligibilityNotes,
      value: (
        <Textarea
          id={`${idPrefix}-notes`}
          value={form.eligibilityNotes}
          onChange={(e) => setField("eligibilityNotes", e.target.value)}
          aria-invalid={Boolean(fieldErrors.eligibilityNotes) || undefined}
          maxLength={2000}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Liên kết nguồn *",
      inputId: `${idPrefix}-source`,
      error: fieldErrors.sourceUrl,
      value: (
        <Input
          id={`${idPrefix}-source`}
          type="url"
          value={form.sourceUrl}
          onChange={(e) => setField("sourceUrl", e.target.value)}
          aria-invalid={Boolean(fieldErrors.sourceUrl) || undefined}
          placeholder="https://"
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Ngày lấy dữ liệu *",
      inputId: `${idPrefix}-retrieved`,
      error: fieldErrors.retrievedAt,
      value: (
        <Input
          id={`${idPrefix}-retrieved`}
          type="date"
          max={todayIso()}
          value={form.retrievedAt}
          onChange={(e) => setField("retrievedAt", e.target.value)}
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
          onChange={(value) => setField("sourceReliability", value)}
          invalid={Boolean(fieldErrors.sourceReliability)}
          disabled={isSaving}
        />
      ),
    },
  ];

  return <EditableTable title={title} rows={rows} edit={edit} />;
}

