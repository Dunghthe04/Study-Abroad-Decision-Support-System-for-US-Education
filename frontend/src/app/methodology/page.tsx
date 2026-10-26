import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRightIcon, AwardIcon, DollarSignIcon, GraduationCapIcon, ScaleIcon, TrendingUpIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { cn } from "@/lib/utils";

export const metadata: Metadata = {
  title: "Giới thiệu phương pháp gợi ý trường – USAS",
  description:
    "Phương pháp khoa học hỗ trợ ra quyết định du học Mỹ: mô hình tuyển sinh toàn diện (Holistic Review), thuật toán AHP & SAW và phân tầng 4 nhóm trường.",
};

const tiers = [
  {
    name: "Khó vào",
    tone: "bg-risk-wash text-risk",
    description: "Hồ sơ hiện tại thấp hơn mức thường nhận của trường. Nên xem như mục tiêu cao.",
    criteria: "GPA ≤ GPA TB trường − 0.3, hoặc SAT dưới mốc 25% của trường",
    share: "20–25%",
  },
  {
    name: "Vừa sức",
    tone: "bg-brand-wash text-brand",
    description: "Hồ sơ nằm trong khoảng thường nhận của trường. Nhóm nên tập trung nhất.",
    criteria: "Hồ sơ trong khoảng 25% – 75% của trường, GPA chênh trong ±0.3",
    share: "45–50%",
  },
  {
    name: "An toàn hơn",
    tone: "bg-ok-wash text-ok",
    description: "Hồ sơ cao hơn mức thường nhận của trường, khả năng đậu cao.",
    criteria: "GPA ≥ GPA TB trường + 0.3, hoặc SAT từ mốc 75%. Cao đẳng cộng đồng mặc định an toàn",
    share: "25–30%",
  },
  {
    name: "Chỉ đi được nếu có học bổng lớn",
    tone: "bg-warn-wash text-warn",
    description: "Phù hợp về học thuật nhưng chi phí vượt ngân sách của bạn nếu không có học bổng lớn.",
    criteria: "Tổng chi phí/năm > 110% ngân sách gia đình tự chi trả",
    share: "Tuỳ ngân sách",
  },
];

const pillars = [
  {
    icon: GraduationCapIcon,
    title: "Năng lực học thuật",
    weight: "50%",
    items: [
      "GPA 3 năm gần nhất quy đổi tuyến tính sang thang 4.0 (GPA 10 / 10 × 4.0), so với trung vị của trường.",
      "SAT/ACT so với khoảng 25% – 75% của khoá sinh viên năm nhất trường đã nhận.",
      "Chứng chỉ tiếng Anh (IELTS, TOEFL, Duolingo) là điều kiện sàn tối thiểu.",
    ],
  },
  {
    icon: DollarSignIcon,
    title: "Khả năng tài chính",
    weight: "30%",
    items: [
      "Tổng chi phí thực tế: học phí, bảo hiểm y tế và sinh hoạt phí theo từng bang.",
      "Lọc nếu chi phí vượt quá 10% ngân sách tự túc khi không có học bổng.",
      "Cơ hội học bổng cho sinh viên quốc tế (Merit-based và Need-based).",
    ],
  },
  {
    icon: AwardIcon,
    title: "Ngoại khoá và thành tích",
    weight: "20%",
    items: [
      "Phạm vi: trường, tỉnh/thành phố, quốc gia hoặc quốc tế.",
      "Vai trò lãnh đạo so với thành viên, số năm duy trì.",
      "Giải thưởng chuyên môn, dự án cộng đồng, năng khiếu đặc thù.",
    ],
  },
];

const presets = [
  { icon: ScaleIcon, name: "Cân bằng", weights: "50% Học thuật · 30% Tài chính · 20% Ngoại khoá" },
  { icon: DollarSignIcon, name: "Chi phí thấp", weights: "50% Tài chính · 35% Học thuật · 15% Ngoại khoá" },
  { icon: AwardIcon, name: "Học bổng nhiều", weights: "45% Học bổng & Tài chính · 35% Học thuật · 20% Ngoại khoá" },
  { icon: TrendingUpIcon, name: "Danh tiếng", weights: "65% Học thuật & Xếp hạng · 20% Tài chính · 15% Ngoại khoá" },
];

const aiRules = [
  {
    title: "Đọc bảng điểm từ ảnh",
    body: "Mô hình đọc ảnh học bạ hoặc PDF, nhận học kỳ và từng môn để điền sẵn. Bạn luôn xem và sửa từng ô trước khi xác nhận.",
  },
  {
    title: "Kiểm tra số liệu",
    body: "Lời giải thích cho từng trường do AI viết được đối chiếu tự động với số liệu tuyển sinh thực tế của trường.",
  },
  {
    title: "Tự lưu nháp",
    body: "Mỗi bước trong form 6 bước được lưu nháp vào tài khoản. Bạn có thể rời đi và quay lại bất cứ lúc nào.",
  },
];

function SectionCard({ title, note, children, className }: { title: string; note?: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={cn("rounded-xl border border-line bg-surface p-5 sm:p-6", className)}>
      <h2 className="font-sans text-lg font-semibold text-foreground">{title}</h2>
      {note && <p className="mt-1 text-[13px] text-muted-foreground">{note}</p>}
      <div className="mt-4">{children}</div>
    </section>
  );
}

export default function MethodologyPage() {
  return (
    <div className="mx-auto max-w-[900px] space-y-6 pb-16">
      <div className="space-y-2 pt-5">
        <div className="text-[11.5px] font-bold tracking-[0.07em] text-brand uppercase">Giới thiệu phương pháp</div>
        <h1 className="text-[34px] leading-tight font-semibold text-foreground">Cách hệ thống xếp trường</h1>
        <p className="max-w-[62ch] text-muted-foreground">
          Hồ sơ được so với số liệu tuyển sinh của từng trường: GPA trung bình, SAT, tỉ lệ nhận học sinh quốc tế và
          chi phí. Kết quả là nhóm, không phải xác suất đỗ.
        </p>
      </div>

      <SectionCard title="Bốn nhóm trường">
        <div className="space-y-2.5">
          {tiers.map((t) => (
            <div key={t.name} className="flex items-start gap-3">
              <span className={cn("inline-flex h-[26px] shrink-0 items-center rounded-[7px] px-2.5 text-[12.5px] font-bold whitespace-nowrap", t.tone)}>
                {t.name}
              </span>
              <span className="pt-0.5 text-sm text-muted-foreground">{t.description}</span>
            </div>
          ))}
        </div>
      </SectionCard>

      <section className="overflow-hidden rounded-xl border border-line bg-surface">
        <div className="px-5 pt-5 pb-2 sm:px-6">
          <h2 className="font-sans text-lg font-semibold text-foreground">Ngưỡng phân nhóm</h2>
          <p className="mt-1 text-[13px] text-muted-foreground">So hồ sơ của bạn với số liệu tuyển sinh của từng trường.</p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-muted-foreground">
                <th className="px-5 py-2.5 font-semibold sm:px-6">Nhóm</th>
                <th className="px-3 py-2.5 font-semibold">Điều kiện</th>
                <th className="px-5 py-2.5 font-semibold sm:px-6">Nên chiếm trong danh sách</th>
              </tr>
            </thead>
            <tbody>
              {tiers.map((t) => (
                <tr key={t.name} className="border-b border-line last:border-0">
                  <td className="px-5 py-3 font-medium whitespace-nowrap text-foreground sm:px-6">{t.name}</td>
                  <td className="px-3 py-3 text-muted-foreground">{t.criteria}</td>
                  <td className="px-5 py-3 text-numeric whitespace-nowrap text-foreground sm:px-6">{t.share}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <SectionCard title="Ba mặt được đánh giá" note="Tuyển sinh đại học Mỹ xét toàn diện (Holistic Review), không chỉ điểm thi.">
        <div className="grid gap-4 md:grid-cols-3">
          {pillars.map((p) => {
            const Icon = p.icon;
            return (
              <div key={p.title} className="space-y-2.5">
                <div className="flex items-center gap-2">
                  <Icon className="size-4.5 text-brand" />
                  <span className="font-semibold text-foreground">{p.title}</span>
                  <span className="text-numeric ml-auto text-sm text-brand">{p.weight}</span>
                </div>
                <ul className="list-disc space-y-1.5 pl-4 text-[13.5px] text-muted-foreground">
                  {p.items.map((it) => (
                    <li key={it}>{it}</li>
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      </SectionCard>

      <SectionCard
        title="Tính điểm phù hợp (AHP và SAW)"
        note="Trọng số lấy từ phương pháp phân tích thứ bậc AHP của Thomas L. Saaty; điểm từng trường tính bằng SAW."
      >
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2 rounded-lg bg-ground p-4">
            <div className="text-sm font-semibold text-foreground">Kiểm tra tính nhất quán của trọng số</div>
            <div className="flex gap-6">
              <div>
                <div className="text-xs text-muted-foreground">CI</div>
                <div className="text-numeric text-foreground">0.0091</div>
              </div>
              <div>
                <div className="text-xs text-muted-foreground">CR</div>
                <div className="text-numeric text-ok">0.016 &lt; 0.10</div>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">CR dưới 0.10 nghĩa là bộ trọng số không mâu thuẫn.</p>
          </div>
          <div className="space-y-2 rounded-lg bg-ground p-4">
            <div className="text-sm font-semibold text-foreground">Công thức</div>
            <div className="font-mono text-[13px] text-brand-ink">Điểm = Σ (Trọng số × Điểm chuẩn hoá) / Σ Trọng số</div>
            <p className="text-xs text-muted-foreground">
              Thiếu một tiêu chí (ví dụ chưa thi SAT) thì chuẩn hoá lại trên các tiêu chí còn lại, không tính là 0.
            </p>
          </div>
        </div>

        <div className="mt-5 text-sm font-semibold text-foreground">Bốn chế độ ưu tiên ở trang đề xuất</div>
        <div className="mt-2.5 grid gap-2.5 sm:grid-cols-2">
          {presets.map((ps) => {
            const Icon = ps.icon;
            return (
              <div key={ps.name} className="flex items-start gap-2.5 rounded-lg border border-line p-3">
                <Icon className="mt-0.5 size-4 shrink-0 text-brand" />
                <div>
                  <div className="text-sm font-semibold text-foreground">{ps.name}</div>
                  <div className="text-xs text-muted-foreground">{ps.weights}</div>
                </div>
              </div>
            );
          })}
        </div>
      </SectionCard>

      <SectionCard title="AI dùng ở đâu" note="AI hỗ trợ, không tự tạo số liệu và không quyết định thay bạn.">
        <div className="grid gap-4 sm:grid-cols-3">
          {aiRules.map((r) => (
            <div key={r.title} className="space-y-1">
              <div className="text-sm font-semibold text-foreground">{r.title}</div>
              <p className="text-[13.5px] text-muted-foreground">{r.body}</p>
            </div>
          ))}
        </div>
      </SectionCard>

      <SectionCard title="Lộ trình">
        <p className="text-muted-foreground">
          Ngoài đại học 4 năm, hệ thống xét cả cao đẳng cộng đồng 2+2: học 2 năm rồi chuyển tiếp lên đại học. Nhóm
          này luôn xếp An toàn hơn, là phương án nên cân nhắc khi tài chính hoặc học lực chưa đủ.
        </p>
        <div className="mt-4 flex flex-wrap gap-3">
          <Link href="/analysis" className={buttonVariants({ size: "lg", className: "h-11 px-5" })}>
            Bắt đầu phân tích
            <ArrowRightIcon className="ml-1.5 size-4" />
          </Link>
          <OpenAssistantButton className="h-11 px-5" question="Vì sao chia trường thành 4 nhóm?" />
        </div>
      </SectionCard>
    </div>
  );
}
