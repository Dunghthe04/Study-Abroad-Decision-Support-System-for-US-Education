"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";

const TABS = [
  { href: "/costs", label: "Ước tính chi phí trường" },
  { href: "/universities/compare", label: "So sánh chi phí trường" },
  { href: "/universities/living-costs", label: "Chi phí sinh hoạt theo bang" },
];

export function CostSectionNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Công cụ chi phí" className="flex flex-wrap gap-2 border-b border-line pb-3">
      {TABS.map((tab) => {
        const active = pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? "page" : undefined}
            className={cn(
              "rounded-lg px-3.5 py-1.5 text-sm font-medium transition-colors",
              active
                ? "bg-brand text-white"
                : "border border-line bg-surface text-ink-2 hover:border-brand/40 hover:text-ink",
            )}
          >
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}
