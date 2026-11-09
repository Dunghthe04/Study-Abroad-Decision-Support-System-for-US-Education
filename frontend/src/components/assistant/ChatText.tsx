"use client";

// Lời trả lời của trợ lý: đoạn văn, gạch đầu dòng và chữ in đậm cho đẹp, dễ đọc.
// Nhận chữ thường (không cần markdown đầy đủ): dòng bắt đầu bằng "-", "•" hay "*" là gạch đầu dòng, "**chữ**" là in đậm,
// dòng ngắn kết thúc bằng dấu ":" là tiêu đề đoạn. Dòng trống ngăn các đoạn.

import { Fragment, type ReactNode } from "react";
import { cn } from "@/lib/utils";

const BULLET = /^\s*[-•*]\s+/;
const BOLD = /\*\*(.+?)\*\*/g;

type Block = { kind: "p"; text: string } | { kind: "lead"; text: string } | { kind: "ul"; items: string[] };

function toBlocks(text: string): Block[] {
  const blocks: Block[] = [];
  for (const raw of text.split("\n")) {
    const line = raw.trim();
    if (!line) continue;
    if (BULLET.test(line)) {
      const item = line.replace(BULLET, "");
      const last = blocks[blocks.length - 1];
      if (last?.kind === "ul") last.items.push(item);
      else blocks.push({ kind: "ul", items: [item] });
    } else if (line.endsWith(":") && line.length <= 90) {
      blocks.push({ kind: "lead", text: line });
    } else {
      blocks.push({ kind: "p", text: line });
    }
  }
  return blocks;
}

function inline(text: string): ReactNode {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(BOLD)) {
    if (match.index > last) parts.push(text.slice(last, match.index));
    parts.push(
      <strong key={match.index} className="font-semibold">
        {match[1]}
      </strong>,
    );
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return parts.map((part, i) => <Fragment key={i}>{part}</Fragment>);
}

export function ChatText({ text, className }: { text: string; className?: string }) {
  const blocks = toBlocks(text);
  if (blocks.length === 0) return null;
  return (
    <div className={cn("space-y-2 leading-relaxed", className)}>
      {blocks.map((block, i) => {
        if (block.kind === "lead") {
          return (
            <p key={i} className="pt-0.5 font-semibold text-brand">
              {inline(block.text)}
            </p>
          );
        }
        if (block.kind === "ul") {
          return (
            <ul key={i} className="list-disc space-y-1.5 pl-5 marker:text-brand/60">
              {block.items.map((item, j) => (
                <li key={j}>{inline(item)}</li>
              ))}
            </ul>
          );
        }
        return <p key={i}>{inline(block.text)}</p>;
      })}
    </div>
  );
}
