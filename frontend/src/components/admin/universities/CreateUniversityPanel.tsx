"use client";

import { useState } from "react";
import { PlusIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull } from "@/lib/admin-form";
import { AdminFormError, createAdminUniversity } from "@/lib/admin-university-api";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { DetailPanel } from "../shared/DetailPanel";
import { RowsTable, TableFrame } from "../shared/EditableTable";
import { UniversityMediaPreview } from "./UniversityMediaPreview";
import { buildUniversityEditRows, EMPTY_UNIVERSITY_FORM, type UniversityFormState } from "./UniversityFormFields";

interface CreateUniversityPanelProps {
  onClose: () => void;
  onCreated: (university: AdminUniversityDetail) => void;
}

export function CreateUniversityPanel({ onClose, onCreated }: CreateUniversityPanelProps) {
  const [form, setForm] = useState<UniversityFormState>({ ...EMPTY_UNIVERSITY_FORM });
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const busy = isSaving || isUploading;

  const setField = <K extends keyof UniversityFormState>(key: K, value: UniversityFormState[K]) => {
    setForm((previous) => ({ ...previous, [key]: value }));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (busy) return;

    const acceptancePercent = numberOrNull(form.acceptancePercent);
    const internationalStudentCount = numberOrNull(form.internationalStudentCount);
    const errors: AdminFieldErrors = {};
    if (!form.code?.trim()) errors.code = "Vui lòng nhập mã trường.";
    if (!form.name.trim()) errors.name = "Vui lòng nhập tên trường.";
    for (const field of findNotNumbers({ acceptanceRate: acceptancePercent, internationalStudentCount })) {
      errors[field] = "Vui lòng nhập số.";
    }
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    let acceptanceRate: number | null = null;
    if (acceptancePercent !== null) acceptanceRate = Math.round(acceptancePercent * 100) / 10000;

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const created = await createAdminUniversity({
        code: (form.code ?? "").trim().toUpperCase(),
        name: form.name.trim(),
        city: textOrNull(form.city),
        state: textOrNull(form.state),
        website: textOrNull(form.website),
        control: textOrNull(form.control),
        acceptanceRate,
        internationalStudentCount,
        isIvy: form.isIvy,
        logoUrl: textOrNull(form.logoUrl ?? ""),
        coverUrl: textOrNull(form.coverUrl ?? ""),
      });
      toast.add({ type: "success", title: "Đã thêm trường mới", description: created.name });
      onCreated(created);
    } catch (err) {
      if (err instanceof AdminFormError) {
        setError(err.message);
        setFieldErrors(err.fieldErrors);
      } else {
        setError("Không thể tạo trường. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const rows = buildUniversityEditRows({ form, setField, fieldErrors, isSaving: busy, includeCode: true, includeMedia: true, onUploadingChange: setIsUploading });
  const mediaRows = rows.filter((row) => row.inputId === "uni-logo" || row.inputId === "uni-cover");
  const infoRows = rows.filter((row) => row.inputId !== "uni-logo" && row.inputId !== "uni-cover");

  return (
    <DetailPanel title="Thêm trường mới" description="Nhập thông tin trường; mã và tên là bắt buộc." onClose={onClose} closeDisabled={busy}>
      <form onSubmit={handleSubmit} className="space-y-5">
        {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
        <TableFrame title="Media">
          <UniversityMediaPreview logoUrl={form.logoUrl} coverUrl={form.coverUrl} name={form.name || "Trường mới"} />
          <RowsTable rows={mediaRows} isEditing />
        </TableFrame>
        <TableFrame title="Thông tin trường">
          <RowsTable rows={infoRows} isEditing />
        </TableFrame>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onClose} disabled={busy}>Hủy</Button>
          <Button type="submit" disabled={busy}>
            {isSaving ? <Spinner /> : <PlusIcon />} Tạo trường
          </Button>
        </div>
      </form>
    </DetailPanel>
  );
}
