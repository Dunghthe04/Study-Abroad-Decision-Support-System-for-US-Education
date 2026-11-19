import type { LabeledOption } from "@/lib/admin-university-display";
import type { KnowledgeEntry, KnowledgeLevel, SaveKnowledgeEntryRequest } from "@/types/knowledge-base";

/** 27 nhãn, cùng danh sách với KnowledgeLabels.All ở backend. Thêm nhãn: thêm ở cả hai nơi. */
export const KNOWLEDGE_LABELS: { id: string; label: string }[] = [
  { id: "LABEL_VISA_F1", label: "Visa F-1" },
  { id: "LABEL_DUY_TRI_F1", label: "Duy trì F-1" },
  { id: "LABEL_TUYEN_SINH_SAU_DAI_HOC", label: "Tuyển sinh sau đại học" },
  { id: "LABEL_LO_TRINH_BAC_HOC", label: "Lộ trình bậc học" },
  { id: "LABEL_TIENG_ANH", label: "Tiếng Anh" },
  { id: "LABEL_THI_CHUAN_HOA", label: "Thi chuẩn hóa" },
  { id: "LABEL_HO_SO_GIAY_TO", label: "Hồ sơ, giấy tờ" },
  { id: "LABEL_CHI_PHI", label: "Chi phí" },
  { id: "LABEL_CHON_TRUONG", label: "Chọn trường" },
  { id: "LABEL_HOC_BONG_HO_TRO", label: "Học bổng, hỗ trợ tài chính" },
  { id: "LABEL_THUAT_NGU", label: "Thuật ngữ" },
  { id: "LABEL_GPA_HOC_BA", label: "GPA, học bạ" },
  { id: "LABEL_I20_SEVIS", label: "I-20, SEVIS" },
  { id: "LABEL_CHUNG_MINH_TAI_CHINH", label: "Chứng minh tài chính" },
  { id: "LABEL_TRUNG_TAM_TU_VAN", label: "Trung tâm tư vấn" },
  { id: "LABEL_HOC_TAP_TAI_MY", label: "Học tập tại Mỹ" },
  { id: "LABEL_XUAT_NHAP_CANH", label: "Xuất nhập cảnh" },
  { id: "LABEL_DOI_SONG_AN_TOAN", label: "Đời sống, an toàn" },
  { id: "LABEL_CHON_NGANH", label: "Chọn ngành" },
  { id: "LABEL_VONG_NOP_HAN_CHOT", label: "Vòng nộp, hạn chót" },
  { id: "LABEL_BAI_LUAN", label: "Bài luận" },
  { id: "LABEL_PHU_HUYNH", label: "Phụ huynh" },
  { id: "LABEL_KET_QUA_TRUNG_TUYEN", label: "Kết quả trúng tuyển" },
  { id: "LABEL_NGOAI_KHOA_GIAI_THUONG", label: "Ngoại khóa, giải thưởng" },
  { id: "LABEL_THU_GIOI_THIEU", label: "Thư giới thiệu" },
  { id: "LABEL_NGOAI_PHAM_VI", label: "Ngoài phạm vi" },
  { id: "LABEL_PHONG_VAN_TUYEN_SINH", label: "Phỏng vấn tuyển sinh" },
];

export const KNOWLEDGE_LEVELS: { id: KnowledgeLevel; label: string }[] = [
  { id: "chung", label: "Chung" },
  { id: "thcs", label: "THCS" },
  { id: "thpt", label: "THPT" },
  { id: "dai_hoc", label: "Đại học" },
  { id: "thac_si", label: "Thạc sĩ" },
  { id: "tien_si", label: "Tiến sĩ" },
];

export const LABEL_OPTIONS: LabeledOption[] = KNOWLEDGE_LABELS.map((item) => ({ value: item.id, label: item.label }));
export const LEVEL_OPTIONS: LabeledOption[] = KNOWLEDGE_LEVELS.map((item) => ({ value: item.id, label: item.label }));

export const ORIGIN_LABELS: Record<string, string> = {
  manual: "Nhập tay",
  excel: "File Excel",
  "chat-unanswered": "Chat chưa trả lời",
};

export const SORT_OPTIONS: LabeledOption[] = [
  { value: "newest", label: "Mới tạo trước" },
  { value: "oldest", label: "Cũ trước" },
  { value: "entryNo", label: "Theo mã số" },
  { value: "updated", label: "Mới sửa trước" },
];

export const labelName = (id: string | null) => (id ? (KNOWLEDGE_LABELS.find((item) => item.id === id)?.label ?? id) : "—");

export const levelName = (id: string) => KNOWLEDGE_LEVELS.find((item) => item.id === id)?.label ?? id;

/** Dòng đầu của trích dẫn nguồn (thường là tên nhà cung cấp). */
export const firstLine = (text: string | null) => (text ? text.split("\n")[0] : "—");

/** Giống KnowledgeBaseService.IsReady ở backend. */
export function isReady(entry: Pick<KnowledgeEntry, "label" | "levels" | "answer" | "source">): boolean {
  return entry.label !== null && entry.levels.length > 0 && !!entry.answer?.trim() && !!entry.source?.trim();
}

export function entryToRequest(entry: KnowledgeEntry): SaveKnowledgeEntryRequest {
  return { label: entry.label, levels: entry.levels, question: entry.question, answer: entry.answer, source: entry.source };
}

const HISTORY_ACTIONS: Record<string, string> = {
  create: "Tạo",
  edit: "Sửa",
  submit: "Gửi duyệt",
  review: "Duyệt",
  delete: "Xóa",
  restore: "Khôi phục",
  capture: "Lấy từ chat",
  migrate: "Chuyển từ mẫu SLM",
};

export const historyActionLabel = (action: string) => HISTORY_ACTIONS[action] ?? action;

const FIELD_LABELS: Record<string, string> = {
  label: "nhãn",
  levels: "bậc học",
  question: "câu hỏi",
  answer: "câu trả lời",
  source: "nguồn",
};

export const fieldLabels = (fields: string[]) => fields.map((field) => FIELD_LABELS[field] ?? field).join(", ");

// Trạng thái và ngày giờ: dùng lại của mẫu SLM (cùng 5 giá trị)
export { REVIEW_STATUSES, canSubmit, formatDateTime, statusLabel } from "@/lib/slm-sample-format";