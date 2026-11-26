import { redirect } from "next/navigation";

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

// Trang cũ đã gộp vào /costs ("Tra cứu chi phí"); giữ đường dẫn để liên kết và dấu trang cũ vẫn mở được
export default async function CompareCostPage({ searchParams }: PageProps) {
  const raw = (await searchParams).codes;
  const codes = (Array.isArray(raw) ? raw[0] : raw)?.trim();
  redirect(codes ? `/costs?codes=${encodeURIComponent(codes).replace(/%2C/gi, ",")}` : "/costs");
}
