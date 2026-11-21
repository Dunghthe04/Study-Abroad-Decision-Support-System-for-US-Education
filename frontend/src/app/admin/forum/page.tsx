import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Kiểm duyệt diễn đàn – Quản trị USDSS",
};

export default function AdminForumPage() {
  return <AdminSection href="/admin/forum" />;
}
