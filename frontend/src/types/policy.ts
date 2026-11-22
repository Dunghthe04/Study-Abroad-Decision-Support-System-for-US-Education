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

// ---------------------------------------------------------------- phía Administrator (quản lý, UC-103)

/** draft: nháp, sửa được · published: đang hiệu lực, khóa · retired: hết hiệu lực (có bản mới thay), giữ để tra cứu. */
export type PolicyStatus = "draft" | "published" | "retired";

/** Một phiên bản đầy đủ kèm toàn văn. Khớp PolicyDocumentDto. */
export interface PolicyDocument {
  id: string;
  kind: PolicyKind;
  /** Trống khi còn là nháp. */
  version: string | null;
  status: PolicyStatus;
  title: string;
  content: string;
  changeSummary: string | null;
  requiresReconsent: boolean;
  /** Bản được chép ra để tạo nháp này. */
  basedOnId: string | null;
  createdByUserId: string | null;
  createdAt: string;
  publishedAt: string | null;
  publishedByUserId: string | null;
  retiredAt: string | null;
}

/** Một dòng lịch sử, không kèm toàn văn. Khớp PolicyDocumentSummaryDto. */
export interface PolicyDocumentSummary {
  id: string;
  kind: PolicyKind;
  version: string | null;
  status: PolicyStatus;
  title: string;
  changeSummary: string | null;
  requiresReconsent: boolean;
  contentLength: number;
  createdAt: string;
  publishedAt: string | null;
  retiredAt: string | null;
}

/** created = false: loại này đã có nháp nên trả lại nháp đó, không tạo thêm. */
export interface PolicyDraftResult {
  document: PolicyDocument;
  created: boolean;
}

export interface UpdatePolicyDraftRequest {
  title: string;
  content: string;
  /** Có thể trống khi đang soạn, bắt buộc lúc công bố. */
  changeSummary: string | null;
  requiresReconsent: boolean;
}
