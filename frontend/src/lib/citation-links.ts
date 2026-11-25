/** Một liên kết nguồn để hiển thị: địa chỉ mở và nhãn ngắn (tên miền). */
export interface CitationLink {
  href: string;
  label: string;
}

function parseHttp(part: string): URL | null {
  try {
    const url = new URL(part);
    return url.protocol === "https:" || url.protocol === "http:" ? url : null;
  } catch {
    return null;
  }
}

/**
 * Các liên kết của một nguồn trích dẫn (UC-37: mục tri thức có nhiều liên kết nguồn thì hiển thị đủ).
 * Người thu thập dán nhiều link cách nhau bằng ";". Chỉ nhận http và https, bỏ link trùng và link hỏng.
 * Nhãn là tên miền (bỏ "www."); nếu hai link cùng tên miền thì thêm số thứ tự để phân biệt.
 */
export function citationLinks(value: string | null | undefined): CitationLink[] {
  const seen = new Set<string>();
  const found: { href: string; host: string }[] = [];
  for (const part of (value ?? "").split(";")) {
    const url = parseHttp(part.trim());
    if (!url || seen.has(url.href)) continue;
    seen.add(url.href);
    found.push({ href: url.href, host: url.hostname.replace(/^www\./, "") });
  }

  const total = new Map<string, number>();
  for (const link of found) total.set(link.host, (total.get(link.host) ?? 0) + 1);

  const order = new Map<string, number>();
  return found.map((link) => {
    const n = (order.get(link.host) ?? 0) + 1;
    order.set(link.host, n);
    return { href: link.href, label: (total.get(link.host) ?? 0) > 1 ? `${link.host} (${n})` : link.host };
  });
}
