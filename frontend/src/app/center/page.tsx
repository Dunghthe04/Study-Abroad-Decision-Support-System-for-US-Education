import type { Metadata } from "next";
import { CenterDashboardView } from "@/components/center/CenterDashboardView";

export const metadata: Metadata = {
  title: "Tổng quan trung tâm – USAS",
  description: "Trang tổng quan trung tâm tư vấn du học đối tác USAS",
};

export default function CenterOverviewPage() {
  return <CenterDashboardView />;
}

