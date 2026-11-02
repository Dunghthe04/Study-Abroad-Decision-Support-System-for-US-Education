"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, updateAdminUniversity } from "@/lib/admin-university-api";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";

interface EditUniversityDialogProps {
  university: AdminUniversityDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
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
  // Tỷ lệ nhận lưu 0–1, form nhập theo phần trăm cho dễ đọc.
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

export function EditUniversityDialog({ university, open, onOpenChange, onSaved }: EditUniversityDialogProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(university));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const initial = toFormState(university);
  const hasChanges = JSON.stringify(form) !== JSON.stringify(initial);

  const setField = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm({ ...form, [key]: value });
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

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
      });
      toast.add({ type: "success", title: "Đã lưu thông tin trường", description: saved.name });
      onSaved(saved);
      onOpenChange(false);
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

  const fieldError = (key: string) =>
    fieldErrors[key] ? <FieldError>{fieldErrors[key]}</FieldError> : null;

  return (
    <Dialog open={open} onOpenChange={(next) => !isSaving && onOpenChange(next)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Sửa thông tin trường</DialogTitle>
          <DialogDescription>
            Mã {university.code}. Mỗi lần lưu được ghi lại người sửa và ngày sửa.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Field data-invalid={Boolean(fieldErrors.name)}>
            <FieldLabel htmlFor="uni-name">Tên trường *</FieldLabel>
            <Input
              id="uni-name"
              value={form.name}
              onChange={(e) => setField("name", e.target.value)}
              disabled={isSaving}
              required
            />
            {fieldError("name")}
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field data-invalid={Boolean(fieldErrors.city)}>
              <FieldLabel htmlFor="uni-city">Thành phố</FieldLabel>
              <Input id="uni-city" value={form.city} onChange={(e) => setField("city", e.target.value)} disabled={isSaving} />
              {fieldError("city")}
            </Field>
            <Field data-invalid={Boolean(fieldErrors.state)}>
              <FieldLabel htmlFor="uni-state">Bang</FieldLabel>
              <Input
                id="uni-state"
                value={form.state}
                onChange={(e) => setField("state", e.target.value.toUpperCase())}
                maxLength={2}
                placeholder="CA"
                disabled={isSaving}
              />
              {fieldError("state")}
            </Field>
            <Field data-invalid={Boolean(fieldErrors.control)}>
              <FieldLabel htmlFor="uni-control">Loại trường</FieldLabel>
              <NativeSelect
                id="uni-control"
                value={form.control}
                onChange={(e) => setField("control", e.target.value)}
                disabled={isSaving}
              >
                <NativeSelectOption value="">Chưa rõ</NativeSelectOption>
                <NativeSelectOption value="public">Công lập</NativeSelectOption>
                <NativeSelectOption value="private">Tư thục</NativeSelectOption>
              </NativeSelect>
              {fieldError("control")}
            </Field>
          </div>

          <Field data-invalid={Boolean(fieldErrors.website)}>
            <FieldLabel htmlFor="uni-website">Website</FieldLabel>
            <Input
              id="uni-website"
              value={form.website}
              onChange={(e) => setField("website", e.target.value)}
              placeholder="https://"
              disabled={isSaving}
            />
            {fieldError("website")}
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field data-invalid={Boolean(fieldErrors.acceptanceRate)}>
              <FieldLabel htmlFor="uni-acceptance">Tỷ lệ nhận (%)</FieldLabel>
              <Input
                id="uni-acceptance"
                inputMode="decimal"
                value={form.acceptancePercent}
                onChange={(e) => setField("acceptancePercent", e.target.value)}
                placeholder="Ví dụ: 48.2"
                disabled={isSaving}
              />
              {fieldError("acceptanceRate")}
            </Field>
            <Field data-invalid={Boolean(fieldErrors.internationalStudentCount)}>
              <FieldLabel htmlFor="uni-intl">Số du học sinh</FieldLabel>
              <Input
                id="uni-intl"
                inputMode="numeric"
                value={form.internationalStudentCount}
                onChange={(e) => setField("internationalStudentCount", e.target.value)}
                disabled={isSaving}
              />
              {fieldError("internationalStudentCount")}
            </Field>
          </div>

          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={form.isIvy}
              onCheckedChange={(checked) => setField("isIvy", checked === true)}
              disabled={isSaving}
            />
            Thuộc Ivy League
          </label>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isSaving}>
              Hủy
            </Button>
            <Button type="submit" disabled={isSaving || !hasChanges}>
              {isSaving && <Spinner />}
              Lưu
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
