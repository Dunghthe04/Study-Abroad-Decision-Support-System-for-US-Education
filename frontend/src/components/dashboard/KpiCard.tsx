import type { ReactNode } from "react";
import Link from "next/link";
import { ArrowRightIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardAction, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";

type BadgeVariant = "brand" | "ok" | "warn" | "risk" | "reach" | "neutral";

interface KpiCardProps {
  title: string;
  icon: ReactNode;
  badge?: { text: string; variant?: BadgeVariant };
  /** Dòng giải thích cách tính, backend sinh sẵn */
  explanation?: string;
  action?: { label: string; href: string };
  children: ReactNode;
}

/** UC-94: một thẻ chỉ số (số chính + dòng cách tính + link sang trang chi tiết). */
export function KpiCard({ title, icon, badge, explanation, action, children }: KpiCardProps) {
  return (
    <Card className="h-full">
      <CardHeader className="border-b">
        <CardTitle className="flex items-center gap-2 text-base">
          <span className="text-muted-foreground [&>svg]:size-5" aria-hidden>
            {icon}
          </span>
          <h2>{title}</h2>
        </CardTitle>
        {badge && (
          <CardAction>
            <Badge variant={badge.variant ?? "neutral"}>{badge.text}</Badge>
          </CardAction>
        )}
      </CardHeader>

      <CardContent className="flex-1">{children}</CardContent>

      {(explanation || action) && (
        <CardFooter className="flex-col items-start gap-2">
          {explanation && <p className="text-xs leading-relaxed text-muted-foreground">{explanation}</p>}
          {action && (
            <Link
              href={action.href}
              className="inline-flex items-center gap-1 text-sm font-semibold text-brand underline-offset-4 hover:underline"
            >
              {action.label}
              <ArrowRightIcon className="size-3.5" aria-hidden />
            </Link>
          )}
        </CardFooter>
      )}
    </Card>
  );
}
