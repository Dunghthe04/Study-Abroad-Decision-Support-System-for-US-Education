import { FinancialCard } from "@/components/profile/FinancialCard";
import { ExtracurricularSection } from "@/components/profile/ExtracurricularSection";
import { AchievementsSection } from "@/components/profile/AchievementsSection";
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
  title: "Hồ sơ Tài chính & Ngoại khóa (USAS-364) – USAS",
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
            <p className="text-body-s">
              Mã chức năng: <strong>USAS-364</strong> (Sprint 1) • Người phụ trách: <strong>Nguyễn Xuân Đức</strong>
            </p>
          </div>
          <Link href="/advisor" className={buttonVariants({ variant: "secondary" })}>
            Chuyển tới Tư vấn AI
            <ArrowRightIcon aria-hidden="true" />
          </Link>
        </div>
      </div>

      {/* Thông báo hướng dẫn nghiệp vụ */}
      <Alert variant="info">
        <LightbulbIcon aria-hidden="true" />
        <AlertTitle>Nguyên tắc đánh giá hồ sơ du học Mỹ (Holistic Review):</AlertTitle>
        <AlertDescription>
          Đại học Mỹ không chỉ nhìn vào bảng điểm GPA mà đánh giá toàn diện 3 trụ cột: <strong>Học thuật (GPA, SAT, IELTS)</strong>, <strong>Khả năng tài chính</strong> và <strong>Hoạt động ngoại khóa & Giải thưởng</strong>. Dữ liệu khai báo dưới đây sẽ giúp AI ở Bước B3–B4 phân loại danh sách trường theo 3 nhóm: <em>Reach (Thử thách)</em>, <em>Match (Vừa tầm)</em> và <em>Safety (An toàn)</em>.
        </AlertDescription>
      </Alert>

      {/* 3 Khối nghiệp vụ chính */}
      <div className="space-y-6">
        {/* Khối 1: Tài chính */}
        <FinancialCard />

        {/* Khối 2: Ngoại khóa & Lãnh đạo */}
        <ExtracurricularSection />

        {/* Khối 3: Nghiên cứu, Thực tập & Giải thưởng */}
        <AchievementsSection />
      </div>
    </div>
  );
}
