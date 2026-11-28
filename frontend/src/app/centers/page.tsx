import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { PagedResult, StudyCenter } from "@/types/api";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { PublicCenterListView } from "@/components/centers/PublicCenterListView";

export const metadata = { title: "Trung tâm – USDSS" };

export default async function CentersPage() {
  await connection();

  let data: PagedResult<StudyCenter> | null = null;
  try {
    data = await apiFetch<PagedResult<StudyCenter>>("/api/v1/study-centers?pageSize=100");
  } catch {
    data = null;
  }

  if (!data) {
    return (
      <ErrorFallback
        title="Không thể tải danh bạ trung tâm"
        description="Không tải được dữ liệu danh bạ trung tâm. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau."
      />
    );
  }

  return (
    <section className="mx-auto w-full max-w-[1560px] px-4 pt-4 pb-8 sm:px-8 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:overflow-hidden lg:px-12 lg:pb-0">
      <PublicCenterListView initialCenters={data.items ?? []} />
    </section>
  );
}
