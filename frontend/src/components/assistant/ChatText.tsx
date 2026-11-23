"use client";

// Lời trả lời của trợ lý: đoạn văn, danh sách, bảng, link và chữ in đậm cho đẹp, dễ đọc.
// Nhận chữ thường (không cần markdown đầy đủ):
//   dòng bắt đầu bằng "-", "•" hay "*" là gạch đầu dòng; "1." hoặc "1)" là danh sách đánh số (giữ đúng số, kể cả khi bị gạch đầu dòng
//   chen giữa các mục); dòng có dấu " | " là hàng của bảng; "**chữ**" là in đậm; link http(s) thành link bấm được;
//   dòng ngắn kết thúc bằng dấu ":" là tiêu đề đoạn. Dòng trống ngăn các đoạn.

import { Fragment, type ReactNode } from "react";
import { cn } from "@/lib/utils";
import { httpLink } from "@/lib/university-display";

const BULLET = /^\s*[-•*]\s+/;
const NUMBERED = /^\s*(\d{1,3})[.)]\s+(.+)$/;
const BOLD = /\*\*(.+?)\*\*/g;
const URL_RE = /https?:\/\/[^\s<>"']+/g;
// Dấu câu dính cuối link không thuộc về link ("xem https://a.org/x.")
const TRAILING_PUNCTUATION = /[.,;:!?)\]]+$/;

type Block =
  | { kind: "p"; text: string }
  | { kind: "lead"; text: string }
  | { kind: "ul"; items: string[] }
  | { kind: "ol"; start: number; items: string[] }
  | { kind: "table"; rows: string[][] };

export function toBlocks(text: string): Block[] {
  const blocks: Block[] = [];
  for (const raw of text.split("\n")) {
    const line = raw.trim();
    if (!line) continue;
    const last = blocks[blocks.length - 1];
    const numbered = NUMBERED.exec(line);
    if (BULLET.test(line)) {
      const item = line.replace(BULLET, "");
      if (last?.kind === "ul") last.items.push(item);
      else blocks.push({ kind: "ul", items: [item] });
    } else if (numbered) {
      if (last?.kind === "ol") last.items.push(numbered[2]);
      else blocks.push({ kind: "ol", start: Number(numbered[1]), items: [numbered[2]] });
    } else if (line.includes(" | ")) {
      const row = line.split("|").map((cell) => cell.trim());
      if (last?.kind === "table") last.rows.push(row);
      else blocks.push({ kind: "table", rows: [row] });
    } else if (line.endsWith(":") && line.length <= 90) {
      blocks.push({ kind: "lead", text: line });
    } else {
      blocks.push({ kind: "p", text: line });
    }
  }
  return blocks;
}

// Biến link http(s) trong đoạn chữ thành link bấm được; link không hợp lệ giữ nguyên là chữ
function linkify(text: string, keyBase: string): ReactNode[] {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(URL_RE)) {
    const trimmed = match[0].replace(TRAILING_PUNCTUATION, "");
    const link = httpLink(trimmed);
    if (!link) continue;
    if (match.index > last) parts.push(text.slice(last, match.index));
    parts.push(
      <a
        key={`${keyBase}-${match.index}`}
        href={link.href}
        target="_blank"
        rel="noopener noreferrer"
        className="break-all text-brand underline hover:text-brand-ink"
      >
        {trimmed}
      </a>,
    );
    last = match.index + trimmed.length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return parts;
}

function inline(text: string): ReactNode {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(BOLD)) {
    if (match.index > last) parts.push(...linkify(text.slice(last, match.index), `t${last}`));
    parts.push(
      <strong key={match.index} className="font-semibold">
        {match[1]}
      </strong>,
    );
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(...linkify(text.slice(last), `t${last}`));
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
        if (block.kind === "ol") {
          // start giữ đúng số thứ tự khi gạch đầu dòng (hậu quả, cách xử lý...) chen giữa các mục
          return (
            <ol key={i} start={block.start} className="list-decimal space-y-1.5 pl-5 marker:font-semibold marker:text-brand">
              {block.items.map((item, j) => (
                <li key={j}>{inline(item)}</li>
              ))}
            </ol>
          );
        }
        if (block.kind === "table") {
          // Cuộn ngang khi bảng rộng hơn khung chat; hàng đầu là tiêu đề khi bảng có từ hai hàng
          const hasHeader = block.rows.length > 1;
          const head = hasHeader ? block.rows[0] : null;
          const body = hasHeader ? block.rows.slice(1) : block.rows;
          return (
            <div key={i} className="overflow-x-auto rounded-lg border border-line">
              <table className="w-full min-w-max border-collapse text-left text-[0.92em]">
                {head && (
                  <thead className="bg-surface-2">
                    <tr>
                      {head.map((cell, j) => (
                        <th key={j} className="border-b border-line px-2.5 py-1.5 font-semibold">
                          {inline(cell)}
                        </th>
                      ))}
                    </tr>
                  </thead>
                )}
                <tbody>
                  {body.map((row, r) => (
                    <tr key={r} className="border-b border-line last:border-0">
                      {row.map((cell, c) => (
                        <td key={c} className="px-2.5 py-1.5 align-top">
                          {inline(cell)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          );
        }
        return <p key={i}>{inline(block.text)}</p>;
      })}
    </div>
  );
}
