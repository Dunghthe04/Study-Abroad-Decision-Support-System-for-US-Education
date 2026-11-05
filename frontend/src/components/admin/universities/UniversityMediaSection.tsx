"use client";

import { useState } from "react";
import { ExternalLinkIcon } from "lucide-react";
import { ImageUploadField } from "../shared/ImageUploadField";
import { toast } from "@/components/ui/toast";
import { textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, updateAdminUniversity } from "@/lib/admin-university-api";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { RowsTable, TableFrame, type TableRow } from "../shared/EditableTable";
import { UniversityMediaPreview } from "./UniversityMediaPreview";

interface UniversityMediaSectionProps {
  university: AdminUniversityDetail;
  isEditing: boolean;
  canEdit: boolean;
  onStartEdit: () => void;
  onStopEdit: () => void;
  onSaved: (university: AdminUniversityDetail) => void;
}

function mediaForm(university: AdminUniversityDetail) {
  return { logoUrl: toInputValue(university.logoUrl), coverUrl: toInputValue(university.coverUrl) };
}

function MediaLink({ url }: { url?: string | null }) {
  if (!url) return <>—</>;
  return (
    <a href={url} target="_blank" rel="noreferrer" className="inline-flex max-w-full items-center gap-1 text-xs text-brand hover:underline">
      <span className="max-w-44 truncate">{url}</span>
      <ExternalLinkIcon className="size-3 shrink-0" />
    </a>
  );
}

export function UniversityMediaSection({ university, isEditing, canEdit, onStartEdit, onStopEdit, onSaved }: UniversityMediaSectionProps) {
  const [form, setForm] = useState(() => mediaForm(university));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const hasChanges = JSON.stringify(form) !== JSON.stringify(mediaForm(university));

  const handleEdit = () => {
    setForm(mediaForm(university));
    setFieldErrors({});
    setError(null);
    onStartEdit();
  };

  const handleSave = async () => {
    if (isSaving || isUploading) return;
    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = await updateAdminUniversity(university.id, {
        name: university.name,
        city: university.city,
        state: university.state,
        website: university.website,
        control: university.control,
        acceptanceRate: university.acceptanceRate,
        internationalStudentCount: university.internationalStudentCount,
        isIvy: university.isIvy,
        logoUrl: textOrNull(form.logoUrl),
        coverUrl: textOrNull(form.coverUrl),
      });
      toast.add({ type: "success", title: "Đã lưu ảnh trường", description: saved.name });
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

  const rows: TableRow[] = [
    {
      label: "Logo trường",
      inputId: "media-logo",
      error: fieldErrors.logoUrl,
      value: isEditing ? (
        <ImageUploadField id="media-logo" value={form.logoUrl} onChange={(url) => setForm((current) => ({ ...current, logoUrl: url }))}
          purpose="school-logo" invalid={Boolean(fieldErrors.logoUrl)} disabled={isSaving || isUploading} onUploadingChange={setIsUploading} autoFocus />
      ) : <MediaLink url={university.logoUrl} />,
    },
    {
      label: "Ảnh bìa (Cover)",
      inputId: "media-cover",
      error: fieldErrors.coverUrl,
      value: isEditing ? (
        <ImageUploadField id="media-cover" value={form.coverUrl} onChange={(url) => setForm((current) => ({ ...current, coverUrl: url }))}
          purpose="school-cover" invalid={Boolean(fieldErrors.coverUrl)} disabled={isSaving || isUploading} onUploadingChange={setIsUploading} />
      ) : <MediaLink url={university.coverUrl} />,
    },
  ];

  return (
    <TableFrame title="Media" edit={{ isEditing, canEdit, isSaving: isSaving || isUploading, hasChanges, error, onEdit: handleEdit, onCancel: onStopEdit, onSave: handleSave }}>
      <UniversityMediaPreview name={university.name} logoUrl={isEditing ? form.logoUrl.trim() : university.logoUrl} coverUrl={isEditing ? form.coverUrl.trim() : university.coverUrl} />
      <RowsTable rows={rows} isEditing={isEditing} />
    </TableFrame>
  );
}
