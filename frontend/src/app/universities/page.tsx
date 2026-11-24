import type { Metadata } from "next";
import { connection } from "next/server";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { UniversityListView } from "@/components/universities/UniversityListView";
import { getUniversityFilterOptions, searchUniversities } from "@/lib/university-api";
import type { UniversityFilterOptions, UniversityListItem, UniversitySearchParams } from "@/types/university";
import type { PagedResult } from "@/types/api";

export const metadata: Metadata = {
  title: "Tìm kiếm trường Đại học Mỹ – USDSS",
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

  const getEnum = (val: string | string[] | undefined, allowed: string[]): string | undefined => {
    const s = getString(val);
    return s && allowed.includes(s) ? s : undefined;
  };

  const getBool = (val: string | string[] | undefined): true | undefined =>
    getString(val) === "true" ? true : undefined;

  const sortBy = getString(raw.sortBy) || "name";
  const params: UniversitySearchParams = {
    search: getString(raw.search),
    state: getString(raw.state),
    region: getEnum(raw.region, ["northeast", "midwest", "south", "west"]),
    control: getString(raw.control),
    major: getString(raw.major),
    minTuitionUsd: getNumber(raw.minTuitionUsd),
    maxTuitionUsd: getNumber(raw.maxTuitionUsd),
    minTotalCostUsd: getNumber(raw.minTotalCostUsd),
    maxTotalCostUsd: getNumber(raw.maxTotalCostUsd),
    minLivingUsd: getNumber(raw.minLivingUsd),
    maxLivingUsd: getNumber(raw.maxLivingUsd),
    maxApplicationFeeUsd: getNumber(raw.maxApplicationFeeUsd),
    maxMinIelts: getNumber(raw.maxMinIelts),
    maxMinToefl: getNumber(raw.maxMinToefl),
    maxMinDuolingo: getNumber(raw.maxMinDuolingo),
    maxMinGpa4: getNumber(raw.maxMinGpa4),
    minAvgGpa4: getNumber(raw.minAvgGpa4),
    maxAvgGpa4: getNumber(raw.maxAvgGpa4),
    minSat: getNumber(raw.minSat),
    maxSat: getNumber(raw.maxSat),
    minAct: getNumber(raw.minAct),
    maxAct: getNumber(raw.maxAct),
    satPolicy: getEnum(raw.satPolicy, ["required", "optional", "not_accepted"]),
    actPolicy: getEnum(raw.actPolicy, ["required", "optional", "not_accepted"]),
    deadlineRound: getEnum(raw.deadlineRound, ["ED", "EA", "RD", "RollingFinal"]),
    c7TestImportance: getEnum(raw.c7TestImportance, ["very_important", "important", "considered", "not_considered"]),
    c7EcImportance: getEnum(raw.c7EcImportance, ["very_important", "important", "considered", "not_considered"]),
    minAcceptanceRate: getNumber(raw.minAcceptanceRate),
    maxAcceptanceRate: getNumber(raw.maxAcceptanceRate),
    minIntlAcceptanceRate: getNumber(raw.minIntlAcceptanceRate),
    maxIntlAcceptanceRate: getNumber(raw.maxIntlAcceptanceRate),
    minStudentFacultyRatio: getNumber(raw.minStudentFacultyRatio),
    maxStudentFacultyRatio: getNumber(raw.maxStudentFacultyRatio),
    minRetentionRate: getNumber(raw.minRetentionRate),
    maxRetentionRate: getNumber(raw.maxRetentionRate),
    minGradRate6Yr: getNumber(raw.minGradRate6Yr),
    maxGradRate6Yr: getNumber(raw.maxGradRate6Yr),
    minIntlStudents: getNumber(raw.minIntlStudents),
    maxIntlStudents: getNumber(raw.maxIntlStudents),
    intlAidPolicy: getEnum(raw.intlAidPolicy, ["need_and_merit", "merit_only", "need_only", "none"]),
    minIntlAidAvgUsd: getNumber(raw.minIntlAidAvgUsd),
    maxIntlAidAvgUsd: getNumber(raw.maxIntlAidAvgUsd),
    minFyOnCampusPct: getNumber(raw.minFyOnCampusPct),
    maxFyOnCampusPct: getNumber(raw.maxFyOnCampusPct),
    housingType: getString(raw.housingType),
    activity: getString(raw.activity),
    isIvy: getBool(raw.isIvy),
    testOptional: getBool(raw.testOptional),
    hasIntlAid: getBool(raw.hasIntlAid),
    hasScholarship: getBool(raw.hasScholarship),
    intlHousing: getBool(raw.intlHousing),
    campusSetting: getEnum(raw.campusSetting, ["city", "suburb", "town", "rural"]),
    maxRank: getNumber(raw.maxRank),
    sortBy,
    rankListId: sortBy === "rank" ? getString(raw.rankListId) || "USN27-NU" : undefined,
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
  } catch {
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
    <section className="mx-auto w-full max-w-[1560px] px-4 pt-4 pb-8 sm:px-8 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:overflow-hidden lg:px-12 lg:pb-0">
      <UniversityListView
        initialData={data}
        options={options}
        currentParams={params}
      />
    </section>
  );
}
