"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAssistant } from "./AssistantProvider";

export function OpenAssistantRedirect() {
  const router = useRouter();
  const { openPanel } = useAssistant();

  useEffect(() => {
    openPanel();
    router.replace("/");
  }, [openPanel, router]);

  return null;
}
