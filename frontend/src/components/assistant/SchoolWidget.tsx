"use client";

import { useEffect, useId, useState } from "react";
import { SearchIcon } from "lucide-react";
import { normalizeSearch } from "@/lib/certification-utils";
import { recommendationApi } from "@/lib/recommendation-api";
import { useStateNames } from "@/lib/us-state-names";
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
function questionFor(name: string, target: unknown, kind: unknown): string {
  // kind "explain": nút "Giải thích một trường". Cùng chữ với nút chọn trường của skill giải thích nên advisor nhận đúng ý
  if (kind === "explain") return `Giải thích giúp mình trường ${name}`;
  const group = typeof target === "string" ? TARGET_TEXT[target] : undefined;
  return group ? `Cần gì để vào ${name} ở nhóm ${group}?` : `Cần gì để vào ${name}?`;
}

/**
 * Ô chọn trường trong chat (widget "school"): gõ đến đâu danh sách thu hẹp đến đó; chọn xong gửi cả câu hỏi như tin nhắn.
 * data.kind "explain": chọn xong giải thích trường (gửi hành động theo mã trường, không qua SLM). data.codes: chỉ cho chọn trong các trường này.
 */
export function SchoolWidget({
  data,
  onPick,
}: {
  data?: Record<string, unknown> | null;
  onPick?: (question: string, schoolCode: string) => void;
}) {
  const [schools, setSchools] = useState<SchoolOption[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const [showAll, setShowAll] = useState(false);
  const stateName = useStateNames();
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
  // data.codes: chỉ cho chọn trong các trường này (vd các trường vừa đổi nhóm sau what-if)
  const only = Array.isArray(data?.codes) ? new Set(data.codes.map(String)) : null;
  const suggested = Array.isArray(data?.suggested) ? data.suggested.map(String) : [];
  const narrowed = only !== null && !showAll;
  const pool = (schools ?? []).filter((s) => !narrowed || only.has(s.code));
  // Chưa gõ gì mà có trường gợi ý sẵn (đầu danh sách của học sinh): hiện các trường đó làm nút bấm, đúng thứ tự gợi ý
  const suggestedSchools = suggested.map((code) => pool.find((s) => s.code === code)).filter((s): s is SchoolOption => Boolean(s));
  const showSuggested = words.length === 0 && suggestedSchools.length > 0 && !narrowed;
  const matches = showSuggested
    ? suggestedSchools
    : pool.filter((s) => {
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
          placeholder={
            schools
              ? narrowed
                ? `Tìm trong ${pool.length} trường vừa đổi…`
                : `Tìm trong ${pool.length} trường…`
              : "Đang tải danh sách trường…"
          }
          className="min-w-0 flex-1 bg-transparent text-[13px] outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed"
        />
      </label>
      <ul className="max-h-48 overflow-y-auto py-1 text-[13px]">
        {showSuggested && matches.length > 0 && (
          <li className="px-3 pb-1 pt-1.5 text-xs text-muted-foreground">Trong danh sách của bạn:</li>
        )}
        {matches.slice(0, MAX_SHOWN).map((s) => (
          <li key={s.code}>
            <button
              type="button"
              disabled={!onPick}
              onClick={() => onPick?.(questionFor(s.name, data?.target, data?.kind), s.code)}
              className="flex w-full cursor-pointer items-baseline justify-between gap-3 px-3 py-1.5 text-left text-foreground hover:bg-brand-wash/50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span>{s.name}</span>
              {s.state && <span className="shrink-0 text-xs text-muted-foreground">{stateName(s.state)}</span>}
            </button>
          </li>
        ))}
        {schools && matches.length === 0 && (
          <li className="px-3 py-1.5 text-muted-foreground">
            {narrowed ? "Không có trường này trong các trường vừa đổi." : "Không thấy trường này, bạn thử từ khóa khác."}
          </li>
        )}
        {narrowed && (
          <li className="px-3 py-1.5">
            <button
              type="button"
              onClick={() => setShowAll(true)}
              className="cursor-pointer text-xs text-brand underline-offset-2 hover:underline"
            >
              Tìm trong tất cả trường
            </button>
          </li>
        )}
        {showSuggested && matches.length > 0 && (
          <li className="px-3 py-1.5 text-xs text-muted-foreground">Gõ tên để tìm trường khác…</li>
        )}
        {matches.length > MAX_SHOWN && (
          <li className="px-3 py-1.5 text-xs text-muted-foreground">Gõ thêm để thu hẹp danh sách…</li>
        )}
      </ul>
    </div>
  );
}
