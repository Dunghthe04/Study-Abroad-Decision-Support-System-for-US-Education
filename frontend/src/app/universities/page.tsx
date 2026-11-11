import type { Metadata } from "next";
import { connection } from "next/server";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { UniversityListView } from "@/components/universities/UniversityListView";
import { getUniversityFilterOptions, searchUniversities } from "@/lib/university-api";
import type { UniversityFilterOptions, UniversityListItem, UniversitySearchParams } from "@/types/university";
import type { PagedResult } from "@/types/api";

export const metadata: Metadata = {
  title: "Tìm kiếm trường Đại học Mỹ – USAS",
  description:
    "Tra cứu và khám phá hơn 240 trường đại học tại Mỹ với bộ lọc chi tiết theo bang, học phí, ngành đào tạo, thứ hạng và yêu cầu tiếng Anh.",
};

interface UniversitiesPageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function UniversitiesPage({ searchParams }: UniversitiesPageProps) {
  await connection();
  const raw = await searchParams;

  const getString = (val: string | string[] | undefined): string | undefined => {
    if (Array.isArray(val)) return val[0];
    return val?.trim() || undefined;
  };

  const getNumber = (val: string | string[] | undefined): number | undefined => {
    const s = getString(val);
    if (!s) return undefined;
    const n = Number(s);
    return Number.isFinite(n) ? n : undefined;
  };

  const params: UniversitySearchParams = {
    search: getString(raw.search),
    state: getString(raw.state),
    control: getString(raw.control),
    major: getString(raw.major),
    maxTuitionUsd: getNumber(raw.maxTuitionUsd),
    maxTotalCostUsd: getNumber(raw.maxTotalCostUsd),
    maxMinIelts: getNumber(raw.maxMinIelts),
    minAcceptanceRate: getNumber(raw.minAcceptanceRate),
    maxAcceptanceRate: getNumber(raw.maxAcceptanceRate),
    isIvy: getString(raw.isIvy) === "true" ? true : undefined,
    sortBy: getString(raw.sortBy) || "rank",
    studyLevel: getString(raw.studyLevel) || "undergraduate",
    page: getNumber(raw.page) || 1,
    pageSize: getNumber(raw.pageSize) || 12,
  };

  let data: PagedResult<UniversityListItem> | null = null;
  let options: UniversityFilterOptions | null = null;

  try {
    const [fetchedData, fetchedOptions] = await Promise.all([
      searchUniversities(params),
      getUniversityFilterOptions(),
    ]);
    data = fetchedData;
    options = fetchedOptions;
  } catch (err) {
    data = null;
    options = null;
  }

  if (!data || !options) {
    return (
      <ErrorFallback
        title="Không thể tải dữ liệu trường học"
        description="Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau."
      />
    );
  }

  return (
    <section className="py-6 pb-16 space-y-6">
      <UniversityListView
        initialData={data}
        options={options}
        currentParams={params}
      />
    </section>
  );
}
