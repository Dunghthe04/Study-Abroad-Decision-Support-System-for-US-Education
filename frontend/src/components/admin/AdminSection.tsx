import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import {
  Card,
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
    <ProtectedRoute allowedRoles={item.roles}>
      {children ?? (
        <Card>
          <CardHeader>
            <CardTitle>
              <h1 className="text-h2">{item.label}</h1>
            </CardTitle>
            <CardDescription>{item.description}</CardDescription>
          </CardHeader>
        </Card>
      )}
    </ProtectedRoute>
  );
}
