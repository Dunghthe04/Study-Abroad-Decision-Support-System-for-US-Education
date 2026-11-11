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
    color: "bg-brand-wash text-brand",
  },
  {
    title: "Xem trường theo nhóm",
    desc: `Xếp vào các nhóm ${GROUP_LABELS.R}, ${GROUP_LABELS.M}, ${GROUP_LABELS.S} kèm lý do rõ ràng.`,
    icon: LayoutGridIcon,
    color: "bg-reach-wash text-reach",
  },
  {
    title: "Thử kịch bản “nếu như”",
    desc: "Tăng điểm SAT hay đổi ngân sách để xem danh sách trường thay đổi ngay lập tức.",
    icon: SlidersHorizontalIcon,
    color: "bg-warn-wash text-warn",
  },
  {
    title: "Hỏi USAS Advisor",
    desc: "Hỏi so sánh trường, điều kiện tuyển sinh và chính sách học bổng bằng tiếng Việt.",
    icon: MessageSquareTextIcon,
    color: "bg-ok-wash text-ok",
  },
  {
    title: "Gặp trung tâm tư vấn",
    desc: "Kết nối trung tâm hỗ trợ bài luận, hoàn thiện hồ sơ và luyện phỏng vấn visa.",
    icon: UsersIcon,
    color: "bg-risk-wash text-risk",
  },
];

/** Năm bước dạng danh sách dọc: số thứ tự lớn bên trái, tên và mô tả ở giữa, biểu tượng có màu bên phải */
export function StepsWheel() {
  return (
    <ol className="space-y-3 lg:col-span-7">
      {STEPS.map((s, i) => (
        <li key={s.title} className="flex items-center gap-5 rounded-xl border border-line bg-surface p-5 sm:gap-6 sm:px-6">
          <span className="w-12 shrink-0 font-heading text-4xl font-semibold text-ink tabular-nums sm:text-5xl">0{i + 1}</span>
          <div className="min-w-0 flex-1 space-y-1">
            <h3 className="font-heading text-lg font-semibold text-ink">{s.title}</h3>
            <p className="text-sm leading-relaxed text-pretty text-ink-2">{s.desc}</p>
          </div>
          <span className={`flex size-12 shrink-0 items-center justify-center rounded-lg ${s.color}`}>
            <s.icon aria-hidden className="size-6" />
          </span>
        </li>
      ))}
    </ol>
  );
}
