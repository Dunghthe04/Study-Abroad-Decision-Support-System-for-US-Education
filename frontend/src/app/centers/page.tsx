import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import { studyLevelLabel } from "@/lib/study-levels";
import type { PagedResult, StudyCenter } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

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
      <h1 className="text-h1">Trung tâm tư vấn du học</h1>
      {!data ? (
        <Alert variant="destructive">
          <AlertDescription>Không tải được dữ liệu. Kiểm tra API đã chạy chưa.</AlertDescription>
        </Alert>
      ) : data.items.length === 0 ? (
        <p className="text-muted-foreground">Chưa có trung tâm nào.</p>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {data.items.map((c) => (
            <li key={c.id}>
              <Card className="h-full">
                <CardHeader>
                  <CardTitle>{c.name}</CardTitle>
                  {c.city && <CardDescription>{c.city}</CardDescription>}
                  {c.studyLevels.length > 0 && (
                    <CardDescription>Bậc: {c.studyLevels.map(studyLevelLabel).join(", ")}</CardDescription>
                  )}
                </CardHeader>
                {c.services.length > 0 && (
                  <CardContent className="flex flex-wrap gap-1">
                    {c.services.map((s) => (
                      <Badge key={s} variant="secondary">
                        {s}
                      </Badge>
                    ))}
                  </CardContent>
                )}
              </Card>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
