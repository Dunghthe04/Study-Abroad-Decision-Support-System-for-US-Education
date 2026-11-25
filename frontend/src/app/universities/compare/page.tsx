import type { Metadata } from "next";
import { CostCompareView } from "@/components/costs/CostCompareView";
import { CostSectionNav } from "@/components/costs/CostSectionNav";

export const metadata: Metadata = {
  title: "So sánh chi phí trường – USAS",
  description: "So sánh chi phí của 2 đến 4 trường đại học Mỹ cạnh nhau và so với ngân sách của bạn.",
};

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function CompareCostPage({ searchParams }: PageProps) {
  const raw = (await searchParams).codes;
  const text = Array.isArray(raw) ? raw[0] : raw;
  // UC-27 E1: không cắt âm thầm .slice(0, 4); truyền đủ để CostCompareView báo lỗi nếu > 4 trường
  const codes = [...new Set((text ?? "").split(",").map((c) => c.trim()).filter(Boolean))];
  return (
    <section className="mx-auto max-w-[1560px] space-y-6 px-4 py-6 pb-16 sm:px-8 lg:px-12">
      <CostSectionNav />
      <h1 className="text-2xl font-bold">So sánh chi phí trường</h1>
      <CostCompareView initialCodes={codes} />
    </section>
  );
}
