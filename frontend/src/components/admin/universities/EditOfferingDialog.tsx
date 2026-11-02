"use client";

import { useEffect, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
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
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue } from "@/lib/admin-form";
import { AdminFormError, getAdminMajors, updateAdminOffering } from "@/lib/admin-university-api";
import { studyLevelLabel } from "@/lib/study-levels";
import type {
  AdminFieldErrors,
  AdminMajorOption,
  AdminUniversityOffering,
} from "@/types/admin-university";

interface EditOfferingDialogProps {
  universityId: string;
  offering: AdminUniversityOffering;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: (offering: AdminUniversityOffering) => void;
}

/** Các ô số của form, khóa trùng tên trường dữ liệu gửi lên API. */
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

export function EditOfferingDialog({ universityId, offering, open, onOpenChange, onSaved }: EditOfferingDialogProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(offering));
  const [majorOptions, setMajorOptions] = useState<AdminMajorOption[] | null>(null);
  const [majorLoadError, setMajorLoadError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
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
  }, []);

  const hasChanges = JSON.stringify(form) !== JSON.stringify(toFormState(offering));

  const unverifiedMajorIds = new Set(
    offering.majorItems.filter((item) => !item.isVerified).map((item) => item.majorId),
  );

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

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

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
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
      return;
    }

    // SAT cần đủ cả 2 mốc (công thức gợi ý dùng cả hai); báo sớm trước khi gửi.
    if ((numbers.sat25 === null) !== (numbers.sat75 === null)) {
      setFieldErrors({ sat25: "Nhập đủ cả SAT 25% và 75%, hoặc để trống cả hai." });
      setError("Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.");
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

  const numberInput = (field: NumberField) => (
    <Field key={field} data-invalid={Boolean(fieldErrors[field])}>
      <FieldLabel htmlFor={`offering-${field}`}>{NUMBER_LABELS[field]}</FieldLabel>
      <Input
        id={`offering-${field}`}
        inputMode="decimal"
        value={form.numbers[field]}
        onChange={(e) => setNumber(field, e.target.value)}
        disabled={isSaving}
      />
      {fieldError(field)}
    </Field>
  );

  return (
    <Dialog open={open} onOpenChange={(next) => !isSaving && onOpenChange(next)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Sửa bậc học: {studyLevelLabel(offering.studyLevel)}</DialogTitle>
          <DialogDescription>Để trống ô nào là chưa có dữ liệu cho ô đó.</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-6">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Chi phí</h3>
            <Field data-invalid={Boolean(fieldErrors.academicYear)}>
              <FieldLabel htmlFor="offering-year">Năm học của số liệu chi phí</FieldLabel>
              <Input
                id="offering-year"
                value={form.academicYear}
                onChange={(e) => setForm({ ...form, academicYear: e.target.value })}
                placeholder="2026-27"
                disabled={isSaving}
              />
              {fieldError("academicYear")}
            </Field>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">{COST_FIELDS.map(numberInput)}</div>
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Điều kiện</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">{REQUIREMENT_FIELDS.map(numberInput)}</div>
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Điểm thi</h3>
            <Field data-invalid={Boolean(fieldErrors.satPolicy)}>
              <FieldLabel htmlFor="offering-sat-policy">Chính sách SAT/ACT</FieldLabel>
              <NativeSelect
                id="offering-sat-policy"
                value={form.satPolicy}
                onChange={(e) => setForm({ ...form, satPolicy: e.target.value })}
                disabled={isSaving}
              >
                <NativeSelectOption value="">Chưa rõ</NativeSelectOption>
                <NativeSelectOption value="required">Bắt buộc</NativeSelectOption>
                <NativeSelectOption value="optional">Không bắt buộc (test-optional)</NativeSelectOption>
                <NativeSelectOption value="not_accepted">Không xét điểm</NativeSelectOption>
              </NativeSelect>
              {fieldError("satPolicy")}
            </Field>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">{TEST_FIELDS.map(numberInput)}</div>
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">
              Ngành đào tạo ({Object.keys(form.majors).length})
            </h3>
            {fieldError("majors")}
            {majorLoadError && (
              <Alert variant="destructive">
                <AlertDescription>{majorLoadError}</AlertDescription>
              </Alert>
            )}
            {!majorOptions && !majorLoadError && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Spinner />
                Đang tải danh mục ngành...
              </div>
            )}
            {majorOptions && (
              <div className="max-h-72 overflow-y-auto rounded-md border border-border">
                {majorOptions.map((major) => {
                  const included = major.id in form.majors;
                  return (
                    <div
                      key={major.id}
                      className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-border px-3 py-1.5 text-sm last:border-b-0"
                    >
                      <label className="flex min-w-0 flex-1 items-center gap-2">
                        <Checkbox
                          checked={included}
                          onCheckedChange={(checked) => toggleMajor(major.id, checked === true)}
                          disabled={isSaving}
                        />
                        <span className="min-w-0">
                          {major.name}
                          {major.cipCode && (
                            <span className="ml-1 text-xs text-muted-foreground">CIP {major.cipCode}</span>
                          )}
                        </span>
                        {included && unverifiedMajorIds.has(major.id) && (
                          <Badge variant="outline">Chưa xác minh</Badge>
                        )}
                      </label>
                      <label className="flex items-center gap-2 text-xs text-muted-foreground">
                        <Checkbox
                          checked={form.majors[major.id] === true}
                          onCheckedChange={(checked) => toggleStem(major.id, checked === true)}
                          disabled={isSaving || !included}
                        />
                        STEM
                      </label>
                    </div>
                  );
                })}
              </div>
            )}
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Nguồn dữ liệu</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-[1fr_12rem]">
              <Field data-invalid={Boolean(fieldErrors.sourceUrl)}>
                <FieldLabel htmlFor="offering-source">Liên kết nguồn</FieldLabel>
                <Input
                  id="offering-source"
                  value={form.sourceUrl}
                  onChange={(e) => setForm({ ...form, sourceUrl: e.target.value })}
                  placeholder="https://"
                  disabled={isSaving}
                />
                {fieldError("sourceUrl")}
              </Field>
              <Field data-invalid={Boolean(fieldErrors.retrievedAt)}>
                <FieldLabel htmlFor="offering-retrieved">Ngày lấy dữ liệu</FieldLabel>
                <Input
                  id="offering-retrieved"
                  type="date"
                  value={form.retrievedAt}
                  onChange={(e) => setForm({ ...form, retrievedAt: e.target.value })}
                  disabled={isSaving}
                />
                {fieldError("retrievedAt")}
              </Field>
            </div>
            <Field data-invalid={Boolean(fieldErrors.sourceReliability)}>
              <FieldLabel htmlFor="offering-reliability">Mức tin cậy của nguồn</FieldLabel>
              <NativeSelect
                id="offering-reliability"
                value={form.sourceReliability}
                onChange={(e) => setForm({ ...form, sourceReliability: e.target.value })}
                disabled={isSaving}
              >
                <NativeSelectOption value="">Chưa rõ</NativeSelectOption>
                <NativeSelectOption value="official">Chính thức (CDS, website trường)</NativeSelectOption>
                <NativeSelectOption value="aggregated">Tổng hợp (College Scorecard, trang tổng hợp)</NativeSelectOption>
                <NativeSelectOption value="self_reported">Tự khai (người dùng, trung tâm cung cấp)</NativeSelectOption>
              </NativeSelect>
              {fieldError("sourceReliability")}
            </Field>
            <Field data-invalid={Boolean(fieldErrors.notes)}>
              <FieldLabel htmlFor="offering-notes">Ghi chú</FieldLabel>
              <Textarea
                id="offering-notes"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                maxLength={2000}
                disabled={isSaving}
              />
              {fieldError("notes")}
            </Field>
          </section>

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
