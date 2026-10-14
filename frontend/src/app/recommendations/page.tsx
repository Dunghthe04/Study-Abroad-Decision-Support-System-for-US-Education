import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
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
  description: "Lọc trường đại học Mỹ phù hợp theo mô hình phân tầng 4 nhóm trường, thuật toán AHP & SAW.",
};

export default function RecommendationsPage() {
  return (
    <ProtectedRoute>
      <div className="space-y-6">
        <div className="flex flex-col gap-1">
          <Breadcrumb className="print:hidden">
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink render={<Link href="/" />}>Trang chủ</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator>/</BreadcrumbSeparator>
              <BreadcrumbItem>
                <BreadcrumbLink render={<Link href="/analysis" />}>Phân tích tư vấn</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator>/</BreadcrumbSeparator>
              <BreadcrumbItem>
                <BreadcrumbPage>Gợi ý trường</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
          <h1 className="pt-1 text-h1 text-foreground">Gợi ý trường theo hồ sơ của bạn</h1>
          <p className="text-sm text-muted-foreground">
            Danh sách trường đại học Mỹ được phân loại thành 4 nhóm chiến lược (Khó vào, Vừa sức, An toàn
            hơn, Cần học bổng lớn). Bạn có thể{" "}
            <Link href="/analysis" className="font-semibold text-brand hover:underline">
              sửa lại hồ sơ tại Luồng phân tích 6 bước
            </Link>{" "}
            bất cứ lúc nào.
          </p>
          <Separator className="mt-3 print:hidden" />
        </div>
  
        <RecommendationPanel />
      </div>
    </ProtectedRoute>
  );
}
