import React from "react";
import Link from "next/link";
import { ArrowUpRight } from "lucide-react";

interface KpiCardProps {
  title: string;
  icon: React.ReactNode;
  badge?: {
    text: string;
    variant?: "success" | "warning" | "info" | "neutral";
  };
  explanation?: string;
  action?: {
    label: string;
    href: string;
  };
  children: React.ReactNode;
  className?: string;
}

export function KpiCard({
  title,
  icon,
  badge,
  explanation,
  action,
  children,
  className = "",
}: KpiCardProps) {
  const badgeClasses = {
    success: "bg-emerald-50 text-emerald-700 border-emerald-200",
    warning: "bg-amber-50 text-amber-700 border-amber-200",
    info: "bg-blue-50 text-blue-700 border-blue-200",
    neutral: "bg-slate-50 text-slate-700 border-slate-200",
  }[badge?.variant || "neutral"];

  return (
    <div
      className={`rounded-2xl border border-slate-200/80 bg-white p-5 shadow-xs transition hover:shadow-md flex flex-col justify-between ${className}`}
    >
      <div>
        <div className="flex items-center justify-between gap-2 pb-3 border-b border-slate-100">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100/80 text-slate-700">
              {icon}
            </div>
            <h3 className="font-semibold text-slate-900 text-sm">{title}</h3>
          </div>
          {badge && (
            <span
              className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${badgeClasses}`}
            >
              {badge.text}
            </span>
          )}
        </div>

        <div className="py-4">{children}</div>
      </div>

      <div className="pt-3 border-t border-slate-100">
        {explanation && (
          <p className="text-xs text-slate-500 mb-3 leading-relaxed">
            {explanation}
          </p>
        )}
        {action && (
          <Link
            href={action.href}
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-blue-600 hover:text-blue-800 transition"
          >
            <span>{action.label}</span>
            <ArrowUpRight className="h-3.5 w-3.5" />
          </Link>
        )}
      </div>
    </div>
  );
}
