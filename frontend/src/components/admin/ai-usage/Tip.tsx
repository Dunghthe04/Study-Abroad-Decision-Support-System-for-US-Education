"use client";

import { useCallback, useRef, useState, type FocusEvent, type PointerEvent, type ReactNode } from "react";

interface TipState {
  x: number;
  y: number;
  /** Con trỏ nằm nửa phải khung thì tooltip mở sang trái, để không tràn ra ngoài khung hẹp. */
  alignRight: boolean;
  content: ReactNode;
}

/**
 * Tooltip nhẹ cho các biểu đồ tự vẽ (thanh, ô lịch, thanh phân đoạn): mỗi mark tự có tooltip, không cần crosshair.
 * Gắn `ref` vào khung có position: relative, rồi `show(event, nội dung)` / `hide()` trên mark; vẽ `layer` trong khung đó.
 * Tooltip chỉ bổ sung: mọi giá trị đều có ở bảng của thẻ biểu đồ.
 */
export function useTip<T extends HTMLElement = HTMLDivElement>() {
  const ref = useRef<T>(null);
  const [tip, setTip] = useState<TipState | null>(null);

  const show = useCallback((event: PointerEvent | FocusEvent, content: ReactNode) => {
    const frame = ref.current?.getBoundingClientRect();
    if (!frame) return;
    let clientX: number;
    let clientY: number;
    if ("clientX" in event) {
      clientX = event.clientX;
      clientY = event.clientY;
    } else {
      const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
      clientX = box.left + box.width / 2;
      clientY = box.top;
    }
    const x = clientX - frame.left;
    setTip({ x, y: clientY - frame.top, alignRight: x > frame.width / 2, content });
  }, []);

  const hide = useCallback(() => setTip(null), []);

  const layer = tip ? (
    <div
      role="status"
      className="pointer-events-none absolute z-30 w-max max-w-64 rounded-md border border-line bg-surface px-2.5 py-2 text-xs shadow-md"
      style={{ left: tip.x, top: tip.y, transform: `translate(${tip.alignRight ? "-100%" : "0"}, calc(-100% - 10px))` }}
    >
      {tip.content}
    </div>
  ) : null;

  return { ref, show, hide, layer };
}

/** Một dòng trong tooltip: giá trị nổi bật, nhãn phụ. `color` là que nhỏ nhận diện chuỗi (không phải ô vuông). */
export function TipRow({ color, label, value }: { color?: string; label: string; value: string }) {
  return (
    <div className="flex items-center gap-2">
      {color ? <span aria-hidden className="h-0.5 w-3 shrink-0 rounded-full" style={{ background: color }} /> : null}
      <span className="text-ink-2">{label}</span>
      <span className="tabular ml-auto pl-3 font-semibold text-ink">{value}</span>
    </div>
  );
}
