export const COMPARE_MIN = 2;
export const COMPARE_MAX = 3;

/** Đọc `?ids=a,b,c` thành danh sách id không trùng, tối đa COMPARE_MAX, giữ thứ tự. */
export function parseCompareIds(raw: string | string[] | undefined): string[] {
  const text = Array.isArray(raw) ? raw.join(",") : (raw ?? "");
  const seen = new Set<string>();
  for (const part of text.split(",")) {
    const id = part.trim();
    if (id) seen.add(id);
  }
  return [...seen].slice(0, COMPARE_MAX);
}

export function compareHref(ids: string[]): string {
  return ids.length ? `/centers/compare?ids=${ids.join(",")}` : "/centers/compare";
}
