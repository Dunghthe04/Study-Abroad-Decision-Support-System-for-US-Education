"use client";

import { Suspense, useEffect, useState, type ComponentType } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowRightIcon,
  AwardIcon,
  BookOpenCheckIcon,
  ExternalLinkIcon,
  FileTextIcon,
  HandCoinsIcon,
  HeartHandshakeIcon,
  LayoutGridIcon,
  MapPinIcon,
  MessageSquareTextIcon,
  PenLineIcon,
  PlaneIcon,
  ShieldAlertIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  WalletIcon,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Bubble, BubbleContent } from "@/components/ui/bubble";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Spinner } from "@/components/ui/spinner";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { useAssistant } from "@/components/assistant/AssistantProvider";
import { LandingHero } from "@/components/landing/LandingHero";
import { LayerStack } from "@/components/landing/LayerStack";
import { LANDING_SCHOOLS, type SchoolType } from "@/lib/landing-schools";
import { useAuth } from "@/contexts/AuthContext";
import { GROUP_LABELS } from "@/lib/recommendation-labels";
import { cn } from "@/lib/utils";

type IconType = ComponentType<{ className?: string }>;

/* ----------------------------- Dữ liệu trang ----------------------------- */

const STEPS: {
  title: string;
  desc: string;
  icon: IconType;
}[] = [
  {
    title: "Khai báo hồ sơ",
    desc: "Nhập điểm hoặc tải bảng điểm để quy đổi GPA sang thang 4.0 và lưu hồ sơ.",
    icon: FileTextIcon,
  },
  {
    title: "Xem trường theo nhóm",
    desc: `Xếp vào các nhóm ${GROUP_LABELS.R}, ${GROUP_LABELS.M}, ${GROUP_LABELS.S} kèm lý do rõ ràng.`,
    icon: LayoutGridIcon,
  },
  {
    title: "Thử kịch bản “nếu như”",
    desc: "Tăng điểm SAT hay đổi ngân sách để xem danh sách trường thay đổi ngay lập tức.",
    icon: SlidersHorizontalIcon,
  },
  {
    title: "Hỏi USAS Advisor",
    desc: "Hỏi so sánh trường, điều kiện tuyển sinh và chính sách học bổng bằng tiếng Việt.",
    icon: MessageSquareTextIcon,
  },
  {
    title: "Gặp trung tâm tư vấn",
    desc: "Kết nối trung tâm hỗ trợ bài luận, hoàn thiện hồ sơ và luyện phỏng vấn visa.",
    icon: UsersIcon,
  },
];

const ASK_EXAMPLES = [
  "GPA 3.5, SAT 1350 thì nên nộp trường nào?",
  "So sánh University of Washington và Purdue ngành Khoa học Máy tính",
  "Trường nào có học bổng cho sinh viên quốc tế?",
];

const SCHOOL_FILTERS: { key: "all" | SchoolType; label: string }[] = [
  { key: "all", label: "Tất cả" },
  { key: "private", label: "Tư thục" },
  { key: "public", label: "Công lập" },
  { key: "liberal", label: "Liberal Arts" },
];

const SCHOLARSHIPS: { icon: IconType; title: string; tag: string; desc: string; examples: string }[] = [
  {
    icon: HeartHandshakeIcon,
    title: "Theo nhu cầu tài chính",
    tag: "Need-based",
    desc: "Trường xét khả năng chi trả của gia đình. Một số trường top đầu xét need-blind, tức không để nhu cầu hỗ trợ ảnh hưởng đến quyết định nhận.",
    examples: "Harvard, MIT, Yale, Princeton, Amherst",
  },
  {
    icon: AwardIcon,
    title: "Theo thành tích",
    tag: "Merit-based",
    desc: "Dựa trên GPA, điểm chuẩn hóa và hoạt động nổi bật. Nhiều trường công lập và tư thục tầm trung có loại này cho sinh viên quốc tế.",
    examples: "Arizona State, University of Alabama",
  },
  {
    icon: WalletIcon,
    title: "Học phí thấp hoặc miễn học phí",
    tag: "Tiết kiệm",
    desc: "Trường có chi phí thấp, hoặc không thu học phí và đổi lại sinh viên làm việc trong trường.",
    examples: "Berea College, các trường công lập",
  },
];

const CENTER_SERVICES: { icon: IconType; title: string; desc: string }[] = [
  { icon: PenLineIcon, title: "Bài luận", desc: "Góp ý Common App và bài luận bổ sung." },
  { icon: FileTextIcon, title: "Hồ sơ", desc: "Rà soát giấy tờ, thư giới thiệu, bảng điểm." },
  { icon: BookOpenCheckIcon, title: "Luyện thi", desc: "Lộ trình SAT, IELTS, TOEFL theo mục tiêu." },
  { icon: PlaneIcon, title: "Visa F-1", desc: "Chứng minh tài chính và phỏng vấn visa." },
  { icon: HandCoinsIcon, title: "Học bổng", desc: "Chọn học bổng phù hợp và chuẩn bị đơn." },
  { icon: MapPinIcon, title: "Lộ trình", desc: "Kế hoạch từ lớp hiện tại đến ngày nhập học." },
];

const FAQS = [
  {
    q: "Hệ thống xếp trường vào nhóm như thế nào?",
    a: "USAS so GPA đã quy đổi, điểm chuẩn hóa, ngoại khóa và ngân sách của bạn với số liệu tuyển sinh từng trường. Kết quả là nhóm tham khảo, không phải xác suất đỗ.",
    link: { href: "/methodology", label: "Xem ngưỡng phân nhóm" },
  },
  {
    q: "Chưa có SAT hoặc IELTS thì dùng được không?",
    a: "Được. Hệ thống gợi ý từ những gì bạn đã có và cho biết thông tin nào còn thiếu để kết quả chính xác hơn.",
  },
  {
    q: "Trợ lý có bịa số liệu không?",
    a: "Trợ lý chỉ diễn đạt lại số liệu có trong hệ thống. Khi chưa có dữ liệu, trợ lý nói rõ là chưa có. Với quyết định quan trọng, hãy kiểm tra lại trên trang chính thức của trường.",
  },
  {
    q: "Tỷ lệ nhận của trường có phải là cơ hội đỗ của em không?",
    a: "Không. Tỷ lệ nhận là con số chung cho mọi hồ sơ nộp vào trường. USAS đặt nhóm của bạn ngay cạnh con số đó để bạn thấy hồ sơ mình đứng ở đâu so với trường, thay vì đoán từ tỷ lệ chung.",
  },
  {
    q: "Trung tâm tư vấn có trả tiền để được gợi ý trường không?",
    a: "Không. Danh sách trường chỉ dựa trên hồ sơ của bạn và số liệu tuyển sinh, theo các ngưỡng công khai ở trang Giới thiệu phương pháp. Trung tâm chỉ nhận yêu cầu khi bạn tự chọn và gửi.",
  },
];

/* --------------------------------- Thành phần -------------------------------- */

function SectionHead({ title, desc, className }: { title: string; desc?: string; className?: string }) {
  return (
    <div className={cn("max-w-2xl space-y-2", className)}>
      <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">{title}</h2>
      {desc && <p className="text-body text-pretty text-ink-2">{desc}</p>}
    </div>
  );
}

function SchoolImage({ src, alt }: { src: string; alt: string }) {
  const [broken, setBroken] = useState(false);
  if (broken) return <div aria-hidden className="aspect-[16/10] w-full bg-brand-wash" />;
  return (
    <div className="overflow-hidden">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt={alt} loading="lazy" onError={() => setBroken(true)} className="aspect-[16/10] w-full object-cover transition-transform duration-500 ease-out group-hover/card:scale-[1.03] motion-reduce:transition-none" />
    </div>
  );
}

/* --------------------------------- Trang -------------------------------- */

function HomeView() {
  const { user, isLoading } = useAuth();
  const { openPanel } = useAssistant();
  const router = useRouter();
  const searchParams = useSearchParams();
  const isPublicPreview = searchParams.get("view") === "public";
  const [schoolFilter, setSchoolFilter] = useState<"all" | SchoolType>("all");

  const isAdmin = user?.role === "admin";
  const isModerator = user?.role === "moderator";
  const isStaff = isAdmin || isModerator;
  const isStudentOrParent = user?.role === "student" || user?.role === "parent";

  useEffect(() => {
    if (!isLoading && !isPublicPreview && isStaff) router.replace("/admin");
  }, [isStaff, isLoading, isPublicPreview, router]);

  if (!isLoading && isStaff && !isPublicPreview) {
    return (
      <div className="flex min-h-[45vh] flex-col items-center justify-center gap-3 text-sm text-muted-foreground">
        <Spinner />
        <p>{isModerator ? "Đang chuyển đến khu vực kiểm duyệt…" : "Đang chuyển đến trang quản trị…"}</p>
      </div>
    );
  }

  const schools = schoolFilter === "all" ? LANDING_SCHOOLS : LANDING_SCHOOLS.filter((s) => s.type === schoolFilter);

  return (
    <>
    <LandingHero />
    <div className="mx-auto max-w-[1560px] space-y-20 px-4 pt-12 pb-20 sm:space-y-28 sm:px-8 sm:pt-16 lg:px-12">
      {isStaff && isPublicPreview && (
        <Alert variant="warning">
          <ShieldAlertIcon />
          <AlertTitle>Bạn đang xem trang chủ như học sinh và phụ huynh</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
            <span>Tài khoản {isModerator ? "kiểm duyệt" : "quản trị"} không thấy trang này khi dùng bình thường.</span>
            <Link href="/admin" className={buttonVariants({ variant: "outline", size: "sm" })}>
              {isModerator ? "Về khu vực kiểm duyệt" : "Về trang quản trị"}
              <ExternalLinkIcon />
            </Link>
          </AlertDescription>
        </Alert>
      )}

      {/* KHÔNG GIAN CÁ NHÂN (đã đăng nhập) */}
      {isStudentOrParent && (
        <section className="flex flex-col gap-4 rounded-xl border border-line bg-surface p-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-sans text-h3 text-ink">Chào {user?.fullName || "bạn"}</h2>
            <p className="text-sm text-ink-2">Tiếp tục từ chỗ bạn dừng lại.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link href="/analysis" className={buttonVariants({ size: "lg" })}>Tiếp tục phân tích</Link>
            <Link href="/target-schools" className={buttonVariants({ variant: "outline", size: "lg" })}>Trường mục tiêu</Link>
          </div>
        </section>
      )}

      {/* CÁCH HOẠT ĐỘNG */}
      <section className="space-y-8">
        <SectionHead title="Năm bước từ hồ sơ đến quyết định" />
        <ol className="grid gap-4 sm:grid-cols-2 lg:grid-flow-col lg:auto-cols-fr">
          {STEPS.map((s, i) => (
            <li key={s.title} className="relative flex">
              <Card className="flex flex-1 flex-col justify-between border-line bg-surface p-5">
                <div className="space-y-4">
                  <div className="flex items-center justify-between gap-3">
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-wash font-heading text-lg font-bold text-brand">
                      0{i + 1}
                    </span>
                    <div className="flex size-9 items-center justify-center rounded-lg bg-ground text-brand">
                      <s.icon className="size-5" />
                    </div>
                  </div>
                  <h3 className="font-heading text-base font-semibold text-ink">{s.title}</h3>
                  <p className="text-sm leading-relaxed text-ink-2">{s.desc}</p>
                </div>
              </Card>
              {i < STEPS.length - 1 && (
                <div
                  aria-hidden
                  className="pointer-events-none absolute top-10 -right-4 z-10 hidden h-0.5 w-4 bg-line lg:block"
                />
              )}
            </li>
          ))}
        </ol>
      </section>

      {/* TRỢ LÝ */}
      <section className="grid gap-10 lg:grid-cols-12 lg:items-center lg:gap-16">
        <div className="space-y-6 lg:col-span-5">
          <SectionHead
            title="Hỏi bằng tiếng Việt, nhiều ý trong một câu"
            desc="So sánh hai trường, hỏi học phí và học bổng cùng lúc. Trợ lý trả lời từ số liệu trong hệ thống và gợi ý bước tiếp theo."
          />
          <div className="flex flex-col items-start gap-2">
            {ASK_EXAMPLES.map((q) => (
              <Button key={q} variant="outline" className="h-auto min-h-9 max-w-full py-2 text-left whitespace-normal" onClick={() => openPanel(q)}>
                <MessageSquareTextIcon className="text-brand" />
                {q}
              </Button>
            ))}
          </div>
        </div>

        <Card className="gap-0 py-0 lg:col-span-7" aria-label="Ví dụ hội thoại với trợ lý">
          <div className="flex items-center justify-between border-b px-4 py-3">
            <span className="text-sm font-semibold text-ink">Trợ lý USAS</span>
            <Badge variant="neutral">Minh họa</Badge>
          </div>
          <div className="flex flex-col gap-4 bg-ground p-4 sm:p-5">
            <Bubble align="end">
              <BubbleContent>So sánh giúp em University of Washington và Purdue, em muốn học Khoa học Máy tính</BubbleContent>
            </Bubble>
            <Bubble variant="outline" className="max-w-[90%]">
              <BubbleContent className="space-y-2 bg-surface">
                <p>Hai trường khác nhau rõ nhất ở môi trường học:</p>
                <ul className="list-disc space-y-1 pl-4 marker:text-brand">
                  <li><b>University of Washington</b> ở Seattle, gần các công ty công nghệ, thuận lợi cho thực tập.</li>
                  <li><b>Purdue</b> có khuôn viên rộng, thiên về kỹ thuật, chi phí thấp hơn trường tư.</li>
                </ul>
                <p className="text-body-s">Nguồn: dữ liệu trường trong hệ thống</p>
              </BubbleContent>
            </Bubble>
            <Bubble align="end">
              <BubbleContent>Em muốn xin học bổng thì sao?</BubbleContent>
            </Bubble>
            <Bubble variant="outline" className="max-w-[90%]">
              <BubbleContent className="space-y-3 bg-surface">
                <p>Cả hai trường có học bổng theo thành tích cho sinh viên quốc tế. Mức hỗ trợ phụ thuộc vào hồ sơ, nên bước tiếp theo là xem hồ sơ của em thuộc nhóm nào.</p>
                <div className="flex flex-wrap gap-1.5">
                  <Badge variant="brand">Phân tích hồ sơ</Badge>
                  <Badge variant="brand">Nhập ngân sách</Badge>
                </div>
              </BubbleContent>
            </Bubble>
          </div>
        </Card>
      </section>

      {/* CÁC LỚP CỦA HỆ THỐNG */}
      <section className="space-y-10">
        <SectionHead
          title="Năm lớp làm việc cùng nhau để trả lời đúng"
          desc="Cuộn xuống hoặc bấm vào từng lớp để xem lớp đó làm gì."
        />
        <LayerStack />
      </section>

      {/* TRƯỜNG */}
      <section className="space-y-8">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <SectionHead title="Một số trường học sinh Việt Nam hay hỏi" desc="Kiểm tra nhanh trường nằm ở nhóm nào với hồ sơ của bạn." />
          <div role="group" aria-label="Lọc theo loại trường" className="flex flex-wrap gap-1.5">
            {SCHOOL_FILTERS.map((f) => (
              <Button
                key={f.key}
                variant={schoolFilter === f.key ? "secondary" : "ghost"}
                aria-pressed={schoolFilter === f.key}
                onClick={() => setSchoolFilter(f.key)}
              >
                {f.label}
              </Button>
            ))}
          </div>
        </div>

        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {schools.map((s) => (
            <Card key={s.name} className="!pt-0">
              <SchoolImage src={s.image} alt={s.name} />
              <CardHeader>
                <div className="flex items-start justify-between gap-2">
                  <CardTitle>{s.name}</CardTitle>
                  <Badge variant="neutral" className="mt-1">{s.typeLabel}</Badge>
                </div>
                <p className="flex items-center gap-1 text-body-s">
                  <MapPinIcon className="size-3.5" />
                  {s.city}
                </p>
              </CardHeader>
              <CardContent className="flex flex-1 flex-col gap-3">
                <p className="text-sm leading-relaxed text-ink-2">{s.highlight}</p>
                <p className="text-body-s">{s.majors.join(" · ")}</p>
              </CardContent>
              <CardFooter className="justify-between gap-2 bg-transparent">
                <Link href={`/analysis?school=${encodeURIComponent(s.name)}`} className={buttonVariants({ variant: "link", className: "px-0" })}>
                  Kiểm tra hồ sơ
                  <ArrowRightIcon />
                </Link>
                <Button variant="ghost" size="sm" onClick={() => openPanel(`Cho em biết điều kiện tuyển sinh và học bổng của ${s.name}`)}>
                  Hỏi trợ lý
                </Button>
              </CardFooter>
            </Card>
          ))}
        </div>
      </section>

      {/* HỌC BỔNG */}
      <section className="space-y-8">
        <SectionHead
          title="Ba hướng để giảm chi phí"
          desc="Nhập ngân sách, hệ thống tách riêng những trường chỉ đi được khi có học bổng lớn."
        />
        <dl className="grid divide-y divide-line rounded-xl border border-line bg-surface md:grid-cols-3 md:divide-x md:divide-y-0">
          {SCHOLARSHIPS.map((s) => (
            <div key={s.title} className="flex flex-col gap-3 p-5 sm:p-6">
              <div className="flex items-center justify-between gap-2">
                <s.icon className="size-5 text-brand" />
                <Badge variant="brand">{s.tag}</Badge>
              </div>
              <dt className="text-h3 text-ink">{s.title}</dt>
              <dd className="flex-1 text-sm leading-relaxed text-ink-2">{s.desc}</dd>
              <dd className="border-t border-line pt-3 text-body-s">Ví dụ: {s.examples}</dd>
            </div>
          ))}
        </dl>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-body-s">Chính sách học bổng thay đổi theo năm. Kiểm tra lại trên trang của trường trước khi nộp.</p>
          <Link href="/profile/financial" className={buttonVariants({ variant: "outline", size: "lg", className: "self-start" })}>
            Nhập ngân sách
            <ArrowRightIcon />
          </Link>
        </div>
      </section>

      {/* TRUNG TÂM */}
      <section className="grid gap-10 rounded-xl bg-brand-wash p-6 sm:p-10 lg:grid-cols-12 lg:gap-16">
        <div className="space-y-6 lg:col-span-5">
          <SectionHead
            title="Khi cần người làm cùng"
            desc="USAS lo phần phân tích và gợi ý trường. Bài luận, hồ sơ, luyện thi và visa do trung tâm tư vấn đảm nhận. Bạn chọn trung tâm và gửi yêu cầu ngay trên hệ thống, trung tâm sẽ liên hệ lại."
          />
          <Link href="/centers" className={buttonVariants({ size: "xl" })}>
            Xem các trung tâm
            <ArrowRightIcon />
          </Link>
        </div>
        <ul className="grid gap-x-8 gap-y-6 sm:grid-cols-2 lg:col-span-7">
          {CENTER_SERVICES.map((s) => (
            <li key={s.title} className="flex gap-3">
              <s.icon className="mt-0.5 size-5 shrink-0 text-brand" />
              <div>
                <div className="text-sm font-semibold text-ink">{s.title}</div>
                <div className="text-sm text-ink-2">{s.desc}</div>
              </div>
            </li>
          ))}
        </ul>
      </section>

      {/* FAQ */}
      <section className="grid gap-8 lg:grid-cols-12 lg:gap-16">
        <SectionHead title="Câu hỏi thường gặp" className="lg:col-span-4" />
        <Accordion className="border-t border-line lg:col-span-8">
          {FAQS.map((f) => (
            <AccordionItem key={f.q} value={f.q} className="border-line">
              <AccordionTrigger className="py-4 text-[15px] font-semibold text-ink">{f.q}</AccordionTrigger>
              <AccordionContent className="pb-4 text-sm leading-relaxed text-ink-2">
                <p className="max-w-[65ch]">{f.a}</p>
                {f.link && (
                  <Link href={f.link.href} className="mt-2 inline-flex items-center gap-1 font-medium text-brand underline-offset-4 hover:underline">
                    {f.link.label}
                    <ArrowRightIcon className="size-3.5" />
                  </Link>
                )}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </section>

      {/* CTA cuối */}
      <section className="flex flex-col items-start gap-5 border-t border-line pt-12 sm:flex-row sm:items-center sm:justify-between">
        <h2 className="text-h1 text-balance text-ink">Có danh sách trường đầu tiên sau vài phút</h2>
        <div className="flex flex-wrap gap-3">
          <Link href="/analysis" className={buttonVariants({ size: "xl" })}>
            Phân tích hồ sơ
            <ArrowRightIcon />
          </Link>
          <OpenAssistantButton size="xl">Hỏi trợ lý</OpenAssistantButton>
        </div>
      </section>
    </div>
    </>
  );
}

export default function HomePage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-[45vh] items-center justify-center gap-2 text-sm text-muted-foreground">
          <Spinner />
          <span>Đang tải…</span>
        </div>
      }
    >
      <HomeView />
    </Suspense>
  );
}
