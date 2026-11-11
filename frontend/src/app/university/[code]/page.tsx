import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { ApiError } from "@/lib/api";
import { getUniversityDetail } from "@/lib/university-api";
import { UniversityDetailView } from "@/components/universities/UniversityDetailView";
import type { UniversityDetail } from "@/types/university";

interface UniversityPageProps {
  params: Promise<{ code: string }>;
}

export async function generateMetadata({ params }: UniversityPageProps): Promise<Metadata> {
  const { code } = await params;
  try {
    const university = await getUniversityDetail(code);
    return {
      title: `${university.name} – Thông tin trường | USAS`,
      description: `Thông tin tuyển sinh, chi phí, hạn nộp và ngành học của ${university.name}, kèm nguồn dữ liệu trên USAS.`,
    };
  } catch {
    return { title: "Thông tin trường | USAS" };
  }
}

export default async function UniversityDetailPage({ params }: UniversityPageProps) {
  await connection();
  const { code } = await params;
  if (!code.trim() || code.length > 32) notFound();
  let university: UniversityDetail;
  try {
    university = await getUniversityDetail(code);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }
  return <UniversityDetailView key={university.code} university={university} />;
}
