"use client";

import { useAuth } from "@/contexts/AuthContext";

// [Q11] Tên chức năng khớp AgeFeatures.Rules ở backend. Server mới là nơi chặn thật (403 age_restricted);
// frontend chỉ đọc user.blockedFeatures để ẩn nút và giải thích lý do.
export const AGE_FEATURES = {
  processStudentData: "process_student_data",
  shareWithCenter: "share_with_center",
} as const;

export type AgeFeature = (typeof AGE_FEATURES)[keyof typeof AGE_FEATURES];

export interface AgeBlockInfo {
  feature: AgeFeature;
  needParentConsent: boolean;
  message: string;
}

// Chức năng mở được khi phụ huynh đồng ý (khớp ParentConsentUnlocks ở backend)
const PARENT_CONSENT_UNLOCKS: Record<AgeFeature, boolean> = {
  process_student_data: true,
  share_with_center: false,
};

/** Null nếu được dùng; ngược lại trả lý do bị chặn. */
export function useAgeBlock(feature: AgeFeature): AgeBlockInfo | null {
  const { user } = useAuth();
  if (!user?.blockedFeatures?.includes(feature)) return null;

  const needParentConsent = PARENT_CONSENT_UNLOCKS[feature];
  const message = !needParentConsent
    ? "Chức năng này chỉ dành cho người từ 18 tuổi."
    : user.parentConsentStatus === "pending"
      ? "Đang chờ phụ huynh xác nhận đồng ý qua email. Học sinh dưới 16 tuổi cần phụ huynh đồng ý trước khi hệ thống lưu và phân tích hồ sơ."
      : "Học sinh dưới 16 tuổi cần phụ huynh xác nhận đồng ý trước khi hệ thống lưu và phân tích hồ sơ (Luật BVDLCN 91/2025, Điều 24).";
  return { feature, needParentConsent, message };
}

export function useFeatureAllowed(feature: AgeFeature): boolean {
  return useAgeBlock(feature) === null;
}
