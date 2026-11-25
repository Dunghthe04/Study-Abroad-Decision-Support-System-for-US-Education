"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { CostEstimateView } from "@/components/costs/CostEstimateView";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { searchUniversities } from "@/lib/university-api";
import type { UniversityListItem } from "@/types/university";

const MIN = 2;
const MAX = 4;

/** UC-27: chọn 2–4 trường, danh sách mã nằm trên URL (?codes=A,B) để chia sẻ được. */
export function CostCompareView({ initialCodes }: { initialCodes: string[] }) {
  const router = useRouter();
  const [codes, setCodes] = useState(initialCodes);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UniversityListItem[]>([]);
  const [limitError, setLimitError] = useState<string | null>(null);

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
    if (codes.includes(code)) return;
    if (codes.length >= MAX) {
      setLimitError(`Chỉ được chọn tối đa ${MAX} trường để so sánh.`);
      return;
    }
    setLimitError(null);
    setCodes((c) => [...c, code]);
    setQuery("");
  };

  const remove = (code: string) => {
    setLimitError(null);
    setCodes((c) => c.filter((x) => x !== code));
  };

  const tooFew = codes.length > 0 && codes.length < MIN;
  const tooMany = codes.length > MAX;
  const canCompare = codes.length >= MIN && codes.length <= MAX;

  return (
    <div className="space-y-6">
      <div className="space-y-3 rounded-xl border bg-card p-4">
        <div className="flex flex-wrap gap-2">
          {codes.map((c) => (
            <Button key={c} type="button" variant="secondary" size="sm" onClick={() => remove(c)} aria-label={`Bỏ ${c}`}>
              {c} ✕
            </Button>
          ))}
          {codes.length === 0 && <span className="text-sm text-muted-foreground">Chưa chọn trường nào. Chọn từ {MIN} đến {MAX} trường để so sánh.</span>}
        </div>
        <div className="relative">
          <Input placeholder={`Tìm trường để thêm (từ ${MIN} đến ${MAX} trường)`} value={query} onChange={(e) => setQuery(e.target.value)} />
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
        {tooFew && (
          <p role="alert" className="text-sm text-destructive">
            Cần chọn từ {MIN} đến {MAX} trường để so sánh chi phí. Hãy chọn thêm ít nhất {MIN - codes.length} trường nữa.
          </p>
        )}
        {tooMany && (
          <p role="alert" className="text-sm text-destructive">
            Chỉ được so sánh tối đa {MAX} trường (đang chọn {codes.length} trường). Vui lòng bỏ bớt {codes.length - MAX} trường.
          </p>
        )}
        {!tooMany && limitError && (
          <p role="alert" className="text-sm text-destructive">{limitError}</p>
        )}
      </div>
      {canCompare && <CostEstimateView codes={codes} />}
    </div>
  );
}
