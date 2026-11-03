import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { findAdminNavItem } from "@/lib/admin-nav";

interface AdminSectionProps {
  href: string;
  children?: React.ReactNode;
}

export function AdminSection({ href, children }: AdminSectionProps) {
  const item = findAdminNavItem(href);
  if (!item) {
    return null;
  }

  return (
    <ProtectedRoute
      allowedRoles={item.roles}
      deniedMessage={
        <>
          Mục <strong>{item.label}</strong> chỉ dành cho tài khoản quản trị
          viên.
        </>
      }
    >
      {children ?? (
        <Card>
          <CardHeader>
            <CardTitle>
              <h1 className="text-h2">{item.label}</h1>
            </CardTitle>
            <CardDescription>
              {item.description}. Trang này đang được xây dựng, các chức năng dự
              kiến:
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {item.useCases.map((code) => (
              <Badge key={code} variant="outline">
                {code}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}
    </ProtectedRoute>
  );
}
