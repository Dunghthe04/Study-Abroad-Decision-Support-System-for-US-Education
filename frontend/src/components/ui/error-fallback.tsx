"use client";

import React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button, buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export interface ErrorFallbackAction {
  label?: string;
  onClick?: () => void;
  href?: string;
}

export interface ErrorFallbackProps {
  title?: string;
  description?: React.ReactNode;
  action?: ErrorFallbackAction | null;
  secondaryAction?: ErrorFallbackAction | null;
  icon?: React.ReactNode;
  className?: string;
  reset?: () => void;
}

export function AlertDiamondIcon({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 128 128"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={cn("size-24 text-destructive", className)}
      aria-hidden="true"
    >
      <rect
        x="36"
        y="36"
        width="56"
        height="56"
        rx="12"
        transform="rotate(45 64 64)"
        stroke="currentColor"
        strokeWidth="3.5"
        strokeLinejoin="round"
      />
      <line
        x1="64"
        y1="44"
        x2="64"
        y2="72"
        stroke="currentColor"
        strokeWidth="4.5"
        strokeLinecap="round"
      />
      <circle cx="64" cy="85" r="3.5" fill="currentColor" />
    </svg>
  );
}

export function ErrorFallback({
  title = "Đã có lỗi xảy ra",
  description = "Đã xảy ra lỗi khi kết nối với máy chủ API. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau.",
  action,
  secondaryAction,
  icon,
  className,
  reset,
}: ErrorFallbackProps) {
  const router = useRouter();

  const handleDefaultRetry = () => {
    if (reset) {
      reset();
    } else {
      router.refresh();
    }
  };

  const resolvedAction: ErrorFallbackAction | null =
    action === undefined
      ? {
          label: "Thử lại",
          onClick: handleDefaultRetry,
        }
      : action;

  return (
    <section
      role="alert"
      className={cn(
        "flex min-h-[55vh] flex-1 flex-col items-center justify-center p-6 text-center animate-in fade-in-50 duration-200",
        className
      )}
    >
      <div className="mb-4 flex items-center justify-center">
        {icon ?? <AlertDiamondIcon />}
      </div>

      <div className="max-w-md space-y-2">
        <h2 className="font-heading text-xl font-semibold tracking-tight text-destructive sm:text-2xl">
          {title}
        </h2>
        {description && (
          <p className="text-sm leading-relaxed text-muted-foreground sm:text-base">
            {description}
          </p>
        )}
      </div>

      {(resolvedAction || secondaryAction) && (
        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          {resolvedAction &&
            (resolvedAction.href ? (
              <Link
                href={resolvedAction.href}
                className={cn(
                  buttonVariants({ variant: "outline" }),
                  "border-destructive text-destructive hover:bg-destructive/10 hover:text-destructive"
                )}
              >
                {resolvedAction.label ?? "Thử lại"}
              </Link>
            ) : (
              <Button
                type="button"
                variant="outline"
                onClick={resolvedAction.onClick ?? handleDefaultRetry}
                className="border-destructive text-destructive hover:bg-destructive/10 hover:text-destructive"
              >
                {resolvedAction.label ?? "Thử lại"}
              </Button>
            ))}

          {secondaryAction &&
            (secondaryAction.href ? (
              <Link
                href={secondaryAction.href}
                className={buttonVariants({ variant: "ghost" })}
              >
                {secondaryAction.label}
              </Link>
            ) : (
              <Button
                type="button"
                variant="ghost"
                onClick={secondaryAction.onClick}
              >
                {secondaryAction.label}
              </Button>
            ))}
        </div>
      )}
    </section>
  );
}
