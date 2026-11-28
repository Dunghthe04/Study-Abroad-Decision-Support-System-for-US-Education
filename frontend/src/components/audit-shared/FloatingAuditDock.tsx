"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  BugIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  Minimize2Icon,
  RotateCcwIcon,
} from "lucide-react";

interface FloatingAuditDockProps {
  title: string;
  route: string;
  caseLabel: string;
  pageCounter: string;
  ariaLabel: string;
  onPrevPage: () => void;
  onNextPage: () => void;
  onReset: () => void;
  children: React.ReactNode;
}

const EDGE = 8;

function clampPos(x: number, y: number, w: number, h: number) {
  const maxX = Math.max(EDGE, window.innerWidth - w - EDGE);
  const maxY = Math.max(EDGE, window.innerHeight - h - EDGE);
  return {
    x: Math.min(Math.max(EDGE, x), maxX),
    y: Math.min(Math.max(EDGE, y), maxY),
  };
}

/**
 * Draggable floating audit window that collapses down to a single circular icon button.
 */
export function FloatingAuditDock({
  title,
  route,
  caseLabel,
  pageCounter,
  ariaLabel,
  onPrevPage,
  onNextPage,
  onReset,
  children,
}: FloatingAuditDockProps) {
  const [open, setOpen] = useState(true);
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null);
  const dockRef = useRef<HTMLElement | null>(null);
  const movedRef = useRef(false);

  // Keep dock inside viewport when toggling open/close or resizing
  useEffect(() => {
    const syncClamp = () => {
      const el = dockRef.current;
      if (!el || !pos) return;
      const rect = el.getBoundingClientRect();
      const clamped = clampPos(pos.x, pos.y, rect.width, rect.height);
      if (clamped.x !== pos.x || clamped.y !== pos.y) setPos(clamped);
    };
    const id = requestAnimationFrame(syncClamp);
    window.addEventListener("resize", syncClamp);
    return () => {
      cancelAnimationFrame(id);
      window.removeEventListener("resize", syncClamp);
    };
  }, [open, pos]);

  const startDrag = (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    const el = dockRef.current;
    if (!el) return;

    const rect = el.getBoundingClientRect();
    const startX = e.clientX;
    const startY = e.clientY;
    const originX = rect.left;
    const originY = rect.top;
    movedRef.current = false;

    const onMove = (ev: PointerEvent) => {
      const dx = ev.clientX - startX;
      const dy = ev.clientY - startY;
      if (!movedRef.current && Math.hypot(dx, dy) > 4) {
        movedRef.current = true;
      }
      if (!movedRef.current) return;
      const r = dockRef.current?.getBoundingClientRect() ?? rect;
      setPos(clampPos(originX + dx, originY + dy, r.width, r.height));
    };

    const onUp = () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
    };

    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
  };

  return (
    <aside
      ref={dockRef}
      aria-label={ariaLabel}
      style={pos ? { left: pos.x, top: pos.y } : undefined}
      className={
        pos
          ? "fixed z-[110] max-w-[calc(100vw-1rem)]"
          : "fixed bottom-4 left-4 z-[110] max-w-[calc(100vw-2rem)]"
      }
    >
      {!open ? (
        <button
          type="button"
          onPointerDown={startDrag}
          onClick={() => {
            if (movedRef.current) return;
            setOpen(true);
          }}
          title={`${title} • ${route} (${caseLabel}) — Bấm để mở, kéo để di chuyển`}
          aria-label={`Mở ${title}`}
          className="relative flex size-11 cursor-grab items-center justify-center rounded-full border border-white/15 bg-ink text-white shadow-xl transition-transform hover:scale-105 active:cursor-grabbing focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-brand/50 touch-none"
        >
          <BugIcon className="h-5 w-5 text-amber-300" />
        </button>
      ) : (
        <div className="w-[min(92vw,420px)] max-h-[82vh] overflow-y-auto rounded-2xl border border-line bg-surface/95 p-3.5 text-xs text-ink shadow-2xl backdrop-blur-md">
          <div
            onPointerDown={startDrag}
            className="flex cursor-grab items-center justify-between gap-2 border-b border-line pb-2.5 select-none active:cursor-grabbing touch-none"
            title="Kéo để di chuyển cửa sổ"
          >
            <div className="flex items-center gap-2 min-w-0">
              <span className="inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-brand text-white">
                <BugIcon className="h-3.5 w-3.5" />
              </span>
              <div className="min-w-0">
                <div className="flex items-center gap-1.5">
                  <span className="font-semibold text-ink">{title}</span>
                  <span className="rounded bg-surface-2 px-1.5 py-0.5 font-mono text-[10px] text-ink-2">
                    {pageCounter}
                  </span>
                </div>
                <p className="truncate font-mono text-[11px] text-ink-3">{route}</p>
              </div>
            </div>

            <div
              className="flex items-center gap-1 shrink-0"
              onPointerDown={(e) => e.stopPropagation()}
            >
              <button
                type="button"
                onClick={onPrevPage}
                className="rounded-lg border border-line p-1.5 text-ink-2 hover:bg-surface-2 hover:text-ink"
                title="Trang trước (Alt + ←)"
                aria-label="Trang trước"
              >
                <ChevronLeftIcon className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onClick={onNextPage}
                className="rounded-lg border border-line p-1.5 text-ink-2 hover:bg-surface-2 hover:text-ink"
                title="Trang tiếp (Alt + →)"
                aria-label="Trang tiếp"
              >
                <ChevronRightIcon className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onClick={onReset}
                className="rounded-lg border border-line p-1.5 text-ink-2 hover:bg-surface-2 hover:text-ink"
                title="Reset state trang hiện tại"
                aria-label="Reset state"
              >
                <RotateCcwIcon className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-lg border border-line p-1.5 text-ink-2 hover:bg-surface-2 hover:text-ink"
                title="Thu gọn thành icon"
                aria-label="Thu gọn thành icon"
              >
                <Minimize2Icon className="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          {children}
        </div>
      )}
    </aside>
  );
}
