import type { Metadata } from "next";
import { CostCompareView } from "@/components/costs/CostCompareView";

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
  const codes = [...new Set((text ?? "").split(",").map((c) => c.trim()).filter(Boolean))].slice(0, 4);
  return (
    <section className="space-y-6 py-6 pb-16">
      <h1 className="text-2xl font-bold">So sánh chi phí trường</h1>
      <CostCompareView initialCodes={codes} />
    </section>
  );
}
