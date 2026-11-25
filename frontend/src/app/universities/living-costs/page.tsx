import type { Metadata } from "next";
import { connection } from "next/server";
import { CostSectionNav } from "@/components/costs/CostSectionNav";
import { StateLivingCostCompare } from "@/components/costs/StateLivingCostCompare";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { getStateLivingCosts } from "@/lib/cost-api";
import type { StateLivingCost } from "@/types/cost";

export const metadata: Metadata = {
  title: "So sánh chi phí sinh hoạt theo bang – USAS",
  description: "Đặt chi phí sinh hoạt của các bang Mỹ cạnh nhau, mỗi số liệu có nguồn và ngày lấy.",
};

export default async function StateLivingCostsPage() {
  await connection();
  let states: StateLivingCost[] | null = null;
  try {
    states = await getStateLivingCosts();
  } catch {
    states = null;
  }
  if (!states) {
    return <ErrorFallback title="Không thể tải dữ liệu các bang" description="Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng thử lại sau." />;
  }
  return (
    <section className="mx-auto max-w-[1560px] space-y-6 px-4 py-6 pb-16 sm:px-8 lg:px-12">
      <CostSectionNav />
      <h1 className="text-2xl font-bold">So sánh chi phí sinh hoạt theo bang</h1>
      <StateLivingCostCompare states={states} />
    </section>
  );
}
