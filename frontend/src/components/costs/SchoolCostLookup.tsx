"use client";

import { X } from "lucide-react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { CostEstimateView } from "@/components/costs/CostEstimateView";
import { SchoolSearch } from "@/components/costs/SchoolSearch";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const MAX = 4;

interface Picked {
  name: string;
  place?: string;
}

/**
 * UC-26 + UC-27: một ô chọn trường cho cả ước tính và so sánh.
 * Một trường thì ra ước tính đầy đủ (năm đầu, cả khóa, ngân sách); từ hai đến bốn trường thì đặt cạnh nhau.
 * Danh sách mã nằm trên URL (?codes=A,B) để chia sẻ được.
 */
export function SchoolCostLookup({ initialCodes }: { initialCodes: string[] }) {
  const router = useRouter();
  const searchRef = useRef<HTMLInputElement>(null);
  const [codes, setCodes] = useState(initialCodes);
  const [names, setNames] = useState<Record<string, Picked>>({});
  const [limitError, setLimitError] = useState<string | null>(null);
  const first = useRef(true);

  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    router.replace(codes.length ? `/costs?codes=${codes.map(encodeURIComponent).join(",")}` : "/costs", { scroll: false });
  }, [codes, router]);

  const add = (code: string, picked: Picked) => {
    if (codes.includes(code)) return;
    if (codes.length >= MAX) {
      setLimitError(`Chỉ được chọn tối đa ${MAX} trường để so sánh.`);
      return;
    }
    setLimitError(null);
    setNames((n) => ({ ...n, [code]: picked }));
    setCodes((c) => [...c, code]);
  };

  const remove = (code: string) => {
    setLimitError(null);
    setCodes((c) => c.filter((x) => x !== code));
  };

  const full = codes.length >= MAX;
  const tooMany = codes.length > MAX;
  const canCompare = codes.length >= 2 && !tooMany;

  // Kết quả nằm ngay dưới; trên màn hình thấp thì cuộn tới đầu phần kết quả
  const showResults = () => {
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    document.getElementById("cost-results")?.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "start" });
  };

  return (
    <div className="space-y-8" data-empty={codes.length === 0 ? "" : undefined}>
      <div className="mx-auto w-full max-w-3xl space-y-4">
        <SchoolSearch
          ref={searchRef}
          label="Tìm trường để tra cứu chi phí"
          disabled={full}
          placeholder={
            full
              ? `Đã đủ ${MAX} trường, bỏ bớt để thêm trường khác`
              : codes.length === 0
                ? undefined
                : `Thêm trường để so sánh (${codes.length}/${MAX})`
          }
          pickedCodes={codes}
          action={
            canCompare ? (
              <Button type="button" onClick={showResults} className="h-10 rounded-xl px-5 text-sm font-semibold">
                So sánh
              </Button>
            ) : undefined
          }
          onPick={(u) => add(u.code, { name: u.name, place: [u.city, u.stateName].filter(Boolean).join(", ") })}
        />

        {codes.length > 0 && (
          <div>
            <ul className="grid grid-cols-[repeat(auto-fit,minmax(11rem,1fr))] gap-3" aria-label="Trường đang tra cứu">
              {codes.map((code) => {
                const picked = names[code];
                return (
                  <li key={code} className="flex min-h-16 items-start justify-between gap-2 rounded-xl border border-line bg-surface p-4 shadow-[var(--elev-1)]">
                    <div className="min-w-0">
                      <p className="line-clamp-2 text-sm font-semibold text-ink">{picked?.name ?? code}</p>
                      <p className="mt-0.5 truncate text-xs text-ink-2">{picked?.place || code}</p>
                    </div>
                    <button
                      type="button"
                      onClick={() => remove(code)}
                      aria-label={`Bỏ ${picked?.name ?? code}`}
                      className="-mt-1 -mr-1 shrink-0 rounded-md p-1.5 text-ink-3 transition-colors hover:bg-risk-wash hover:text-risk"
                    >
                      <X aria-hidden className="size-4" />
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        )}

        {tooMany && (
          <p role="alert" className="text-center text-sm text-risk">
            Chỉ được tra cứu tối đa {MAX} trường (đang chọn {codes.length}). Vui lòng bỏ bớt {codes.length - MAX} trường.
          </p>
        )}
        {!tooMany && limitError && <p role="alert" className="text-center text-sm text-risk">{limitError}</p>}
      </div>

      {codes.length > 0 && !tooMany && (
        <div id="cost-results" className={cn("scroll-mt-24", codes.length === 1 && "mx-auto w-full max-w-4xl")}>
          <CostEstimateView
            codes={codes}
            onLoaded={(schools) =>
              setNames((n) => {
                const next = { ...n };
                for (const s of schools) next[s.code] ??= { name: s.name, place: s.state ?? undefined };
                return next;
              })
            }
          />
        </div>
      )}
    </div>
  );
}
