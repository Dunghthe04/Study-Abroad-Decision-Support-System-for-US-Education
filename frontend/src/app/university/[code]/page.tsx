import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { ApiError } from "@/lib/api";
import { searchScholarships } from "@/lib/scholarship-api";
import { getUniversityDetail } from "@/lib/university-api";
import { UniversityDetailView } from "@/components/universities/UniversityDetailView";
import type { ScholarshipItem } from "@/types/scholarship";
import type { UniversityDetail } from "@/types/university";

interface UniversityPageProps {
  params: Promise<{ code: string }>;
}

export async function generateMetadata({ params }: UniversityPageProps): Promise<Metadata> {
  const { code } = await params;
  try {
    const university = await getUniversityDetail(code);
    return {
      title: `${university.name} – Thông tin trường | USDSS`,
      description: `Thông tin tuyển sinh, chi phí, hạn nộp và ngành học của ${university.name}, kèm nguồn dữ liệu trên USDSS.`,
    };
  } catch {
    return { title: "Thông tin trường | USDSS" };
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
  // Ngày hôm nay theo giờ Việt Nam (yyyy-MM-dd): chỉ lấy học bổng còn hạn, tính ở server để nhãn "Còn N ngày" không lệch khi hydrate
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
  // API chưa lọc theo mã trường: tìm theo tên rồi giữ đúng học bổng của trường này. Lỗi thì ẩn mục học bổng, không làm hỏng trang
  let scholarships: ScholarshipItem[] = [];
  try {
    const found = await searchScholarships({ search: university.name, deadlineFrom: today, pageSize: 50 });
    scholarships = found.items.filter((s) => s.universityCode === university.code);
  } catch {
    scholarships = [];
  }
  return <UniversityDetailView key={university.code} university={university} scholarships={scholarships} today={today} />;
}
