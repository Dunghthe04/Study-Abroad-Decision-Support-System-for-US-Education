import { FinancialCard } from "@/components/profile/FinancialCard";
import { ExtracurricularSection } from "@/components/profile/ExtracurricularSection";
import { AchievementsSection } from "@/components/profile/AchievementsSection";
import Link from "next/link";

export const metadata = {
  title: "Hồ sơ Tài chính & Ngoại khóa (USAS-364) – USAS",
  description: "Khai báo khả năng tài chính, hoạt động ngoại khóa, nghiên cứu và giải thưởng phục vụ tư vấn du học Mỹ.",
};

export default function FinancialProfilePage() {
  return (
    <div className="space-y-6">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex flex-col gap-1 border-b border-slate-200 pb-4">
        <div className="flex items-center gap-2 text-xs text-slate-500">
          <Link href="/" className="hover:text-blue-600">
            Trang chủ
          </Link>
          <span>/</span>
          <span className="text-slate-800 font-medium">Hồ sơ cá nhân</span>
          <span>/</span>
          <span className="text-blue-600 font-medium">Tài chính & Ngoại khóa</span>
        </div>
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 pt-1">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">
              Hồ sơ Tài chính & Ngoại khóa
            </h1>
            <p className="text-sm text-slate-600">
              Mã chức năng: <span className="font-semibold text-blue-700">USAS-364</span> (Sprint 1) • Người phụ trách: <span className="font-semibold text-slate-800">Nguyễn Xuân Đức</span>
            </p>
          </div>
          <Link
            href="/advisor"
            className="inline-flex items-center justify-center rounded-lg bg-blue-50 px-4 py-2 text-xs font-semibold text-blue-700 border border-blue-200 hover:bg-blue-100 transition-colors"
          >
            Chuyển tới Tư vấn AI &rarr;
          </Link>
        </div>
      </div>

      {/* Thông báo hướng dẫn nghiệp vụ */}
      <div className="rounded-lg bg-blue-50/70 border border-blue-200 p-4 text-xs text-blue-800 leading-relaxed">
        <strong className="font-semibold">💡 Nguyên tắc đánh giá hồ sơ du học Mỹ (Holistic Review):</strong>
        <p className="mt-1 text-blue-900/90">
          Đại học Mỹ không chỉ nhìn vào bảng điểm GPA mà đánh giá toàn diện 3 trụ cột: <strong>Học thuật (GPA, SAT, IELTS)</strong>, <strong>Khả năng tài chính</strong> và <strong>Hoạt động ngoại khóa & Giải thưởng</strong>. Dữ liệu khai báo dưới đây sẽ giúp AI ở Bước B3–B4 phân loại danh sách trường theo 3 nhóm: <em>Reach (Thử thách)</em>, <em>Match (Vừa tầm)</em> và <em>Safety (An toàn)</em>.
        </p>
      </div>

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
