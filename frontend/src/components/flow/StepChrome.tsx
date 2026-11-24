"use client";

import { createContext, useContext, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { ArrowLeftIcon, ChevronDownIcon, ChevronUpIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { setHeaderHidden, useHeaderHidden } from "@/lib/header-visibility";
import { cn } from "@/lib/utils";

/** Khung chung của bảng bên phải: chỗ gắn thanh đầu / thanh cuối (nằm ngoài vùng cuộn) và thông báo lỗi / đã lưu */
interface FlowChrome {
  headerSlot: HTMLElement | null;
  navSlot: HTMLElement | null;
  /** Tên bước, trùng với tên ở thanh bước bên trái */
  title?: string;
  /** Thanh bước thu gọn đang hiện (khung chat mở): nút ẩn/hiện thanh trên cùng nằm ở thanh đó */
  compactBar?: boolean;
  notice?: ReactNode;
}

const FlowChromeContext = createContext<FlowChrome | null>(null);
export const FlowChromeProvider = FlowChromeContext.Provider;

/** Gắn vào chỗ Wizard dành sẵn; chưa có chỗ (lần vẽ đầu) thì chưa hiện */
function InSlot({ slot, children }: { slot: HTMLElement | null | undefined; children: ReactNode }) {
  return slot ? createPortal(children, slot) : null;
}

/** Thanh đầu bước, đứng yên ngoài vùng cuộn: tiêu đề (nhận focus khi đổi bước), nút ẩn/hiện thanh trên cùng, rồi thông báo lỗi / đã lưu.
 *  Lời dẫn của bước đặt ở FormBody (lede). */
export function StepHeader({ title }: { title: string }) {
  const chrome = useContext(FlowChromeContext);
  return (
    <InSlot slot={chrome?.headerSlot}>
      <header className="border-b border-line bg-(--surface-2) px-5 py-3 sm:px-8">
        <div className="mx-auto flex w-full max-w-(--flow-form-w) items-center gap-4">
          <h1 data-step-heading tabIndex={-1} className="min-w-0 flex-1 truncate text-lg font-semibold text-ink outline-none">
            {chrome?.title ?? title}
          </h1>
          {/* Màn hẹp (hoặc khung chat mở) thì nút này nằm ở thanh bước thu gọn phía trên */}
          <HeaderToggle className={chrome?.compactBar ? "hidden" : "hidden lg:inline-flex"} />
        </div>
      </header>
      {chrome?.notice && (
        <div className="px-5 pt-4 sm:px-8">
          <div className="mx-auto w-full max-w-(--flow-form-w)">{chrome.notice}</div>
        </div>
      )}
    </InSlot>
  );
}

/** Thanh cuối bước, đứng yên ngoài vùng cuộn: "Quay lại" (bước 1 không có) bên trái, nút chính (children) bên phải, thẳng hai mép cột câu hỏi */
export function StepNav({ onPrev, children }: { onPrev?: () => void; children: ReactNode }) {
  const chrome = useContext(FlowChromeContext);
  return (
    <InSlot slot={chrome?.navSlot}>
      <div className="border-t border-line bg-(--surface-2) px-5 py-3 sm:px-8">
        <div className="mx-auto flex w-full max-w-(--flow-form-w) items-center justify-between gap-3">
          {onPrev && (
            <Button type="button" variant="outline" size="lg" onClick={onPrev} className="gap-1.5">
              <ArrowLeftIcon className="size-4" />
              Quay lại
            </Button>
          )}
          <div className="ml-auto flex items-center gap-3">{children}</div>
        </div>
      </div>
    </InSlot>
  );
}

export { CHOICE_CARD } from "./FormKit";

/** Ẩn/hiện thanh điều hướng trên cùng của trang để biểu mẫu chiếm trọn màn hình */
export function HeaderToggle({ className, tone = "default" }: { className?: string; tone?: "default" | "inverse" }) {
  const hidden = useHeaderHidden();
  const label = hidden ? "Hiện thanh trên cùng" : "Ẩn thanh trên cùng";
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      onClick={() => setHeaderHidden(!hidden)}
      aria-label={label}
      aria-expanded={!hidden}
      title={label}
      className={cn("shrink-0", tone === "inverse" && "text-white hover:bg-white/10 hover:text-white", className)}
    >
      {hidden ? <ChevronDownIcon className="size-4" /> : <ChevronUpIcon className="size-4" />}
    </Button>
  );
}
