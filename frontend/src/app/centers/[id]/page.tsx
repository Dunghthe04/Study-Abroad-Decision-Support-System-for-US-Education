import type { Metadata } from "next";
import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { StudyCenter } from "@/types/api";
import { StudyCenterDetailView } from "@/components/centers/StudyCenterDetailView";
import { ErrorFallback } from "@/components/ui/error-fallback";

interface CenterPageProps {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: CenterPageProps): Promise<Metadata> {
  const { id } = await params;
  try {
    const center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`);
    if (center?.name) {
      return { title: `${center.name} – Chi tiết Trung tâm USAS` };
    }
  } catch {
    // ignore
  }
  return { title: "Chi tiết Trung tâm tư vấn – USAS" };
}

export default async function CenterDetailPage({ params }: CenterPageProps) {
  await connection();
  const { id } = await params;

  let center: StudyCenter | null = null;
  let fetchError = false;

  try {
    center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`);
  } catch {
    fetchError = true;
  }

  if (fetchError || !center) {
    return (
      <ErrorFallback
        title="Không tìm thấy trung tâm"
        description="Trung tâm tư vấn này không tồn tại hoặc đã bị ẩn khỏi danh bạ hệ thống."
        action={{
          label: "Quay lại danh sách trung tâm",
          href: "/centers",
        }}
        secondaryAction={{
          label: "Về trang chủ",
          href: "/",
        }}
      />
    );
  }

  return <StudyCenterDetailView center={center} />;
}
