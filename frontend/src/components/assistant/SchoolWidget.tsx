"use client";

import { useEffect, useId, useState } from "react";
import { SearchIcon } from "lucide-react";
import { normalizeSearch } from "@/lib/certification-utils";
import { recommendationApi } from "@/lib/recommendation-api";
import type { SchoolOption } from "@/types/recommendation";

const MAX_SHOWN = 8;

// Danh sách trường tải một lần cho cả phiên rồi dùng lại: học sinh có thể mở ô chọn nhiều lần
let cache: Promise<SchoolOption[]> | null = null;
function loadSchools(): Promise<SchoolOption[]> {
  cache ??= recommendationApi.schoolOptions().catch((err: unknown) => {
    cache = null; // lỗi thì lần sau thử lại
    throw err;
  });
  return cache;
}

const TARGET_TEXT: Record<string, string> = { reach: "Khó vào", match: "Vừa sức", safety: "An toàn hơn" };

// Câu hỏi gửi lại khi chọn một trường. Cùng khuôn với nút chọn trường của backend (ChatGoalSeek.Question),
// nên advisor đọc như mọi câu hỏi khác và không cần nhớ trạng thái "đang chờ chọn trường".
function questionFor(name: string, target: unknown): string {
  const group = typeof target === "string" ? TARGET_TEXT[target] : undefined;
  return group ? `Cần gì để vào ${name} ở nhóm ${group}?` : `Cần gì để vào ${name}?`;
}

/** Ô chọn trường trong chat (widget "school"): gõ đến đâu danh sách thu hẹp đến đó; chọn xong gửi cả câu hỏi như tin nhắn */
export function SchoolWidget({
  data,
  onPick,
}: {
  data?: Record<string, unknown> | null;
  onPick?: (question: string) => void;
}) {
  const [schools, setSchools] = useState<SchoolOption[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const inputId = useId();

  useEffect(() => {
    let alive = true;
    loadSchools()
      .then((list) => alive && setSchools(list))
      .catch(() => alive && setFailed(true));
    return () => {
      alive = false;
    };
  }, []);

  if (failed) {
    return <p className="mt-2 text-xs text-muted-foreground">Chưa tải được danh sách trường, bạn gõ tên trường vào ô chat nhé.</p>;
  }

  // Tìm không dấu, không phân biệt hoa thường, theo từng từ gõ vào ("bos uni" khớp "Boston University")
  const words = normalizeSearch(query).split(" ").filter(Boolean);
  const matches = (schools ?? []).filter((s) => {
    const name = normalizeSearch(s.name);
    return words.every((w) => name.includes(w));
  });

  return (
    <div className="mt-2.5 overflow-hidden rounded-xl border border-line bg-surface">
      <label htmlFor={inputId} className="flex items-center gap-2 border-b border-line px-3 py-2">
        <SearchIcon className="size-4 shrink-0 text-muted-foreground" />
        <input
          id={inputId}
          value={query}
          disabled={!onPick}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={schools ? `Tìm trong ${schools.length} trường…` : "Đang tải danh sách trường…"}
          className="min-w-0 flex-1 bg-transparent text-[13px] outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed"
        />
      </label>
      <ul className="max-h-48 overflow-y-auto py-1 text-[13px]">
        {matches.slice(0, MAX_SHOWN).map((s) => (
          <li key={s.code}>
            <button
              type="button"
              disabled={!onPick}
              onClick={() => onPick?.(questionFor(s.name, data?.target))}
              className="flex w-full cursor-pointer items-baseline justify-between gap-3 px-3 py-1.5 text-left text-foreground hover:bg-brand-wash/50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span>{s.name}</span>
              {s.state && <span className="shrink-0 text-xs text-muted-foreground">{s.state}</span>}
            </button>
          </li>
        ))}
        {schools && matches.length === 0 && (
          <li className="px-3 py-1.5 text-muted-foreground">Không thấy trường này, bạn thử từ khóa khác.</li>
        )}
        {matches.length > MAX_SHOWN && (
          <li className="px-3 py-1.5 text-xs text-muted-foreground">Gõ thêm để thu hẹp danh sách…</li>
        )}
      </ul>
    </div>
  );
}
