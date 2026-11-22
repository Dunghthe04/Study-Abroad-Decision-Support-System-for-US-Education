// [UC-103] Văn bản chính sách và điều khoản. Khớp PublicPolicyDto của backend (StudyAbroad.Application.Admin.Policies).

/** Loại tài liệu; khớp PolicyKinds của backend. */
export type PolicyKind = "terms_of_use" | "privacy_policy" | "child_data_consent" | "forum_rules";

/** Bản ĐANG HIỆU LỰC của một loại, cho người dùng đọc. Không có mã người soạn. */
export interface PublicPolicy {
  kind: PolicyKind;
  /** yyyy-MM, thêm -02, -03 nếu một tháng có nhiều bản. */
  version: string;
  title: string;
  /** Toàn văn, Markdown đơn giản. */
  content: string;
  /** Tóm tắt thay đổi so với bản trước. */
  changeSummary: string | null;
  /** Bản này có bắt người dùng đồng ý lại không (UC-104). */
  requiresReconsent: boolean;
  publishedAt: string;
}
