"use client";

import { useState } from "react";
import {
  FileTextIcon,
  LayoutGridIcon,
  MessageSquareTextIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  type LucideIcon,
} from "lucide-react";
import { GROUP_LABELS } from "@/lib/recommendation-labels";
import { cn } from "@/lib/utils";

const STEPS: { title: string; short: string; desc: string; icon: LucideIcon }[] = [
  {
    title: "Khai báo hồ sơ",
    short: "Hồ sơ",
    desc: "Nhập điểm hoặc tải bảng điểm để quy đổi GPA sang thang 4.0 và lưu hồ sơ.",
    icon: FileTextIcon,
  },
  {
    title: "Xem trường theo nhóm",
    short: "Xếp nhóm",
    desc: `Xếp vào các nhóm ${GROUP_LABELS.R}, ${GROUP_LABELS.M}, ${GROUP_LABELS.S} kèm lý do rõ ràng.`,
    icon: LayoutGridIcon,
  },
  {
    title: "Thử kịch bản “nếu như”",
    short: "Nếu như",
    desc: "Tăng điểm SAT hay đổi ngân sách để xem danh sách trường thay đổi ngay lập tức.",
    icon: SlidersHorizontalIcon,
  },
  {
    title: "Hỏi USAS Advisor",
    short: "Advisor",
    desc: "Hỏi so sánh trường, điều kiện tuyển sinh và chính sách học bổng bằng tiếng Việt.",
    icon: MessageSquareTextIcon,
  },
  {
    title: "Gặp trung tâm tư vấn",
    short: "Tư vấn",
    desc: "Kết nối trung tâm hỗ trợ bài luận, hoàn thiện hồ sơ và luyện phỏng vấn visa.",
    icon: UsersIcon,
  },
];

// Hình học vòng tròn (đơn vị viewBox 400×400)
const C = 200;
const R = 196; // bán kính ngoài
const r = 78; // bán kính trong
const GAP = 4; // nửa khe hở giữa hai phần, tính theo độ dài thật nên khe đều từ trong ra ngoài
const SWEEP = 360 / STEPS.length;

const pt = (rad: number, deg: number) => {
  const a = ((deg - 90) * Math.PI) / 180; // 0° là đỉnh trên, đi theo chiều kim đồng hồ
  return [C + rad * Math.cos(a), C + rad * Math.sin(a)].map((n) => n.toFixed(2)).join(" ");
};

function segmentPath(i: number) {
  const a0 = i * SWEEP;
  const a1 = a0 + SWEEP;
  const po = (GAP / R) * (180 / Math.PI);
  const pi = (GAP / r) * (180 / Math.PI);
  return [
    `M ${pt(R, a0 + po)}`,
    `A ${R} ${R} 0 0 1 ${pt(R, a1 - po)}`,
    `L ${pt(r, a1 - pi)}`,
    `A ${r} ${r} 0 0 0 ${pt(r, a0 + pi)}`,
    "Z",
  ].join(" ");
}

function centroid(i: number) {
  const a = (((i + 0.5) * SWEEP - 90) * Math.PI) / 180;
  const m = (R + r) / 2;
  return { x: C + m * Math.cos(a), y: C + m * Math.sin(a) };
}

// ponytail: không tự chuyển bước khi cuộn; thêm như LayerStack nếu cần
export function StepsWheel() {
  const [active, setActive] = useState(0);
  const step = STEPS[active];

  return (
    <div className="grid items-center gap-8 lg:grid-cols-12 lg:gap-16">
      <div className="flex justify-center lg:col-span-6">
        <svg viewBox="0 0 400 400" className="w-full max-w-[440px]" role="group" aria-labelledby="steps-wheel-title">
          <title id="steps-wheel-title">Năm bước từ hồ sơ đến quyết định</title>
          {STEPS.map((s, i) => {
            const on = i === active;
            const { x, y } = centroid(i);
            const Icon = s.icon;
            return (
              <g
                key={s.title}
                role="button"
                tabIndex={0}
                aria-label={`Bước ${i + 1}: ${s.title}`}
                aria-pressed={on}
                onClick={() => setActive(i)}
                onMouseEnter={() => setActive(i)}
                onFocus={() => setActive(i)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    setActive(i);
                  }
                }}
                className="group cursor-pointer outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
              >
                <path
                  d={segmentPath(i)}
                  strokeWidth="1.5"
                  strokeLinejoin="round"
                  className={cn(
                    "transition-colors duration-200 motion-reduce:transition-none",
                    on ? "fill-brand stroke-brand" : "fill-surface stroke-line group-hover:fill-brand-wash",
                  )}
                />
                <text
                  x={x}
                  y={y - 24}
                  textAnchor="middle"
                  fontSize="13"
                  fontWeight="700"
                  className={cn("font-heading transition-colors duration-200", on ? "fill-white" : "fill-ink-3")}
                >
                  0{i + 1}
                </text>
                <text
                  x={x}
                  y={y + 28}
                  textAnchor="middle"
                  fontSize="13"
                  fontWeight="600"
                  className={cn("transition-colors duration-200", on ? "fill-white" : "fill-ink")}
                >
                  {s.short}
                </text>
                <Icon
                  x={x - 13}
                  y={y - 15}
                  width={26}
                  height={26}
                  strokeWidth={1.75}
                  aria-hidden
                  className={cn("transition-colors duration-200", on ? "text-white" : "text-ink-2")}
                />
              </g>
            );
          })}

          {/* Đĩa giữa: bước đang chọn */}
          <circle cx={C} cy={C} r={r - 8} className="fill-brand-wash" />
          <text x={C} y={C + 4} textAnchor="middle" fontSize="36" fontWeight="700" className="fill-brand font-heading">
            0{active + 1}
          </text>
          <text x={C} y={C + 26} textAnchor="middle" fontSize="11" fontWeight="600" letterSpacing="1.5" className="fill-ink-3">
            / 05
          </text>
        </svg>
      </div>

      <div className="space-y-4 lg:col-span-6" aria-live="polite">
        <p className="text-sm font-semibold tracking-wide text-brand">Bước 0{active + 1}</p>
        <div key={active} className="space-y-3 animate-in fade-in slide-in-from-bottom-2 duration-300 motion-reduce:animate-none">
          <h3 className="text-h1 font-semibold text-balance text-ink">{step.title}</h3>
          <p className="text-body max-w-[52ch] leading-relaxed text-ink-2">{step.desc}</p>
        </div>
        <div className="flex gap-1.5 pt-2" aria-hidden>
          {STEPS.map((s, i) => (
            <span key={s.title} className={cn("h-1 rounded-full transition-all duration-300", i === active ? "w-8 bg-brand" : "w-4 bg-line")} />
          ))}
        </div>
      </div>
    </div>
  );
}
