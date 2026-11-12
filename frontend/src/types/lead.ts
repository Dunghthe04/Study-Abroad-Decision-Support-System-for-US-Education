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

/** Trạng thái lead (backend: new | contacted | closed). */
export type LeadStatus = "new" | "contacted" | "closed";

/**
 * [UC-44] Nhãn trạng thái hiện cho học sinh / phụ huynh.
 * Giống badge phía trung tâm (UC-73, CenterLeadsListView) trừ "new": trung tâm thấy "Mới nhận", người gửi thấy "Mới gửi".
 */
export const LEAD_STATUS_LABEL: Record<LeadStatus, string> = {
  new: "Mới gửi",
  contacted: "Đang trao đổi",
  closed: "Đã hoàn tất",
};

/** Nhãn trạng thái; giá trị backend chưa biết thì hiện nguyên giá trị đó. */
export const leadStatusLabel = (status: string) => LEAD_STATUS_LABEL[status as LeadStatus] ?? status;

/** [UC-44] Một yêu cầu liên hệ đã gửi — khớp SentLeadDto ở backend (GET /api/v1/leads/sent). */
export interface SentLeadDto {
  id: string;
  studyCenterId: string;
  studyCenterName: string;
  /** Để lỏng như CenterReceivedLeadDto (UC-73); hiện nhãn bằng leadStatusLabel. */
  status: LeadStatus | string;
  createdAt: string;
  sharedFields: LeadSharedField[];
  studyLevel: string | null;
  contactPhone: string | null;
  message: string | null;
  /** Học sinh có hồ sơ được chia sẻ (phụ huynh dùng để biết lead của con nào). */
  studentUserId: string;
  studentName: string;
  /** true khi phụ huynh bấm gửi thay con. */
  submittedByParent: boolean;
  /** Số tin nhắn đang hiện do phía trung tâm gửi. */
  centerMessageCount: number;
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
  | "daily_limit"
  // [Nhắn tin phía người gửi] GET/POST /leads/sent/{id}/messages
  | "lead_closed"
  | "not_found"
  | "invalid_content";

export interface LeadRuleError {
  status: number;
  code: LeadRuleCode;
  message: string;
  /** Chỉ có với center_cooldown: thời điểm (UTC, ISO) được gửi lại cho trung tâm này. */
  retryAt: string | null;
}
