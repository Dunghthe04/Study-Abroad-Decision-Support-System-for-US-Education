// [UC-42] Gửi yêu cầu liên hệ trung tâm — khớp với CreateLeadRequest / LeadDto / LeadRuleException ở backend.

/** 5 nhóm dữ liệu người gửi chọn chia sẻ cho trung tâm (backend: LeadSharedFields). */
export type LeadSharedField = "contact" | "academic" | "tests" | "goals" | "budget";

export const LEAD_SHARED_FIELDS: readonly LeadSharedField[] = ["contact", "academic", "tests", "goals", "budget"];

/** Nhãn và mô tả từng nhóm; `defaultChecked` theo bảng trong kế hoạch UC-42 (contact luôn bật và khóa). */
export const LEAD_SHARED_FIELD_INFO: Record<
  LeadSharedField,
  { label: string; description: string; defaultChecked: boolean; locked?: boolean }
> = {
  contact: { label: "Liên hệ", description: "Họ tên, email", defaultChecked: true, locked: true },
  academic: { label: "Học tập", description: "GPA, hệ học, lớp hiện tại", defaultChecked: true },
  tests: { label: "Bài thi", description: "Loại bài thi và điểm tiếng Anh, SAT, ACT", defaultChecked: true },
  goals: { label: "Mục tiêu", description: "Bậc học muốn học, ngành dự định", defaultChecked: true },
  budget: { label: "Ngân sách", description: "Mức ngân sách, mức hoạt động ngoại khóa", defaultChecked: false },
};

export interface CreateLeadRequest {
  studyCenterId: string;
  studyLevel?: string | null;
  contactPhone?: string | null;
  message?: string | null;
  /** Bắt buộc khi phụ huynh gửi thay con (id lấy từ ParentLinkDto.studentUserId). */
  studentUserId?: string | null;
  /** Phải có "contact". */
  sharedFields: LeadSharedField[];
  /** Ô "Tôi đồng ý chia sẻ các thông tin đã chọn cho trung tâm" — bắt buộc true. */
  consentAccepted: boolean;
  /** Học sinh 16–17 tuổi tự gửi phải true. */
  acknowledgedCenterContact?: boolean;
}

export interface LeadDto {
  id: string;
  /** Học sinh của lead (khi phụ huynh gửi thay thì là con). */
  userId: string;
  studyCenterId: string;
  studyCenterName: string | null;
  consentId: string | null;
  studyLevel: string | null;
  contactPhone: string | null;
  message: string | null;
  sharedProfile: boolean;
  status: string;
  createdAt: string;
  /** Người bấm gửi (học sinh hoặc phụ huynh). */
  submittedByUserId: string | null;
  sharedFields: LeadSharedField[];
}

/** Mã lỗi quy tắc backend trả trong body { code, message, retryAt? }. */
export type LeadRuleCode =
  | "center_not_verified"
  | "student_required"
  | "not_linked"
  | "role_not_allowed"
  | "age_restricted"
  | "acknowledgement_required"
  | "invalid_shared_fields"
  | "contact_required"
  | "consent_required"
  | "center_cooldown"
  | "daily_limit";

export interface LeadRuleError {
  status: number;
  code: LeadRuleCode;
  message: string;
  /** Chỉ có với center_cooldown: thời điểm (UTC, ISO) được gửi lại cho trung tâm này. */
  retryAt: string | null;
}
