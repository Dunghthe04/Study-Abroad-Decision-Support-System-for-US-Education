import { apiFetch } from "@/lib/api";
import type {
  CreateReviewBatchRequest,
  ReviewBatchCreated,
  ReviewBatchListItem,
  ReviewItemDetail,
  ReviewItemList,
  ReviewVerdict,
  SaveReviewVerdictRequest,
} from "@/types/review-batch";

const base = "/api/v1/admin/review-batches";

// Đợt duyệt mẫu cho admin. Danh sách đợt và danh sách câu không có nội dung chat.
export const adminReviewBatchApi = {
  /** Tạo đợt: bốc ngẫu nhiên có seed, chia nhóm theo skill. Lỗi 400 kèm lý do (tên, số câu, khoảng ngày, không có câu nào để bốc). */
  create: (request: CreateReviewBatchRequest) =>
    apiFetch<ReviewBatchCreated>(base, { method: "POST", body: JSON.stringify(request) }),

  /** Các đợt của tôi (đã tạo hoặc được giao duyệt), mới nhất trước, kèm tiến độ. */
  list: () => apiFetch<ReviewBatchListItem[]>(base),

  /** Các câu của một đợt theo thứ tự đọc. 404 nếu đợt không có hoặc tôi không được giao duyệt. */
  items: (batchId: string) => apiFetch<ReviewItemList>(`${base}/${encodeURIComponent(batchId)}/items`),

  /**
   * Một câu để chấm, CÓ nội dung chat. Mỗi lần gọi được ghi nhật ký người xem, nên chỉ gọi khi người duyệt chủ động mở câu đó:
   * không tải trước các câu kế tiếp, không gọi lại khi không cần.
   */
  item: (batchId: string, itemId: string) =>
    apiFetch<ReviewItemDetail>(`${base}/${encodeURIComponent(batchId)}/items/${encodeURIComponent(itemId)}`),

  /** Lưu kết luận của tôi cho một câu (ghi đè nếu đã chấm). 400 nếu sai quy tắc. */
  saveVerdict: (batchId: string, itemId: string, request: SaveReviewVerdictRequest) =>
    apiFetch<ReviewVerdict>(`${base}/${encodeURIComponent(batchId)}/items/${encodeURIComponent(itemId)}/verdict`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),
};
