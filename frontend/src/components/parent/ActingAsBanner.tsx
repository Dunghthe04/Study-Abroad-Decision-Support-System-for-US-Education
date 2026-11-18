"use client";

// 1.5b: thanh "Đang nhập cho …" dưới header khi phụ huynh đang nhập hồ sơ thay con managed.
// Không làm thay thì không vẽ gì. Không dùng sticky/fixed để không đè header, thanh mục trang trường hay nút trợ lý.

import { useState } from "react";
import { UserRoundPen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";

export function ActingAsBanner() {
  const { acting, stopActing } = useAuth();
  const [leaving, setLeaving] = useState(false);

  if (!acting) return null;

  const leave = async () => {
    setLeaving(true);
    try {
      await stopActing(); // thành công thì trang tải lại, không cần bỏ trạng thái leaving
    } catch (err) {
      setLeaving(false);
      toast.add({
        type: "error",
        title: "Chưa quay lại được tài khoản của bạn",
        description:
          err instanceof ApiError ? err.message : "Vui lòng thử lại.",
      });
    }
  };

  return (
    <div
      role="status"
      className="border-b border-line bg-warn-wash print:hidden"
    >
      <div className="mx-auto flex max-w-[1560px] items-center gap-3 px-4 py-2 text-sm text-foreground sm:px-8 lg:px-12">
        <UserRoundPen
          className="size-4 shrink-0 text-warn"
          aria-hidden="true"
        />
        <p className="min-w-0 flex-1 break-words">
          Đang nhập hồ sơ cho:{" "}
          <span className="font-semibold">{acting.childName}</span>
        </p>
        <Button
          variant="outline"
          size="sm"
          className="shrink-0"
          aria-label="Quay lại tài khoản của tôi"
          onClick={leave}
          disabled={leaving}
        >
          {leaving ? (
            "Đang quay lại…"
          ) : (
            <>
              <span className="sm:hidden">Quay lại</span>
              <span className="hidden sm:inline">
                Quay lại tài khoản của tôi
              </span>
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
