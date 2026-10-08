import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ChatBox } from "@/components/advisor/ChatBox";

export const metadata = { title: "Tư vấn AI – USAS" };

export default function AdvisorPage() {
  return (
    <ProtectedRoute>
      <section className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-bold">Tư vấn du học Mỹ bằng AI</h1>
            <p className="text-sm text-slate-600">
              Hỗ trợ mọi bậc: THCS/THPT, cao đẳng, đại học, thạc sĩ, tiến sĩ. Chọn bậc học để câu trả lời sát hơn.
            </p>
          </div>
          <Link
            href="/profile/academic"
            className="inline-flex items-center gap-1.5 rounded-xl border border-blue-200 bg-blue-50 px-3.5 py-2 text-xs font-semibold text-blue-700 shadow-sm transition hover:bg-blue-100 whitespace-nowrap self-start sm:self-auto"
          >
            <span>📑</span> Điền bảng điểm & chứng chỉ →
          </Link>
        </div>

        {/* Banner gợi ý kết nối với Hồ sơ học thuật */}
        <div className="rounded-xl border border-amber-200 bg-amber-50/80 p-3.5 text-xs text-amber-900 flex items-center justify-between gap-3 shadow-xs">
          <div className="flex items-center gap-2">
            <span className="text-base">💡</span>
            <span>
              <strong>Mẹo nhỏ:</strong> Để AI gợi ý nhóm trường Reach / Match / Safety và tính toán khả năng trúng tuyển chuẩn xác nhất, bạn hãy cập nhật đầy đủ điểm 3 năm gần nhất tại{" "}
              <Link href="/profile/academic" className="font-bold underline hover:text-amber-950">
                Hồ sơ học thuật
              </Link>.
            </span>
          </div>
        </div>

        <ChatBox />
      </section>
    </ProtectedRoute>
  );
}
