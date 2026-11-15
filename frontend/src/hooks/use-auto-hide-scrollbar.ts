"use client";

import { useEffect } from "react";

/**
 * Tự động thêm class `is-scrolling` vào phần tử đang cuộn
 * và tự động xóa sau 900ms khi dừng cuộn để ẩn thanh cuộn.
 * Áp dụng trên toàn bộ ứng dụng (chế độ capture event).
 */
export function useAutoHideScrollbar() {
  useEffect(() => {
    const timeouts = new WeakMap<Element, ReturnType<typeof setTimeout>>();
    let docTimeout: ReturnType<typeof setTimeout> | null = null;

    const onScroll = (e: Event) => {
      const target = e.target as HTMLElement | Document | null;
      if (!target) return;

      if (target === document || target === document.documentElement || target === document.body) {
        document.documentElement.classList.add("is-scrolling");
        document.body.classList.add("is-scrolling");
        if (docTimeout) clearTimeout(docTimeout);
        docTimeout = setTimeout(() => {
          document.documentElement.classList.remove("is-scrolling");
          document.body.classList.remove("is-scrolling");
          docTimeout = null;
        }, 900);
        return;
      }

      if (target instanceof Element) {
        target.classList.add("is-scrolling");
        const existing = timeouts.get(target);
        if (existing) clearTimeout(existing);
        const timer = setTimeout(() => {
          target.classList.remove("is-scrolling");
          timeouts.delete(target);
        }, 900);
        timeouts.set(target, timer);
      }
    };

    window.addEventListener("scroll", onScroll, { capture: true, passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll, { capture: true });
      if (docTimeout) clearTimeout(docTimeout);
    };
  }, []);
}
