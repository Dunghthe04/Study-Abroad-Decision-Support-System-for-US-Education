import type { Metadata } from "next";
import { Inter, Geist } from "next/font/google";
import { AuthProvider } from "@/contexts/AuthContext";
import { SiteHeader } from "@/components/layout/SiteHeader";
import "./globals.css";
import { cn } from "@/lib/utils";

const geist = Geist({subsets:['latin'],variable:'--font-sans'});

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin", "vietnamese"],
});

export const metadata: Metadata = {
  title: "USAS – Study Abroad Decision Support System",
  description: "Hệ thống hỗ trợ ra quyết định du học Mỹ: tư vấn AI, tra cứu thông tin và tìm trung tâm.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="vi" className={cn("h-full", "antialiased", inter.variable, "font-sans", geist.variable)} suppressHydrationWarning>
      <body className="flex min-h-full flex-col bg-slate-50 text-slate-900" suppressHydrationWarning>
        <AuthProvider>
          <SiteHeader />
          <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">{children}</main>
        </AuthProvider>
      </body>
    </html>
  );
}
