// [UC-42] API gửi yêu cầu liên hệ trung tâm (học sinh tự gửi hoặc phụ huynh gửi thay con).
// [UC-44] listSent: yêu cầu liên hệ đã gửi.
// [Nhắn tin phía người gửi] listSentMessages / sendSentMessage: tin nhắn với trung tâm trong yêu cầu đã gửi.
// Danh sách con đã liên kết của phụ huynh dùng lại parentLinksApi.getMine() trong lib/api.ts.

import { apiFetch, ApiError, buildImageForm } from "@/lib/api";
// Kiểu tin nhắn dùng lại của Đức (UC-74) — cùng DTO với phía trung tâm, không khai báo trùng
import type {
  LeadMessageDto,
  LeadMessagesThreadDto,
  PagedResult,
  SendLeadMessageRequest,
  SendLeadMessageResultDto,
} from "@/types/api";
import type { CreateLeadRequest, LeadDto, LeadParentConfirmInfo, LeadRuleCode, LeadRuleError, SentLeadDto } from "@/types/lead";

export const leadApi = {
  create: (request: CreateLeadRequest) =>
    apiFetch<LeadDto>("/api/v1/leads", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  getById: (id: string) => apiFetch<LeadDto>(`/api/v1/leads/${encodeURIComponent(id)}`),

  /** [UC-44] Yêu cầu đã gửi, mới nhất trước. Học sinh dưới 16 nhận 403 age_restricted (đọc bằng getLeadRuleError). */
  // Dựng query giống centerLeadsApi.list (UC-73): chỉ gửi tham số có giá trị, backend mặc định trang 1, 10 dòng.
  listSent: (params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<PagedResult<SentLeadDto>>(`/api/v1/leads/sent${qs ? `?${qs}` : ""}`);
  },

  /**
   * [Nhắn tin phía người gửi] Luồng tin của một yêu cầu đã gửi, cũ nhất trước.
   * Lỗi (đọc bằng getLeadRuleError): 403 age_restricted, 404 not_found.
   */
  listSentMessages: (leadId: string) =>
    apiFetch<LeadMessagesThreadDto>(`/api/v1/leads/sent/${encodeURIComponent(leadId)}/messages`),

  /**
   * [Nhắn tin phía người gửi] Gửi tin.
   * Lỗi thêm: 400 lead_closed (lead đã hoàn tất), 400 invalid_content (rỗng hoặc quá 4000 ký tự).
   */
  sendSentMessage: (leadId: string, content: string) => {
    const payload: SendLeadMessageRequest = { content };
    return apiFetch<SendLeadMessageResultDto>(`/api/v1/leads/sent/${encodeURIComponent(leadId)}/messages`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  /** Gửi tin kèm ảnh (jpg/png/webp, tối đa 5MB), `content` không bắt buộc. */
  sendSentImageMessage: (leadId: string, file: File, content?: string) =>
    apiFetch<SendLeadMessageResultDto>(`/api/v1/leads/sent/${encodeURIComponent(leadId)}/messages/image`, {
      method: "POST",
      body: buildImageForm(file, content),
    }),

  /**
   * [UC-143] Báo cáo một tin của trung tâm vi phạm quy tắc (trong 90 ngày kể từ ngày gửi yêu cầu); tin vào hàng đợi của Admin.
   * Lý do bắt buộc 3–64 ký tự; chi tiết thêm tùy chọn tối đa 1.000 ký tự.
   */
  reportSentMessage: (leadId: string, messageId: string, reason: string, detail?: string) =>
    apiFetch<LeadMessageDto>(
      `/api/v1/leads/sent/${encodeURIComponent(leadId)}/messages/${encodeURIComponent(messageId)}/report`,
      {
        method: "POST",
        body: JSON.stringify({ reason: reason.trim(), detail: detail?.trim() || null }),
      }
    ),
};

// [UC-116] Không cần đăng nhập: token một lần trong mail là xác thực, gửi trong body để không lộ ở log URL.
// Lỗi: 404 link sai, 410 hết hạn, 409 đã có phụ huynh trả lời.
type LeadParentConfirmResponse = { message: string | null; data: LeadParentConfirmInfo };

const postLeadParentConfirm = (action: "lookup" | "accept" | "reject", token: string) =>
  apiFetch<LeadParentConfirmResponse>(`/api/v1/leads/parent-confirm/${action}`, {
    method: "POST",
    body: JSON.stringify({ token }),
  });

export const leadParentConfirmApi = {
  lookup: (token: string) => postLeadParentConfirm("lookup", token),
  accept: (token: string) => postLeadParentConfirm("accept", token),
  reject: (token: string) => postLeadParentConfirm("reject", token),
};

/**
 * Đọc lỗi quy tắc { code, message, retryAt? } từ ApiError của leadApi.
 * Trả null nếu không phải lỗi quy tắc (lỗi mạng, lỗi kiểm tra dữ liệu dạng ProblemDetails...).
 */
export function getLeadRuleError(error: unknown): LeadRuleError | null {
  if (!(error instanceof ApiError)) return null;
  const body = error.body as { code?: unknown; message?: unknown; retryAt?: unknown } | null;
  if (!body || typeof body.code !== "string") return null;

  return {
    status: error.status,
    code: body.code as LeadRuleCode,
    message: typeof body.message === "string" ? body.message : error.message,
    retryAt: typeof body.retryAt === "string" ? body.retryAt : null,
  };
}
