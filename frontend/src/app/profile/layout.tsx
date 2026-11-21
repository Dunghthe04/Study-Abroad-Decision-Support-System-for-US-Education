import type { ReactNode } from "react";
import { LinkedParentRedirect } from "@/components/parent/LinkedParentRedirect";

// 1f: phụ huynh đã liên kết không có hồ sơ riêng, mở trang này thì chuyển sang "Con của tôi"
export default function Layout({ children }: { children: ReactNode }) {
  return <LinkedParentRedirect tab="overview">{children}</LinkedParentRedirect>;
}
