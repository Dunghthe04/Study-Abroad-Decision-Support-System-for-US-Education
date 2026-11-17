"use client";

import { useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { createAdminScholarship, updateAdminScholarship } from "@/lib/admin-scholarship-api";
import { COVERAGE_OPTIONS, scholarshipValueText } from "@/lib/admin-scholarship-display";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue, todayIso } from "@/lib/admin-form";
import { AdminFormError } from "@/lib/admin-university-api";
import {
  formatNumber,
  formatRetrievedAt,
  SOURCE_RELIABILITY_OPTIONS,
  sourceReliabilityLabel,
} from "@/lib/admin-university-display";
import { STUDY_LEVELS, studyLevelLabel } from "@/lib/study-levels";
import type { AdminScholarship } from "@/types/admin-scholarship";
import type { AdminFieldErrors } from "@/types/admin-university";
import { EditableTable, type TableRow } from "../universities/EditableTable";
import { OptionSelect } from "../universities/OptionSelect";
import { ScholarshipVisibilityToggle } from "./ScholarshipVisibilityToggle";
import { UniversityPicker } from "./UniversityPicker";

const LEVEL_OPTIONS = STUDY_LEVELS.map((level) => ({ value: level, label: studyLevelLabel(level) }));

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

function sourceLink(url: string | null) {
  if (!url) return "—";
  return (
    <a href={url} target="_blank" rel="noreferrer" className="text-brand underline-offset-2 hover:underline">
      Mở nguồn
    </a>
  );
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
    const notNumbers = findNotNumbers({ amountUsd, minGpa4, minIelts });
    if (notNumbers.length > 0) {
      setFieldErrors(Object.fromEntries(notNumbers.map((field) => [field, "Vui lòng nhập số."])));
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    const body = {
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
      { label: "GPA tối thiểu (thang 4)", value: formatNumber(scholarship.minGpa4) },
      { label: "IELTS tối thiểu", value: formatNumber(scholarship.minIelts) },
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
            label: "Trường",
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
      label: lockedUniversityId ? "Đơn vị cấp" : "Đơn vị cấp (nếu không gắn trường) *",
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

