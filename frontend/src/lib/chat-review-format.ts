// Nhãn tiếng Việt cho mã SLM (loại ý, loại ngoài phạm vi) và độ tin cậy.

// Loại con của ý ngoài phạm vi (out_of_scope)
const OUT_OF_SCOPE_LABELS: Record<string, string> = {
  ambiguous: "Câu mơ hồ",
  exception: "Ngoại lệ",
  sensitive: "Nội dung nhạy cảm",
  greeting: "Lời chào",
  farewell: "Cảm ơn, tạm biệt",
  about_system: "Hỏi về hệ thống",
  human_handoff: "Muốn gặp người thật",
};

// Sáu loại ý SLM gán cho câu hỏi
const INTENT_LABELS: Record<string, string> = {
  collect_profile: "Nhập thông tin hồ sơ",
  profile_review: "Đánh giá hồ sơ",
  scenario: "Kịch bản what-if",
  knowledge: "Hỏi kiến thức",
  center_referral: "Chuyển trung tâm tư vấn",
  out_of_scope: "Ngoài phạm vi",
};

export const outOfScopeLabel = (subtype: string) => OUT_OF_SCOPE_LABELS[subtype] ?? subtype;
export const intentLabel = (type: string) => INTENT_LABELS[type] ?? type;

/** 0.4 → "40%"; không có số thì null. */
export function formatConfidence(confidence: number | null | undefined): string | null {
  if (confidence == null || !Number.isFinite(confidence)) return null;
  return `${Math.round(confidence * 100)}%`;
}
