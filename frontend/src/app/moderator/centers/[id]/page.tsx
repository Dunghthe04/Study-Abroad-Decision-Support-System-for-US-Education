import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { apiFetch } from "@/lib/api";
import type { StudyCenter } from "@/types/api";
import { AdminCenterDetailView } from "@/components/admin/centers/AdminCenterDetailView";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { ArrowLeftIcon, AlertCircleIcon } from "lucide-react";
import { cn } from "cn";

interface ModeratorCenterDetailPageProps {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: ModeratorCenterDetailPageProps): Promise<Metadata> {
  const { id } = await params;
  try {
    const center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`);
    if (center?.name) {
      return { title: `${center.name} – Chi tiết Trung tâm – Moderator USAS` };
    }
  } catch {
    // ignore
  }
  return { title: "Chi tiết Trung tâm – Moderator USAS" };
}

export default async function ModeratorCenterDetailPage({ params }: ModeratorCenterDetailPageProps) {
  await connection();
  const { id } = await params;

  let center: StudyCenter | null = null;
  let fetchError = false;

  try {
    center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`);
  } catch {
    fetchError = true;
  }

  return (
    <div className="space-y-4">
      {fetchError || !center ? (
        <div className="max-w-xl mx-auto py-12 space-y-4">
          <Alert variant="destructive">
            <AlertCircleIcon className="w-4 h-4" />
            <AlertTitle>Không tìm thấy trung tâm</AlertTitle>
            <AlertDescription>
              Trung tâm tư vấn này không tồn tại hoặc đã bị ẩn khỏi hệ thống.
            </AlertDescription>
          </Alert>
          <div>
            <Link
              href="/moderator/centers"
              className={cn(buttonVariants({ variant: "outline" }), "gap-2 inline-flex items-center text-sm")}
            >
              <ArrowLeftIcon className="w-4 h-4" />
              Quay lại danh bạ trung tâm
            </Link>
          </div>
        </div>
      ) : (
        <AdminCenterDetailView center={center} />
      )}
    </div>
  );
}
