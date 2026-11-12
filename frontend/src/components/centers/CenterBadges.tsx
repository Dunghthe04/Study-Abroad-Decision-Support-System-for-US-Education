import { CheckCircle2Icon, ClockIcon, EyeOffIcon, ShieldCheckIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export function CenterTypeBadge({
  centerType,
  compact = false,
}: {
  centerType?: string | null;
  compact?: boolean;
}) {
  switch (centerType) {
    case "hybrid":
      return (
        <Badge variant="reach">
          {compact ? "Toàn diện" : "Toàn diện (Du học & Luyện thi)"}
        </Badge>
      );
    case "english_training":
    case "test_prep":
      return <Badge variant="warn">Luyện thi Chứng chỉ</Badge>;
    default:
      return <Badge variant="brand">Tư vấn Du học</Badge>;
  }
}

export function CenterVerificationBadge({
  verified,
  label,
}: {
  verified: boolean;
  label?: string;
}) {
  if (verified) {
    return (
      <Badge variant="ok" className="gap-1">
        <CheckCircle2Icon className="size-3" />
        {label ?? "Đã xác minh"}
      </Badge>
    );
  }
  return (
    <Badge variant="warn" className="gap-1">
      <ClockIcon className="size-3" />
      {label ?? "Chờ xác minh"}
    </Badge>
  );
}

export function CenterVisibilityBadge({ active }: { active: boolean }) {
  if (active) return null;
  return (
    <Badge variant="risk" className="gap-1">
      <EyeOffIcon className="size-3" />
      Đang ẩn
    </Badge>
  );
}

export function CenterLegalModeBadge({ mode }: { mode: "licensed" | "notified" }) {
  if (mode === "licensed") {
    return (
      <Badge variant="ok" className="gap-1">
        <ShieldCheckIcon className="size-3" />
        Cấp phép Sở GD&amp;ĐT
      </Badge>
    );
  }
  return <Badge variant="neutral">Thông báo hoạt động Sở GD</Badge>;
}

