import type { StudyCenter } from "@/types/api";

export type CenterType = "study_abroad" | "test_prep" | "hybrid";

/**
 * Hồ sơ hiển thị của trung tâm: chỉ gồm dữ liệu thật do trung tâm / hệ thống lưu.
 * Trường nào chưa có thì là null / rỗng để giao diện ghi "Chưa cập nhật" hoặc ẩn đi,
 * không tự điền số liệu mẫu. Đánh giá lấy riêng từ API đánh giá.
 */
export interface EnrichedCenterProfile {
  center: StudyCenter;
  type: CenterType;
  typeLabel: string;
  coverImage: string | null;
  logoImage: string | null;
  galleryImages: { url: string; caption: string }[];
  establishedYear: number | null;
  staffCount: number | null;
  licenseNumber: string | null;
  licenseIssuedBy: string | null;
  taxCode?: string | null;
  legalRepresentative?: string | null;
  licenseIssuedDate?: string | null;
  legalComplianceMode: "licensed" | "notified";
  trainingLicenseNumber?: string | null;
  certifiedCounselorCount?: number | null;
  legalDocumentUrls: string[];
  verificationNote?: string | null;
  accreditations: string[];
  visaSuccessRate: number | null;
}

export function determineCenterType(services: string[] = [], explicitType?: string): { type: CenterType; label: string } {
  if (explicitType === "hybrid") {
    return { type: "hybrid", label: "Tổ chức Toàn diện (Du học & Luyện thi SAT/IELTS)" };
  }
  if (explicitType === "test_prep" || explicitType === "english_training") {
    return { type: "test_prep", label: "Trung tâm Đào tạo Tiếng Anh & Luyện thi Chứng chỉ" };
  }
  if (explicitType === "study_abroad") {
    return { type: "study_abroad", label: "Trung tâm Tư vấn Du học Mỹ" };
  }

  const hasPrep = services.some(s => s.includes("LUYEN_THI") || s.includes("CHUNG_CHI") || s.includes("IELTS") || s.includes("SAT"));
  const hasStudyAbroad = services.some(s =>
    s.includes("CHON_TRUONG") ||
    s.includes("SAN_HOC_BONG") ||
    s.includes("NOP_HO_SO") ||
    s.includes("HO_SO") ||
    s.includes("VISA") ||
    s.includes("CHUYEN_TIEP")
  );

  if (hasPrep && hasStudyAbroad) {
    return { type: "hybrid", label: "Tổ chức Toàn diện (Du học & Luyện thi SAT/IELTS)" };
  }
  if (hasPrep) {
    return { type: "test_prep", label: "Trung tâm Đào tạo Tiếng Anh & Luyện thi Chứng chỉ" };
  }
  return { type: "study_abroad", label: "Trung tâm Tư vấn Du học Mỹ" };
}

export function getEnrichedCenterProfile(center: StudyCenter): EnrichedCenterProfile {
  const { type, label: typeLabel } = determineCenterType(center.services, center.centerType);

  return {
    center,
    type,
    typeLabel,
    coverImage: center.coverImageUrl || null,
    logoImage: center.logoUrl || null,
    galleryImages: (center.galleryImages ?? []).map((url, idx) => ({
      url,
      caption: `Ảnh cơ sở ${idx + 1}`,
    })),
    establishedYear: center.establishedYear ?? null,
    staffCount: center.staffCount ?? null,
    licenseNumber: center.licenseNumber || null,
    licenseIssuedBy: center.licenseIssuedBy || null,
    taxCode: center.taxCode ?? null,
    legalRepresentative: center.legalRepresentative ?? null,
    licenseIssuedDate: center.licenseIssuedDate ?? null,
    legalComplianceMode: center.legalComplianceMode === "notified" ? "notified" : "licensed",
    trainingLicenseNumber: center.trainingLicenseNumber ?? null,
    certifiedCounselorCount: center.certifiedCounselorCount ?? null,
    legalDocumentUrls: center.legalDocumentUrls ?? [],
    verificationNote: center.verificationNote ?? null,
    accreditations: center.accreditations ?? [],
    visaSuccessRate: center.visaSuccessRate ?? null,
  };
}
