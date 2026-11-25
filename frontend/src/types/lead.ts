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

/** Trạng thái lead (backend: LeadStatuses). */
export type LeadStatus =
  | "new" | "accepted" | "rejected" | "contacted" | "closed" | "withdrawn" | "pending_parent" | "cancelled" | "expired" | "center_inactive";

/** [UC-73] Trung tâm đã chấp nhận (gồm contacted cũ): điều kiện để nhắn tin (UC-74). */
export const isLeadAccepted = (status: string) => status === "accepted" || status === "contacted";

/** [UC-73] Sau 90 ngày kể từ khi gửi, dữ liệu chia sẻ bị ẩn và yêu cầu coi như hết hạn. */
export const LEAD_DATA_VISIBLE_DAYS = 90;

/** Mã lỗi backend chặn nhắn tin theo trạng thái (UC-74), dùng để khóa khung soạn ngay khi server từ chối. */
export const LEAD_MESSAGING_BLOCK_CODES = [
  "lead_not_accepted",
  "lead_not_open",
  "lead_rejected",
  "lead_closed",
  "lead_withdrawn",
  "lead_cancelled",
  "lead_expired",
  "center_inactive",
] as const;

/**
 * [UC-74] Lý do không nhắn tin được trong yêu cầu liên hệ; null nghĩa là nhắn được.
 * Chỉ nhắn khi Đã chấp nhận; chặn khi Mới, Bị từ chối, Đã đóng, Đã thu hồi, Đã hủy, Hết hạn hoặc quá 90 ngày.
 * Backend kiểm tra lại cùng quy tắc (LeadService.EnsureCanMessage).
 */
export function getLeadMessagingBlockReason(status: string, createdAt?: string | null): string | null {
  switch (status) {
    case "closed":
      return "Yêu cầu đã đóng, không gửi thêm tin nhắn được.";
    case "withdrawn":
      return "Yêu cầu đã được thu hồi, không gửi thêm tin nhắn được.";
    case "rejected":
      return "Yêu cầu đã bị trung tâm từ chối, không nhắn tin được.";
    case "cancelled":
      return "Yêu cầu đã bị hủy, không nhắn tin được.";
    case "expired":
      return "Yêu cầu đã hết hạn, không nhắn tin được.";
    case "center_inactive":
      return "Trung tâm hiện tạm ngừng hoạt động, không thể tiếp tục nhắn tin.";
    case "pending_parent":
      return "Yêu cầu đang chờ phụ huynh xác nhận nên chưa nhắn tin được.";
  }
  if (createdAt) {
    const ageMs = Date.now() - new Date(createdAt).getTime();
    if (ageMs > LEAD_DATA_VISIBLE_DAYS * 24 * 60 * 60 * 1000) {
      return "Yêu cầu đã quá 90 ngày, không nhắn tin được nữa.";
    }
  }
  if (!isLeadAccepted(status)) {
    return "Trung tâm chưa chấp nhận yêu cầu này nên chưa nhắn tin được.";
  }
  return null;
}

/** [UC-116] Trung tâm chưa nhận yêu cầu (chờ phụ huynh / phụ huynh từ chối / hết hạn): chưa nhắn tin được. */
export const isLeadAwaitingCenter = (status: string) =>
  status === "pending_parent" || status === "cancelled" || status === "expired";

/**
 * [UC-44] Nhãn trạng thái hiện cho học sinh / phụ huynh.
 * Giống badge phía trung tâm (UC-73, CenterLeadsListView) trừ "new": trung tâm thấy "Mới nhận", người gửi thấy "Mới gửi".
 */
export const LEAD_STATUS_LABEL: Record<LeadStatus, string> = {
  new: "Mới gửi",
  accepted: "Trung tâm đã chấp nhận", // [UC-73]
  rejected: "Trung tâm từ chối", // [UC-73]
  contacted: "Đang trao đổi",
  closed: "Đã hoàn tất",
  withdrawn: "Đã thu hồi", // [UC-80] người gửi đã xóa tài khoản
  // [UC-116] học sinh dưới 16 tự gửi: phụ huynh xác nhận qua mail
  pending_parent: "Chờ phụ huynh xác nhận",
  cancelled: "Phụ huynh từ chối",
  expired: "Hết hạn",
  center_inactive: "Trung tâm ngừng hoạt động",
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
  | "lead_withdrawn" // [UC-80] người gửi đã xóa tài khoản
  | "lead_not_open" // [UC-116] trung tâm chưa nhận (chờ phụ huynh / từ chối / hết hạn)
  | "center_inactive" // [UC-57] trung tâm bị khóa / tạm ngừng hoạt động
  | "not_found"
  | "invalid_content";

export interface LeadRuleError {
  status: number;
  code: LeadRuleCode;
  message: string;
  /** Chỉ có với center_cooldown: thời điểm (UTC, ISO) được gửi lại cho trung tâm này. */
  retryAt: string | null;
}

/** [UC-116] Trang phụ huynh xác nhận yêu cầu liên hệ của con (/parent-consent/lead) — khớp LeadParentConfirmInfo ở backend. */
export type LeadParentConfirmState = "pending" | "accepted" | "rejected";

export interface LeadParentConfirmInfo {
  state: LeadParentConfirmState;
  studentName: string;
  studentAge: number | null;
  centerName: string;
  sharedFields: LeadSharedField[];
  contactPhone: string | null;
  message: string | null;
  /** Email phụ huynh đang mở link */
  parentEmail: string;
  /** Chỉ có khi còn chờ */
  expiresAt: string | null;
  decidedAt: string | null;
  /** Người bấm trước có phải chính phụ huynh này không (con có nhiều phụ huynh) */
  decidedByYou: boolean;
}
