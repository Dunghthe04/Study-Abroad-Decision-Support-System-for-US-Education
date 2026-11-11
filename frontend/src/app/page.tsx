"use client";

import { Suspense, useCallback, useEffect, useRef, useState, type ComponentType } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowRightIcon,
  ArrowUpIcon,
  AwardIcon,
  BookOpenCheckIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ExternalLinkIcon,
  FileTextIcon,
  HandCoinsIcon,
  HeartHandshakeIcon,
  LayoutGridIcon,
  MapPinIcon,
  Maximize2Icon,
  MessageSquareTextIcon,
  PenLineIcon,
  PlaneIcon,
  PlusIcon,
  ShieldAlertIcon,
  SlidersHorizontalIcon,
  UsersIcon,
  WalletIcon,
  XIcon,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Spinner } from "@/components/ui/spinner";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { useAssistant } from "@/components/assistant/AssistantProvider";
import { LandingHero } from "@/components/landing/LandingHero";
import { LayerStack } from "@/components/landing/LayerStack";
import { UniversityCard } from "@/components/universities/UniversityCard";
import { LANDING_SCHOOLS, type LandingSchool } from "@/lib/landing-schools";
import { searchUniversities } from "@/lib/university-api";
import { useAuth } from "@/contexts/AuthContext";
import { GROUP_LABELS } from "@/lib/recommendation-labels";
import type { UniversityListItem } from "@/types/university";
import { cn } from "@/lib/utils";

type IconType = ComponentType<{ className?: string }>;

/* ----------------------------- Dữ liệu trang ----------------------------- */

const STEPS: {
  title: string;
  desc: string;
  icon: IconType;
  color: string;
}[] = [
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
    color: "text-brand-ink",
  },
];

const ASK_EXAMPLES = [
  "GPA 3.5, SAT 1350 thì nên nộp trường nào?",
  "So sánh University of Washington và Purdue ngành Khoa học Máy tính",
  "Trường nào có học bổng cho sinh viên quốc tế?",
];

const SCHOLARSHIPS: { icon: IconType; title: string; tag: string; desc: string; examples: string }[] = [
  {
    icon: HeartHandshakeIcon,
    title: "Xin bằng giấy nhu cầu tài chính?",
    tag: "Need-based",
    desc: "Trường xét khả năng chi trả của gia đình. Một số trường top đầu xét need-blind, tức không để nhu cầu hỗ trợ ảnh hưởng đến quyết định nhận.",
    examples: "Harvard, MIT, Yale, Princeton, Amherst",
  },
  {
    icon: AwardIcon,
    title: "Xin bằng thành tích?",
    tag: "Merit-based",
    desc: "Dựa trên GPA, điểm chuẩn hóa và hoạt động nổi bật. Nhiều trường công lập và tư thục tầm trung có loại này cho sinh viên quốc tế.",
    examples: "Arizona State, University of Alabama",
  },
  {
    icon: WalletIcon,
    title: "Học phí thấp hoặc miễn học phí?",
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

function fallbackToUniversityListItem(s: LandingSchool, idx: number): UniversityListItem {
  const [city, state] = s.city.split(", ");
  return {
    id: `landing-${idx}`,
    code: s.name.toLowerCase().replace(/[^a-z0-9]+/g, "-"),
    name: s.name,
    city: city ?? null,
    state: state ?? null,
    stateName: null,
    control: s.type === "public" ? "public" : "private",
    acceptanceRate: s.acceptRate,
    isIvy: s.typeLabel === "Ivy League",
    logoUrl: null,
    coverUrl: s.image,
    tuitionUsd: null,
    totalCostUsd: null,
    minIelts: null,
    minToefl: null,
    minDuolingo: null,
    bestRank: null,
    bestRankList: null,
    satPolicy: null,
    intlAidPolicy: null,
    majors: s.majors,
  };
}

function SectionHead({ title, desc, className }: { title: string; desc?: string; className?: string }) {
  return (
    <div className={cn("max-w-2xl space-y-2", className)}>
      <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">{title}</h2>
      {desc && <p className="text-body text-pretty text-ink-2">{desc}</p>}
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
  const [customQuestion, setCustomQuestion] = useState("");
  const [schools, setSchools] = useState<UniversityListItem[]>(() =>
    LANDING_SCHOOLS.map(fallbackToUniversityListItem),
  );
  const [schoolsLoading, setSchoolsLoading] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = useState(false);
  const [canScrollRight, setCanScrollRight] = useState(false);

  useEffect(() => {
    let alive = true;
    searchUniversities({ sortBy: "rank", pageSize: 10 })
      .then((res) => {
        if (!alive) return;
        if (res.items && res.items.length > 0) {
          setSchools(res.items);
        }
      })
      .catch(() => {
        // Giữ fallback đã khởi tạo từ LANDING_SCHOOLS
      })
      .finally(() => {
        if (alive) setSchoolsLoading(false);
      });
    return () => {
      alive = false;
    };
  }, []);

  const updateScrollState = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    setCanScrollLeft(el.scrollLeft > 4);
    setCanScrollRight(el.scrollLeft < el.scrollWidth - el.clientWidth - 4);
  }, []);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    updateScrollState();
    el.addEventListener("scroll", updateScrollState, { passive: true });
    window.addEventListener("resize", updateScrollState);
    return () => {
      el.removeEventListener("scroll", updateScrollState);
      window.removeEventListener("resize", updateScrollState);
    };
  }, [schools, schoolsLoading, updateScrollState]);

  const scrollByCard = (direction: "left" | "right") => {
    const el = scrollRef.current;
    if (!el) return;
    const firstChild = el.firstElementChild as HTMLElement | null;
    const delta = firstChild ? firstChild.offsetWidth + 20 : 340;
    el.scrollBy({ left: direction === "left" ? -delta : delta, behavior: "smooth" });
  };

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
                    <s.icon className={cn("size-7.5 shrink-0", s.color)} />
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
        <div className="space-y-6 lg:col-span-6">
          <div className="space-y-3">
            <h2 className="text-display text-balance text-ink">USAS Advisor</h2>
            <p className="text-body text-pretty text-ink-2">
              <span className="font-semibold text-ink">Trợ lý đắc lực của bạn.</span> Hỏi bằng tiếng Việt về điểm chuẩn, học phí, học bổng hay so sánh trường. Advisor trả lời từ số liệu trong hệ thống và gợi ý bước tiếp theo cho hồ sơ của bạn.
            </p>
          </div>
          <div className="flex flex-col items-start gap-2">
            {ASK_EXAMPLES.map((q) => (
              <Button
                key={q}
                variant="outline"
                className="h-auto min-h-10 max-w-full justify-start border-line bg-surface px-3.5 py-2.5 text-left text-sm text-ink whitespace-normal transition-colors hover:border-brand/40 hover:bg-brand-wash"
                onClick={() => openPanel(q)}
              >
                <MessageSquareTextIcon className="size-4 shrink-0 text-brand" />
                <span>{q}</span>
              </Button>
            ))}
          </div>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const q = customQuestion.trim();
              if (!q) return;
              openPanel(q);
              setCustomQuestion("");
            }}
            className="relative w-full max-w-md pt-1"
          >
            <label htmlFor="landing-advisor-input" className="sr-only">
              Đặt câu hỏi cho USAS Advisor
            </label>
            <input
              id="landing-advisor-input"
              type="text"
              maxLength={1000}
              value={customQuestion}
              onChange={(e) => setCustomQuestion(e.target.value)}
              placeholder="Câu hỏi của bạn…"
              className="h-11 w-full rounded-xl border border-line bg-surface pr-12 pl-4 text-sm text-ink outline-none transition-colors placeholder:text-ink-3 focus-visible:border-brand focus-visible:ring-3 focus-visible:ring-ring/20"
            />
            <button
              type="submit"
              aria-label="Gửi câu hỏi"
              disabled={!customQuestion.trim()}
              className="absolute top-[calc(50%+2px)] right-1.5 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg bg-brand text-brand-foreground transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ArrowUpIcon className="size-4" />
            </button>
          </form>
        </div>

        <Card className="gap-0 overflow-hidden rounded-2xl border-line bg-surface py-0 lg:col-span-6 lg:max-w-xl lg:justify-self-end" aria-label="Ví dụ hội thoại với trợ lý">
          <div aria-hidden className="flex items-center gap-2 px-4 py-3 select-none">
            <span className="inline-flex size-8 shrink-0 items-center justify-center rounded-full border border-line text-ink-2">
              <PlusIcon className="size-4" />
            </span>
            <span className="min-w-0 flex-1 truncate font-heading text-base font-semibold text-ink">
              USAS Advisor
            </span>
            <Badge variant="neutral">Minh họa</Badge>
            <div className="flex shrink-0 items-center rounded-lg border border-line p-0.5">
              <span className="inline-flex size-7 items-center justify-center text-ink-2">
                <Maximize2Icon className="size-3.5" />
              </span>
              <span className="inline-flex size-7 items-center justify-center text-ink-2">
                <XIcon className="size-4" />
              </span>
            </div>
          </div>
          <div className="flex flex-col gap-4 bg-surface p-4 sm:p-5 text-[14.5px] leading-relaxed">
            <div className="rounded-xl bg-brand-wash px-4 py-3 text-ink">
              So sánh giúp em University of Washington và Purdue, em muốn học Khoa học Máy tính
            </div>
            <div className="w-full px-1 space-y-2 text-ink">
              <p>Hai trường khác nhau rõ nhất ở môi trường học:</p>
              <ul className="list-disc space-y-1 pl-4 marker:text-brand">
                <li>
                  <b>University of Washington</b> ở Seattle, gần các công ty công nghệ, thuận lợi cho thực tập.
                </li>
                <li>
                  <b>Purdue</b> có khuôn viên rộng, thiên về kỹ thuật, chi phí thấp hơn trường tư.
                </li>
              </ul>
              <p className="text-body-s text-ink-2">Nguồn: dữ liệu trường trong hệ thống</p>
            </div>
            <div className="rounded-xl bg-brand-wash px-4 py-3 text-ink">
              Em muốn xin học bổng thì sao?
            </div>
            <div className="w-full px-1 space-y-3 text-ink">
              <p>
                Cả hai trường có học bổng theo thành tích cho sinh viên quốc tế. Mức hỗ trợ phụ thuộc vào hồ sơ, nên bước tiếp theo là xem hồ sơ của em thuộc nhóm nào.
              </p>
              <div className="flex flex-wrap gap-2">
                <span className="inline-flex items-center gap-1 rounded-lg border border-brand/20 bg-brand-wash px-3 py-1.5 text-[13px] font-medium text-brand">
                  Phân tích hồ sơ
                </span>
                <span className="inline-flex items-center gap-1 rounded-lg border border-brand/20 bg-brand-wash px-3 py-1.5 text-[13px] font-medium text-brand">
                  Nhập ngân sách
                </span>
              </div>
            </div>
          </div>
          <div aria-hidden className="p-3 pt-1 select-none">
            <div className="flex flex-col gap-2 rounded-2xl border border-line bg-surface px-3.5 pt-3 pb-2.5">
              <div className="min-h-11 w-full text-sm text-ink-3">Hỏi USAS Advisor</div>
              <div className="flex size-9 shrink-0 items-center justify-center self-end rounded-lg bg-brand text-brand-foreground">
                <ArrowUpIcon className="size-4.5" />
              </div>
            </div>
          </div>
        </Card>
      </section>

      {/* CÁC LỚP CỦA HỆ THỐNG */}
      <section>
        <LayerStack />
      </section>

      {/* TRƯỜNG */}
      <section className="space-y-6">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-h1 text-balance text-ink sm:text-[2rem]">Mỹ có các trường gì?</h2>
          <Link
            href="/universities"
            className={buttonVariants({ variant: "outline", size: "default" })}
          >
            Xem tất cả
          </Link>
        </div>

        <div className="relative group/carousel">
          {/* Nút cuộn trái */}
          <button
            type="button"
            aria-label="Cuộn sang trái"
            disabled={!canScrollLeft}
            onClick={() => scrollByCard("left")}
            className="absolute -left-3 sm:-left-5 top-1/2 z-20 flex size-10 -translate-y-1/2 items-center justify-center rounded-full border border-line bg-surface text-ink shadow-sm transition-all hover:bg-ground active:scale-95 disabled:pointer-events-none disabled:opacity-0"
          >
            <ChevronLeftIcon className="size-5" />
          </button>

          {/* Dải thẻ trường cuộn ngang */}
          <div
            ref={scrollRef}
            className="flex gap-5 overflow-x-auto pb-4 pt-1 snap-x snap-mandatory scroll-smooth [scrollbar-width:none] [-webkit-overflow-scrolling:touch]"
          >
            {schoolsLoading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="w-[300px] sm:w-[340px] shrink-0 snap-start">
                    <Card className="gap-0 py-0">
                      <Skeleton className="aspect-[4/3] w-full" />
                      <div className="space-y-3 p-4">
                        <Skeleton className="h-5 w-3/4" />
                        <Skeleton className="h-4 w-1/2" />
                        <div className="grid grid-cols-2 gap-2 pt-2">
                          <Skeleton className="h-9 w-full" />
                          <Skeleton className="h-9 w-full" />
                        </div>
                      </div>
                    </Card>
                  </div>
                ))
              : schools.map((school) => (
                  <div key={school.id || school.code} className="w-[300px] sm:w-[340px] shrink-0 snap-start">
                    <UniversityCard university={school} />
                  </div>
                ))}
          </div>

          {/* Nút cuộn phải */}
          <button
            type="button"
            aria-label="Cuộn sang phải"
            disabled={!canScrollRight}
            onClick={() => scrollByCard("right")}
            className="absolute -right-3 sm:-right-5 top-1/2 z-20 flex size-10 -translate-y-1/2 items-center justify-center rounded-full border border-line bg-surface text-ink shadow-sm transition-all hover:bg-ground active:scale-95 disabled:pointer-events-none disabled:opacity-0"
          >
            <ChevronRightIcon className="size-5" />
          </button>
        </div>
      </section>

      {/* HỌC BỔNG */}
      <section className="space-y-8">
        <SectionHead title="Săn học bổng" />
        <dl className="grid divide-y divide-line rounded-xl border border-line bg-surface md:grid-cols-3 md:divide-x md:divide-y-0">
          {SCHOLARSHIPS.map((s) => (
            <div key={s.title} className="flex flex-col gap-3 p-5 sm:p-6">
              <div className="flex items-center justify-between gap-2">
                <s.icon className="size-5 text-brand" />
                <Badge variant="brand">{s.tag}</Badge>
              </div>
              <dt className="text-h3 text-ink">{s.title}</dt>
              <dd className="flex-1 text-sm leading-relaxed text-ink-2">{s.desc}</dd>
              <dd className="text-body-s text-ink-3">Ví dụ: {s.examples}</dd>
            </div>
          ))}
        </dl>
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
