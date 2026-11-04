"use client";

import { useState } from "react";
import { ExternalLinkIcon, ImageIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/toast";
import { textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, updateAdminUniversity } from "@/lib/admin-university-api";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { TableFrame } from "./EditableTable";

interface UniversityMediaSectionProps {
  university: AdminUniversityDetail;
  isEditing: boolean;
  canEdit: boolean;
  onStartEdit: () => void;
  onStopEdit: () => void;
  onSaved: (university: AdminUniversityDetail) => void;
}

interface FormState {
  logoUrl: string;
  coverUrl: string;
}

function toFormState(university: AdminUniversityDetail): FormState {
  return {
    logoUrl: toInputValue(university.logoUrl),
    coverUrl: toInputValue(university.coverUrl),
  };
}

export function UniversityMediaSection({
  university,
  isEditing,
  canEdit,
  onStartEdit,
  onStopEdit,
  onSaved,
}: UniversityMediaSectionProps) {
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

  const activeCover = isEditing ? form.coverUrl.trim() : (university.coverUrl ?? "");
  const activeLogo = isEditing ? form.logoUrl.trim() : (university.logoUrl ?? "");

  return (
    <TableFrame title="Media" edit={edit}>
      <div className="relative h-32 w-full border-b border-border bg-muted/40">
        {activeCover ? (
          <img
            src={activeCover}
            alt="Ảnh bìa trường"
            className="h-full w-full object-cover"
            onError={(e) => {
              (e.target as HTMLElement).style.display = "none";
            }}
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center gap-1.5 text-xs text-muted-foreground">
            <ImageIcon className="size-4" />
            Chưa có ảnh bìa
          </div>
        )}

        <div className="absolute bottom-2 left-3">
          {activeLogo ? (
            <img
              src={activeLogo}
              alt={university.name}
              className="size-12 bg-white object-contain p-1 shadow-sm"
              onError={(e) => {
                (e.target as HTMLElement).style.display = "none";
              }}
            />
          ) : (
            <div className="flex size-12 items-center justify-center bg-card text-xs font-bold text-muted-foreground uppercase shadow-sm">
              {university.code.slice(0, 2)}
            </div>
          )}
        </div>
      </div>

      <table className="w-full border-collapse">
        <tbody>
          {!isEditing ? (
            <>
              <tr>
                <th
                  scope="row"
                  className="w-1/2 border-r border-border bg-muted px-3 py-1.5 text-left align-middle text-xs font-normal text-muted-foreground"
                >
                  Logo trường
                </th>
                <td className="px-3 py-1.5 text-right text-sm font-medium">
                  {university.logoUrl ? (
                    <a
                      href={university.logoUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex max-w-[200px] items-center gap-1 truncate text-xs text-brand underline-offset-2 hover:underline"
                    >
                      <span className="truncate">{university.logoUrl}</span>
                      <ExternalLinkIcon className="size-3 shrink-0" />
                    </a>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
              <tr>
                <th
                  scope="row"
                  className="w-1/2 border-t border-r border-border bg-muted px-3 py-1.5 text-left align-middle text-xs font-normal text-muted-foreground"
                >
                  Ảnh bìa (Cover)
                </th>
                <td className="border-t border-border px-3 py-1.5 text-right text-sm font-medium">
                  {university.coverUrl ? (
                    <a
                      href={university.coverUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex max-w-[200px] items-center gap-1 truncate text-xs text-brand underline-offset-2 hover:underline"
                    >
                      <span className="truncate">{university.coverUrl}</span>
                      <ExternalLinkIcon className="size-3 shrink-0" />
                    </a>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            </>
          ) : (
            <>
              <tr>
                <th
                  scope="row"
                  className="w-1/2 border-r border-border bg-muted px-3 py-1.5 text-left align-middle text-xs font-normal text-muted-foreground"
                >
                  <label htmlFor="media-logo">Logo URL</label>
                  {fieldErrors.logoUrl && (
                    <div className="text-xs text-destructive">{fieldErrors.logoUrl}</div>
                  )}
                </th>
                <td className="px-2 py-1 text-left text-sm">
                  <Input
                    id="media-logo"
                    value={form.logoUrl}
                    onChange={(e) => setField("logoUrl", e.target.value)}
                    aria-invalid={Boolean(fieldErrors.logoUrl) || undefined}
                    placeholder="https://res.cloudinary.com/.../logo.png"
                    disabled={isSaving}
                    autoFocus
                  />
                </td>
              </tr>
              <tr>
                <th
                  scope="row"
                  className="w-1/2 border-t border-r border-border bg-muted px-3 py-1.5 text-left align-middle text-xs font-normal text-muted-foreground"
                >
                  <label htmlFor="media-cover">Ảnh bìa URL</label>
                  {fieldErrors.coverUrl && (
                    <div className="text-xs text-destructive">{fieldErrors.coverUrl}</div>
                  )}
                </th>
                <td className="border-t border-border px-2 py-1 text-left text-sm">
                  <Input
                    id="media-cover"
                    value={form.coverUrl}
                    onChange={(e) => setField("coverUrl", e.target.value)}
                    aria-invalid={Boolean(fieldErrors.coverUrl) || undefined}
                    placeholder="https://res.cloudinary.com/.../campus.jpg"
                    disabled={isSaving}
                  />
                </td>
              </tr>
            </>
          )}
        </tbody>
      </table>
    </TableFrame>
  );
}
