import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { StudyCenter } from "@/types/api";
import { StudyCenterDetailView } from "@/components/centers/StudyCenterDetailView";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { ArrowLeftIcon, AlertCircleIcon } from "lucide-react";
import { cn } from "cn";

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
      <div className="max-w-xl mx-auto py-12 space-y-4">
        <Alert variant="destructive">
          <AlertCircleIcon className="w-4 h-4" />
          <AlertTitle>Không tìm thấy trung tâm</AlertTitle>
          <AlertDescription>
            Trung tâm tư vấn này không tồn tại hoặc đã bị ẩn khỏi danh bạ hệ thống.
          </AlertDescription>
        </Alert>
        <div>
          <Link
            href="/centers"
            className={cn(buttonVariants({ variant: "outline" }), "gap-2 inline-flex items-center")}
          >
            <ArrowLeftIcon className="w-4 h-4" />
            Quay lại danh sách trung tâm
          </Link>
        </div>
      </div>
    );
  }

  return <StudyCenterDetailView center={center} />;
}
