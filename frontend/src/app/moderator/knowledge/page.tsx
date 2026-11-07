import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Tri thức tư vấn – Moderator USAS",
  description: "Duyệt hoặc từ chối dòng tri thức và xem câu hỏi chưa trả lời",
};

export default function ModeratorKnowledgePage() {
  return <AdminSection href="/admin/knowledge" />;
}
