import type { PickedSchool, RecommendationItems, ScoredSchool } from "@/types/recommendation";

// Ba nhóm chính của danh sách gợi ý; mã nhóm của từng trường đã chấm (ScoredSchool.g2) tương ứng.
export type MainGroup = "reach" | "match" | "safety";

export const GROUP_CODE: Record<MainGroup, string> = { reach: "R", match: "M", safety: "S" };

// Backend chỉ giữ 4 trường điểm phù hợp cao nhất mỗi nhóm. Các trường còn lại của nhóm (đã chấm, trong ngân sách,
// cùng nhóm) lấy từ danh sách đã chấm, xếp theo đúng thứ tự backend dùng: điểm phù hợp giảm dần, cùng điểm thì theo mã trường.
export function restOfGroup(items: RecommendationItems, group: MainGroup): { picked: PickedSchool; school: ScoredSchool }[] {
  const shown = new Set(items[group].map((p) => p.code));
  return items.scored
    .filter((s) => s.outcome === "Keep" && s.g2 === GROUP_CODE[group] && !shown.has(s.code))
    .sort((a, b) => (b.fit ?? 0) - (a.fit ?? 0) || (a.code < b.code ? -1 : a.code > b.code ? 1 : 0))
    .map((school) => ({ picked: { code: school.code, x: school.fit ?? null, why: school.why ?? [] }, school }));
}

// Nhóm chính mà trường đang nằm (chỉ trường trong ngân sách); null nếu thuộc khối học bổng, thiếu dữ liệu hoặc không có
export function mainGroupOf(school: ScoredSchool | undefined): MainGroup | null {
  if (!school || school.outcome !== "Keep") return null;
  return (Object.keys(GROUP_CODE) as MainGroup[]).find((g) => GROUP_CODE[g] === school.g2) ?? null;
}
