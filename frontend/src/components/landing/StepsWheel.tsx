import {
  FileTextIcon,
  LayoutGridIcon,
  MessageSquareTextIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  type LucideIcon,
} from "lucide-react";
import { GROUP_LABELS } from "@/lib/recommendation-labels";

const STEPS: { title: string; desc: string; icon: LucideIcon; color: string }[] = [
  {
    title: "Khai báo hồ sơ",
    desc: "Nhập điểm hoặc tải bảng điểm để quy đổi GPA sang thang 4.0 và lưu hồ sơ.",
    icon: FileTextIcon,
    color: "text-brand",
  },
  {
    title: "Xem trường theo nhóm",
    desc: `Xếp vào các nhóm ${GROUP_LABELS.R}, ${GROUP_LABELS.M}, ${GROUP_LABELS.S} kèm lý do rõ ràng.`,
    icon: LayoutGridIcon,
    color: "text-reach",
  },
  {
    title: "Thử kịch bản “nếu như”",
    desc: "Tăng điểm SAT hay đổi ngân sách để xem danh sách trường thay đổi ngay lập tức.",
    icon: SlidersHorizontalIcon,
    color: "text-warn",
  },
  {
    title: "Hỏi USAS Advisor",
    desc: "Hỏi so sánh trường, điều kiện tuyển sinh và chính sách học bổng bằng tiếng Việt.",
    icon: MessageSquareTextIcon,
    color: "text-ok",
  },
  {
    title: "Gặp trung tâm tư vấn",
    desc: "Kết nối trung tâm hỗ trợ bài luận, hoàn thiện hồ sơ và luyện phỏng vấn visa.",
    icon: UsersIcon,
    color: "text-risk",
  },
];

// Bước 1 nằm chính giữa phía trên, các bước tiếp theo đi theo chiều kim đồng hồ.
const SWEEP = 360 / STEPS.length;
const INNER = 9; // bán kính vòng trong (đơn vị viewBox 0–100, tâm 50)
const TEXT_AT = 0.6; // vị trí khối chữ theo tỉ lệ bán kính

const polar = (radius: number, deg: number) => {
  const a = ((deg - 90) * Math.PI) / 180;
  return { x: 50 + radius * Math.cos(a), y: 50 + radius * Math.sin(a) };
};

function StepText({ i }: { i: number }) {
  const s = STEPS[i];
  return (
    <>
      <div className="flex items-center gap-2">
        <s.icon aria-hidden className={`size-6 shrink-0 ${s.color}`} />
        <span className="font-heading text-sm font-bold text-ink-3">0{i + 1}</span>
      </div>
      <h3 className="font-heading text-base font-semibold text-ink">{s.title}</h3>
      <p className="text-sm leading-relaxed text-ink-2">{s.desc}</p>
    </>
  );
}

export function StepsWheel() {
  return (
    <>
      {/* Màn hình nhỏ: danh sách dọc */}
      <ol className="divide-y divide-line lg:hidden">
        {STEPS.map((s, i) => (
          <li key={s.title} className="space-y-2 py-5 first:pt-0 last:pb-0">
            <StepText i={i} />
          </li>
        ))}
      </ol>

      {/* Màn hình rộng: vòng tròn chia năm phần */}
      <div className="relative mx-auto hidden aspect-square w-full max-w-[960px] lg:block">
        <svg viewBox="0 0 100 100" aria-hidden className="absolute inset-0 size-full overflow-visible">
          <g className="fill-none stroke-line" strokeWidth="1" vectorEffect="non-scaling-stroke">
            <circle cx="50" cy="50" r="49.5" vectorEffect="non-scaling-stroke" />
            <circle cx="50" cy="50" r={INNER} vectorEffect="non-scaling-stroke" />
            {STEPS.map((s, i) => {
              const a = (i + 0.5) * SWEEP; // đường ngăn cách giữa bước i và i+1
              const p0 = polar(INNER, a);
              const p1 = polar(49.5, a);
              return <line key={s.title} x1={p0.x} y1={p0.y} x2={p1.x} y2={p1.y} vectorEffect="non-scaling-stroke" />;
            })}
          </g>
        </svg>
        <ol className="absolute inset-0">
          {STEPS.map((s, i) => {
            const { x, y } = polar(50 * TEXT_AT, i * SWEEP);
            return (
              <li
                key={s.title}
                className="absolute flex w-[23%] -translate-x-1/2 -translate-y-1/2 flex-col items-center gap-2 text-center"
                style={{ left: `${x}%`, top: `${y}%` }}
              >
                <StepText i={i} />
              </li>
            );
          })}
        </ol>
      </div>
    </>
  );
}
