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
import { adminNavForRole } from "@/lib/admin-nav";

export function AdminOverview() {
  const { user } = useAuth();
  const isModerator = user?.role === "moderator";
  const sections = adminNavForRole(user?.role).filter(
    (item) => item.href !== "/admin",
  );

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-h1">{isModerator ? "Khu vực Kiểm duyệt & Vận hành" : "Trang quản trị"}</h1>
        <p className="text-body-s text-muted-foreground">
          Xin chào <strong>{user?.fullName}</strong>. {isModerator ? "Chọn một phân hệ kiểm duyệt để bắt đầu làm việc." : "Chọn một khu vực quản trị để bắt đầu."}
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
