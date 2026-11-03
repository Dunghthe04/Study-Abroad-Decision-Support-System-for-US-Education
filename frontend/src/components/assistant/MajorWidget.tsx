"use client";

import { useEffect, useId, useState } from "react";
import { SearchIcon } from "lucide-react";
import { profileFlowApi } from "@/lib/profile-flow-api";

const MAX_SHOWN = 30;

/** E: ô chọn ngành trong chat (widget "major" khi bị chặn thiếu ngành ở B4); chọn xong gửi tên ngành như tin nhắn */
export function MajorWidget({ onPick }: { onPick?: (major: string) => void }) {
  const [majors, setMajors] = useState<string[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const inputId = useId();

  // Lấy 366 ngành giống bước 1 của form
  useEffect(() => {
    let alive = true;
    profileFlowApi
      .getMetadata()
      .then((m) => alive && setMajors(m.offeringMajors ?? []))
      .catch(() => alive && setFailed(true));
    return () => {
      alive = false;
    };
  }, []);

  if (failed) {
    return <p className="mt-2 text-xs text-muted-foreground">Chưa tải được danh sách ngành, bạn gõ tên ngành vào ô chat nhé.</p>;
  }

  const q = query.trim().toLowerCase();
  const matches = (majors ?? []).filter((m) => !q || m.toLowerCase().includes(q));

  return (
    <div className="mt-2.5 overflow-hidden rounded-xl border border-line bg-surface">
      <label htmlFor={inputId} className="flex items-center gap-2 border-b border-line px-3 py-2">
        <SearchIcon className="size-4 shrink-0 text-muted-foreground" />
        <input
          id={inputId}
          value={query}
          disabled={!onPick}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={majors ? `Tìm trong ${majors.length} ngành…` : "Đang tải danh sách ngành…"}
          className="min-w-0 flex-1 bg-transparent text-[13px] outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed"
        />
      </label>
      <ul className="max-h-48 overflow-y-auto py-1 text-[13px]">
        {matches.slice(0, MAX_SHOWN).map((m) => (
          <li key={m}>
            <button
              type="button"
              disabled={!onPick}
              onClick={() => onPick?.(m)}
              className="w-full cursor-pointer px-3 py-1.5 text-left text-foreground hover:bg-brand-wash/50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {m}
            </button>
          </li>
        ))}
        {majors && matches.length === 0 && (
          <li className="px-3 py-1.5 text-muted-foreground">Không thấy ngành này, bạn thử từ khóa khác.</li>
        )}
        {matches.length > MAX_SHOWN && (
          <li className="px-3 py-1.5 text-xs text-muted-foreground">Gõ thêm để thu hẹp danh sách…</li>
        )}
      </ul>
    </div>
  );
}
