// Dựng đường dẫn sang trang /universities từ điều kiện và cách xếp mà chat đã dùng để tìm trường,
// để nút "Xem tất cả" mở đúng danh sách học sinh vừa hỏi.
// Chỉ mang theo điều kiện mà trang /universities đọc được; điều kiện khác bỏ qua (chat đã nói rõ ở dòng tóm tắt).

import type { SchoolSearchData } from "@/types/recommendation";

// Cách xếp của chat -> sortBy của trang. Trang chưa có cách xếp theo học bổng nên dùng thứ hạng.
const SORT_BY: Record<string, string> = {
  quality: "rank",
  lowestCost: "total_cost_asc",
  highestCost: "total_cost_desc",
  lowestTuition: "tuition_asc",
  highestTuition: "tuition_desc",
  easiestAdmission: "acceptance_desc",
  mostSelective: "acceptance_asc",
  mostAid: "rank",
};

// Nói rõ danh sách sẽ xếp thế nào ngay trên nút, để học sinh biết bấm vào sẽ thấy gì
const SORT_LABEL: Record<string, string> = {
  quality: "thứ hạng tốt nhất",
  lowestCost: "tổng chi phí thấp đến cao",
  highestCost: "tổng chi phí cao đến thấp",
  lowestTuition: "học phí thấp đến cao",
  highestTuition: "học phí cao đến thấp",
  easiestAdmission: "dễ trúng tuyển nhất",
  mostSelective: "cạnh tranh nhất",
};

export function universitiesListLabel(data: Pick<SchoolSearchData, "total" | "sort">): string {
  const how = data.sort ? SORT_LABEL[data.sort] : undefined;
  return how ? `Xem toàn bộ ${data.total} trường, xếp ${how}` : `Xem toàn bộ ${data.total} trường`;
}

export function universitiesListHref(data: Pick<SchoolSearchData, "filters" | "sort">): string {
  const qs = new URLSearchParams();
  const f = data.filters ?? {};
  // Trang chỉ lọc một bang: chat hỏi nhiều bang thì không lọc theo bang
  if (Array.isArray(f.states) && f.states.length === 1) qs.set("state", f.states[0]);
  if (f.control) qs.set("control", f.control);
  if (f.major) qs.set("major", f.major);
  if (f.maxCostUsd) qs.set("maxTotalCostUsd", String(f.maxCostUsd));
  if (f.maxIelts) qs.set("maxMinIelts", String(f.maxIelts));
  if (f.ivy) qs.set("isIvy", "true");
  const sortBy = data.sort ? SORT_BY[data.sort] : undefined;
  if (sortBy && sortBy !== "rank") qs.set("sortBy", sortBy);
  const query = qs.toString();
  return query ? `/universities?${query}` : "/universities";
}
