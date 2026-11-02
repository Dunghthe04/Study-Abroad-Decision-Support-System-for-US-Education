"use client";

import { useEffect, useId, useState } from "react";
import { SearchIcon } from "lucide-react";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { normalizeSearch } from "@/lib/certification-utils";
import type { MajorOption } from "@/types/profile-flow";

const MAX_SHOWN = 30;

/** E: ô chọn ngành trong chat (widget "major" ở B1.3); chọn xong gửi tên Việt như tin nhắn, advisor đổi sang tên Anh */
export function MajorWidget({ onPick }: { onPick?: (major: string) => void }) {
  const [majors, setMajors] = useState<MajorOption[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const inputId = useId();

  // Lấy ngành trong bảng majors giống bước 1 của form
  useEffect(() => {
    let alive = true;
    profileFlowApi
      .getMetadata()
      .then((m) => alive && setMajors(m.majors ?? []))
      .catch(() => alive && setFailed(true));
    return () => {
      alive = false;
    };
  }, []);

  if (failed) {
    return <p className="mt-2 text-xs text-muted-foreground">Chưa tải được danh sách ngành, bạn gõ tên ngành vào ô chat nhé.</p>;
  }

  // Tìm không dấu theo tên Việt, tên Anh và tên gọi khác ("may tinh", "cntt")
  const q = normalizeSearch(query);
  const matches = (majors ?? []).filter(
    (m) => !q || [m.label, m.value, ...m.aliases].some((s) => normalizeSearch(s).includes(q))
  );

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
          <li key={m.value}>
            <button
              type="button"
              disabled={!onPick}
              onClick={() => onPick?.(m.label)}
              className="w-full cursor-pointer px-3 py-1.5 text-left text-foreground hover:bg-brand-wash/50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {m.label}
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
