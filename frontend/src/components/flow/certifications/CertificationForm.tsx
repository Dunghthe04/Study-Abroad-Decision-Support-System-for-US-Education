"use client";

import { useState } from "react";
import { AlertTriangleIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RequiredMark } from "../RequiredMark";
import { MonthPicker } from "@/components/ui/month-picker";
import { cn } from "@/lib/utils";
import {
  findCertType,
  getExpiry,
  isOtherCert,
  parseYearMonth,
  toYearMonth,
  validateCertification,
  type CertFormErrors,
} from "@/lib/certification-utils";
import {
  OTHER_CERT_TYPE,
  type CertificationCategory,
  type CertificationItem,
  type CertificationTypeOption,
} from "@/types/profile-flow";
import { CertificationTypeCombobox } from "./CertificationTypeCombobox";

interface CertificationFormProps {
  title: string;
  /** chứng chỉ đang sửa; thêm mới thì truyền mục rỗng có sẵn id */
  initial: CertificationItem;
  types: CertificationTypeOption[];
  categories: CertificationCategory[];
  /** nhóm gán cho loại "Khác" (không có trong danh mục) */
  otherCategory: CertificationCategory;
  onSave: (cert: CertificationItem) => void;
  onCancel: () => void;
  /** lỗi so với các thẻ khác (trùng dự định, điểm mục tiêu không cao hơn điểm đã thi); null = không lỗi */
  checkConflict?: (cert: CertificationItem) => string | null;
}

const numText = (v: number | null | undefined) => (v == null ? "" : String(v));
const parseNum = (s: string) => (s.trim() === "" || Number.isNaN(Number(s)) ? null : Number(s));

/** Form thêm / sửa một chứng chỉ ngay trên trang; ô điểm dựng theo thang và phần thi trong metadata */
export function CertificationForm({
  title,
  initial,
  types,
  categories,
  otherCategory,
  onSave,
  onCancel,
  checkConflict,
}: CertificationFormProps) {
  const [cert, setCert] = useState<CertificationItem>(initial);
  const [overallText, setOverallText] = useState(numText(initial.overall));
  const [sectionTexts, setSectionTexts] = useState<Record<string, string>>(() =>
    Object.fromEntries(Object.entries(initial.sections ?? {}).map(([k, v]) => [k, numText(v)]))
  );
  const [errors, setErrors] = useState<CertFormErrors>({});
  const [conflict, setConflict] = useState<string | null>(null);

  const type = findCertType(types, cert.type);
  const isOther = isOtherCert(cert);
  const hasNumericScale = type?.min != null && type?.max != null;
  const isTarget = Boolean(cert.isTarget);
  const expiry = isTarget ? null : getExpiry(cert.testDate, type);
  const now = new Date();
  const thisMonth = { year: now.getFullYear(), month: now.getMonth() + 1 };

  const patch = (val: Partial<CertificationItem>) => setCert((c) => ({ ...c, ...val }));

  // Đổi loại → điểm cũ không còn đúng thang, giữ lại ngày thi
  const handleTypeChange = (typeId: string) => {
    if (typeId === cert.type) return;
    const next = findCertType(types, typeId);
    setCert({
      id: cert.id,
      type: typeId,
      category: typeId === OTHER_CERT_TYPE ? otherCategory : (next?.category ?? otherCategory),
      testDate: cert.testDate ?? null,
      isTarget: cert.isTarget,
    });
    setOverallText("");
    setSectionTexts({});
    setErrors({});
    setConflict(null);
  };

  // Đổi giữa "Đã có điểm" và "Dự định thi": tháng thi đã qua / tháng dự kiến không dùng chung được nên xóa;
  // điểm cũng xóa vì điểm đã thi khác điểm mục tiêu
  const handleModeChange = (target: boolean) => {
    if (target === isTarget) return;
    patch({ isTarget: target, testDate: null, overall: null, scaleText: isOther ? cert.scaleText : null });
    setOverallText("");
    setSectionTexts({});
    setErrors({});
    setConflict(null);
  };

  const buildCert = (): CertificationItem => {
    const sections: Record<string, number | null> = {};
    for (const s of isTarget ? [] : (type?.sections ?? [])) {
      const v = parseNum(sectionTexts[s.id] ?? "");
      if (v != null) sections[s.id] = v;
    }
    return {
      ...cert,
      customName: isOther ? cert.customName?.trim() || null : null,
      subject: type?.requiresSubject ? cert.subject?.trim() || null : null,
      overall: isOther || hasNumericScale ? parseNum(overallText) : null,
      scaleText: isOther || (type && !hasNumericScale) ? cert.scaleText?.trim() || null : null,
      sections: Object.keys(sections).length > 0 ? sections : null,
    };
  };

  const handleSave = () => {
    const result = buildCert();
    const nextErrors = validateCertification(result, type);
    setErrors(nextErrors);
    const nextConflict = Object.keys(nextErrors).length === 0 ? (checkConflict?.(result) ?? null) : null;
    setConflict(nextConflict);
    if (Object.keys(nextErrors).length === 0 && !nextConflict) onSave(result);
  };

  const scoreHint =
    type && hasNumericScale
      ? [
          type.min != null && type.max != null ? `Thang ${type.min}–${type.max}` : type.scaleLabel,
          type.step && type.step !== 1 ? `bước ${type.step}` : null,
        ]
          .filter(Boolean)
          .join(", ")
      : null;

  const datePicker = (
    <MonthPicker
      id={`${cert.id}-date`}
      placeholder="Tháng / năm"
      value={parseYearMonth(cert.testDate)}
      {...(isTarget ? { min: thisMonth } : { max: thisMonth })}
      onChange={(m) => patch({ testDate: toYearMonth(m) })}
    />
  );

  return (
    <div className="space-y-5 rounded-xl border border-brand/40 bg-surface p-5 ">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold text-foreground">{title}</h3>
        <button
          type="button"
          onClick={onCancel}
          aria-label="Đóng"
          className="cursor-pointer rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          <XIcon className="size-4" />
        </button>
      </div>

      <div role="radiogroup" aria-label="Đã thi hay dự định thi" className="inline-flex rounded-lg border border-line p-0.5">
        {[
          { target: false, label: "Đã có điểm" },
          { target: true, label: "Dự định thi" },
        ].map((opt) => (
          <button
            key={opt.label}
            type="button"
            role="radio"
            aria-checked={isTarget === opt.target}
            onClick={() => handleModeChange(opt.target)}
            className={cn(
              "cursor-pointer rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              isTarget === opt.target ? "bg-brand text-white" : "text-muted-foreground hover:text-foreground"
            )}
          >
            {opt.label}
          </button>
        ))}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor={`${cert.id}-type`} className="text-xs font-medium">
          Loại chứng chỉ <RequiredMark />
        </Label>
        <CertificationTypeCombobox
          id={`${cert.id}-type`}
          types={types}
          categories={categories}
          value={cert.type || null}
          onChange={handleTypeChange}
          invalid={Boolean(errors.type)}
        />
        <FieldError message={errors.type} />
      </div>

      {isOther && (
        <>
          <div className="space-y-1.5">
            <Label htmlFor={`${cert.id}-name`} className="text-xs font-medium">
              Tên chứng chỉ <RequiredMark />
            </Label>
            <Input
              id={`${cert.id}-name`}
              placeholder="Ghi tên đầy đủ, như trên giấy chứng nhận"
              maxLength={128}
              value={cert.customName ?? ""}
              aria-invalid={Boolean(errors.customName) || undefined}
              onChange={(e) => patch({ customName: e.target.value })}
            />
            <FieldError message={errors.customName} />
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-overall`} className="text-xs font-medium">
                {isTarget ? "Điểm mục tiêu (tùy chọn)" : "Điểm tổng"}
              </Label>
              <Input
                id={`${cert.id}-overall`}
                type="number"
                min={0}
                placeholder="VD: 85"
                value={overallText}
                aria-invalid={Boolean(errors.overall) || undefined}
                onChange={(e) => setOverallText(e.target.value)}
              />
              <FieldError message={errors.overall} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-scale`} className="text-xs font-medium">
                Thang điểm
              </Label>
              <Input
                id={`${cert.id}-scale`}
                placeholder="VD: 0–100"
                maxLength={32}
                value={cert.scaleText ?? ""}
                onChange={(e) => patch({ scaleText: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-date`} className="text-xs font-medium">
                {isTarget ? "Dự kiến thi (tùy chọn)" : "Ngày thi"}
              </Label>
              {datePicker}
              <FieldError message={errors.testDate} />
            </div>
          </div>
          <div className="flex gap-2 rounded-lg border border-warn/30 bg-warn-wash px-3 py-2.5 text-sm text-warn">
            <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" />
            <p>
              <strong className="font-semibold">Lưu ý:</strong> Chứng chỉ này chưa có trong dữ liệu của hệ thống nên
              không được dùng để xếp hạng gợi ý. Không phải trường nào cũng công nhận, bạn nên kiểm tra với từng
              trường.
            </p>
          </div>
        </>
      )}

      {type && (
        <>
          {type.requiresSubject && (
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-subject`} className="text-xs font-medium">
                {type.subjectLabel ?? "Tên môn"} {isTarget ? "(tùy chọn)" : <RequiredMark />}
              </Label>
              <Input
                id={`${cert.id}-subject`}
                placeholder="VD: Calculus BC"
                maxLength={128}
                value={cert.subject ?? ""}
                aria-invalid={Boolean(errors.subject) || undefined}
                onChange={(e) => patch({ subject: e.target.value })}
              />
              <FieldError message={errors.subject} />
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-overall`} className="text-xs font-medium">
                {isTarget ? "Điểm mục tiêu (tùy chọn)" : type.sections.length > 0 ? "Điểm tổng" : "Điểm"}
              </Label>
              {hasNumericScale ? (
                <Input
                  id={`${cert.id}-overall`}
                  type="number"
                  min={type.min ?? undefined}
                  max={type.max ?? undefined}
                  step={type.step ?? "any"}
                  placeholder={type.placeholder ?? undefined}
                  value={overallText}
                  aria-invalid={Boolean(errors.overall) || undefined}
                  onChange={(e) => setOverallText(e.target.value)}
                />
              ) : (
                <Input
                  id={`${cert.id}-overall`}
                  placeholder={type.placeholder ?? undefined}
                  maxLength={32}
                  value={cert.scaleText ?? ""}
                  aria-invalid={Boolean(errors.overall) || undefined}
                  onChange={(e) => patch({ scaleText: e.target.value })}
                />
              )}
              {errors.overall ? (
                <FieldError message={errors.overall} />
              ) : (
                scoreHint && <p className="text-xs text-muted-foreground">{scoreHint}</p>
              )}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor={`${cert.id}-date`} className="text-xs font-medium">
                {isTarget ? "Dự kiến thi (tùy chọn)" : "Ngày thi"}
              </Label>
              {datePicker}
              {errors.testDate ? (
                <FieldError message={errors.testDate} />
              ) : (
                expiry && (
                  <p className={cn("text-xs", expiry.expired ? "text-risk" : "text-ok")}>{expiry.label}</p>
                )
              )}
            </div>
          </div>

          {!isTarget && type.sections.length > 0 && (
            <div className="space-y-3 rounded-lg border border-line bg-ground/50 p-4">
              <div>
                <p className="text-sm font-medium text-foreground">Điểm từng kỹ năng</p>
                {type.sectionsHint && <p className="text-xs text-muted-foreground">{type.sectionsHint}</p>}
              </div>
              <div className={cn("grid gap-3", type.sections.length >= 4 ? "grid-cols-2 sm:grid-cols-4" : "grid-cols-2")}>
                {type.sections.map((s) => {
                  const err = errors[`section.${s.id}`];
                  return (
                    <div key={s.id} className="space-y-1">
                      <Label htmlFor={`${cert.id}-${s.id}`} className="text-xs text-muted-foreground">
                        {s.label}
                      </Label>
                      <Input
                        id={`${cert.id}-${s.id}`}
                        type="number"
                        min={s.min}
                        max={s.max}
                        step={s.step}
                        placeholder={`${s.min}–${s.max}`}
                        value={sectionTexts[s.id] ?? ""}
                        aria-invalid={Boolean(err) || undefined}
                        onChange={(e) => setSectionTexts((prev) => ({ ...prev, [s.id]: e.target.value }))}
                      />
                      <FieldError message={err} />
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </>
      )}

      {conflict && (
        <p className="flex items-center gap-1.5 text-sm text-risk">
          <AlertTriangleIcon className="size-4 shrink-0" />
          {conflict}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          Hủy
        </Button>
        <Button type="button" onClick={handleSave} disabled={!cert.type}>
          {isTarget ? "Lưu dự định" : "Lưu chứng chỉ"}
        </Button>
      </div>
    </div>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs text-risk">{message}</p>;
}
