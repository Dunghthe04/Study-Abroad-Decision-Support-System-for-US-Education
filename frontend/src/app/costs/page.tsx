import type { Metadata } from "next";
import { CostSectionNav } from "@/components/costs/CostSectionNav";
import { SingleCostEstimateView } from "@/components/costs/SingleCostEstimateView";

export const metadata: Metadata = {
  title: "Ước tính chi phí trường – USAS",
  description: "Ước tính chi phí du học Mỹ theo năm và cả khóa 4 năm, quy đổi sang VND tham khảo và đối chiếu ngân sách.",
};

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function CostsPage({ searchParams }: PageProps) {
  const raw = (await searchParams).code;
  const text = Array.isArray(raw) ? raw[0] : raw;
  const code = text?.trim() || null;
  return (
    <section className="mx-auto max-w-[1560px] space-y-6 px-4 py-6 pb-16 sm:px-8 lg:px-12">
      <CostSectionNav />
      <h1 className="text-2xl font-bold">Ước tính chi phí trường</h1>
      <SingleCostEstimateView initialCode={code} />
    </section>
  );
}
