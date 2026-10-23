import { OpenAssistantRedirect } from "@/components/assistant/OpenAssistantRedirect";

export const metadata = { title: "Trợ lý USAS" };

// Trang chat cũ: giờ trợ lý nằm ở khung bên phải mọi trang → mở khung rồi về trang chủ
export default function AdvisorPage() {
  return <OpenAssistantRedirect />;
}
