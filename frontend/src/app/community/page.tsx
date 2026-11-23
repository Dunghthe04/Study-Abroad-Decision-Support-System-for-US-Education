import type { Metadata } from "next";
import { CommunityChatView } from "@/components/community/CommunityChatView";

export const metadata: Metadata = { title: "Cộng đồng – USDSS" };

export default function CommunityPage() {
  return <CommunityChatView />;
}
