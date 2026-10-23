"use client";

import { ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAssistant } from "./AssistantProvider";

/** Nút mở khung trợ lý, dùng được trong server component */
export function OpenAssistantButton({
  question,
  variant = "outline",
  size = "lg",
  className,
  children = "Hỏi trợ lý",
}: {
  question?: string;
  variant?: "default" | "outline" | "secondary" | "ghost";
  size?: "default" | "sm" | "lg";
  className?: string;
  children?: React.ReactNode;
}) {
  const { openPanel } = useAssistant();
  return (
    <Button type="button" variant={variant} size={size} className={className} onClick={() => openPanel(question)}>
      {children}
      <ArrowRightIcon className="ml-1.5 size-4" />
    </Button>
  );
}
