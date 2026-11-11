"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils";

type Layer = {
  level: number;
  name: string;
  title: string;
  desc: string;
  label: string;
};

// Thứ tự từ trên xuống dưới, khớp với hình
const LAYERS: Layer[] = [
  {
    level: 5,
    name: "Con người",
    title: "Người thật làm phần việc cần người",
    desc: "Bài luận, hồ sơ và visa cần người đồng hành, nên hệ thống chuyển sang trung tâm bạn chọn thay vì tự làm.",
    label: "Chuyển sang trung tâm",
  },
  {
    level: 4,
    name: "Viết câu trả lời",
    title: "Viết lại số liệu thành câu dễ hiểu",
    desc: "Nhận số liệu đã lấy sẵn và viết thành tiếng Việt có bố cục rõ ràng. Chỉ dùng đúng những gì được cung cấp.",
    label: "Chỉ viết từ số liệu có sẵn",
  },
  {
    level: 3,
    name: "Hiểu câu hỏi",
    title: "Hiểu đúng bạn đang hỏi gì",
    desc: "Một mô hình nhỏ huấn luyện riêng cho du học, tách câu hỏi nhiều ý thành từng việc và chọn đúng công cụ để xử lý.",
    label: "Tách câu hỏi nhiều ý",
  },
  {
    level: 2,
    name: "Quy tắc xếp nhóm",
    title: "Quy tắc rõ ràng, kết quả giải thích được",
    desc: "So hồ sơ với số liệu từng trường theo ngưỡng cố định, nên cùng một hồ sơ luôn cho cùng một kết quả.",
    label: "Ngưỡng cố định",
  },
  {
    level: 1,
    name: "Dữ liệu",
    title: "Mọi con số đều có nguồn",
    desc: "Bảng điểm, hồ sơ của bạn và số liệu các trường được lưu và quản trị tại đây. Mọi lớp phía trên đều dựa vào lớp này.",
    label: "Bảng điểm · Dữ liệu trường",
  },
];

const ROWS = [90, 200, 310, 420, 530]; // vị trí dọc của từng tầng, trên xuống dưới
const ISO = "matrix(0.866 0.5 -0.866 0.5 0 0)";

function PlateDetail({ level, active }: { level: number; active: boolean }) {
  const s = active ? "stroke-white" : "stroke-ink-3";
  const f = (c: string) => (active ? c : "fill-none");
  switch (level) {
    case 1:
      return (
        <g className={cn(s, "fill-none")} strokeWidth="1.4">
          <ellipse cx="0" cy="-26" rx="26" ry="8" className={f("fill-brand-wash")} />
          <path d="M-26 -26 v16 a26 8 0 0 0 52 0 v-16" />
          <path d="M-26 -10 v16 a26 8 0 0 0 52 0 v-16" />
          <path d="M-26 6 v16 a26 8 0 0 0 52 0 v-16" />
        </g>
      );
    case 2:
      // Bốn ô cùng màu với bốn nhóm trường ở màn kết quả
      return (
        <g className={s} strokeWidth="1.2">
          <rect x="-40" y="-40" width="36" height="36" rx="5" className={f("fill-risk-wash")} />
          <rect x="4" y="-40" width="36" height="36" rx="5" className={f("fill-brand-wash")} />
          <rect x="-40" y="4" width="36" height="36" rx="5" className={f("fill-ok-wash")} />
          <rect x="4" y="4" width="36" height="36" rx="5" className={f("fill-warn-wash")} />
        </g>
      );
    case 3:
      return (
        <g className={cn(s, "fill-none")} strokeWidth="1.4">
          <rect x="-20" y="-20" width="40" height="40" rx="6" className={f("fill-brand")} />
          <rect x="-8" y="-8" width="16" height="16" rx="3" />
          {[-12, 0, 12].map((p) => (
            <path key={p} d={`M${p} -20 v-12 M${p} 20 v12 M-20 ${p} h-12 M20 ${p} h12`} />
          ))}
        </g>
      );
    case 4:
      return (
        <g strokeLinecap="round" strokeWidth="5" className={active ? "stroke-white" : "stroke-line"}>
          <path d="M-40 -32 h50" className={active ? "stroke-brand-wash" : "stroke-ink-3"} />
          <path d="M-40 -16 h80 M-40 0 h68 M-40 16 h76 M-40 32 h40" />
        </g>
      );
    default:
      return (
        <g className={cn(s, "fill-none")} strokeWidth="1.4">
          <circle cx="0" cy="-14" r="13" className={f("fill-brand-wash")} />
          <circle cx="-34" cy="2" r="9" className={f("fill-brand-wash")} />
          <circle cx="34" cy="2" r="9" className={f("fill-brand-wash")} />
          <path d="M-22 40 a22 22 0 0 1 44 0 z" className={f("fill-brand-wash")} />
          <path d="M-44 36 a14 14 0 0 1 24 -8 M44 36 a14 14 0 0 0 -24 -8" />
        </g>
      );
  }
}

export function LayerStack() {
  const [active, setActive] = useState(3);
  const wrapRef = useRef<HTMLDivElement>(null);

  // Cuộn trang để lần lượt đi qua từng lớp (chỉ trên màn hình rộng)
  useEffect(() => {
    const onScroll = () => {
      const el = wrapRef.current;
      if (!el || window.innerWidth < 1024) return;
      const rect = el.getBoundingClientRect();
      const total = rect.height - window.innerHeight;
      if (total <= 0) return;
      const p = Math.min(0.999, Math.max(0, -(rect.top - 96) / total));
      setActive(1 + Math.floor(p * 5));
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, []);

  const current = LAYERS.find((l) => l.level === active) ?? LAYERS[2];

  return (
    <div ref={wrapRef} className="lg:h-[260vh]">
      <div className="lg:sticky lg:top-[var(--header-h)] lg:flex lg:h-[calc(100svh-var(--header-h))] lg:items-center">
        <div className="mx-auto grid w-full max-w-5xl items-center gap-8 lg:grid-cols-12 lg:gap-12">
          {/* Cột trái: tiêu đề + nội dung tầng đang chọn */}
          <div className="order-2 space-y-4 lg:order-1 lg:col-span-5">
            <h2 className="text-sm font-semibold tracking-wide text-brand sm:text-base">
              Năm lớp làm việc cùng nhau để trả lời đúng
            </h2>
            <div key={current.level} className="min-h-[180px] space-y-4 animate-in fade-in slide-in-from-bottom-2 duration-500">
              <h3 className="text-h1 sm:text-[2.25rem] font-semibold text-balance text-ink leading-tight">
                {current.title}
              </h3>
              <p className="text-body max-w-[52ch] text-ink-2 leading-relaxed">
                {current.desc}
              </p>
            </div>
          </div>

          {/* Cột phải: các tầng 3D */}
          <div className="order-1 flex justify-center lg:order-2 lg:col-span-7">
            <svg viewBox="0 0 600 640" className="max-h-[70vh] w-full max-w-[600px] overflow-visible" role="img" aria-label="Năm lớp của hệ thống USAS xếp chồng lên nhau">
              <defs>
                <pattern id="plateGrid" width="9" height="9" patternUnits="userSpaceOnUse">
                  <path d="M9 0H0V9" fill="none" className="stroke-line" strokeWidth="0.8" />
                </pattern>
              </defs>

              {/* Trục dữ liệu chạy xuyên các tầng */}
              <line x1="250" y1="20" x2="250" y2="620" className="stroke-brand" strokeOpacity="0.5" strokeWidth="1.2" strokeDasharray="3 6" />
              {/* Chấm dữ liệu chạy dọc trục; ẩn khi người dùng chọn giảm chuyển động */}
              <circle r="4.5" cx="250" className="fill-brand motion-reduce:hidden">
                <animate attributeName="cy" values="20;620" dur="4s" repeatCount="indefinite" />
                <animate attributeName="opacity" values="0;1;1;0" dur="4s" repeatCount="indefinite" />
              </circle>

              {/* Vẽ từ tầng dưới lên để tầng trên che đúng chỗ */}
              {[...LAYERS].reverse().map((l) => {
                const idx = LAYERS.indexOf(l);
                const on = l.level === active;
                const y = ROWS[idx] - (on ? 18 : 0);
                return (
                  <g
                    key={l.level}
                    role="button"
                    tabIndex={0}
                    aria-label={`Lớp ${l.level}: ${l.name}`}
                    aria-pressed={on}
                    onClick={() => setActive(l.level)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        setActive(l.level);
                      }
                    }}
                    className="cursor-pointer outline-none focus-visible:outline-2 focus-visible:outline-brand focus-visible:outline-offset-4"
                    style={{ transform: `translate(250px, ${y}px)`, transition: "transform 0.6s cubic-bezier(.2,.8,.2,1)" }}
                  >
                    {/* Cạnh dày của tầng, giả lập bằng nhiều bản sao lệch xuống */}
                    {Array.from({ length: 11 }, (_, k) => 10 - k).map((k) => (
                      <g key={k} transform={`translate(0 ${k * 1.5})`}>
                        <rect transform={ISO} x="-65" y="-65" width="130" height="130" rx="14" strokeWidth="1" className={on ? "fill-brand-ink stroke-brand" : "fill-surface stroke-line"} />
                      </g>
                    ))}
                    {/* Mặt trên */}
                    <g transform={ISO}>
                      <rect x="-65" y="-65" width="130" height="130" rx="14" strokeWidth={on ? 2 : 1} className={on ? "fill-brand-ink stroke-brand" : "fill-surface stroke-ink-3"} />
                      {!on && <rect x="-65" y="-65" width="130" height="130" rx="14" fill="url(#plateGrid)" />}
                      <rect x="-56" y="-56" width="112" height="112" rx="10" fill="none" strokeDasharray="2 4" className={on ? "stroke-white/25" : "stroke-ink-3/50"} />
                      <PlateDetail level={l.level} active={on} />
                    </g>
                    {/* Số thứ tự ở góc phải */}
                    <circle cx="112" cy="0" r="9" className={on ? "fill-brand" : "fill-line"} />
                    <text x="112" y="3.5" textAnchor="middle" fontSize="10" fontWeight="700" className={on ? "fill-white" : "fill-ink-2"}>
                      {l.level}
                    </text>

                    {/* Nhãn chú thích có đường dẫn */}
                    {on && (
                      <g className="animate-in fade-in duration-300">
                        <path d="M121 0 L150 -34 H165" fill="none" strokeWidth="1" className="stroke-ink-3" />
                        <rect x="165" y="-47" width={l.label.length * 6.3 + 20} height="26" rx="6" className="fill-surface stroke-line" />
                        <text x="175" y="-30" fontSize="11" fontWeight="600" className="fill-ink">
                          {l.label}
                        </text>
                      </g>
                    )}
                  </g>
                );
              })}
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
}
