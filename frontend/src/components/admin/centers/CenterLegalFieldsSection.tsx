"use client";

import React from "react";
import { Badge } from "@/components/ui/badge";
import { Field, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  centerNeedsTrainingLicense,
  LEGAL_COMPLIANCE_MODE_DESCRIPTIONS,
  LEGAL_COMPLIANCE_MODE_LABELS,
  type LegalComplianceMode,
  type LegalFormValues,
} from "@/lib/center-legal";

interface CenterLegalFieldsSectionProps {
  idPrefix: string;
  centerType: string;
  values: LegalFormValues;
  onChange: (patch: Partial<LegalFormValues>) => void;
  disabled?: boolean;
}

export function CenterLegalFieldsSection({
  idPrefix,
  centerType,
  values,
  onChange,
  disabled = false,
}: CenterLegalFieldsSectionProps) {
  const needsTrainingLicense = centerNeedsTrainingLicense(centerType);

  return (
    <div className="rounded-lg border bg-muted/20 p-3.5 space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-xs font-semibold text-foreground">
          Hồ sơ pháp lý & Năng lực đối soát (Xác minh)
        </div>
        <Badge variant="outline" className="text-[11px] font-normal">
          {LEGAL_COMPLIANCE_MODE_LABELS[values.legalComplianceMode]}
        </Badge>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-legal-mode`}>Hình thức tuân thủ pháp lý</FieldLabel>
          <select
            id={`${idPrefix}-legal-mode`}
            value={values.legalComplianceMode}
            onChange={(e) =>
              onChange({ legalComplianceMode: e.target.value as LegalComplianceMode })
            }
            disabled={disabled}
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
          >
            <option value="licensed">
              Cấp GCN trước 07/2026 (Giấy phép Sở GD&ĐT)
            </option>
            <option value="notified">
              Thông báo hoạt động hậu kiểm (từ 01/07/2026)
            </option>
          </select>
          <p className="text-[11px] text-muted-foreground mt-1 leading-snug">
            {LEGAL_COMPLIANCE_MODE_DESCRIPTIONS[values.legalComplianceMode]}
          </p>
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-tax-code`}>
            Mã số thuế / Mã số doanh nghiệp (ĐKKD)
          </FieldLabel>
          <Input
            id={`${idPrefix}-tax-code`}
            placeholder="Ví dụ: 0102381920"
            value={values.taxCode}
            onChange={(e) => onChange({ taxCode: e.target.value })}
            disabled={disabled}
            maxLength={32}
          />
        </Field>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-legal-rep`}>
            Người đại diện pháp luật / Giám đốc
          </FieldLabel>
          <Input
            id={`${idPrefix}-legal-rep`}
            placeholder="Ví dụ: Nguyễn Văn A"
            value={values.legalRepresentative}
            onChange={(e) => onChange({ legalRepresentative: e.target.value })}
            disabled={disabled}
            maxLength={150}
          />
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-license`}>
            {values.legalComplianceMode === "notified"
              ? "Mã hồ sơ thông báo Sở GD&ĐT"
              : "Số giấy phép tư vấn du học"}
          </FieldLabel>
          <Input
            id={`${idPrefix}-license`}
            placeholder={
              values.legalComplianceMode === "notified"
                ? "Ví dụ: TBHĐ-2026/HN-015"
                : "Ví dụ: GPHĐ-GD-2010/HN-088"
            }
            value={values.licenseNumber}
            onChange={(e) => onChange({ licenseNumber: e.target.value })}
            disabled={disabled}
            maxLength={128}
          />
        </Field>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-issued-by`}>
            Cơ quan cấp phép / tiếp nhận thông báo
          </FieldLabel>
          <Input
            id={`${idPrefix}-issued-by`}
            placeholder="Ví dụ: Sở GD&ĐT Hà Nội"
            value={values.licenseIssuedBy}
            onChange={(e) => onChange({ licenseIssuedBy: e.target.value })}
            disabled={disabled}
            maxLength={256}
          />
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-issued-date`}>
            Ngày cấp phép / gửi thông báo
          </FieldLabel>
          <Input
            id={`${idPrefix}-issued-date`}
            type="date"
            value={values.licenseIssuedDate}
            onChange={(e) => onChange({ licenseIssuedDate: e.target.value })}
            disabled={disabled}
          />
        </Field>
      </div>

      <Field>
        <div className="flex items-center justify-between gap-2">
          <FieldLabel htmlFor={`${idPrefix}-training-license`}>
            Số quyết định / giấy phép đào tạo ngoại ngữ
          </FieldLabel>
          {needsTrainingLicense && (
            <Badge
              variant="outline"
              className="text-[11px] text-amber-700 border-amber-300 bg-amber-50 dark:bg-amber-950/30"
            >
              Khuyến nghị bắt buộc cho mô hình Luyện thi / Toàn diện
            </Badge>
          )}
        </div>
        <Input
          id={`${idPrefix}-training-license`}
          placeholder="Ví dụ: GPĐT-NN-102/QĐ-SGDĐT-HN (nếu có hoạt động giảng dạy IELTS/SAT)"
          value={values.trainingLicenseNumber}
          onChange={(e) => onChange({ trainingLicenseNumber: e.target.value })}
          disabled={disabled}
          maxLength={128}
        />
      </Field>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-year`}>Năm thành lập</FieldLabel>
          <Input
            id={`${idPrefix}-year`}
            type="number"
            placeholder="2010"
            value={values.establishedYear}
            onChange={(e) => onChange({ establishedYear: e.target.value })}
            disabled={disabled}
          />
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-staff`}>Tổng nhân sự</FieldLabel>
          <Input
            id={`${idPrefix}-staff`}
            type="number"
            min={0}
            placeholder="35"
            value={values.staffCount}
            onChange={(e) => onChange({ staffCount: e.target.value })}
            disabled={disabled}
          />
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-certified`}>
            TVV có chứng chỉ
          </FieldLabel>
          <Input
            id={`${idPrefix}-certified`}
            type="number"
            min={0}
            placeholder="15"
            value={values.certifiedCounselorCount}
            onChange={(e) => onChange({ certifiedCounselorCount: e.target.value })}
            disabled={disabled}
          />
        </Field>

        <Field>
          <FieldLabel htmlFor={`${idPrefix}-visa`}>Tỷ lệ đỗ Visa (%)</FieldLabel>
          <Input
            id={`${idPrefix}-visa`}
            type="number"
            step="0.1"
            placeholder="98.5"
            value={values.visaSuccessRate}
            onChange={(e) => onChange({ visaSuccessRate: e.target.value })}
            disabled={disabled}
          />
        </Field>
      </div>

      <Field>
        <FieldLabel htmlFor={`${idPrefix}-accreditations`}>
          Chứng nhận / Kiểm định quốc tế (ngăn cách bằng dấu phẩy)
        </FieldLabel>
        <Input
          id={`${idPrefix}-accreditations`}
          placeholder="Ví dụ: AIRC, ICEF, PIER, British Council"
          value={values.accreditationsInput}
          onChange={(e) => onChange({ accreditationsInput: e.target.value })}
          disabled={disabled}
        />
      </Field>

      <Field>
        <FieldLabel htmlFor={`${idPrefix}-legal-docs`}>
          Link tài liệu pháp lý minh chứng (mỗi dòng 1 URL: ĐKKD, GP Sở GD&ĐT, PCCC...)
        </FieldLabel>
        <Textarea
          id={`${idPrefix}-legal-docs`}
          rows={2}
          placeholder={"https://example.com/gcn-dkdn.pdf\n/uploads/giay-phep-so-gddt.pdf"}
          value={values.legalDocumentUrlsInput}
          onChange={(e) => onChange({ legalDocumentUrlsInput: e.target.value })}
          disabled={disabled}
          className="text-xs font-mono"
        />
      </Field>

      <Field>
        <FieldLabel htmlFor={`${idPrefix}-verification-note`}>
          Ghi chú pháp lý / kiểm duyệt nội bộ
        </FieldLabel>
        <Textarea
          id={`${idPrefix}-verification-note`}
          rows={2}
          placeholder="Ghi chú đối chiếu bản gốc, thời hạn thuê trụ sở hoặc các lưu ý thẩm định..."
          value={values.verificationNote}
          onChange={(e) => onChange({ verificationNote: e.target.value })}
          disabled={disabled}
          maxLength={1000}
          className="text-xs"
        />
      </Field>
    </div>
  );
}

