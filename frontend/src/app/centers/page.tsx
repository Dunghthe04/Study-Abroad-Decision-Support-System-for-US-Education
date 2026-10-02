import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import { studyLevelLabel } from "@/lib/study-levels";
import type { PagedResult, StudyCenter } from "@/types/api";

export const metadata = { title: "Trung tâm tư vấn – USAS" };

export default async function CentersPage() {
  await connection(); // render per request: data comes from the API at runtime

  let data: PagedResult<StudyCenter> | null = null;
  try {
    data = await apiFetch<PagedResult<StudyCenter>>("/api/v1/study-centers?pageSize=50");
  } catch {
    data = null;
  }

  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Trung tâm tư vấn du học</h1>
      {!data ? (
        <p className="text-red-600">Không tải được dữ liệu. Kiểm tra API đã chạy chưa.</p>
      ) : data.items.length === 0 ? (
        <p className="text-slate-600">Chưa có trung tâm nào.</p>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {data.items.map((c) => (
            <li key={c.id} className="rounded-lg border border-slate-200 bg-white p-4">
              <p className="font-semibold">{c.name}</p>
              {c.city && <p className="text-sm text-slate-600">{c.city}</p>}
              {c.studyLevels.length > 0 && (
                <p className="text-sm text-slate-600">Bậc: {c.studyLevels.map(studyLevelLabel).join(", ")}</p>
              )}
              <div className="mt-2 flex flex-wrap gap-1">
                {c.services.map((s) => (
                  <span key={s} className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                    {s}
                  </span>
                ))}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
