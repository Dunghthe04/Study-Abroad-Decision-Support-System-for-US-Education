import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import { studyLevelLabel } from "@/lib/study-levels";
import { centerServiceLabel } from "@/lib/center-services";
import type { PagedResult, StudyCenter } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AddCenterButton } from "@/components/admin/AddCenterButton";

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
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-h1">Trung tâm tư vấn du học</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Danh bạ các trung tâm tư vấn du học uy tín được xác minh trên hệ thống USAS.
          </p>
        </div>
        <AddCenterButton />
      </div>
      {!data ? (
        <Alert variant="destructive">
          <AlertDescription>Không tải được dữ liệu. Kiểm tra API đã chạy chưa.</AlertDescription>
        </Alert>
      ) : data.items.length === 0 ? (
        <p className="text-muted-foreground">Chưa có trung tâm nào.</p>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2">
          {data.items.map((c) => (
            <li key={c.id}>
              <Card className="h-full flex flex-col justify-between hover:border-brand/40 transition-colors">
                <CardHeader>
                  <div className="flex items-start justify-between gap-2">
                    <CardTitle className="text-lg">{c.name}</CardTitle>
                    {c.city && (
                      <Badge variant="outline" className="shrink-0 font-normal">
                        📍 {c.city}
                      </Badge>
                    )}
                  </div>
                  {c.address && (
                    <CardDescription className="text-xs">{c.address}</CardDescription>
                  )}
                  {c.studyLevels.length > 0 && (
                    <CardDescription className="text-xs pt-1">
                      <span className="font-medium text-foreground">Bậc học:</span>{" "}
                      {c.studyLevels.map(studyLevelLabel).join(", ")}
                    </CardDescription>
                  )}
                </CardHeader>
                {c.services.length > 0 && (
                  <CardContent className="pt-0">
                    <div className="text-xs font-medium text-foreground mb-1.5">Dịch vụ cung cấp:</div>
                    <div className="flex flex-wrap gap-1.5">
                      {c.services.map((s) => (
                        <Badge key={s} variant="secondary" className="font-normal text-xs py-0.5">
                          {centerServiceLabel(s)}
                        </Badge>
                      ))}
                    </div>
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
