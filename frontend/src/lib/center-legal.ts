import type { StudyCenter } from "@/types/api";

export type LegalComplianceMode = "licensed" | "notified";

export const LEGAL_COMPLIANCE_MODE_LABELS: Record<LegalComplianceMode, string> = {
  licensed: "Cấp GCN trước 07/2026",
  notified: "Thông báo hậu kiểm (07/2026)",
};

export const LEGAL_COMPLIANCE_MODE_DESCRIPTIONS: Record<LegalComplianceMode, string> = {
  licensed: "Đã cấp Giấy chứng nhận đăng ký kinh doanh dịch vụ tư vấn du học trước 01/07/2026.",
  notified: "Doanh nghiệp gửi thông báo hoạt động tới Sở GD&ĐT theo cơ chế hậu kiểm (NQ 23/2026/NQ-CP từ 01/07/2026).",
};

export function normalizeComplianceMode(mode?: string | null): LegalComplianceMode {
  return mode?.trim().toLowerCase() === "notified" ? "notified" : "licensed";
}

export function centerNeedsTrainingLicense(centerType?: string | null): boolean {
  const t = centerType?.trim().toLowerCase();
  return t === "test_prep" || t === "hybrid";
}

export function formatLegalDate(isoDate?: string | null): string | null {
  if (!isoDate) return null;
  const trimmed = isoDate.trim();
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed);
  if (match) {
    return `${match[3]}/${match[2]}/${match[1]}`;
  }
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return trimmed;
  return parsed.toLocaleDateString("vi-VN");
}

export function getCenterLegalMissingChecklist(center: Partial<StudyCenter>): string[] {
  const missing: string[] = [];
  if (!center.taxCode?.trim()) missing.push("Mã số thuế / ĐKKD");
  if (!center.legalRepresentative?.trim()) missing.push("Người đại diện pháp luật");
  if (!center.licenseNumber?.trim()) {
    missing.push(
      normalizeComplianceMode(center.legalComplianceMode) === "notified"
        ? "Mã hồ sơ thông báo Sở GD&ĐT"
        : "Số giấy phép tư vấn du học"
    );
  }
  if (!center.licenseIssuedBy?.trim()) missing.push("Cơ quan cấp phép / tiếp nhận");
  if (!center.licenseIssuedDate?.trim()) missing.push("Ngày cấp phép / gửi thông báo");
  if (center.certifiedCounselorCount == null) missing.push("Số tư vấn viên có chứng chỉ");
  if (centerNeedsTrainingLicense(center.centerType) && !center.trainingLicenseNumber?.trim()) {
    missing.push("Số giấy phép hoạt động đào tạo ngoại ngữ");
  }
  return missing;
}

export interface LegalFormValues {
  legalComplianceMode: LegalComplianceMode;
  taxCode: string;
  legalRepresentative: string;
  licenseNumber: string;
  trainingLicenseNumber: string;
  licenseIssuedBy: string;
  licenseIssuedDate: string;
  establishedYear: string;
  staffCount: string;
  certifiedCounselorCount: string;
  visaSuccessRate: string;
  accreditationsInput: string;
  legalDocumentUrlsInput: string;
  verificationNote: string;
}

export function createInitialLegalFormValues(center?: Partial<StudyCenter>): LegalFormValues {
  return {
    legalComplianceMode: normalizeComplianceMode(center?.legalComplianceMode),
    taxCode: center?.taxCode || "",
    legalRepresentative: center?.legalRepresentative || "",
    licenseNumber: center?.licenseNumber || "",
    trainingLicenseNumber: center?.trainingLicenseNumber || "",
    licenseIssuedBy: center?.licenseIssuedBy || "",
    licenseIssuedDate: center?.licenseIssuedDate || "",
    establishedYear: center?.establishedYear != null ? String(center.establishedYear) : "",
    staffCount: center?.staffCount != null ? String(center.staffCount) : "",
    certifiedCounselorCount:
      center?.certifiedCounselorCount != null ? String(center.certifiedCounselorCount) : "",
    visaSuccessRate: center?.visaSuccessRate != null ? String(center.visaSuccessRate) : "",
    accreditationsInput: center?.accreditations?.join(", ") || "",
    legalDocumentUrlsInput: center?.legalDocumentUrls?.join("\n") || "",
    verificationNote: center?.verificationNote || "",
  };
}

function isValidLegalDocumentUrl(raw: string): boolean {
  if (raw.startsWith("/")) return true;
  try {
    const parsed = new URL(raw);
    return parsed.protocol === "http:" || parsed.protocol === "https:";
  } catch {
    return false;
  }
}

export interface ParsedLegalPayload {
  legalComplianceMode: LegalComplianceMode;
  taxCode: string | null;
  legalRepresentative: string | null;
  licenseNumber: string | null;
  trainingLicenseNumber: string | null;
  licenseIssuedBy: string | null;
  licenseIssuedDate: string | null;
  establishedYear: number | null;
  staffCount: number | null;
  certifiedCounselorCount: number | null;
  visaSuccessRate: number | null;
  accreditations: string[];
  legalDocumentUrls: string[];
  verificationNote: string;
}

export function parseAndValidateLegalFormValues(
  values: LegalFormValues
): { payload?: ParsedLegalPayload; error?: string } {
  const parsedEstYear = values.establishedYear.trim()
    ? parseInt(values.establishedYear.trim(), 10)
    : null;
  const parsedStaff = values.staffCount.trim() ? parseInt(values.staffCount.trim(), 10) : null;
  const parsedCertified = values.certifiedCounselorCount.trim()
    ? parseInt(values.certifiedCounselorCount.trim(), 10)
    : null;
  const parsedVisa = values.visaSuccessRate.trim()
    ? parseFloat(values.visaSuccessRate.trim())
    : null;

  if (parsedStaff !== null && (Number.isNaN(parsedStaff) || parsedStaff < 0)) {
    return { error: "Quy mô nhân sự phải là số nguyên không âm." };
  }
  if (parsedCertified !== null && (Number.isNaN(parsedCertified) || parsedCertified < 0)) {
    return { error: "Số tư vấn viên có chứng chỉ phải là số nguyên không âm." };
  }
  if (parsedStaff !== null && parsedCertified !== null && parsedCertified > parsedStaff) {
    return { error: "Số tư vấn viên có chứng chỉ không được vượt quá tổng nhân sự." };
  }

  const legalDocumentUrls: string[] = [];
  for (const line of values.legalDocumentUrlsInput.split(/[\n,;]/)) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    if (!isValidLegalDocumentUrl(trimmed)) {
      return {
        error: `Đường dẫn tài liệu pháp lý không hợp lệ: "${trimmed}". Chỉ chấp nhận URL http(s) hoặc đường dẫn bắt đầu bằng "/".`,
      };
    }
    if (!legalDocumentUrls.includes(trimmed)) {
      legalDocumentUrls.push(trimmed);
    }
  }

  const accreditations = values.accreditationsInput
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);

  return {
    payload: {
      legalComplianceMode: values.legalComplianceMode,
      taxCode: values.taxCode.trim() || null,
      legalRepresentative: values.legalRepresentative.trim() || null,
      licenseNumber: values.licenseNumber.trim() || null,
      trainingLicenseNumber: values.trainingLicenseNumber.trim() || null,
      licenseIssuedBy: values.licenseIssuedBy.trim() || null,
      licenseIssuedDate: values.licenseIssuedDate.trim() || null,
      establishedYear:
        parsedEstYear !== null && !Number.isNaN(parsedEstYear) ? parsedEstYear : null,
      staffCount: parsedStaff,
      certifiedCounselorCount: parsedCertified,
      visaSuccessRate: parsedVisa !== null && !Number.isNaN(parsedVisa) ? parsedVisa : null,
      accreditations,
      legalDocumentUrls,
      verificationNote: values.verificationNote.trim(),
    },
  };
}

