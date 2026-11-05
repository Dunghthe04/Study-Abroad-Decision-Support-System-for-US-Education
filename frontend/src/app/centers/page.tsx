import Link from "next/link";
import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import { studyLevelLabel } from "@/lib/study-levels";
import { centerServiceLabel } from "@/lib/center-services";
import type { PagedResult, StudyCenter } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { AddCenterButton } from "@/components/admin/AddCenterButton";
import { ChevronRightIcon, CheckCircle2Icon } from "lucide-react";
import { cn } from "cn";

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
              <Card className="h-full flex flex-col justify-between hover:border-brand/50 hover:shadow-md transition-all group">
                <CardHeader>
                  <div className="flex items-start justify-between gap-2">
                    <div className="space-y-1">
                      <Link href={`/centers/${c.id}`} className="hover:text-brand transition-colors">
                        <CardTitle className="text-lg group-hover:text-brand transition-colors">
                          {c.name}
                        </CardTitle>
                      </Link>
                      {c.verificationStatus === "verified" && (
                        <div className="flex items-center gap-1 text-[11px] text-emerald-600 font-medium">
                          <CheckCircle2Icon className="w-3.5 h-3.5" />
                          Đã xác minh
                        </div>
                      )}
                    </div>
                    {c.city && (
                      <Badge variant="outline" className="shrink-0 font-normal">
                        {c.city}
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
                <CardFooter className="pt-2 border-t mt-auto flex justify-between items-center text-xs">
                  <span className="text-muted-foreground text-[11px]">Mã: {c.code}</span>
                  <Link
                    href={`/centers/${c.id}`}
                    className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "gap-1 text-xs hover:text-brand -mr-2")}
                  >
                    Xem chi tiết
                    <ChevronRightIcon className="w-3.5 h-3.5" />
                  </Link>
                </CardFooter>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
