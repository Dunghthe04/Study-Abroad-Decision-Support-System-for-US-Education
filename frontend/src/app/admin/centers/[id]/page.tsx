import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { cookies } from "next/headers";
import { apiFetch } from "@/lib/api";
import type { StudyCenter } from "@/types/api";
import { AdminSection } from "@/components/admin/AdminSection";
import { AdminCenterDetailView } from "@/components/admin/centers/AdminCenterDetailView";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { ArrowLeftIcon, AlertCircleIcon } from "lucide-react";
import { cn } from "cn";

interface AdminCenterDetailPageProps {
  params: Promise<{ id: string }>;
}

export async function generateMetadata({ params }: AdminCenterDetailPageProps): Promise<Metadata> {
  const { id } = await params;
  try {
    const center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`);
    if (center?.name) {
      return { title: `${center.name} – Chi tiết Trung tâm USAS` };
    }
  } catch {
    // ignore
  }
  return { title: "Chi tiết Trung tâm – USAS" };
}

export default async function AdminCenterDetailPage({ params }: AdminCenterDetailPageProps) {
  await connection();
  const { id } = await params;

  let center: StudyCenter | null = null;
  let fetchError = false;

  try {
    // Trang render trên server nên phải chuyển tiếp cookie đăng nhập, nếu không backend coi là khách
    // và trả 404 cho trung tâm chưa xác minh hoặc đang ẩn.
    const cookie = (await cookies()).toString();
    center = await apiFetch<StudyCenter>(`/api/v1/study-centers/${id}`, {
      headers: cookie ? { Cookie: cookie } : undefined,
    });
  } catch {
    fetchError = true;
  }

  return (
    <AdminSection href="/admin/centers">
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
              href="/admin/centers"
              className={cn(buttonVariants({ variant: "outline" }), "gap-2 inline-flex items-center text-sm")}
            >
              <ArrowLeftIcon className="w-4 h-4" />
              Quay lại danh sách trung tâm
            </Link>
          </div>
        </div>
      ) : (
        <AdminCenterDetailView center={center} />
      )}
    </AdminSection>
  );
}
