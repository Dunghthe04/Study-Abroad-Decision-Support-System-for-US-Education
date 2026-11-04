"use client";

import { useState } from "react";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull } from "@/lib/admin-form";
import { AdminFormError, updateAdminUniversity } from "@/lib/admin-university-api";
import {
  controlLabel,
  formatAcceptanceRate,
  formatLastEdited,
  formatNumber,
} from "@/lib/admin-university-display";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { EditableTable, type TableRow } from "./EditableTable";
import {
  buildUniversityEditRows,
  universityToFormState,
  type UniversityFormState,
} from "./UniversityFormFields";

interface UniversityInfoSectionProps {
  university: AdminUniversityDetail;
  isEditing: boolean;
  /** false khi đang sửa một bảng khác trong panel. */
  canEdit: boolean;
  onStartEdit: () => void;
  onStopEdit: () => void;
  onSaved: (university: AdminUniversityDetail) => void;
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
  const [form, setForm] = useState<UniversityFormState>(() => universityToFormState(university));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const hasChanges = JSON.stringify(form) !== JSON.stringify(universityToFormState(university));

  const setField = <K extends keyof UniversityFormState>(key: K, value: UniversityFormState[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleEdit = () => {
    setForm(universityToFormState(university));
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

  const editRows = buildUniversityEditRows({
    form,
    setField,
    fieldErrors,
    isSaving,
    includeCode: false,
    includeMedia: false,
  });

  return <EditableTable title="Thông tin trường" rows={editRows} edit={edit} />;
}
