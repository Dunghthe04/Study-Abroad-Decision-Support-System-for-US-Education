import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { PagedResult, StudyCenter } from "@/types/api";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { CenterCompareView } from "@/components/centers/CenterCompareView";
import { parseCompareIds } from "@/lib/center-compare";

export const metadata = { title: "So sánh trung tâm – USAS" };

interface ComparePageProps {
  searchParams: Promise<{ ids?: string | string[] }>;
}

export default async function CenterComparePage({ searchParams }: ComparePageProps) {
  await connection();
  const { ids: rawIds } = await searchParams;
  const ids = parseCompareIds(rawIds);

  let all: StudyCenter[];
  try {
    const data = await apiFetch<PagedResult<StudyCenter>>("/api/v1/study-centers?pageSize=100");
    all = data.items ?? [];
  } catch {
    return (
      <ErrorFallback
        title="Không thể tải danh bạ trung tâm"
        description="Không tải được dữ liệu trung tâm. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau."
        action={{ label: "Quay lại danh sách trung tâm", href: "/centers" }}
      />
    );
  }

  // Chỉ giữ trung tâm còn hiển thị công khai, theo đúng thứ tự học sinh chọn
  const byId = new Map(all.map((c) => [c.id, c]));
  const selected = ids.map((id) => byId.get(id)).filter((c): c is StudyCenter => !!c);

  return (
    <section className="space-y-6 pb-12">
      <CenterCompareView selected={selected} all={all} />
    </section>
  );
}
