import type { Metadata } from "next";
import { connection } from "next/server";
import { CostLookup } from "@/components/costs/CostLookup";
import { getStateLivingCosts } from "@/lib/cost-api";
import type { StateLivingCost } from "@/types/cost";

export const metadata: Metadata = {
  title: "Tra cứu chi phí – USAS",
  description:
    "Tra cứu chi phí du học Mỹ theo trường hoặc theo bang: ước tính năm đầu và cả khóa, so sánh 2 đến 4 trường, so mặt bằng giá sinh hoạt các bang.",
};

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value);

export default async function CostsPage({ searchParams }: PageProps) {
  const params = await searchParams;
  const mode = first(params.mode) === "state" ? "state" : "school";

  // ?codes=A,B (so sánh) hoặc ?code=A (liên kết cũ từ trang chi tiết trường). UC-27 E1: không cắt âm thầm, để màn hình báo nếu > 4 trường
  const raw = first(params.codes) ?? first(params.code) ?? "";
  const codes = [...new Set(raw.split(",").map((c) => c.trim()).filter(Boolean))];

  let states: StateLivingCost[] | null = null;
  if (mode === "state") {
    await connection();
    try {
      states = await getStateLivingCosts();
    } catch {
      states = null;
    }
  }

  return <CostLookup mode={mode} initialCodes={codes} states={states} />;
}
