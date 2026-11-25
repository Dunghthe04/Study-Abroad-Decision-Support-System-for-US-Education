import type { PickBlockName, PickedSchool, RecommendationItems, ScoredSchool } from "@/types/recommendation";

// Ba nhóm chính của danh sách gợi ý; mã nhóm của từng trường đã chấm (ScoredSchool.g2) tương ứng.
export type MainGroup = "reach" | "match" | "safety";
export type BlockGroup = PickBlockName | "other";

export const GROUP_CODE: Record<MainGroup, string> = { reach: "R", match: "M", safety: "S" };

const byFitDesc = (a: ScoredSchool, b: ScoredSchool) =>
  (b.fit ?? 0) - (a.fit ?? 0) || (a.code < b.code ? -1 : a.code > b.code ? 1 : 0);

// Backend chỉ giữ 4 trường điểm phù hợp cao nhất mỗi khối. Các trường còn lại lấy từ danh sách đã chấm
// theo đúng thứ tự backend dùng: điểm phù hợp giảm dần, cùng điểm thì theo mã trường.
export function restOfGroup(items: RecommendationItems, group: BlockGroup): { picked: PickedSchool; school: ScoredSchool }[] {
  const shown = new Set((group === "other" ? items.other.slice(0, 4) : items[group]).map((p) => p.code));
  const matches = (s: ScoredSchool) => {
    if (group === "aid") return s.outcome === "AidBlock";
    if (group === "other") return s.outcome === "Keep" && s.g2 === "NA";
    return s.outcome === "Keep" && s.g2 === GROUP_CODE[group];
  };
  return items.scored
    .filter((s) => matches(s) && !shown.has(s.code))
    .sort(byFitDesc)
    .map((school) => ({ picked: { code: school.code, x: school.fit ?? null, why: school.why ?? [] }, school }));
}

// Khối mà trường đang nằm (gồm cả khối cần học bổng lớn và chưa xếp được nhóm)
export function blockOf(school: ScoredSchool | undefined): BlockGroup | null {
  if (!school) return null;
  if (school.outcome === "AidBlock") return "aid";
  if (school.outcome !== "Keep") return null;
  if (school.g2 === "NA") return "other";
  return (Object.keys(GROUP_CODE) as MainGroup[]).find((g) => GROUP_CODE[g] === school.g2) ?? null;
}

// Nhóm chính mà trường đang nằm (chỉ trường trong ngân sách); null nếu thuộc khối học bổng, thiếu dữ liệu hoặc không có
export function mainGroupOf(school: ScoredSchool | undefined): MainGroup | null {
  if (!school || school.outcome !== "Keep") return null;
  return (Object.keys(GROUP_CODE) as MainGroup[]).find((g) => GROUP_CODE[g] === school.g2) ?? null;
}
