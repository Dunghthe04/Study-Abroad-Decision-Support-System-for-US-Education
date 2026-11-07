"use client";

import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { MODERATOR_NAV } from "@/lib/moderator-nav";

export function ModeratorOverview() {
  const { user } = useAuth();
  const sections = MODERATOR_NAV.filter((item) => item.href !== "/moderator");

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-h1">Khu vực Kiểm duyệt & Vận hành</h1>
        <p className="text-body-s text-muted-foreground">
          Xin chào <strong>{user?.fullName}</strong>. Đây là không gian làm việc chuyên biệt cho Moderator quản lý danh bạ trung tâm, duyệt tri thức và kiểm duyệt diễn đàn.
        </p>
      </header>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {sections.map((item) => {
          const Icon = item.icon;
          return (
            <Link key={item.href} href={item.href} className="group">
              <Card className="h-full transition-shadow group-hover:ring-2 group-hover:ring-brand/40">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Icon className="size-4 text-brand" />
                    <h2>{item.label}</h2>
                  </CardTitle>
                  <CardDescription>{item.description}</CardDescription>
                  <div className="flex flex-wrap gap-1.5 pt-1">
                    {item.useCases.map((code) => (
                      <Badge key={code} variant="outline">
                        {code}
                      </Badge>
                    ))}
                  </div>
                </CardHeader>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
