"use client";

import { useState } from "react";
import Link from "next/link";
import {
  ArrowDownIcon,
  ArrowRightIcon,
  BanIcon,
  BotIcon,
  CalculatorIcon,
  CheckCircle2Icon,
  FilterIcon,
  LayersIcon,
  ListOrderedIcon,
  MapPinIcon,
  ScanTextIcon,
  ShieldAlertIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { SchoolPhotoBackdrop } from "@/components/landing/SchoolPhotoBackdrop";
import { GradeScaleTable } from "@/components/methodology/GradeScaleTable";
import { ParameterList } from "@/components/methodology/ParameterList";
import { LANDING_SCHOOLS } from "@/lib/landing-schools";
import { groupLabel, groupVariant, statusLabel } from "@/lib/recommendation-labels";
import { cn } from "@/lib/utils";
import type { GradeScaleConfig } from "@/types/academic";
import type { MethodologyParameters } from "@/types/methodology";

const HERO_CAMPUS = [LANDING_SCHOOLS[2]]; // MIT Great Dome

const STEPS = [
  {
    title: "Lọc điều kiện cứng",
    body: "Bỏ trường không hợp ngành, bang, chứng chỉ tiếng Anh hoặc vượt quá ngân sách của bạn.",
    icon: FilterIcon,
    tone: "bg-brand-wash text-brand",
  },
  {
    title: "Tính vị trí và xếp nhóm",
    body: "So GPA, điểm thi và ngoại khóa với sinh viên trường đã nhận để xếp vào Khó vào, Vừa sức hay An toàn hơn.",
    icon: LayersIcon,
    tone: "bg-reach-wash text-reach",
  },
  {
    title: "Sắp thứ tự theo độ phù hợp",
    body: "Trong mỗi nhóm, trường hợp với bạn hơn đứng trước theo năm trọng số. Mỗi nhóm hiện tối đa 4 trường.",
    icon: ListOrderedIcon,
    tone: "bg-ok-wash text-ok",
  },
];

const EXAMPLES = [
  {
    id: "A",
    profile: "GPA 3,5 · SAT 1250",
    position: 46,
    group: "M" as const,
    note: "Nằm giữa nhóm sinh viên đã nhập học (25%–75%), nên xếp vào Vừa sức.",
  },
  {
    id: "B",
    profile: "GPA 3,2 · SAT 1150",
    position: 22,
    group: "R" as const,
    note: "Dưới mốc 25% của trường mẫu, nên xếp vào Khó vào.",
  },
  {
    id: "C",
    profile: "GPA 3,9 · SAT 1400",
    position: 83,
    group: "S" as const,
    note: "Vượt mốc 75% ở trường nhận 55% hồ sơ, nên xếp vào An toàn hơn.",
  },
];

const SCENARIOS = [
  {
    id: "selective",
    title: "Trường rất chọn lọc",
    condition: "Nhận dưới 15% hồ sơ (tính theo tỉ lệ nhận của du học sinh nếu trường có số riêng)",
    effect: "Luôn Khó vào",
    variant: "R" as const,
    example: "Hồ sơ C (vị trí 83%) ở trường chỉ nhận 12% vẫn là Khó vào, dù điểm cao hơn phần lớn sinh viên.",
  },
  {
    id: "under-half",
    title: "Trường nhận ít hơn nửa số hồ sơ",
    condition: "Tỉ lệ nhận dưới 50% (du học sinh nếu trường có số riêng)",
    effect: "Không được An toàn hơn",
    variant: "M" as const,
    example: "Hồ sơ C (vị trí 83%) ở trường nhận 40% bị hạ từ An toàn hơn xuống Vừa sức.",
  },
  {
    id: "weak-metric",
    title: "Một chỉ số quá yếu",
    condition: "GPA hoặc điểm thi dưới 25%",
    effect: "Chỉ được Vừa sức",
    variant: "M" as const,
    example: "Chỉ số mạnh không che được chỉ số yếu: nếu một trong hai dưới mốc 25%, trường không thể vào An toàn hơn.",
  },
  {
    id: "intl-rate",
    title: "Trường có tỉ lệ nhận riêng cho du học sinh",
    condition: "Trường công bố tỉ lệ nhận của du học sinh",
    effect: "Hai quy tắc về tỉ lệ nhận dùng số này",
    variant: "M" as const,
    example: "Trường nhận 55% chung nhưng chỉ nhận 20% sinh viên quốc tế: quy tắc dưới 50% xét 20%, nên trường không được An toàn hơn. Không có số riêng thì dùng tỉ lệ chung.",
  },
];

const COST_OUTCOMES = [
  {
    icon: CheckCircle2Icon,
    tone: "text-ok",
    condition: "Chi phí một năm không vượt ngân sách quá 10%",
    result: "Nằm trong ba nhóm",
    badge: null,
  },
  {
    icon: ArrowDownIcon,
    tone: "text-warn",
    condition: "Vượt quá 10%, bạn chọn cần học bổng, và trường có học bổng cho du học sinh",
    result: `Khối ${statusLabel("AID")}`,
    badge: "AID" as const,
  },
  {
    icon: BanIcon,
    tone: "text-ink-3",
    condition: "Vượt quá 10% trong các trường hợp còn lại",
    result: "Bị loại ở bước lọc",
    badge: null,
  },
];

const FIT_PARTS = [
  { name: "Độ vừa tầm", weight: 30, body: "Cao nhất khi hồ sơ của bạn ở giữa nhóm sinh viên của trường.", tone: "bg-brand text-white" },
  { name: "Ngân sách", weight: 25, body: "Càng rẻ hơn ngân sách càng cao; vượt thì bằng 0.", tone: "bg-brand/80 text-white" },
  { name: "Chất lượng", weight: 20, body: "Tính từ vị trí của trường trong các bảng xếp hạng.", tone: "bg-brand/60 text-white" },
  { name: "Học bổng", weight: 15, body: "Phần trăm chi phí cả năm mà học bổng trung bình chi trả.", tone: "bg-brand/40 text-brand-ink" },
  { name: "Tiếng Anh", weight: 10, body: "Đạt mức tối thiểu của trường thì tối đa.", tone: "bg-brand/25 text-brand-ink" },
];

const LIMITS = [
  { title: "Không phải xác suất đỗ", body: "Trường còn xét bài luận, thư giới thiệu và nhiều yếu tố hệ thống không thấy." },
  { title: "Thiếu số liệu thì không đoán", body: "Trường thiếu số liệu được ghi “Chưa đủ dữ liệu”, không bị xếp bừa." },
  { title: "Có giả định phân bố", body: "Trường chỉ công bố mốc 25% và 75% của điểm thi nên hệ thống giả định điểm phân bố hình chuông quanh hai mốc đó." },
];

const AI_RULES = [
  { icon: CalculatorIcon, title: "Nhóm do code tính", body: "Cùng hồ sơ và cùng số liệu trường thì luôn ra cùng kết quả. AI không quyết định nhóm." },
  { icon: BotIcon, title: "AI viết lời giải thích", body: "Các con số trong lời giải thích được đối chiếu tự động với số liệu tuyển sinh của trường." },
  { icon: ScanTextIcon, title: "Đọc bảng điểm từ ảnh", body: "Mô hình điền sẵn từ ảnh học bạ. Bạn luôn xem và sửa trước khi xác nhận." },
];

const RULES = [
  { rule: "Tỉ lệ nhận của trường dưới 15% (của du học sinh nếu có, không thì chung)", effect: "Luôn là Khó vào, dù hồ sơ mạnh đến đâu." },
  { rule: "Vị trí học thuật đủ cao nhưng tỉ lệ nhận dưới 50% (của du học sinh nếu có, không thì chung)", effect: "Chỉ là Vừa sức, không được An toàn hơn." },
  { rule: "Vị trí học thuật đủ cao nhưng GPA hoặc điểm thi riêng lẻ dưới 25%", effect: "Chỉ là Vừa sức: chỉ số mạnh không che được chỉ số yếu." },
  { rule: "Trường công bố tỉ lệ nhận riêng cho du học sinh", effect: "Hai quy tắc tỉ lệ nhận ở trên dùng tỉ lệ này thay cho tỉ lệ chung." },
  { rule: "Dưới 50% sinh viên của trường nộp SAT", effect: "SAT chỉ đại diện nhóm có nộp (thường điểm cao) nên trọng số điểm thi giảm một nửa." },
  { rule: "Trường bắt buộc SAT mà bạn chưa có điểm thi", effect: "Chưa đủ dữ liệu để xếp nhóm." },
];

interface MethodologyViewProps {
  parameters: MethodologyParameters | null;
  gradeScale: GradeScaleConfig | null;
}

export function MethodologyView({ parameters, gradeScale }: MethodologyViewProps) {
  const [selectedExampleId, setSelectedExampleId] = useState<string>("A");
  const [selectedScenarioId, setSelectedScenarioId] = useState<string>(SCENARIOS[0].id);

  const activeExample = EXAMPLES.find((e) => e.id === selectedExampleId) ?? EXAMPLES[0];
  const activeScenario = SCENARIOS.find((s) => s.id === selectedScenarioId) ?? SCENARIOS[0];

  return (
    <>
      {/* HERO: cùng ngôn ngữ tràn màn hình với trang chủ, thẻ bên phải minh họa thang vị trí 0–100% */}
      <section
        aria-label="Cách hệ thống xếp trường"
        className="relative isolate -mt-[var(--header-h)] flex min-h-[min(100svh,960px)] items-end overflow-hidden bg-brand-deep text-white"
      >
        <SchoolPhotoBackdrop schools={HERO_CAMPUS} activeIndex={0} />

        <div className="mx-auto grid w-full max-w-[1560px] gap-10 px-4 pt-[calc(var(--header-h)+4rem)] pb-12 sm:px-8 sm:pb-16 lg:grid-cols-12 lg:items-end lg:px-12 lg:pb-20">
          <div className="space-y-6 lg:col-span-7 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-3 motion-safe:duration-700">
            <h1 className="text-4xl leading-[1.1] font-semibold tracking-[-0.02em] text-balance sm:text-6xl">
              Cách USDSS xếp trường theo hồ sơ của bạn
            </h1>
            <p className="max-w-[56ch] text-base leading-relaxed text-pretty text-white/85 sm:text-lg">
              Hệ thống trả lời một câu hỏi: hồ sơ của bạn đứng ở đâu so với sinh viên trường đã nhận? Kết quả là nhóm tham khảo theo ngưỡng công khai, không phải xác suất đỗ.
            </p>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/analysis"
                className={buttonVariants({ size: "xl", className: "!bg-white !text-brand-deep hover:!bg-white/90" })}
              >
                Phân tích hồ sơ
                <ArrowRightIcon className="transition-transform group-hover/button:translate-x-0.5" />
              </Link>
              <a
                href="#parameters"
                className={buttonVariants({
                  variant: "outline",
                  size: "xl",
                  className: "!border-white/40 !bg-transparent !text-white hover:!bg-white/10",
                })}
              >
                Xem tham số đang chạy
              </a>
            </div>
          </div>

          {/* Thẻ tương tác: thang vị trí học thuật 0–100% với 3 hồ sơ ví dụ */}
          <div className="lg:col-span-5 lg:justify-self-end">
            <div className="w-full max-w-lg rounded-xl bg-surface p-5 text-ink sm:p-6">
              <div className="flex items-baseline justify-between gap-3 pb-4">
                <div>
                  <p className="text-xs font-medium text-ink-2">Thang vị trí học thuật</p>
                  <p className="text-sm font-semibold text-ink">Trường mẫu: GPA 3,6 · SAT 1100–1350 · nhận 55%</p>
                </div>
                <span className="text-numeric shrink-0 whitespace-nowrap text-xs text-ink-3">0–100%</span>
              </div>

              {/* Thanh 3 vùng 25% / 50% / 25% */}
              <div
                role="img"
                aria-label={`Thang vị trí học thuật từ 0 đến 100%. Dưới 25% là Khó vào, từ 25% đến dưới 75% là Vừa sức, từ 75% là An toàn hơn. ${EXAMPLES.map((e) => `Hồ sơ ${e.id} ở ${e.position}%, nhóm ${groupLabel(e.group)}`).join(". ")}.`}
                className="pb-4"
              >
                <div className="relative h-11">
                  <div className="absolute inset-0 flex overflow-hidden rounded-lg">
                    <div className="w-1/4 bg-reach-wash" />
                    <div className="w-1/2 bg-brand-wash" />
                    <div className="w-1/4 bg-ok-wash" />
                  </div>
                  {EXAMPLES.map((e) => {
                    const selected = e.id === activeExample.id;
                    return (
                      <button
                        key={e.id}
                        type="button"
                        onClick={() => setSelectedExampleId(e.id)}
                        aria-label={`Chọn hồ sơ ${e.id}: ${e.profile}, vị trí ${e.position}%`}
                        aria-pressed={selected}
                        style={{ left: `${e.position}%` }}
                        className={cn(
                          "absolute top-1/2 flex size-8 -translate-x-1/2 -translate-y-1/2 cursor-pointer items-center justify-center rounded-full text-sm font-semibold transition-transform focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
                          selected
                            ? "z-10 scale-110 bg-brand text-white ring-2 ring-surface"
                            : "bg-surface text-ink ring-1 ring-line hover:scale-105",
                        )}
                      >
                        {e.id}
                      </button>
                    );
                  })}
                </div>
                <div className="relative mt-1.5 h-4 text-xs text-ink-3" aria-hidden>
                  <span className="absolute left-0">0%</span>
                  <span className="absolute -translate-x-1/2" style={{ left: "25%" }}>25%</span>
                  <span className="absolute -translate-x-1/2" style={{ left: "75%" }}>75%</span>
                  <span className="absolute right-0">100%</span>
                </div>
                <div className="mt-1.5 flex text-center text-xs font-medium" aria-hidden>
                  <span className="w-1/4 text-reach">Khó vào</span>
                  <span className="w-1/2 text-brand-ink">Vừa sức</span>
                  <span className="w-1/4 text-ok">An toàn hơn</span>
                </div>
              </div>

              <ul className="divide-y divide-line border-t border-line">
                {EXAMPLES.map((e) => {
                  const selected = e.id === activeExample.id;
                  return (
                    <li key={e.id}>
                      <button
                        type="button"
                        onClick={() => setSelectedExampleId(e.id)}
                        aria-pressed={selected}
                        className={cn(
                          "flex w-full cursor-pointer items-center justify-between gap-3 py-2.5 text-left outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
                          selected ? "text-ink" : "text-ink-2 hover:text-ink",
                        )}
                      >
                        <span className="flex min-w-0 items-center gap-2.5">
                          <span
                            className={cn(
                              "flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold",
                              selected ? "bg-brand text-white" : "bg-ground text-ink-2",
                            )}
                          >
                            {e.id}
                          </span>
                          <span className="min-w-0">
                            <span className="block truncate text-sm font-medium">{e.profile}</span>
                            <span className="block text-xs text-ink-2">
                              Vị trí <span className="text-numeric font-semibold">{e.position}%</span>
                            </span>
                          </span>
                        </span>
                        <Badge variant={groupVariant(e.group)}>{groupLabel(e.group)}</Badge>
                      </button>
                    </li>
                  );
                })}
              </ul>

              <p className="mt-3 border-t border-line pt-3 text-xs leading-relaxed text-ink-2">
                {activeExample.note}
              </p>
            </div>

            <div className="mt-3 flex max-w-lg items-center gap-1.5 text-xs text-white/80">
              <MapPinIcon className="size-3.5" />
              <span>{HERO_CAMPUS[0].name} · {HERO_CAMPUS[0].city}</span>
            </div>
          </div>
        </div>
      </section>

      {/* NỘI DUNG CHÍNH: cùng nhịp rộng max-w-[1560px] và khoảng thở với trang chủ */}
      <div className="mx-auto max-w-[1560px] space-y-24 px-4 pt-16 pb-24 sm:space-y-32 sm:px-8 sm:pt-24 lg:px-12">
        {/* BƯỚC XỬ LÝ: tiêu đề dính bên trái, 3 bước đánh số bên phải (giống StepsWheel) */}
        <section className="grid gap-8 lg:grid-cols-12 lg:gap-16">
          <div className="space-y-3 lg:sticky lg:top-[calc(var(--header-h)+2rem)] lg:col-span-5 lg:self-start">
            <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">
              Ba bước từ hồ sơ đến danh sách trường
            </h2>
            <p className="max-w-[48ch] text-body text-pretty text-ink-2">
              Mỗi trường đi qua cùng một quy trình cố định. Cùng hồ sơ và cùng số liệu trường luôn cho cùng một kết quả.
            </p>
          </div>

          <ol className="space-y-3 lg:col-span-7">
            {STEPS.map((step, index) => {
              const Icon = step.icon;
              return (
                <li
                  key={step.title}
                  className="flex items-center gap-5 rounded-xl border border-line bg-surface p-5 transition-colors hover:border-brand/30 sm:gap-6 sm:px-6"
                >
                  <span className="w-12 shrink-0 font-heading text-4xl font-semibold text-ink tabular-nums sm:text-5xl">
                    0{index + 1}
                  </span>
                  <div className="min-w-0 flex-1 space-y-1">
                    <h3 className="font-heading text-lg font-semibold text-ink">{step.title}</h3>
                    <p className="text-sm leading-relaxed text-pretty text-ink-2">{step.body}</p>
                  </div>
                  <span className={cn("flex size-12 shrink-0 items-center justify-center rounded-lg", step.tone)}>
                    <Icon aria-hidden className="size-6" />
                  </span>
                </li>
              );
            })}
          </ol>
        </section>

        {/* KHI NÀO HẠ NHÓM & CHI PHÍ: tương tác chọn quy tắc bên trái, kết quả bên phải */}
        <section className="grid gap-10 lg:grid-cols-12 lg:items-start lg:gap-16">
          <div className="space-y-6 lg:col-span-6">
            <div className="space-y-2">
              <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">
                Điểm cao chưa chắc là An toàn hơn
              </h2>
              <p className="max-w-[54ch] text-body text-pretty text-ink-2">
                Bốn quy tắc an toàn có thể hạ nhóm của trường dù vị trí học thuật của bạn cao. Chọn từng trường hợp để xem tác động.
              </p>
            </div>

            <div className="divide-y divide-line rounded-xl border border-line bg-surface">
              {SCENARIOS.map((s) => {
                const active = s.id === activeScenario.id;
                return (
                  <button
                    key={s.id}
                    type="button"
                    onClick={() => setSelectedScenarioId(s.id)}
                    aria-pressed={active}
                    className={cn(
                      "flex w-full cursor-pointer items-center justify-between gap-4 px-5 py-4 text-left transition-colors focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
                      active ? "bg-brand-wash/60" : "hover:bg-ground/70",
                    )}
                  >
                    <span className="min-w-0">
                      <span className="block text-sm font-semibold text-ink">{s.title}</span>
                      <span className="mt-0.5 block text-sm text-ink-2">{s.condition}</span>
                    </span>
                    <Badge variant={groupVariant(s.variant)} className="shrink-0">
                      {s.effect}
                    </Badge>
                  </button>
                );
              })}
            </div>
          </div>

          <div className="space-y-6 lg:col-span-6">
            <div className="rounded-2xl border border-line bg-surface p-6 sm:p-7">
              <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line pb-4">
                <div>
                  <p className="text-xs font-medium text-ink-2">Quy tắc đang xem</p>
                  <h3 className="mt-0.5 font-heading text-xl font-semibold text-ink">{activeScenario.title}</h3>
                </div>
                <Badge variant={groupVariant(activeScenario.variant)} className="h-7 px-3 text-xs font-semibold">
                  {activeScenario.effect}
                </Badge>
              </div>
              <dl className="mt-4 space-y-3 text-sm">
                <div className="flex items-baseline justify-between gap-4">
                  <dt className="text-ink-2">Điều kiện kích hoạt</dt>
                  <dd className="font-medium text-ink text-right">{activeScenario.condition}</dd>
                </div>
                <div className="rounded-lg bg-ground p-4 text-sm leading-relaxed text-ink">
                  {activeScenario.example}
                </div>
              </dl>
            </div>

            <div className="rounded-2xl border border-line bg-surface p-6 sm:p-7">
              <h3 className="font-heading text-lg font-semibold text-ink">Còn chi phí và tiếng Anh thì sao</h3>
              <p className="mt-1 text-sm text-ink-2">
                Chi phí không tạo thêm nhóm. Nó quyết định trường nằm trong ba nhóm, vào khối riêng, hay bị loại ở bước lọc.
              </p>
              <ul className="mt-4 divide-y divide-line border-t border-line">
                {COST_OUTCOMES.map((item) => {
                  const Icon = item.icon;
                  return (
                    <li key={item.condition} className="flex flex-wrap items-center gap-3 py-3 text-sm">
                      <Icon aria-hidden className={cn("size-4.5 shrink-0", item.tone)} />
                      <span className="min-w-0 flex-1 text-ink">{item.condition}</span>
                      {item.badge ? (
                        <Badge variant={groupVariant(item.badge)}>{item.result}</Badge>
                      ) : (
                        <span className="font-medium text-ink-2">{item.result}</span>
                      )}
                    </li>
                  );
                })}
              </ul>
              <p className="mt-3 border-t border-line pt-3 text-xs leading-relaxed text-ink-2">
                Tiếng Anh cũng không tạo nhóm: chưa có chứng chỉ, hoặc thiếu từ 1 band so với mức tối thiểu của trường thì trường bị loại ở bước lọc.
              </p>
            </div>
          </div>
        </section>

        {/* ĐIỂM PHÙ HỢP: dải nền brand-wash giống mục Trung tâm trên trang chủ */}
        <section className="grid gap-10 rounded-xl bg-brand-wash p-6 sm:p-10 lg:grid-cols-12 lg:gap-16 lg:p-12">
          <div className="space-y-4 lg:col-span-5">
            <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">
              Cách sắp thứ tự trong từng nhóm
            </h2>
            <p className="text-body text-pretty text-ink-2">
              Trường nào hợp với bạn hơn thì đứng trước. Điểm phù hợp gồm năm thành phần theo tỉ lệ cố định bên cạnh.
            </p>
            <p className="text-sm leading-relaxed text-ink-2">
              Ba thanh ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng) chỉ đổi thứ tự trong nhóm, không đổi nhóm của trường.
            </p>
          </div>

          <div className="space-y-6 lg:col-span-7">
            <div
              role="img"
              aria-label={`Tỉ lệ năm phần của điểm phù hợp: ${FIT_PARTS.map((p) => `${p.name} ${p.weight}%`).join(", ")}.`}
              className="flex overflow-hidden rounded-xl border border-line bg-surface"
            >
              {FIT_PARTS.map((p) => (
                <div
                  key={p.name}
                  style={{ width: `${p.weight}%` }}
                  className={cn("flex h-12 items-center justify-center text-sm font-semibold tabular-nums", p.tone)}
                >
                  {p.weight}%
                </div>
              ))}
            </div>

            <ul className="grid gap-4 sm:grid-cols-2">
              {FIT_PARTS.map((p) => (
                <li key={p.name} className="flex gap-3 rounded-xl border border-line bg-surface p-4 text-sm">
                  <span className={cn("mt-1 size-3 shrink-0 rounded-sm", p.tone.split(" ")[0])} aria-hidden />
                  <div>
                    <div className="font-semibold text-ink">
                      {p.name} · <span className="text-numeric">{p.weight}%</span>
                    </div>
                    <p className="mt-0.5 text-ink-2">{p.body}</p>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        </section>

        {/* GIỚI HẠN & VAI TRÒ CỦA AI: lưới chia vạch giống mục Săn học bổng trên trang chủ */}
        <section className="space-y-10">
          <div className="max-w-2xl space-y-2">
            <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">
              Giới hạn của mô hình và vai trò của AI
            </h2>
            <p className="text-body text-pretty text-ink-2">
              AI hỗ trợ đọc bảng điểm và viết lời giải thích, không tự tạo số liệu và không quyết định nhóm thay bạn.
            </p>
          </div>

          <div className="space-y-4">
            <dl className="grid divide-y divide-line rounded-xl border border-line bg-surface md:grid-cols-3 md:divide-x md:divide-y-0">
              {LIMITS.map((item) => (
                <div key={item.title} className="flex flex-col gap-2 p-5 sm:p-6">
                  <div className="flex items-center gap-2 text-warn">
                    <ShieldAlertIcon aria-hidden className="size-4.5 shrink-0" />
                    <span className="text-xs font-medium">Giới hạn</span>
                  </div>
                  <dt className="text-h3 text-ink">{item.title}</dt>
                  <dd className="text-sm leading-relaxed text-ink-2">{item.body}</dd>
                </div>
              ))}
            </dl>

            <dl className="grid divide-y divide-line rounded-xl border border-line bg-surface md:grid-cols-3 md:divide-x md:divide-y-0">
              {AI_RULES.map((r) => {
                const Icon = r.icon;
                return (
                  <div key={r.title} className="flex flex-col gap-2 p-5 sm:p-6">
                    <div className="flex items-center gap-2 text-brand">
                      <Icon aria-hidden className="size-4.5 shrink-0" />
                      <span className="text-xs font-medium">Phân tách AI</span>
                    </div>
                    <dt className="text-h3 text-ink">{r.title}</dt>
                    <dd className="text-sm leading-relaxed text-ink-2">{r.body}</dd>
                  </div>
                );
              })}
            </dl>
          </div>
        </section>

        {/* THAM SỐ & QUY TẮC CHI TIẾT: tab chuyển đổi sạch sẽ, không ẩn trong accordion */}
        <section id="parameters" className="scroll-mt-[calc(var(--header-h)+1.5rem)] space-y-6">
          <div className="max-w-2xl space-y-2">
            <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">
              Quy tắc chi tiết và tham số đang chạy
            </h2>
            <p className="text-body text-pretty text-ink-2">
              Toàn bộ ngưỡng phân nhóm, tham số thuật toán và bảng quy đổi điểm được đọc trực tiếp từ hệ thống.
            </p>
          </div>

          <Tabs defaultValue="rules" className="space-y-6">
            <TabsList variant="line" className="h-auto w-full justify-start gap-4 overflow-x-auto border-b border-line p-0">
              <TabsTrigger value="rules" className="flex-none px-3 py-2.5 text-sm">
                Quy tắc chi tiết
              </TabsTrigger>
              <TabsTrigger value="parameters" className="flex-none px-3 py-2.5 text-sm">
                Các tham số đang chạy
              </TabsTrigger>
              <TabsTrigger value="grade-scale" className="flex-none px-3 py-2.5 text-sm">
                Bảng quy đổi điểm 10 → 4
              </TabsTrigger>
            </TabsList>

            <TabsContent value="rules" className="space-y-6 rounded-xl border border-line bg-surface p-5 sm:p-6">
              <div>
                <h3 className="text-base font-semibold text-ink">Cách tính vị trí học thuật</h3>
                <ul className="mt-2 list-disc space-y-1.5 pl-4 text-sm leading-relaxed text-ink-2">
                  <li>
                    Vị trí theo GPA: dùng phân bố GPA của trường nếu có; không có thì giả định hình chuông quanh GPA trung bình của trường (độ lệch 0,3).
                  </li>
                  <li>
                    Vị trí theo điểm thi (SAT, hoặc ACT nếu không có SAT): từ mốc 25% và 75% của trường, giả định điểm phân bố hình chuông. Hai bài thi không quy đổi qua nhau.
                  </li>
                  <li>
                    Hai vị trí được gộp có trọng số theo mức trường coi trọng GPA và điểm thi (rất coi trọng 1; quan trọng 0,75; có xét 0,5; không xét 0). Thiếu một chỉ số thì dùng chỉ số còn lại.
                  </li>
                  <li>
                    Ngoại khoá: bậc 1 đến 5 tính từ hoạt động và giải thưởng bạn khai (vai trò, phạm vi, thời gian). Bậc 3 là trung bình, không điều chỉnh. Mỗi bậc lệch cộng hoặc trừ 5 điểm phần trăm, nhân với mức trường coi trọng ngoại khoá, tối đa 10 điểm phần trăm.
                  </li>
                  <li>GPA của bạn được quy đổi sang thang 4,0 theo quy tắc quy đổi của hệ thống trước khi so sánh.</li>
                </ul>
              </div>

              <div>
                <h3 className="text-base font-semibold text-ink">Ngưỡng và các quy tắc có thể hạ nhóm</h3>
                <div className="mt-2 overflow-x-auto">
                  <table className="w-full min-w-[520px] text-sm">
                    <thead>
                      <tr className="border-b border-line text-left text-xs text-ink-2">
                        <th className="py-2.5 pr-3 font-semibold">Điều kiện</th>
                        <th className="py-2.5 font-semibold">Kết quả</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr className="border-b border-line">
                        <td className="py-3 pr-3 text-ink">Vị trí học thuật dưới 25%</td>
                        <td className="py-3 text-ink-2">Khó vào</td>
                      </tr>
                      <tr className="border-b border-line">
                        <td className="py-3 pr-3 text-ink">Vị trí học thuật từ 25% đến dưới 75%</td>
                        <td className="py-3 text-ink-2">Vừa sức</td>
                      </tr>
                      <tr className="border-b border-line">
                        <td className="py-3 pr-3 text-ink">Vị trí học thuật từ 75%</td>
                        <td className="py-3 text-ink-2">An toàn hơn (nếu không vướng các quy tắc dưới đây)</td>
                      </tr>
                      {RULES.map((r) => (
                        <tr key={r.rule} className="border-b border-line last:border-0">
                          <td className="py-3 pr-3 text-ink">{r.rule}</td>
                          <td className="py-3 text-ink-2">{r.effect}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              <div>
                <h3 className="text-base font-semibold text-ink">Điểm phù hợp</h3>
                <p className="mt-1 text-sm leading-relaxed text-ink-2">
                  Mỗi thành phần có giá trị từ 0 đến 1, nhân với trọng số rồi cộng lại. Thành phần nào thiếu số liệu thì bị bỏ ra và trọng số được chia lại cho các thành phần còn lại. Ba thanh ưu tiên, mỗi thanh từ 0 đến 4 với 2 là cân bằng, nhân trọng số của ngân sách, học bổng và chất lượng với mức chọn chia 2.
                </p>
              </div>
            </TabsContent>

            <TabsContent value="parameters" className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <ParameterList data={parameters} />
            </TabsContent>

            <TabsContent value="grade-scale" className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <GradeScaleTable config={gradeScale} />
            </TabsContent>
          </Tabs>
        </section>

        {/* CTA CUỐI: đồng bộ với trang chủ */}
        <section className="flex flex-col items-start gap-5 border-t border-line pt-12 sm:flex-row sm:items-center sm:justify-between">
          <h2 className="text-h1 text-balance text-ink">Kiểm tra hồ sơ của bạn với các ngưỡng này</h2>
          <div className="flex flex-wrap gap-3">
            <Link href="/analysis" className={buttonVariants({ size: "xl" })}>
              Phân tích hồ sơ
              <ArrowRightIcon />
            </Link>
            <OpenAssistantButton size="xl" question="Vì sao chia trường thành 3 nhóm?" />
          </div>
        </section>
      </div>
    </>
  );
}
