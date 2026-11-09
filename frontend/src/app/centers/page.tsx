import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { PagedResult, StudyCenter } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { PublicCenterListView } from "@/components/centers/PublicCenterListView";

export const metadata = { title: "Trung tâm tư vấn – USAS" };

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
      <section className="space-y-4 py-6">
        <Alert variant="destructive">
          <AlertDescription>Không tải được dữ liệu danh bạ trung tâm. Vui lòng kiểm tra lại kết nối API.</AlertDescription>
        </Alert>
      </section>
    );
  }

  return (
    <section className="space-y-6 pb-12">
      <PublicCenterListView initialCenters={data.items ?? []} />
    </section>
  );
}
