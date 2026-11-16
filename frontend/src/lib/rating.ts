/**
 * Định dạng điểm trung bình thành 1 chữ số thập phân, nửa thì làm tròn lên (4,25 thành 4,3).
 * Dùng chung cho danh sách và trang chi tiết để cùng một trung tâm luôn hiện cùng một số.
 * Backend trả điểm đã làm tròn 2 chữ số, bước nhân 100 trước giúp tránh sai số số thực (4,35 không thành 4,3).
 */
export function formatRating(value: number | string | null | undefined): string {
  const n = Number(value);
  if (value === null || value === undefined || !Number.isFinite(n)) return "";
  return (Math.round(Math.round(n * 100) / 10) / 10).toFixed(1);
}
