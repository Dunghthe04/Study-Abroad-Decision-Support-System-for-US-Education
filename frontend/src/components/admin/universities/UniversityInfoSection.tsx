"use client";

import { useState } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, updateAdminUniversity } from "@/lib/admin-university-api";
import {
  CONTROL_OPTIONS,
  controlLabel,
  formatAcceptanceRate,
  formatLastEdited,
  formatNumber,
} from "@/lib/admin-university-display";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { EditableTable, type TableRow } from "./EditableTable";
import { OptionSelect } from "./OptionSelect";

interface UniversityInfoSectionProps {
  university: AdminUniversityDetail;
  isEditing: boolean;
  /** false khi đang sửa một bảng khác trong panel. */
  canEdit: boolean;
  onStartEdit: () => void;
  onStopEdit: () => void;
  onSaved: (university: AdminUniversityDetail) => void;
}

interface FormState {
  name: string;
  city: string;
  state: string;
  website: string;
  control: string;
  acceptancePercent: string;
  internationalStudentCount: string;
  isIvy: boolean;
}

function toFormState(university: AdminUniversityDetail): FormState {
  // Tỷ lệ nhận lưu 0–1, ô nhập theo phần trăm cho dễ đọc.
  let acceptancePercent = "";
  if (university.acceptanceRate !== null) {
    acceptancePercent = String(Math.round(university.acceptanceRate * 10000) / 100);
  }
  return {
    name: university.name,
    city: toInputValue(university.city),
    state: toInputValue(university.state),
    website: toInputValue(university.website),
    control: toInputValue(university.control),
    acceptancePercent,
    internationalStudentCount: toInputValue(university.internationalStudentCount),
    isIvy: university.isIvy,
  };
}

function viewRows(university: AdminUniversityDetail): TableRow[] {
  const website = university.website ? (
    <a
      href={university.website}
      target="_blank"
      rel="noreferrer"
      className="text-brand underline-offset-2 hover:underline"
    >
      Mở website
    </a>
  ) : (
    "—"
  );

  return [
    { label: "Tên trường", value: university.name },
    { label: "Thành phố", value: university.city ?? "—" },
    { label: "Bang", value: university.state ?? "—" },
    { label: "Loại trường", value: controlLabel(university.control) },
    { label: "Website", value: website },
    { label: "Tỷ lệ nhận", value: formatAcceptanceRate(university.acceptanceRate) },
    { label: "Số du học sinh", value: formatNumber(university.internationalStudentCount) },
    { label: "Ivy League", value: university.isIvy ? "Có" : "Không" },
    { label: "Sửa lần cuối", value: formatLastEdited(university.updatedAt, university.lastEditedBy) },
  ];
}

export function UniversityInfoSection({
  university,
  isEditing,
  canEdit,
  onStartEdit,
  onStopEdit,
  onSaved,
}: UniversityInfoSectionProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(university));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const hasChanges = JSON.stringify(form) !== JSON.stringify(toFormState(university));

  const setField = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm({ ...form, [key]: value });
  };

  const handleEdit = () => {
    setForm(toFormState(university));
    setFieldErrors({});
    setError(null);
    onStartEdit();
  };

  const handleSave = async () => {
    const acceptancePercent = numberOrNull(form.acceptancePercent);
    const internationalStudentCount = numberOrNull(form.internationalStudentCount);
    const notNumbers = findNotNumbers({
      acceptanceRate: acceptancePercent,
      internationalStudentCount,
    });
    if (notNumbers.length > 0) {
      const errors: AdminFieldErrors = {};
      for (const field of notNumbers) {
        errors[field] = "Vui lòng nhập số.";
      }
      setFieldErrors(errors);
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    let acceptanceRate: number | null = null;
    if (acceptancePercent !== null) {
      acceptanceRate = Math.round(acceptancePercent * 100) / 10000;
    }

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = await updateAdminUniversity(university.id, {
        name: form.name.trim(),
        city: textOrNull(form.city),
        state: textOrNull(form.state),
        website: textOrNull(form.website),
        control: textOrNull(form.control),
        acceptanceRate,
        internationalStudentCount,
        isIvy: form.isIvy,
        logoUrl: university.logoUrl,
        coverUrl: university.coverUrl,
      });
      toast.add({ type: "success", title: "Đã lưu thông tin trường", description: saved.name });
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

  const edit = {
    isEditing,
    canEdit,
    isSaving,
    hasChanges,
    error,
    onEdit: handleEdit,
    onCancel: onStopEdit,
    onSave: handleSave,
  };

  if (!isEditing) {
    return <EditableTable title="Thông tin trường" rows={viewRows(university)} edit={edit} />;
  }

  const editRows: TableRow[] = [
    {
      label: "Tên trường *",
      inputId: "uni-name",
      error: fieldErrors.name,
      value: (
        <Input
          id="uni-name"
          value={form.name}
          onChange={(e) => setField("name", e.target.value)}
          aria-invalid={Boolean(fieldErrors.name) || undefined}
          disabled={isSaving}
          required
          autoFocus
        />
      ),
    },
    {
      label: "Thành phố",
      inputId: "uni-city",
      error: fieldErrors.city,
      value: (
        <Input
          id="uni-city"
          value={form.city}
          onChange={(e) => setField("city", e.target.value)}
          aria-invalid={Boolean(fieldErrors.city) || undefined}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Bang",
      inputId: "uni-state",
      error: fieldErrors.state,
      value: (
        <Input
          id="uni-state"
          value={form.state}
          onChange={(e) => setField("state", e.target.value.toUpperCase())}
          aria-invalid={Boolean(fieldErrors.state) || undefined}
          maxLength={2}
          placeholder="CA"
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Loại trường",
      inputId: "uni-control",
      error: fieldErrors.control,
      value: (
        <OptionSelect
          id="uni-control"
          value={form.control}
          options={CONTROL_OPTIONS}
          onChange={(value) => setField("control", value)}
          invalid={Boolean(fieldErrors.control)}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Website",
      inputId: "uni-website",
      error: fieldErrors.website,
      value: (
        <Input
          id="uni-website"
          value={form.website}
          onChange={(e) => setField("website", e.target.value)}
          aria-invalid={Boolean(fieldErrors.website) || undefined}
          placeholder="https://"
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Tỷ lệ nhận (%)",
      inputId: "uni-acceptance",
      error: fieldErrors.acceptanceRate,
      value: (
        <Input
          id="uni-acceptance"
          inputMode="decimal"
          value={form.acceptancePercent}
          onChange={(e) => setField("acceptancePercent", e.target.value)}
          aria-invalid={Boolean(fieldErrors.acceptanceRate) || undefined}
          placeholder="Ví dụ: 48.2"
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Số du học sinh",
      inputId: "uni-intl",
      error: fieldErrors.internationalStudentCount,
      value: (
        <Input
          id="uni-intl"
          inputMode="numeric"
          value={form.internationalStudentCount}
          onChange={(e) => setField("internationalStudentCount", e.target.value)}
          aria-invalid={Boolean(fieldErrors.internationalStudentCount) || undefined}
          disabled={isSaving}
        />
      ),
    },
    {
      label: "Ivy League",
      value: (
        <label className="flex items-center gap-2">
          <Checkbox
            checked={form.isIvy}
            onCheckedChange={(checked) => setField("isIvy", checked === true)}
            disabled={isSaving}
          />
          Thuộc Ivy League
        </label>
      ),
    },
  ];

  return <EditableTable title="Thông tin trường" rows={editRows} edit={edit} />;
}
