import Link from "next/link";
import { RecommendationPanel } from "@/components/recommendations/RecommendationPanel";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { Separator } from "@/components/ui/separator";

export const metadata = {
  title: "Gợi ý trường – USAS",
  description: "Lọc trường đại học Mỹ phù hợp theo hồ sơ học thuật, tài chính và ngoại khóa.",
};

export default function RecommendationsPage() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-1">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink render={<Link href="/" />}>Trang chủ</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator>/</BreadcrumbSeparator>
            <BreadcrumbItem>
              <BreadcrumbPage>Gợi ý trường</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
        <h1 className="pt-1 text-h1">Gợi ý trường theo hồ sơ</h1>
        <p className="text-sm text-muted-foreground">
          Hệ thống chấm điểm theo học thuật, tài chính và ngoại khóa, chia trường thành 3 nhóm. Cập nhật hồ sơ ở{" "}
          <Link href="/profile/academic" className="text-primary hover:underline">
            Học thuật
          </Link>{" "}
          và{" "}
          <Link href="/profile/financial" className="text-primary hover:underline">
            Tài chính & Ngoại khóa
          </Link>{" "}
          rồi lọc lại.
        </p>
        <Separator className="mt-3" />
      </div>

      <RecommendationPanel />
    </div>
  );
}
