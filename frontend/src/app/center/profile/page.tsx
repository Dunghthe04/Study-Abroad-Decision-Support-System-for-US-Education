import type { Metadata } from "next";
import { CenterProfileView } from "@/components/center/CenterProfileView";

export const metadata: Metadata = {
  title: "Hồ sơ công bố trung tâm – USAS",
  description: "Chỉnh sửa thông tin giới thiệu, dịch vụ và mức phí trung tâm tư vấn đối tác",
};

export default function CenterProfilePage() {
  return <CenterProfileView />;
}

