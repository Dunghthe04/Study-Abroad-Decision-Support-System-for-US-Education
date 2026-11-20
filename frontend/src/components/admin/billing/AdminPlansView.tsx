"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PlansTab } from "./PlansTab";

/** [UC-79] Gói dịch vụ. */
export function AdminPlansView() {
  return (
    <Tabs defaultValue="plans" className="flex min-h-0 flex-1 flex-col gap-4">
      <TabsList>
        <TabsTrigger value="plans">Gói</TabsTrigger>
      </TabsList>
      <TabsContent value="plans" className="flex min-h-0 flex-col gap-4">
        <PlansTab />
      </TabsContent>
    </Tabs>
  );
}
