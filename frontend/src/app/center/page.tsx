import type { Metadata } from "next";
import { CenterDashboardView } from "@/components/center/CenterDashboardView";

export const metadata: Metadata = {
  title: "Tổng quan trung tâm – USDSS",
  description: "Trang tổng quan trung tâm tư vấn du học đối tác USDSS",
};

export default function CenterOverviewPage() {
  return <CenterDashboardView />;
}

