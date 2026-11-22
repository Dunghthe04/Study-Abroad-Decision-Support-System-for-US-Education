import type { Metadata } from "next";
import { AdminForumView } from "@/components/admin/forum/AdminForumView";

export const metadata: Metadata = {
  title: "Kiểm duyệt diễn đàn – Quản trị USDSS",
};

export default function AdminForumPage() {
  return <AdminForumView />;
}
