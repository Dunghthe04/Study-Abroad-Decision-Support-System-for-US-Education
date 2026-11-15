/** [UC-44 / UC-74] Hằng số và hàm dùng chung cho tin nhắn yêu cầu liên hệ. */
export const MAX_LEAD_MESSAGE_LENGTH = 4000;

/** Ảnh đính kèm tin nhắn: jpg / png / webp, tối đa 5MB (backend kiểm tra lại). */
export const LEAD_IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const LEAD_IMAGE_ACCEPT = "image/jpeg,image/png,image/webp";
const LEAD_IMAGE_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);

/** Trả về câu lỗi tiếng Việt nếu tệp không hợp lệ, null nếu hợp lệ. */
export function validateLeadImage(file: File): string | null {
  if (!LEAD_IMAGE_TYPES.has(file.type)) return "Chỉ chấp nhận ảnh .jpg, .png hoặc .webp.";
  if (file.size === 0) return "Tệp ảnh trống.";
  if (file.size > LEAD_IMAGE_MAX_BYTES) return "Ảnh tối đa 5MB.";
  return null;
}

export function formatLeadMessageRoleLabel(role: string): string {
  switch (role.toLowerCase()) {
    case "center":
      return "Trung tâm";
    case "moderator":
      return "Kiểm duyệt viên";
    case "admin":
      return "Quản trị viên";
    case "parent":
      return "Phụ huynh";
    case "student":
      return "Học sinh";
    default:
      return "Người dùng";
  }
}
