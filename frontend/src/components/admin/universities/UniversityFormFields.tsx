"use client";

import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { CONTROL_OPTIONS } from "@/lib/admin-university-display";
import type { AdminFieldErrors, AdminUniversityDetail } from "@/types/admin-university";
import { toInputValue } from "@/lib/admin-form";
import type { TableRow } from "./EditableTable";
import { OptionSelect } from "./OptionSelect";
import { ImageUploadField } from "../shared/ImageUploadField";

export interface UniversityFormState {
  code?: string;
  name: string;
  city: string;
  state: string;
  website: string;
  control: string;
  acceptancePercent: string;
  internationalStudentCount: string;
  isIvy: boolean;
  logoUrl?: string;
  coverUrl?: string;
}

export const EMPTY_UNIVERSITY_FORM: UniversityFormState = {
  code: "",
  name: "",
  city: "",
  state: "",
  website: "",
  control: "public",
  acceptancePercent: "",
  internationalStudentCount: "",
  isIvy: false,
  logoUrl: "",
  coverUrl: "",
};

export function universityToFormState(university: AdminUniversityDetail): UniversityFormState {
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
    logoUrl: toInputValue(university.logoUrl),
    coverUrl: toInputValue(university.coverUrl),
  };
}

export function buildUniversityEditRows({
  form,
  setField,
  fieldErrors,
  isSaving,
  includeCode = false,
  includeMedia = false,
  onUploadingChange,
}: {
  form: UniversityFormState;
  setField: <K extends keyof UniversityFormState>(key: K, value: UniversityFormState[K]) => void;
  fieldErrors: AdminFieldErrors;
  isSaving: boolean;
  includeCode?: boolean;
  includeMedia?: boolean;
  onUploadingChange?: (uploading: boolean) => void;
}): TableRow[] {
  const rows: TableRow[] = [];

  if (includeCode) {
    rows.push({
      label: "Mã trường *",
      inputId: "uni-code",
      error: fieldErrors.code,
      value: (
        <Input
          id="uni-code"
          value={form.code ?? ""}
          onChange={(e) => setField("code", e.target.value.toUpperCase().trim())}
          aria-invalid={Boolean(fieldErrors.code) || undefined}
          placeholder="Ví dụ: HARVARD"
          maxLength={32}
          disabled={isSaving}
          required
          autoFocus
        />
      ),
    });
  }

  rows.push(
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
          autoFocus={!includeCode}
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
  );

  if (includeMedia) {
    rows.push(
      {
        label: "Logo URL",
        inputId: "uni-logo",
        error: fieldErrors.logoUrl,
        value: (
          <ImageUploadField
            id="uni-logo"
            value={form.logoUrl ?? ""}
            onChange={(url) => setField("logoUrl", url)}
            purpose="school-logo"
            invalid={Boolean(fieldErrors.logoUrl)}
            disabled={isSaving}
            onUploadingChange={onUploadingChange}
          />
        ),
      },
      {
        label: "Ảnh bìa URL",
        inputId: "uni-cover",
        error: fieldErrors.coverUrl,
        value: (
          <ImageUploadField
            id="uni-cover"
            value={form.coverUrl ?? ""}
            onChange={(url) => setField("coverUrl", url)}
            purpose="school-cover"
            invalid={Boolean(fieldErrors.coverUrl)}
            disabled={isSaving}
            onUploadingChange={onUploadingChange}
          />
        ),
      },
    );
  }

  return rows;
}
