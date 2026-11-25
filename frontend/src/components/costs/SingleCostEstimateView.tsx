"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { CostEstimateView } from "@/components/costs/CostEstimateView";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { searchUniversities } from "@/lib/university-api";
import type { UniversityListItem } from "@/types/university";

/** UC-26: Chọn một trường để ước tính chi phí năm đầu và cả khóa 4 năm. */
export function SingleCostEstimateView({ initialCode }: { initialCode: string | null }) {
  const router = useRouter();
  const [code, setCode] = useState<string | null>(initialCode);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UniversityListItem[]>([]);

  useEffect(() => {
    router.replace(code ? `/costs?code=${encodeURIComponent(code)}` : "/costs", { scroll: false });
  }, [code, router]);

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

  const selectSchool = (nextCode: string) => {
    setCode(nextCode);
    setQuery("");
    setResults([]);
  };

  return (
    <div className="space-y-6">
      <div className="space-y-3 rounded-xl border bg-card p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm font-medium">Chọn trường để ước tính chi phí</p>
          {code && (
            <Button type="button" variant="secondary" size="sm" onClick={() => setCode(null)} aria-label={`Bỏ chọn ${code}`}>
              {code} ✕
            </Button>
          )}
        </div>
        <div className="relative">
          <Input
            placeholder="Tìm trường theo tên, mã trường hoặc thành phố..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="Tìm trường để ước tính chi phí"
          />
          {query.trim().length >= 2 && results.length > 0 && (
            <ul className="absolute z-10 mt-1 w-full rounded-lg border bg-popover shadow">
              {results.map((u) => (
                <li key={u.code}>
                  <button
                    type="button"
                    className="w-full px-3 py-2 text-left text-sm hover:bg-muted"
                    onClick={() => selectSchool(u.code)}
                  >
                    {u.name}{u.stateName ? `, ${u.stateName}` : ""}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
        {!code && (
          <p className="text-sm text-muted-foreground">
            Nhập tên trường ở ô tìm kiếm phía trên hoặc mở từ nút «Ước tính chi phí» trong trang chi tiết trường.
          </p>
        )}
      </div>

      {code && <CostEstimateView codes={[code]} />}
    </div>
  );
}
