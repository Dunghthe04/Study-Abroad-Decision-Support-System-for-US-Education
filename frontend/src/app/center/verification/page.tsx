import type { Metadata } from "next";
import { CenterVerificationView } from "@/components/center/CenterVerificationView";

export const metadata: Metadata = {
  title: "Xác minh pháp lý trung tâm – USAS",
  description: "Quy trình xác minh thông tin doanh nghiệp và nộp hồ sơ pháp lý đối tác USAS",
};

export default function CenterVerificationPage() {
  return <CenterVerificationView />;
}

