"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { CostEstimateView } from "@/components/costs/CostEstimateView";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { searchUniversities } from "@/lib/university-api";
import type { UniversityListItem } from "@/types/university";

const MAX = 4;

/** UC-27: chọn 2–4 trường, danh sách mã nằm trên URL (?codes=A,B) để chia sẻ được. */
export function CostCompareView({ initialCodes }: { initialCodes: string[] }) {
  const router = useRouter();
  const [codes, setCodes] = useState(initialCodes);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UniversityListItem[]>([]);

  useEffect(() => {
    router.replace(codes.length ? `/universities/compare?codes=${codes.join(",")}` : "/universities/compare", { scroll: false });
  }, [codes, router]);

  useEffect(() => {
    if (query.trim().length < 2) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      searchUniversities({ search: query, pageSize: 6, page: 1 }, controller.signal)
        .then((r) => setResults(r.items))
        .catch(() => { if (!controller.signal.aborted) setResults([]); });
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [query]);

  const add = (code: string) => {
    setCodes((c) => (c.includes(code) || c.length >= MAX ? c : [...c, code]));
    setQuery("");
  };

  return (
    <div className="space-y-6">
      <div className="space-y-3 rounded-xl border bg-card p-4">
        <div className="flex flex-wrap gap-2">
          {codes.map((c) => (
            <Button key={c} type="button" variant="secondary" size="sm" onClick={() => setCodes(codes.filter((x) => x !== c))} aria-label={`Bỏ ${c}`}>
              {c} ✕
            </Button>
          ))}
          {codes.length === 0 && <span className="text-sm text-muted-foreground">Chưa chọn trường nào.</span>}
        </div>
        {codes.length < MAX ? (
          <div className="relative">
            <Input placeholder={`Tìm trường để thêm (tối đa ${MAX})`} value={query} onChange={(e) => setQuery(e.target.value)} />
            {query.trim().length >= 2 && results.length > 0 && (
              <ul className="absolute z-10 mt-1 w-full rounded-lg border bg-popover shadow">
                {results.map((u) => (
                  <li key={u.code}>
                    <button type="button" className="w-full px-3 py-2 text-left text-sm hover:bg-muted disabled:opacity-50" disabled={codes.includes(u.code)} onClick={() => add(u.code)}>
                      {u.name}{u.stateName ? `, ${u.stateName}` : ""}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">Đã chọn tối đa {MAX} trường.</p>
        )}
        {codes.length === 1 && <p className="text-sm text-muted-foreground">Chọn thêm ít nhất một trường nữa để so sánh.</p>}
      </div>
      <CostEstimateView codes={codes} />
    </div>
  );
}
