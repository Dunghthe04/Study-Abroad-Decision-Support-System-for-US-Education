import { FinancialProfileTabs } from "@/components/profile/FinancialProfileTabs";
import Link from "next/link";
import { ArrowRightIcon, LightbulbIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { buttonVariants } from "@/components/ui/button";

export const metadata = {
  title: "Hồ sơ Tài chính & Ngoại khóa – USAS",
  description: "Khai báo khả năng tài chính, hoạt động ngoại khóa, nghiên cứu và giải thưởng phục vụ tư vấn du học Mỹ.",
};

export default function FinancialProfilePage() {
  return (
    <div className="space-y-6">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex flex-col gap-1">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink render={<Link href="/" />}>Trang chủ</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator>/</BreadcrumbSeparator>
            <BreadcrumbItem>
              Hồ sơ cá nhân
            </BreadcrumbItem>
            <BreadcrumbSeparator>/</BreadcrumbSeparator>
            <BreadcrumbItem>
              <BreadcrumbPage>Tài chính & Ngoại khóa</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 pt-1">
          <div>
            <h1 className="text-h1">
              Hồ sơ Tài chính & Ngoại khóa
            </h1>
            <p className="text-sm text-slate-500 mt-1">
              Khai báo khả năng tài chính, hoạt động ngoại khóa và thành tích của bạn.
            </p>
          </div>
          <Link href="/advisor" className={buttonVariants({ variant: "secondary" })}>
            Chuyển tới Tư vấn AI
            <ArrowRightIcon aria-hidden="true" />
          </Link>
        </div>
      </div>

      {/* Tab-based layout thay vì stack dọc 3 khối */}
      <FinancialProfileTabs />
    </div>
  );
}
