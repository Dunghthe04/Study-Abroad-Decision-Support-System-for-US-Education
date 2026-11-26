import { redirect } from "next/navigation";

// Trang cũ đã gộp vào /costs ("Tra cứu chi phí", chế độ Bang); giữ đường dẫn để liên kết và dấu trang cũ vẫn mở được
export default function StateLivingCostsPage() {
  redirect("/costs?mode=state");
}
