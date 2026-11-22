// Phân tích Markdown đơn giản của văn bản chính sách (UC-103) thành khối, để giao diện tự dựng phần tử.
// Cố ý KHÔNG chuyển ra HTML: chữ nào cũng đi qua React nên không có đường chèn mã. Chỉ hỗ trợ những gì admin cần khi soạn điều khoản:
//   ## Tiêu đề mục · ### Tiêu đề nhỏ · "- " hoặc "* " gạch đầu dòng · "1. " danh sách đánh số · đoạn văn · **in đậm** · *nghiêng* · [chữ](liên kết)
// Mọi thứ khác (thẻ HTML, ảnh, bảng...) hiện nguyên chữ, không có tác dụng.

export type InlineNode =
  | { type: "text"; text: string }
  | { type: "bold"; text: string }
  | { type: "italic"; text: string }
  | { type: "link"; text: string; href: string };

export type PolicyBlock =
  | { type: "heading"; level: 2 | 3; text: string }
  | { type: "paragraph"; text: string }
  | { type: "list"; ordered: boolean; items: string[] };

const BULLET = /^[-*]\s+(.*)$/;
const NUMBERED = /^\d+[.)]\s+(.*)$/;

/** Tách văn bản thành các khối. Dòng trống ngăn cách khối; các dòng liền nhau của một đoạn văn được nối bằng dấu cách. */
export function parsePolicyMarkdown(source: string): PolicyBlock[] {
  const lines = source.replace(/\r\n?/g, "\n").split("\n");
  const blocks: PolicyBlock[] = [];
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;

  const flushParagraph = () => {
    if (paragraph.length) blocks.push({ type: "paragraph", text: paragraph.join(" ") });
    paragraph = [];
  };
  const flushList = () => {
    if (list) blocks.push({ type: "list", ordered: list.ordered, items: list.items });
    list = null;
  };

  for (const raw of lines) {
    const line = raw.trim();
    if (line === "") {
      flushParagraph();
      flushList();
      continue;
    }
    const heading = /^(#{1,3})\s+(.*)$/.exec(line);
    if (heading) {
      flushParagraph();
      flushList();
      // Trang đã có tiêu đề cấp 1 là tên văn bản, nên "#" và "##" đều là tiêu đề mục
      blocks.push({ type: "heading", level: heading[1].length === 3 ? 3 : 2, text: heading[2].trim() });
      continue;
    }
    const bullet = BULLET.exec(line);
    const numbered = bullet ? null : NUMBERED.exec(line);
    if (bullet || numbered) {
      flushParagraph();
      const ordered = numbered !== null;
      if (list && list.ordered !== ordered) flushList();
      list ??= { ordered, items: [] };
      list.items.push((bullet ?? numbered)![1].trim());
      continue;
    }
    flushList();
    paragraph.push(line);
  }
  flushParagraph();
  flushList();
  return blocks;
}

/** Liên kết an toàn: chỉ https, http, mailto và đường dẫn trong site (một dấu "/" đầu). Không nhận javascript:, data: hay //. */
export function isSafeHref(href: string): boolean {
  return /^(https?:\/\/|mailto:)/i.test(href) || (href.startsWith("/") && !href.startsWith("//"));
}

const INLINE = /(\*\*[^*]+\*\*|\*[^*\s][^*]*\*|\[[^\]]+\]\([^)\s]+\))/;

/** Tách một đoạn chữ thành chữ thường, in đậm, nghiêng, liên kết. Liên kết không an toàn chỉ còn là chữ. */
export function parseInline(text: string): InlineNode[] {
  const nodes: InlineNode[] = [];
  for (const part of text.split(INLINE)) {
    if (part === "") continue;
    const bold = /^\*\*([^*]+)\*\*$/.exec(part);
    if (bold) { nodes.push({ type: "bold", text: bold[1] }); continue; }
    const italic = /^\*([^*\s][^*]*)\*$/.exec(part);
    if (italic) { nodes.push({ type: "italic", text: italic[1] }); continue; }
    const link = /^\[([^\]]+)\]\(([^)\s]+)\)$/.exec(part);
    if (link) {
      nodes.push(isSafeHref(link[2]) ? { type: "link", text: link[1], href: link[2] } : { type: "text", text: link[1] });
      continue;
    }
    nodes.push({ type: "text", text: part });
  }
  return nodes;
}
