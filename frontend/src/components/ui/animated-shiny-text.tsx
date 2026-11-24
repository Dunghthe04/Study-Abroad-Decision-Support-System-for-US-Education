"use client";

import type { ComponentPropsWithoutRef, CSSProperties } from "react";
import { cn } from "@/lib/utils";

export interface AnimatedShinyTextProps extends ComponentPropsWithoutRef<"span"> {
  shimmerWidth?: number;
}

export function AnimatedShinyText({
  children,
  className,
  shimmerWidth = 100,
  style,
  ...props
}: AnimatedShinyTextProps) {
  return (
    <span
      style={
        {
          "--shiny-width": `${shimmerWidth}px`,
          ...style,
        } as CSSProperties
      }
      className={cn(
        "animate-shiny-text bg-clip-text bg-no-repeat [background-position:0_0] [background-size:var(--shiny-width)_100%] [transition:background-position_1s_cubic-bezier(.6,.6,0,1)_infinite]",
        "bg-gradient-to-r from-transparent via-ink/85 via-50% to-transparent text-ink-2/70",
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}
