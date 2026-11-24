import type { LabeledOption } from "@/lib/admin-university-display";
import type { KnowledgeDocType } from "@/types/knowledge-base";

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

export const KNOWLEDGE_DOC_TYPES: { id: KnowledgeDocType; label: string }[] = [
  { id: "faq", label: "Hỏi đáp" },
  { id: "glossary", label: "Thuật ngữ" },
  { id: "proc_overview", label: "Thủ tục: tổng quan" },
  { id: "proc_prepare", label: "Thủ tục: chuẩn bị" },
  { id: "proc_steps", label: "Thủ tục: các bước" },
  { id: "proc_checklist", label: "Thủ tục: danh sách kiểm tra" },
  { id: "proc_mistakes", label: "Thủ tục: lỗi hay gặp" },
  { id: "timeline", label: "Mốc thời gian" },
  { id: "compare", label: "So sánh" },
  { id: "situation", label: "Tình huống" },
  { id: "data_table", label: "Bảng số liệu" },
  { id: "resource", label: "Công cụ và link" },
  { id: "notice", label: "Quy định và cảnh báo" },
];

export const LABEL_OPTIONS: LabeledOption[] = KNOWLEDGE_LABELS.map((item) => ({ value: item.id, label: item.label }));
export const DOC_TYPE_OPTIONS: LabeledOption[] = KNOWLEDGE_DOC_TYPES.map((item) => ({ value: item.id, label: item.label }));

export const SORT_OPTIONS: LabeledOption[] = [
  { value: "newest", label: "Mới tạo trước" },
  { value: "oldest", label: "Cũ trước" },
  { value: "entryNo", label: "Theo mã số" },
  { value: "updated", label: "Mới sửa trước" },
];

export const labelName = (id: string) => KNOWLEDGE_LABELS.find((item) => item.id === id)?.label ?? id;

export const docTypeName = (id: string) => KNOWLEDGE_DOC_TYPES.find((item) => item.id === id)?.label ?? id;

export const isProcedure = (docType: string) => docType.startsWith("proc_");

/** Dòng đầu của trích dẫn nguồn (thường là tên nhà cung cấp). */
export const firstLine = (text: string | null) => (text ? text.split("\n")[0] : "—");

export { formatDateTime } from "@/lib/slm-sample-format";
