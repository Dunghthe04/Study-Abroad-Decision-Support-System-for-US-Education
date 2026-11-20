"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Building2Icon, HomeIcon, LogOutIcon, UserIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";

const ROLE_LABELS: Record<string, string> = {
  admin: "Quản trị viên",
  moderator: "Moderator",
};

export function AdminUserMenu() {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();

  const handleLogout = async () => {
    await logout();
    router.push("/login");
  };

  if (isLoading) {
    return <Skeleton className="h-10 w-full" />;
  }

  if (!user) {
    return null;
  }

  const roleLabel = ROLE_LABELS[user.role] ?? user.role;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors hover:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none">
        <span
          aria-hidden="true"
          className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-border bg-surface text-muted-foreground"
        >
          <Building2Icon className="size-4" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-medium text-foreground">
            {user.fullName}
          </span>
          <span className="block truncate text-xs text-muted-foreground">
            {roleLabel}
          </span>
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="min-w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex flex-col gap-0.5">
            <span className="truncate text-foreground">{user.fullName}</span>
            <span className="truncate text-xs font-normal text-muted-foreground">
              {user.email}
            </span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem render={<Link href="/?view=public" />}>
          <HomeIcon />
          Xem trang chủ công khai
        </DropdownMenuItem>
        <DropdownMenuItem render={<Link href="/account" />}>
          <UserIcon />
          Hồ sơ cá nhân
        </DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onClick={handleLogout}>
          <LogOutIcon />
          Đăng xuất
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
