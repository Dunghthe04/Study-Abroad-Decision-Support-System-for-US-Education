import type { Metadata } from "next";
import { connection } from "next/server";
import { ScholarshipListView } from "@/components/scholarships/ScholarshipListView";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { searchScholarships } from "@/lib/scholarship-api";
import type { PagedResult } from "@/types/api";
import type { ScholarshipItem, ScholarshipSearchParams } from "@/types/scholarship";

export const metadata: Metadata = {
  title: "Tìm học bổng – USAS",
  description: "Tìm và lọc học bổng du học Mỹ bậc đại học theo giá trị, điều kiện và hạn nộp, mỗi học bổng đều có nguồn.",
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
  // Ngày hôm nay theo giờ Việt Nam, dạng yyyy-MM-dd
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
  const includeExpired = str(raw.includeExpired) === "true" ? true : undefined;
  const customDeadlineFrom = str(raw.deadlineFrom);
  // UC-29: mặc định bật lọc mở cho người Việt; người dùng có thể tắt (?vnEligible=false)
  const vnEligible = str(raw.vnEligible) === "false" ? false : true;

  const params: ScholarshipSearchParams = {
    search: str(raw.search),
    coverageType: str(raw.coverageType),
    minAmountUsd: num(raw.minAmountUsd),
    maxAmountUsd: num(raw.maxAmountUsd),
    maxMinGpa4: num(raw.maxMinGpa4),
    maxMinIelts: num(raw.maxMinIelts),
    maxMinToefl: num(raw.maxMinToefl),
    maxMinDuolingo: num(raw.maxMinDuolingo),
    kind: str(raw.kind),
    vnEligible,
    includeExpired,
    deadlineFrom: customDeadlineFrom ?? (includeExpired ? undefined : today),
    page: num(raw.page) || 1,
    pageSize: 20,
  };

  let data: PagedResult<ScholarshipItem> | null = null;
  try {
    data = await searchScholarships({
      ...params,
      vnEligible: vnEligible ? true : undefined,
      includeExpired: undefined,
    });
  } catch {
    data = null;
  }
  if (!data) {
    return <ErrorFallback title="Không thể tải danh sách học bổng" description="Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng thử lại sau." />;
  }

  return (
    <section className="mx-auto w-full max-w-[1560px] px-4 pt-4 pb-8 sm:px-8 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:overflow-hidden lg:px-12 lg:pb-0">
      <ScholarshipListView data={data} params={params} today={today} hasCustomDeadline={Boolean(customDeadlineFrom)} />
    </section>
  );
}
