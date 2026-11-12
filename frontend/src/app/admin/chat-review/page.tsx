import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { ChatReviewView } from "@/components/admin/chat-review/ChatReviewView";

export const metadata: Metadata = {
  title: "Câu cần xem – Quản trị USAS",
};

export default function AdminChatReviewPage() {
  return (
    <AdminSection href="/admin/chat-review">
      <ChatReviewView />
    </AdminSection>
  );
}
