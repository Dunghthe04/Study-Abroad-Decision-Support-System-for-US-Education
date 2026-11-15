"use client";

// Ô mật khẩu có nút con mắt để hiện hoặc ẩn mật khẩu.
// Dùng ở form đăng nhập, đăng ký, đặt mật khẩu mới và đổi mật khẩu.
// Nút làm theo mẫu của Đức (CreateCenterAccountButton), nhưng vẫn nhận Tab để dùng được bằng bàn phím.

import { useState } from "react";
import { EyeIcon, EyeOffIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

type PasswordInputProps = Omit<React.ComponentProps<typeof Input>, "type">;

export function PasswordInput({ className, disabled, ...props }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="relative">
      <Input
        {...props}
        type={visible ? "text" : "password"}
        disabled={disabled}
        className={cn("pr-9", className)}
      />
      <button
        // type="button" để bấm nút không gửi form
        type="button"
        onClick={() => setVisible((v) => !v)}
        disabled={disabled}
        aria-label={visible ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
        aria-pressed={visible}
        className="absolute inset-y-0 right-0 flex w-9 cursor-pointer items-center justify-center rounded-r-lg text-muted-foreground outline-none hover:text-foreground focus-visible:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {visible ? <EyeOffIcon className="size-4" /> : <EyeIcon className="size-4" />}
      </button>
    </div>
  );
}
