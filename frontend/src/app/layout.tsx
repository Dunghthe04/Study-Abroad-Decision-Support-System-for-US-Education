import type { Metadata } from "next";
import { Inter, Source_Serif_4 } from "next/font/google";
import { AuthProvider } from "@/contexts/AuthContext";
import { AppShell } from "@/components/assistant/AppShell";
import { AssistantProvider } from "@/components/assistant/AssistantProvider";
import { TooltipProvider } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import "./globals.css";

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin", "vietnamese"],
});

const sourceSerif = Source_Serif_4({
  variable: "--font-serif",
  subsets: ["latin", "vietnamese"],
});

export const metadata: Metadata = {
  title: "USAS – Study Abroad Decision Support System",
  description: "Hệ thống hỗ trợ ra quyết định du học Mỹ: tư vấn AI, tra cứu thông tin và tìm trung tâm.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="vi" className={cn("h-full antialiased font-sans", inter.variable, sourceSerif.variable)} suppressHydrationWarning>
      <body className="flex min-h-full flex-col bg-background text-foreground" suppressHydrationWarning>
        <AuthProvider>
          <TooltipProvider>
            <AssistantProvider>
              <AppShell>{children}</AppShell>
            </AssistantProvider>
          </TooltipProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
