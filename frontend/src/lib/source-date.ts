// Chữ hiển thị ngày thu thập thông tin của một nguồn trích dẫn (UC-37).

/**
 * "2026-10-05" → "lấy dữ liệu ngày 05/10/2026". Không có ngày thì nói thẳng là chưa rõ chứ không bỏ trống:
 * người đọc cần biết nguồn này không cho biết thông tin mới hay cũ, thay vì tưởng là mới.
 */
export function sourceDateText(raw: string | null | undefined): string {
  const match = raw ? /^(\d{4})-(\d{2})-(\d{2})/.exec(raw) : null;
  return match ? `lấy dữ liệu ngày ${match[3]}/${match[2]}/${match[1]}` : "chưa rõ ngày lấy dữ liệu";
}
