import Link from "next/link";
import { RecommendationPanel } from "@/components/recommendations/RecommendationPanel";

export const metadata = {
  title: "Gợi ý trường – USAS",
  description: "Lọc trường đại học Mỹ phù hợp theo hồ sơ học thuật, tài chính và ngoại khóa.",
};

export default function RecommendationsPage() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-1 border-b border-slate-200 pb-4">
        <div className="flex items-center gap-2 text-xs text-slate-500">
          <Link href="/" className="hover:text-blue-600">
            Trang chủ
          </Link>
          <span>/</span>
          <span className="font-medium text-blue-600">Gợi ý trường</span>
        </div>
        <h1 className="pt-1 text-2xl font-bold tracking-tight text-slate-900">Gợi ý trường theo hồ sơ</h1>
        <p className="text-sm text-slate-600">
          Hệ thống chấm điểm theo học thuật, tài chính và ngoại khóa, chia trường thành 3 nhóm. Cập nhật hồ sơ ở{" "}
          <Link href="/profile/academic" className="text-blue-600 hover:underline">
            Học thuật
          </Link>{" "}
          và{" "}
          <Link href="/profile/financial" className="text-blue-600 hover:underline">
            Tài chính & Ngoại khóa
          </Link>{" "}
          rồi lọc lại.
        </p>
      </div>

      <RecommendationPanel />
    </div>
  );
}
