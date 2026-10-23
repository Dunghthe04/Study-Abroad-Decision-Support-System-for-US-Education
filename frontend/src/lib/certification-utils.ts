// [USAS-640] Hàm xử lý chứng chỉ bước 3. Chỉ có hàm, không có danh mục:
// mọi loại chứng chỉ, thang điểm, điểm thành phần, hạn dùng đều đến từ metadata (app_settings profile.metadata)

import {
  OTHER_CERT_TYPE,
  type CertificationCategory,
  type CertificationDraft,
  type CertificationItem,
  type CertificationTypeOption,
  type LegacyCertificationDraft,
} from "@/types/profile-flow";

/** Bỏ dấu tiếng Việt + chữ thường để tìm kiếm ("chứng chỉ" khớp "chung chi") */
export function normalizeSearch(text: string): string {
  return text
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .trim();
}

/** Các loại thuộc nhóm `categories`, khớp `query` theo tên hoặc aliases; sắp theo order */
export function searchCertTypes(
  types: CertificationTypeOption[],
  categories: CertificationCategory[],
  query: string
): CertificationTypeOption[] {
  const q = normalizeSearch(query);
  return types
    .filter((t) => categories.includes(t.category))
    .filter((t) => !q || [t.name, t.id, ...t.aliases].some((s) => normalizeSearch(s).includes(q)))
    .sort((a, b) => a.order - b.order);
}

export function findCertType(
  types: CertificationTypeOption[],
  typeId: string | null | undefined
): CertificationTypeOption | undefined {
  if (!typeId) return undefined;
  return types.find((t) => t.id.toLowerCase() === typeId.toLowerCase());
}

export function isOtherCert(cert: Pick<CertificationItem, "type">): boolean {
  return cert.type === OTHER_CERT_TYPE;
}

/** "yyyy-MM" → {year, month}; sai định dạng → null */
export function parseYearMonth(value: string | null | undefined): { year: number; month: number } | null {
  if (!value) return null;
  const m = /^(\d{4})-(\d{1,2})/.exec(value);
  if (!m) return null;
  const year = Number(m[1]);
  const month = Number(m[2]);
  if (month < 1 || month > 12) return null;
  return { year, month };
}

export function toYearMonth(value: { year: number; month: number }): string {
  return `${value.year}-${String(value.month).padStart(2, "0")}`;
}

/** "yyyy-MM" → "MM/yyyy" */
export function formatYearMonth(value: string | null | undefined): string | null {
  const ym = parseYearMonth(value);
  return ym ? `${String(ym.month).padStart(2, "0")}/${ym.year}` : null;
}

export interface CertExpiry {
  expired: boolean;
  label: string;
}

/** Hạn dùng = tháng thi + validityYears (metadata). Không có ngày thi hoặc loại không hết hạn → null */
export function getExpiry(
  testDate: string | null | undefined,
  type: CertificationTypeOption | undefined,
  now: Date = new Date()
): CertExpiry | null {
  const ym = parseYearMonth(testDate);
  if (!ym || !type?.validityYears) return null;
  const expiryYear = ym.year + type.validityYears;
  const expiryIndex = expiryYear * 12 + ym.month;
  const nowIndex = now.getFullYear() * 12 + (now.getMonth() + 1);
  if (expiryIndex < nowIndex) return { expired: true, label: "Đã hết hạn" };
  return {
    expired: false,
    label: `Còn hạn đến ${String(ym.month).padStart(2, "0")}/${expiryYear}`,
  };
}

/** Số lẻ theo bước của thang: bước 0.5 → "7.0", bước 1 → "7" */
export function formatScore(value: number, step?: number | null): string {
  if (step != null && step > 0 && !Number.isInteger(step)) {
    const decimals = String(step).split(".")[1]?.length ?? 1;
    return value.toFixed(decimals);
  }
  return String(value);
}

/** Tên hiển thị: "Khác" → tên tự nhập; AP / A-Level → "AP · Calculus BC" */
export function getCertDisplayName(cert: CertificationItem, type: CertificationTypeOption | undefined): string {
  if (isOtherCert(cert)) return cert.customName?.trim() || "Chứng chỉ khác";
  const name = type?.name ?? cert.type.toUpperCase();
  const subject = cert.subject?.trim();
  return subject ? `${shortTypeName(name)} · ${subject}` : name;
}

// "AP (Advanced Placement)" → "AP" khi đi kèm tên môn
function shortTypeName(name: string): string {
  return name.replace(/\s*\(.*\)\s*$/, "");
}

export interface CertSummaryPart {
  label?: string;
  value: string;
  /** phần điểm chính, in đậm */
  strong?: boolean;
  suffix?: string;
}

/** Dòng tóm tắt dưới tên thẻ, ví dụ: Điểm tổng 6.5 · Nghe 7.0 · … · Thi 03/2026 */
export function getCertSummaryParts(
  cert: CertificationItem,
  type: CertificationTypeOption | undefined
): CertSummaryPart[] {
  const parts: CertSummaryPart[] = [];
  const hasSections = (type?.sections.length ?? 0) > 0;

  if (cert.overall != null) {
    const label = hasSections ? "Điểm tổng" : "Điểm";
    let suffix: string | undefined;
    if (isOtherCert(cert)) suffix = cert.scaleText?.trim() ? `/ ${cert.scaleText.trim()}` : undefined;
    else if (type?.requiresSubject && type.max != null) suffix = `/ ${formatScore(type.max, type.step)}`;
    parts.push({ label, value: formatScore(cert.overall, type?.step), strong: true, suffix });
  } else if (!isOtherCert(cert) && cert.scaleText?.trim()) {
    // Loại không có thang số (A-Level) → điểm chữ nằm ở scaleText
    parts.push({ label: "Điểm", value: cert.scaleText.trim(), strong: true });
  }

  for (const section of type?.sections ?? []) {
    const v = cert.sections?.[section.id];
    if (v != null) parts.push({ label: section.label, value: formatScore(v, section.step) });
  }

  const date = formatYearMonth(cert.testDate);
  if (date) parts.push({ label: "Thi", value: date });
  return parts;
}

/** Bản chữ của tóm tắt (bước 6) */
export function formatCertSummary(cert: CertificationItem, type: CertificationTypeOption | undefined): string {
  const summary = getCertSummaryParts(cert, type)
    .map((p) => [p.label, p.value, p.suffix].filter(Boolean).join(" "))
    .join(" · ");
  return summary ? `${getCertDisplayName(cert, type)}: ${summary}` : getCertDisplayName(cert, type);
}

function isOnScale(value: number, min: number, max: number, step: number | null): boolean {
  if (value < min || value > max) return false;
  if (!step || step <= 0) return true;
  const n = (value - min) / step;
  return Math.abs(n - Math.round(n)) < 1e-6;
}

function scaleText(min: number, max: number, step: number | null): string {
  return `từ ${min} đến ${max}${step && step !== 1 ? `, bước ${step}` : ""}`;
}

// "Tên môn AP" → "tên môn AP" (giữ nguyên chữ viết tắt)
const lowerFirst = (s: string) => s.charAt(0).toLowerCase() + s.slice(1);

export type CertFormErrors = Partial<Record<"type" | "customName" | "subject" | "overall" | "testDate" | `section.${string}`, string>>;

/** Kiểm tra một chứng chỉ theo luật trong metadata (giống BE validate lúc commit) */
export function validateCertification(
  cert: CertificationItem,
  type: CertificationTypeOption | undefined,
  now: Date = new Date()
): CertFormErrors {
  const errors: CertFormErrors = {};

  if (!cert.type) {
    errors.type = "Chọn loại chứng chỉ";
    return errors;
  }

  if (isOtherCert(cert)) {
    if (!cert.customName?.trim()) errors.customName = "Nhập tên chứng chỉ";
    if (cert.overall != null && cert.overall < 0) errors.overall = "Điểm không được âm";
  } else if (!type) {
    errors.type = "Loại chứng chỉ này không còn trong danh sách, hãy chọn lại";
  } else {
    if (type.min != null && type.max != null) {
      if (cert.overall == null) errors.overall = "Nhập điểm";
      else if (!isOnScale(cert.overall, type.min, type.max, type.step))
        errors.overall = `Điểm phải ${scaleText(type.min, type.max, type.step)}`;
    } else if (!cert.scaleText?.trim()) {
      errors.overall = "Nhập điểm";
    }

    for (const section of type.sections) {
      const v = cert.sections?.[section.id];
      if (v != null && !isOnScale(v, section.min, section.max, section.step))
        errors[`section.${section.id}`] = `Phải ${scaleText(section.min, section.max, section.step)}`;
    }

    if (type.requiresSubject && !cert.subject?.trim())
      errors.subject = `Nhập ${lowerFirst(type.subjectLabel ?? "tên môn")}`;
  }

  const ym = parseYearMonth(cert.testDate);
  if (ym && ym.year * 12 + ym.month > now.getFullYear() * 12 + now.getMonth() + 1)
    errors.testDate = "Ngày thi không được ở tương lai";

  return errors;
}

export function newCertId(): string {
  return `c_${Math.random().toString(36).slice(2, 8)}`;
}

// Mã loại trong nháp cũ (nút IELTS/TOEFL/Duolingo, SAT/ACT/AP) — trùng id trong metadata khi viết thường
const LEGACY_STANDARDIZED = new Set(["SAT", "ACT", "AP"]);

/**
 * Bước 3 trong nháp đã lưu → dạng mới. Nháp mới giữ nguyên; nháp cũ (englishTestType / otherTests)
 * được chuyển thành danh sách chứng chỉ, giống NormalizeCertificationStep bên BE.
 */
export function migrateLegacyStep3(
  raw: (Partial<CertificationDraft> & LegacyCertificationDraft) | null | undefined
): CertificationDraft {
  if (!raw) return { certifications: [], englishPlanned: false, englishPlannedDate: null };

  if (Array.isArray(raw.certifications)) {
    return {
      certifications: raw.certifications.map((c) => ({ ...c, id: c.id || newCertId() })),
      englishPlanned: Boolean(raw.englishPlanned),
      englishPlannedDate: raw.englishPlannedDate ?? null,
    };
  }

  const certifications: CertificationItem[] = [];
  const english = raw.englishTestType;
  if (english && english.toLowerCase() !== "none" && raw.englishOverallScore != null) {
    certifications.push({
      id: newCertId(),
      category: "english",
      type: english.toLowerCase(),
      overall: raw.englishOverallScore,
      testDate: raw.englishTestDate ?? null,
    });
  }
  for (const test of raw.otherTests ?? []) {
    if (!LEGACY_STANDARDIZED.has(test.testType) || test.score == null) continue;
    certifications.push({
      id: newCertId(),
      category: "standardized",
      type: test.testType.toLowerCase(),
      overall: test.score,
    });
  }

  return { certifications, englishPlanned: false, englishPlannedDate: null };
}
