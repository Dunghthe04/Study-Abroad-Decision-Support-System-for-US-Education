import type { Metadata } from "next";
import { Inter, Source_Serif_4 } from "next/font/google";
import { AuthProvider } from "@/contexts/AuthContext";
import { PolicyGate } from "@/components/policy/PolicyGate";
import { AppShell } from "@/components/assistant/AppShell";
import { RealtimeProvider } from "@/components/realtime/RealtimeProvider";
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
  title: "USDSS – Du học Mỹ",
  description: "Hệ thống hỗ trợ ra quyết định du học Mỹ: tư vấn AI, tra cứu thông tin và tìm trung tâm.",
  icons: {
    icon: "/icons/USDSS.png",
    shortcut: "/icons/USDSS.png",
    apple: "/icons/USDSS.png",
  },
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="vi" className={cn("h-full antialiased font-sans", inter.variable, sourceSerif.variable)} suppressHydrationWarning>
      <head>
        {/* Áp dụng theme sớm trước khi vẽ để tránh nháy sáng/tối */}
        <script
          dangerouslySetInnerHTML={{
            __html: `try{var t=localStorage.getItem("theme");var d=t==="dark"||(t==="auto"&&matchMedia("(prefers-color-scheme:dark)").matches);document.documentElement.classList.toggle("dark",d)}catch(e){}`,
          }}
        />
      </head>
      <body className="flex min-h-full flex-col bg-background text-foreground" suppressHydrationWarning>
        <AuthProvider>
          <RealtimeProvider />
          <TooltipProvider>
            <AssistantProvider>
              <PolicyGate>
                <AppShell>{children}</AppShell>
              </PolicyGate>
            </AssistantProvider>
          </TooltipProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
