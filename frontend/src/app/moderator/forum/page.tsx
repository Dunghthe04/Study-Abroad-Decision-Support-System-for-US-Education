import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Kiểm duyệt diễn đàn – Moderator USAS",
  description: "Nội dung bị báo cáo, gỡ bài, hạn chế người dùng, gỡ đánh giá và tin nhắn",
};

export default function ModeratorForumPage() {
  return <AdminSection href="/admin/forum" />;
}
