"use client";

import { CheckIcon, XIcon } from "lucide-react";
import { cn } from "@/lib/utils";

// Đánh giá độ mạnh mật khẩu (1..4)
function getPasswordStrength(pass: string): {
  score: number;
  label: string;
  color: string;
  barColor: string;
} {
  if (!pass) {
    return { score: 0, label: "", color: "", barColor: "" };
  }

  let score = 0;
  if (pass.length >= 8) score++;
  if (pass.length >= 10 && /[A-Z]/.test(pass) && /[a-z]/.test(pass)) score++;
  if (/\d/.test(pass)) score++;
  if (/[^a-zA-Z0-9]/.test(pass)) score++;

  const finalScore = Math.min(4, Math.max(1, score));

  switch (finalScore) {
    case 1:
      return { score: 1, label: "Yếu", color: "text-destructive", barColor: "bg-destructive" };
    case 2:
      return { score: 2, label: "Trung bình", color: "text-amber-500", barColor: "bg-amber-500" };
    case 3:
      return { score: 3, label: "Khá mạnh", color: "text-blue-500", barColor: "bg-blue-500" };
    case 4:
    default:
      return {
        score: 4,
        label: "Rất mạnh",
        color: "text-emerald-600 dark:text-emerald-400",
        barColor: "bg-emerald-600 dark:bg-emerald-500",
      };
  }
}

/** [UC-01] Thanh độ mạnh mật khẩu dùng chung cho các form đăng ký; ẩn khi chưa nhập. */
export function PasswordStrengthMeter({ password }: { password: string }) {
  if (!password) return null;
  const strength = getPasswordStrength(password);
  return (
    <div className="space-y-1.5 pt-1">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">Độ mạnh mật khẩu:</span>
        <span className={cn("font-medium", strength.color)}>{strength.label}</span>
      </div>
      <div className="grid grid-cols-4 gap-1.5" aria-hidden="true">
        {[1, 2, 3, 4].map((level) => (
          <div
            key={level}
            className={cn(
              "h-1.5 rounded-full transition-all duration-300",
              strength.score >= level ? strength.barColor : "bg-muted"
            )}
          />
        ))}
      </div>
    </div>
  );
}

/** [UC-01] Báo "Mật khẩu khớp" / "Mật khẩu chưa khớp" dưới ô xác nhận; id dùng cho aria-describedby. */
export function PasswordMatchStatus({ matched, id }: { matched: boolean; id: string }) {
  return matched ? (
    <div
      id={id}
      role="status"
      className="flex items-center gap-1.5 pt-1 text-xs font-medium text-emerald-600 dark:text-emerald-400"
    >
      <CheckIcon className="size-4 shrink-0" aria-hidden="true" />
      <span>Mật khẩu khớp</span>
    </div>
  ) : (
    <div id={id} role="alert" className="flex items-center gap-1.5 pt-1 text-xs font-medium text-destructive">
      <XIcon className="size-4 shrink-0" aria-hidden="true" />
      <span>Mật khẩu chưa khớp</span>
    </div>
  );
}
