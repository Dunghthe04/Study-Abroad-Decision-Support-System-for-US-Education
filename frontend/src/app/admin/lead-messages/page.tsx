import { redirect } from "next/navigation";

/** Trang cũ của Moderator đã bỏ; chuyển sang trang báo cáo của Admin. */
export default function LegacyAdminLeadMessagesPage() {
  redirect("/admin/lead-message-reports");
}
