"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { OrdersTab } from "./OrdersTab";
import { PlansTab } from "./PlansTab";

/** [UC-79] Gói dịch vụ, [UC-132..134] đơn hàng. */
export function AdminPlansView() {
  return (
    <Tabs defaultValue="plans" className="flex min-h-0 flex-1 flex-col gap-4">
      <TabsList>
        <TabsTrigger value="plans">Gói</TabsTrigger>
        <TabsTrigger value="orders">Đơn hàng</TabsTrigger>
      </TabsList>
      <TabsContent value="plans" className="flex min-h-0 flex-col gap-4">
        <PlansTab />
      </TabsContent>
      <TabsContent value="orders" className="flex min-h-0 flex-col gap-4">
        <OrdersTab />
      </TabsContent>
    </Tabs>
  );
}
