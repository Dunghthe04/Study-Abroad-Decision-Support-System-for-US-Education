import { redirect } from "next/navigation";

interface AdminCenterDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function AdminCenterDetailPage({ params }: AdminCenterDetailPageProps) {
  const { id } = await params;
  redirect(`/moderator/centers/${id}`);
}
