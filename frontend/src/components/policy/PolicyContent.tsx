import type { ReactNode } from "react";
import { parseInline, parsePolicyMarkdown } from "@/lib/policy-markdown";

function Inline({ text }: { text: string }): ReactNode {
  return parseInline(text).map((node, i) => {
    switch (node.type) {
      case "bold":
        return <strong key={i} className="font-semibold text-ink">{node.text}</strong>;
      case "italic":
        return <em key={i}>{node.text}</em>;
      case "link": {
        const external = /^(https?:|mailto:)/i.test(node.href);
        return (
          <a
            key={i}
            href={node.href}
            className="text-brand underline underline-offset-2"
            {...(external ? { target: "_blank", rel: "noopener noreferrer" } : {})}
          >
            {node.text}
          </a>
        );
      }
      default:
        return <span key={i}>{node.text}</span>;
    }
  });
}

/**
 * Toàn văn chính sách (Markdown đơn giản do admin soạn ở UC-103) thành tiêu đề, đoạn, danh sách.
 * Chữ nào cũng đi qua React nên HTML thô trong nội dung chỉ hiện thành chữ, không chạy.
 */
export function PolicyContent({ content }: { content: string }) {
  const blocks = parsePolicyMarkdown(content);
  return (
    <div className="space-y-4 text-sm leading-relaxed text-ink-2">
      {blocks.map((block, i) => {
        switch (block.type) {
          case "heading":
            return block.level === 2 ? (
              <h2 key={i} className="pt-2 text-base font-semibold text-ink">{block.text}</h2>
            ) : (
              <h3 key={i} className="text-sm font-semibold text-ink">{block.text}</h3>
            );
          case "list": {
            const Tag = block.ordered ? "ol" : "ul";
            return (
              <Tag key={i} className={`space-y-1 pl-5 ${block.ordered ? "list-decimal" : "list-disc"}`}>
                {block.items.map((item, j) => (
                  <li key={j}><Inline text={item} /></li>
                ))}
              </Tag>
            );
          }
          default:
            return <p key={i}><Inline text={block.text} /></p>;
        }
      })}
    </div>
  );
}
