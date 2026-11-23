import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { ScholarshipFilters } from "@/components/scholarships/ScholarshipFilters";
import { ScholarshipList } from "@/components/scholarships/ScholarshipList";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { searchScholarships } from "@/lib/scholarship-api";
import type { PagedResult } from "@/types/api";
import type { ScholarshipItem, ScholarshipSearchParams } from "@/types/scholarship";

export const metadata: Metadata = {
  title: "Tìm học bổng – USAS",
  description: "Tìm và lọc học bổng du học Mỹ theo bậc học, giá trị, điều kiện và hạn nộp, mỗi học bổng đều có nguồn.",
};

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function ScholarshipsPage({ searchParams }: PageProps) {
  await connection();
  const raw = await searchParams;
  const str = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v?.trim()) || undefined;
  const num = (v: string | string[] | undefined) => {
    const s = str(v);
    const n = s === undefined ? NaN : Number(s);
    return Number.isFinite(n) ? n : undefined;
  };

  const params: ScholarshipSearchParams = {
    search: str(raw.search),
    studyLevel: str(raw.studyLevel),
    coverageType: str(raw.coverageType),
    minAmountUsd: num(raw.minAmountUsd),
    maxAmountUsd: num(raw.maxAmountUsd),
    maxMinGpa4: num(raw.maxMinGpa4),
    maxMinIelts: num(raw.maxMinIelts),
    kind: str(raw.kind),
    vnEligible: str(raw.vnEligible) === "true" ? true : undefined,
    page: num(raw.page) || 1,
    pageSize: 20,
  };

  let data: PagedResult<ScholarshipItem> | null = null;
  try {
    data = await searchScholarships(params);
  } catch {
    data = null;
  }
  if (!data) {
    return <ErrorFallback title="Không thể tải danh sách học bổng" description="Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng thử lại sau." />;
  }

  const pageHref = (page: number) => {
    const q = new URLSearchParams();
    for (const [k, v] of Object.entries({ ...params, page })) if (v !== undefined) q.set(k, String(v));
    return `/scholarships?${q}`;
  };

  return (
    <section className="space-y-6 py-6 pb-16">
      <h1 className="text-2xl font-bold">Tìm học bổng</h1>
      <ScholarshipFilters params={params} />
      <p className="text-sm text-muted-foreground">{data.totalCount} học bổng</p>
      <ScholarshipList items={data.items} />
      {data.totalPages > 1 && (
        <nav className="flex items-center justify-center gap-4 text-sm">
          {data.page > 1 && <Link href={pageHref(data.page - 1)} className="underline">Trang trước</Link>}
          <span>Trang {data.page}/{data.totalPages}</span>
          {data.page < data.totalPages && <Link href={pageHref(data.page + 1)} className="underline">Trang sau</Link>}
        </nav>
      )}
    </section>
  );
}
