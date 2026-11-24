"use client";

import { useState } from "react";
import { AlertTriangleIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { MonthPicker } from "@/components/ui/month-picker";
import { cn } from "@/lib/utils";
import {
  findCertType,
  getExpiry,
  isOtherCert,
  parseYearMonth,
  searchCertTypes,
  toYearMonth,
  type CertFormErrors,
} from "@/lib/certification-utils";
import {
  OTHER_CERT_TYPE,
  type CertificationCategory,
  type CertificationItem,
  type CertificationTypeOption,
} from "@/types/profile-flow";
import {
  ChoicePicker,
  DATA_TABLE,
  FieldError,
  INLINE_INPUT,
  FieldRow,
  SegmentedControl,
  type Option,
} from "../FormKit";


interface CertificationCardProps {
  /** Số thứ tự trong mục, hiện ở tiêu đề "Chứng chỉ N" */
  index: number;
  cert: CertificationItem;
  types: CertificationTypeOption[];
  categories: CertificationCategory[];
  /** nhóm gán cho loại "Khác" (không có trong danh mục) */
  otherCategory: CertificationCategory;
  /** lỗi từng ô; chỉ truyền những lỗi cần hiện */
  errors: CertFormErrors;
  /** lỗi so với các thẻ khác (trùng dự định, điểm mục tiêu không cao hơn điểm đã thi) */
  conflict: string | null;
  onChange: (cert: CertificationItem) => void;
  onRemove: () => void;
}

const MODES: Option<"taken" | "target">[] = [
  { value: "taken", label: "Đã có điểm" },
  { value: "target", label: "Dự định thi" },
];

const numText = (v: number | null | undefined) => (v == null ? "" : String(v));
const parseNum = (s: string) => (s.trim() === "" || Number.isNaN(Number(s)) ? null : Number(s));
const orNull = (s: string) => (s === "" ? null : s);

/**
 * Một chứng chỉ, sửa thẳng trên trang: tiêu đề + chọn "Đã có điểm / Dự định thi" + nút xóa, rồi các ô nhập.
 * Mọi thay đổi đi lên cha ngay; kiểm tra chạy khi bấm Tiếp tục. Ô điểm giữ chuỗi đang gõ riêng để gõ "6." không bị mất dấu chấm.
 */
export function CertificationCard({
  index,
  cert,
  types,
  categories,
  otherCategory,
  errors,
  conflict,
  onChange,
  onRemove,
}: CertificationCardProps) {
  const [overallText, setOverallText] = useState(numText(cert.overall));
  const [sectionTexts, setSectionTexts] = useState<Record<string, string>>(() =>
    Object.fromEntries(Object.entries(cert.sections ?? {}).map(([k, v]) => [k, numText(v)])),
  );

  const type = findCertType(types, cert.type);
  const isOther = isOtherCert(cert);
  const hasNumericScale = type?.min != null && type?.max != null;
  const isTarget = Boolean(cert.isTarget);
  const expiry = isTarget ? null : getExpiry(cert.testDate, type);
  const now = new Date();
  const thisMonth = { year: now.getFullYear(), month: now.getMonth() + 1 };
  const label = `Chứng chỉ ${index}`;

  const patch = (val: Partial<CertificationItem>) => onChange({ ...cert, ...val });

  // Danh sách chọn: theo thứ tự metadata, cuối là "Khác"; loại đã lưu mà không còn trong danh mục vẫn hiện để chọn lại
  const options: Option[] = [
    ...searchCertTypes(types, categories, "").map((t) => ({ value: t.id, label: t.name })),
    ...(cert.type && !isOther && !type ? [{ value: cert.type, label: cert.type.toUpperCase() }] : []),
    { value: OTHER_CERT_TYPE, label: "Khác" },
  ];

  // Đổi loại → điểm cũ không còn đúng thang, giữ lại ngày thi
  const handleTypeChange = (typeId: string | null) => {
    if (!typeId || typeId === cert.type) return;
    const next = findCertType(types, typeId);
    onChange({
      id: cert.id,
      type: typeId,
      category: typeId === OTHER_CERT_TYPE ? otherCategory : (next?.category ?? otherCategory),
      testDate: cert.testDate ?? null,
      isTarget: cert.isTarget,
    });
    setOverallText("");
    setSectionTexts({});
  };

  // Đổi giữa "Đã có điểm" và "Dự định thi": tháng thi đã qua / tháng dự kiến không dùng chung được nên xóa,
  // trừ dự định đã quá hạn chuyển sang "Đã có điểm" (tháng đó thường chính là tháng đã thi); điểm luôn xóa
  const handleModeChange = (mode: "taken" | "target") => {
    const target = mode === "target";
    if (target === isTarget) return;
    const ym = parseYearMonth(cert.testDate);
    const keepDate = !target && ym && ym.year * 12 + ym.month < thisMonth.year * 12 + thisMonth.month;
    onChange({
      ...cert,
      isTarget: target,
      testDate: keepDate ? cert.testDate : null,
      overall: null,
      scaleText: isOther ? cert.scaleText : null,
      sections: null,
    });
    setOverallText("");
    setSectionTexts({});
  };

  const handleOverall = (text: string) => {
    setOverallText(text);
    patch({ overall: parseNum(text) });
  };

  const handleSection = (id: string, text: string) => {
    const texts = { ...sectionTexts, [id]: text };
    setSectionTexts(texts);
    const sections: Record<string, number | null> = {};
    for (const s of type?.sections ?? []) {
      const v = parseNum(texts[s.id] ?? "");
      if (v != null) sections[s.id] = v;
    }
    patch({ sections: Object.keys(sections).length > 0 ? sections : null });
  };

  // Ô chữ: gõ thế nào lưu thế đó (để gõ được dấu cách), rời ô thì cắt khoảng trắng đầu cuối
  const textProps = (key: "customName" | "subject" | "scaleText") => ({
    value: cert[key] ?? "",
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => patch({ [key]: orNull(e.target.value) }),
    onBlur: (e: React.FocusEvent<HTMLInputElement>) => {
      const trimmed = orNull(e.target.value.trim());
      if (trimmed !== (cert[key] ?? null)) patch({ [key]: trimmed });
    },
  });

  const scoreHint =
    type && hasNumericScale
      ? [`Thang ${type.min}–${type.max}`, type.step && type.step !== 1 ? `bước ${type.step}` : null]
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

  const dateField = (
    <FieldRow
      label={isTarget ? "Dự kiến thi" : "Ngày thi"}
      htmlFor={`${cert.id}-date`}
      error={errors.testDate}
      hint={expiry && <span className={expiry.expired ? "text-risk" : "text-ok"}>{expiry.label}</span>}
    >
      {datePicker}
    </FieldRow>
  );

  const sectionError = Object.entries(errors)
    .filter(([k]) => k.startsWith("section."))
    .map(([k, msg]) => `${type?.sections.find((s) => `section.${s.id}` === k)?.label ?? ""} ${msg}`.trim())
    .join(". ");

  return (
    <div>
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-line bg-(--surface-2) px-4 py-2.5">
        <h3 className="min-w-0 flex-1 text-sm font-semibold text-ink">{label}</h3>
        <SegmentedControl
          options={MODES}
          value={isTarget ? "target" : "taken"}
          onChange={handleModeChange}
          ariaLabel={`${label}: đã thi hay dự định thi`}
        />
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={onRemove}
          aria-label={`Xóa ${label.toLowerCase()}`}
          className="text-ink-2"
        >
          <Trash2Icon className="size-4" />
        </Button>
      </div>

      <div className="@container divide-y divide-dashed divide-line px-4">
        <FieldRow label="Loại chứng chỉ" required error={errors.type}>
          <ChoicePicker
            id={`${cert.id}-type`}
            options={options}
            value={isOther ? OTHER_CERT_TYPE : (type?.id ?? (cert.type || null))}
            onChange={handleTypeChange}
            ariaLabel={`${label}: loại chứng chỉ`}
            placeholder="Chọn loại"
          />
          {!types.some((t) => categories.includes(t.category)) && (
            <p className="mt-2 text-xs text-ink-2">Chưa tải được danh sách chứng chỉ. Chọn “Khác” và tự nhập tên.</p>
          )}
        </FieldRow>

        {isOther && (
          <>
            <FieldRow label="Tên chứng chỉ" htmlFor={`${cert.id}-name`} required error={errors.customName}>
              <Input
                id={`${cert.id}-name`}
                placeholder="Tên đầy đủ, như trên giấy chứng nhận"
                maxLength={128}
                aria-invalid={Boolean(errors.customName) || undefined}
                {...textProps("customName")}
              />
            </FieldRow>
            <FieldRow label={isTarget ? "Điểm mục tiêu" : "Điểm tổng"} htmlFor={`${cert.id}-overall`} error={errors.overall}>
              <Input
                id={`${cert.id}-overall`}
                type="number"
                min={0}
                placeholder="VD: 85"
                value={overallText}
                aria-invalid={Boolean(errors.overall) || undefined}
                onChange={(e) => handleOverall(e.target.value)}
              />
            </FieldRow>
            <FieldRow label="Thang điểm" htmlFor={`${cert.id}-scale`}>
              <Input
                id={`${cert.id}-scale`}
                placeholder="VD: 0–100"
                maxLength={32}
                {...textProps("scaleText")}
              />
            </FieldRow>
            {dateField}
            <p className="flex items-start gap-2 py-5 text-sm text-warn">
              <AlertTriangleIcon aria-hidden className="mt-0.5 size-4 shrink-0" />
              Chứng chỉ này chưa có trong dữ liệu hệ thống nên không dùng để xếp hạng gợi ý. Bạn nên kiểm tra với từng
              trường xem có công nhận không.
            </p>
          </>
        )}

        {type && (
          <>
            {type.requiresSubject && (
              <FieldRow
                label={type.subjectLabel ?? "Tên môn"}
                htmlFor={`${cert.id}-subject`}
                required={!isTarget}
                error={errors.subject}
              >
                <Input
                  id={`${cert.id}-subject`}
                  placeholder="VD: Calculus BC"
                  maxLength={128}
                  aria-invalid={Boolean(errors.subject) || undefined}
                  {...textProps("subject")}
                />
              </FieldRow>
            )}

            <FieldRow
              label={isTarget ? "Điểm mục tiêu" : type.sections.length > 0 ? "Điểm tổng" : "Điểm"}
              htmlFor={`${cert.id}-overall`}
              required={!isTarget}
              error={errors.overall}
              hint={scoreHint}
            >
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
                  onChange={(e) => handleOverall(e.target.value)}
                />
              ) : (
                <Input
                  id={`${cert.id}-overall`}
                  placeholder={type.placeholder ?? undefined}
                  maxLength={32}
                  aria-invalid={Boolean(errors.overall) || undefined}
                  {...textProps("scaleText")}
                />
              )}
            </FieldRow>

            {dateField}

            {!isTarget && type.sections.length > 0 && (
              <FieldRow label="Điểm từng kỹ năng" error={sectionError || null} wide>
                <div className="overflow-x-auto rounded-lg border border-line">
                  <table className={cn(DATA_TABLE, "table-fixed")}>
                    <thead>
                      <tr className="bg-(--surface-2) text-xs font-medium text-ink-2">
                        {type.sections.map((s) => (
                          <th key={s.id} scope="col" className="px-2 py-2 font-medium">
                            {s.label}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        {type.sections.map((s) => (
                          <td key={s.id} className="border-t p-0">
                            <input
                              type="number"
                              inputMode="decimal"
                              min={s.min}
                              max={s.max}
                              step={s.step}
                              placeholder={`${s.min}–${s.max}`}
                              aria-label={`${label}: ${s.label}`}
                              aria-invalid={Boolean(errors[`section.${s.id}`]) || undefined}
                              value={sectionTexts[s.id] ?? ""}
                              onChange={(e) => handleSection(s.id, e.target.value)}
                              className={INLINE_INPUT}
                            />
                          </td>
                        ))}
                      </tr>
                    </tbody>
                  </table>
                </div>
              </FieldRow>
            )}
          </>
        )}

        {conflict && (
          <div className="py-5">
            <FieldError>{conflict}</FieldError>
          </div>
        )}
      </div>
    </div>
  );
}
