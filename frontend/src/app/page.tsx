"use client";

import { Suspense, useEffect, useRef, useState, type ComponentType } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowRightIcon,
  AwardIcon,
  BotIcon,
  Building2Icon,
  CheckCircle2Icon,
  ChevronRightIcon,
  ExternalLinkIcon,
  FileScanIcon,
  FileTextIcon,
  GraduationCapIcon,
  HandCoinsIcon,
  HeartHandshakeIcon,
  MapPinIcon,
  MessageSquareTextIcon,
  PlaneIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  SlidersHorizontalIcon,
  StarIcon,
  TargetIcon,
  WalletIcon,
  PenLineIcon,
  BookOpenCheckIcon,
} from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { useAssistant } from "@/components/assistant/AssistantProvider";
import { useAuth } from "@/contexts/AuthContext";
import { Spinner } from "@/components/ui/spinner";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";

type IconType = ComponentType<{ className?: string }>;

// Gộp class đúng thứ tự (twMerge) để màu tùy biến ghi đè được màu mặc định của nút
const btn = ({ className, ...o }: NonNullable<Parameters<typeof buttonVariants>[0]> & { className?: string }) =>
  cn(buttonVariants(o), className);

/*
  Bảng màu "nước Mỹ": navy #0B1F44, đỏ #B31942, vàng #C9A227, kem #FAF6EE.
  Chỉ dùng trong trang chủ để tạo cảm giác riêng, không đổi theme chung của app.
*/

/* ----------------------------- Dữ liệu trang ----------------------------- */

const HERO_IMG =
  "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=1800&q=80";

const DEMO_TIERS = [
  { name: "Carnegie Mellon", tag: "Khó vào", dot: "bg-rose-400", badge: "bg-rose-500/15 text-rose-200 border-rose-300/30" },
  { name: "Univ. of Washington", tag: "Vừa sức", dot: "bg-amber-400", badge: "bg-amber-400/15 text-amber-200 border-amber-300/30" },
  { name: "Arizona State", tag: "An toàn", dot: "bg-emerald-400", badge: "bg-emerald-400/15 text-emerald-200 border-emerald-300/30" },
];

const TICKER = [
  "Harvard", "Stanford", "MIT", "Yale", "Princeton", "UC Berkeley", "Carnegie Mellon",
  "Univ. of Washington", "Purdue", "Arizona State", "Williams College", "Amherst College", "Berea College",
];

type SchoolType = "ivy" | "public" | "liberal";

const SCHOOLS: {
  name: string;
  city: string;
  type: SchoolType;
  typeLabel: string;
  image: string;
  highlight: string;
  majors: string[];
}[] = [
  {
    name: "Harvard University",
    city: "Cambridge, MA",
    type: "ivy",
    typeLabel: "Ivy League",
    image: "https://images.unsplash.com/photo-1583373834259-46cc92173cb7?auto=format&fit=crop&w=800&q=80",
    highlight: "Need-blind cho cả sinh viên quốc tế và cam kết đáp ứng toàn bộ nhu cầu tài chính được duyệt.",
    majors: ["Computer Science", "Economics", "Applied Math"],
  },
  {
    name: "Stanford University",
    city: "Stanford, CA",
    type: "ivy",
    typeLabel: "Tư thục top đầu",
    image: "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?auto=format&fit=crop&w=800&q=80",
    highlight: "Ở ngay trung tâm Thung lũng Silicon, gần các công ty công nghệ hàng đầu.",
    majors: ["AI & Data Science", "Management", "Bioengineering"],
  },
  {
    name: "University of Washington",
    city: "Seattle, WA",
    type: "public",
    typeLabel: "Công lập",
    image: "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?auto=format&fit=crop&w=800&q=80",
    highlight: "Mạnh về Khoa học Máy tính, nằm giữa thành phố công nghệ Seattle.",
    majors: ["Computer Science", "Bioengineering", "Information Systems"],
  },
  {
    name: "Purdue University",
    city: "West Lafayette, IN",
    type: "public",
    typeLabel: "Công lập",
    image: "https://images.unsplash.com/photo-1564981797816-1043664bf78d?auto=format&fit=crop&w=800&q=80",
    highlight: "Nổi tiếng về Kỹ thuật và Hàng không vũ trụ, học phí công lập dễ tiếp cận hơn.",
    majors: ["Aerospace Eng", "Mechanical Eng", "Cybersecurity"],
  },
  {
    name: "Arizona State University",
    city: "Tempe, AZ",
    type: "public",
    typeLabel: "Công lập",
    image: "https://images.unsplash.com/photo-1562774053-701939374585?auto=format&fit=crop&w=800&q=80",
    highlight: "Quy mô lớn, nhiều ngành, có học bổng merit cho sinh viên quốc tế.",
    majors: ["Software Engineering", "Business", "Data Science"],
  },
  {
    name: "Williams College",
    city: "Williamstown, MA",
    type: "liberal",
    typeLabel: "Liberal Arts",
    image: "https://images.unsplash.com/photo-1607237138185-eedd9c632b0b?auto=format&fit=crop&w=800&q=80",
    highlight: "Lớp học nhỏ, thầy cô theo sát từng sinh viên, thế mạnh về nghiên cứu bậc cử nhân.",
    majors: ["Economics", "Mathematics", "Political Science"],
  },
];

const SCHOOL_FILTERS: { key: "all" | SchoolType; label: string }[] = [
  { key: "all", label: "Tất cả" },
  { key: "ivy", label: "Tư thục top đầu" },
  { key: "public", label: "Công lập" },
  { key: "liberal", label: "Liberal Arts" },
];

const CAPABILITIES: {
  icon: IconType;
  title: string;
  desc: string;
  href: string;
  cta: string;
  accent: string;
}[] = [
  { icon: FileScanIcon, title: "Phân tích hồ sơ", desc: "Nhập thông tin hoặc tải bảng điểm, hệ thống đọc và quy đổi GPA sang thang 4.0 cho bạn.", href: "/analysis", cta: "Bắt đầu phân tích", accent: "from-[#0B1F44] to-[#1d3a73]" },
  { icon: TargetIcon, title: "Gợi ý trường theo 4 nhóm", desc: "Khó vào, Vừa sức, An toàn và Tài chính tối ưu, mỗi trường có lý do đi kèm.", href: "/recommendations", cta: "Xem trường gợi ý", accent: "from-[#B31942] to-[#d4455f]" },
  { icon: SlidersHorizontalIcon, title: "Thử kịch bản “nếu như…”", desc: "Tăng điểm SAT hay đổi ngân sách, xem danh sách trường thay đổi ra sao.", href: "/analysis", cta: "Thử mô phỏng", accent: "from-[#C9A227] to-[#e0bf4f]" },
  { icon: MessageSquareTextIcon, title: "Trợ lý hỏi đáp tiếng Việt", desc: "Hỏi điểm chuẩn, học phí, học bổng hay so sánh hai trường, trả lời từ số liệu có sẵn.", href: "/advisor", cta: "Mở trợ lý", accent: "from-[#0e5f7a] to-[#1b86a8]" },
  { icon: GraduationCapIcon, title: "Trường mục tiêu", desc: "Lưu những trường bạn thích, quay lại so sánh khi hồ sơ có thay đổi.", href: "/target-schools", cta: "Xem danh sách", accent: "from-[#5b3fa3] to-[#7f63c7]" },
  { icon: Building2Icon, title: "Kết nối trung tâm tư vấn", desc: "Cần người đồng hành thật sự? Chọn trung tâm phù hợp và gửi yêu cầu ngay trên hệ thống.", href: "/centers", cta: "Tìm trung tâm", accent: "from-[#0a7a4f] to-[#17a672]" },
];

const SCHOLARSHIPS: {
  icon: IconType;
  title: string;
  tag: string;
  desc: string;
  examples: string;
}[] = [
  {
    icon: HeartHandshakeIcon,
    title: "Học bổng theo nhu cầu tài chính",
    tag: "Need-based",
    desc: "Trường xét theo khả năng chi trả của gia đình. Một số trường top đầu xét hồ sơ không phụ thuộc vào việc bạn có cần hỗ trợ hay không (need-blind) và hỗ trợ cả sinh viên quốc tế.",
    examples: "Harvard · MIT · Yale · Princeton · Amherst",
  },
  {
    icon: AwardIcon,
    title: "Học bổng theo thành tích",
    tag: "Merit-based",
    desc: "Dựa trên GPA, điểm chuẩn hóa và hoạt động nổi bật. Nhiều trường công lập và tư thục tầm trung cấp học bổng này cho sinh viên quốc tế.",
    examples: "Arizona State · Univ. of Alabama · nhiều trường khác",
  },
  {
    icon: WalletIcon,
    title: "Trường học phí thấp hoặc miễn phí",
    tag: "Tối ưu ngân sách",
    desc: "Nhóm trường có chi phí thấp, hoặc không thu học phí và đổi lại sinh viên tham gia làm việc trong trường.",
    examples: "Berea College · các trường công lập",
  },
];

const CENTER_SERVICES: { icon: IconType; title: string; desc: string }[] = [
  { icon: PenLineIcon, title: "Chỉnh sửa bài luận", desc: "Góp ý Common App và bài luận bổ sung." },
  { icon: FileTextIcon, title: "Hoàn thiện hồ sơ", desc: "Rà soát giấy tờ, thư giới thiệu, bảng điểm." },
  { icon: BookOpenCheckIcon, title: "Luyện thi chứng chỉ", desc: "Lộ trình SAT, IELTS, TOEFL theo mục tiêu." },
  { icon: PlaneIcon, title: "Hồ sơ visa F-1", desc: "Chuẩn bị chứng minh tài chính, phỏng vấn visa." },
  { icon: HandCoinsIcon, title: "Săn học bổng", desc: "Chọn học bổng phù hợp và chuẩn bị đơn." },
  { icon: MapPinIcon, title: "Định hướng lộ trình", desc: "Lập kế hoạch từ lớp hiện tại đến ngày nhập học." },
];

const CENTER_STEPS = [
  { title: "Hệ thống hiểu hồ sơ của bạn", desc: "Phân tích xong, bạn biết mình đang mạnh và còn thiếu gì." },
  { title: "Chọn trung tâm phù hợp", desc: "Xem giới thiệu, dịch vụ của từng trung tâm rồi tự chọn." },
  { title: "Gửi yêu cầu tư vấn", desc: "Một lần bấm, trung tâm nhận yêu cầu và liên hệ lại với bạn." },
];

const FAQS = [
  {
    q: "Hệ thống chia trường thành các nhóm như thế nào?",
    a: "USAS so sánh hồ sơ của bạn (GPA đã quy đổi, điểm chuẩn hóa, ngoại khóa, ngân sách) với dữ liệu của từng trường để xếp vào Khó vào, Vừa sức, An toàn hoặc Tài chính tối ưu, kèm lý do cụ thể. Đây là gợi ý tham khảo, không phải cam kết trúng tuyển. Xem thêm ở trang Giới thiệu phương pháp.",
  },
  {
    q: "Em chưa có SAT hoặc IELTS thì có dùng được không?",
    a: "Được. Bạn có thể khai báo phần đã có, hệ thống dùng thông tin hiện có để gợi ý và cho biết thông tin nào còn thiếu để kết quả chính xác hơn.",
  },
  {
    q: "Trợ lý AI có trả lời sai hoặc bịa số liệu không?",
    a: "Trợ lý được thiết kế chỉ diễn đạt lại số liệu lấy từ hệ thống. Khi chưa có dữ liệu, trợ lý sẽ nói rõ là chưa có thay vì đoán. Với quyết định quan trọng, bạn vẫn nên kiểm tra lại trên trang chính thức của trường.",
  },
  {
    q: "Việc nào hệ thống làm, việc nào do trung tâm làm?",
    a: "USAS lo phần thông tin, phân tích và gợi ý. Các việc cần người đồng hành như chỉnh sửa bài luận, làm hồ sơ hay phỏng vấn visa được chuyển tới trung tâm tư vấn phù hợp.",
  },
];

/* --------------------------------- Thành phần -------------------------------- */

function SectionHead({ tag, title, desc, dark }: { tag: string; title: string; desc?: string; dark?: boolean }) {
  return (
    <div className="mx-auto max-w-2xl space-y-3 text-center">
      <div
        className={`inline-flex items-center gap-1.5 rounded-full px-3.5 py-1 text-[11px] font-bold uppercase tracking-[0.14em] ${
          dark ? "bg-white/10 text-[#e7c85a]" : "bg-[#B31942]/10 text-[#B31942]"
        }`}
      >
        <StarIcon className="size-3 fill-current" />
        {tag}
      </div>
      <h2 className={`font-heading text-2xl font-bold tracking-tight sm:text-4xl ${dark ? "text-white" : "text-[#0B1F44]"}`}>{title}</h2>
      {desc && <p className={`text-sm leading-relaxed sm:text-base ${dark ? "text-white/70" : "text-ink-2"}`}>{desc}</p>}
    </div>
  );
}

function StarsPattern() {
  return (
    <div
      aria-hidden
      className="pointer-events-none absolute inset-0 opacity-[0.07]"
      style={{
        backgroundImage:
          "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='44' height='44' viewBox='0 0 44 44'%3E%3Cpath fill='white' d='M22 14l1.9 5.1h5.4l-4.4 3.2 1.7 5.2-4.6-3.2-4.6 3.2 1.7-5.2-4.4-3.2h5.4z'/%3E%3C/svg%3E\")",
      }}
    />
  );
}

function Stripes({ className = "" }: { className?: string }) {
  return (
    <div
      aria-hidden
      className={`h-2 w-full ${className}`}
      style={{
        backgroundImage: "repeating-linear-gradient(90deg,#B31942 0 36px,#ffffff 36px 72px)",
      }}
    />
  );
}

function SchoolImage({ src, alt }: { src: string; alt: string }) {
  const [broken, setBroken] = useState(false);
  if (broken) {
    return <div className="h-full w-full bg-gradient-to-br from-[#0B1F44] to-[#1d3a73]" />;
  }
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src}
      alt={alt}
      loading="lazy"
      onError={() => setBroken(true)}
      className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-110"
    />
  );
}

/* ------------------- Hình các lớp hệ thống (xếp chồng 3D) ------------------- */

type Layer = {
  level: number;
  name: string;
  tech: string;
  title: string;
  desc: string;
  gives: string;
  bullets: string[];
  label: string;
};

// Thứ tự từ trên xuống dưới, khớp với hình
const LAYERS: Layer[] = [
  {
    level: 5,
    name: "Con người",
    tech: "Trung tâm tư vấn",
    title: "Trung tâm tư vấn đồng hành",
    desc: "Những việc cần người thật như chỉnh bài luận, làm hồ sơ hay visa được chuyển tới trung tâm phù hợp. Hệ thống làm cầu nối, không làm thay.",
    gives: "Có người đồng hành đúng lúc cần",
    bullets: ["Chọn trung tâm và gửi yêu cầu ngay trên hệ thống", "Phần việc rõ ràng, tách bạch với phần của hệ thống"],
    label: "Gửi yêu cầu tư vấn",
  },
  {
    level: 4,
    name: "Viết câu trả lời",
    tech: "Mô hình ngôn ngữ lớn (LLM)",
    title: "AI diễn đạt thành câu trả lời dễ hiểu",
    desc: "Nhận số liệu đã được lấy sẵn và viết lại thành tiếng Việt gọn gàng, có bố cục rõ ràng. Chỉ dùng đúng những gì được cung cấp.",
    gives: "Dễ đọc mà không bịa thêm",
    bullets: ["Không có số liệu thì nói rõ là chưa có", "Không hứa hẹn kết quả trúng tuyển"],
    label: "Chỉ viết từ số liệu có sẵn",
  },
  {
    level: 3,
    name: "Hiểu câu hỏi",
    tech: "Mô hình nhỏ (SLM)",
    title: "Hiểu đúng bạn đang hỏi gì",
    desc: "Một mô hình nhỏ, huấn luyện riêng cho bài toán du học, tách câu hỏi nhiều ý thành từng việc và chọn đúng công cụ để xử lý.",
    gives: "Hỏi tự nhiên vẫn được hiểu đúng",
    bullets: ["Câu hỏi có nhiều ý vẫn tách được", "Nhẹ và nhanh, không cần gọi mô hình lớn cho bước này"],
    label: "Tách câu hỏi nhiều ý",
  },
  {
    level: 2,
    name: "Phân tích và quy tắc",
    tech: "Hệ hỗ trợ ra quyết định",
    title: "Quy tắc rõ ràng để xếp nhóm trường",
    desc: "So hồ sơ của bạn với dữ liệu từng trường để xếp vào 4 nhóm, mô phỏng kịch bản “nếu như” và nêu lý do cho từng kết quả.",
    gives: "Kết quả có thể giải thích được",
    bullets: ["Khó vào · Vừa sức · An toàn · Tài chính", "Thử thay đổi điểm số để xem tác động"],
    label: "4 nhóm · Lý do từng trường",
  },
  {
    level: 1,
    name: "Dữ liệu và hồ sơ",
    tech: "OCR · Cơ sở dữ liệu",
    title: "Nền tảng là dữ liệu có nguồn",
    desc: "Đọc bảng điểm, quy đổi GPA sang thang 4.0 và lưu thông tin hồ sơ cùng dữ liệu các trường. Mọi lớp phía trên đều dựa vào đây.",
    gives: "Mọi con số đều có gốc",
    bullets: ["Tải bảng điểm để hệ thống đọc giúp", "Dữ liệu trường được quản trị và cập nhật"],
    label: "Bảng điểm · Dữ liệu trường",
  },
];

const PLATE_ACCENT = ["#B31942", "#C9A227", "#0e7a9a", "#B31942", "#0B1F44"];

function PlateDetail({ level, active }: { level: number; active: boolean }) {
  const s = active ? "#ffffff" : "#94a3b8";
  const f = (c: string) => (active ? c : "none");
  switch (level) {
    case 1:
      return (
        <g stroke={s} strokeWidth="1.4" fill="none">
          <ellipse cx="0" cy="-26" rx="26" ry="8" fill={f("#C9A227")} />
          <path d="M-26 -26 v16 a26 8 0 0 0 52 0 v-16" />
          <path d="M-26 -10 v16 a26 8 0 0 0 52 0 v-16" />
          <path d="M-26 6 v16 a26 8 0 0 0 52 0 v-16" />
        </g>
      );
    case 2:
      return (
        <g stroke={s} strokeWidth="1.2">
          <rect x="-40" y="-40" width="36" height="36" rx="5" fill={f("#fb7185")} />
          <rect x="4" y="-40" width="36" height="36" rx="5" fill={f("#fbbf24")} />
          <rect x="-40" y="4" width="36" height="36" rx="5" fill={f("#34d399")} />
          <rect x="4" y="4" width="36" height="36" rx="5" fill={f("#38bdf8")} />
        </g>
      );
    case 3:
      return (
        <g stroke={s} strokeWidth="1.4" fill="none">
          <rect x="-20" y="-20" width="40" height="40" rx="6" fill={f("#0e7a9a")} />
          <rect x="-8" y="-8" width="16" height="16" rx="3" />
          {[-12, 0, 12].map((p) => (
            <g key={p}>
              <path d={`M${p} -20 v-12 M${p} 20 v12 M-20 ${p} h-12 M20 ${p} h12`} />
            </g>
          ))}
        </g>
      );
    case 4:
      return (
        <g strokeLinecap="round" strokeWidth="5" stroke={active ? "#ffffff" : "#cbd5e1"}>
          <path d="M-40 -32 h50" stroke={active ? "#fde68a" : "#94a3b8"} />
          <path d="M-40 -16 h80 M-40 0 h68 M-40 16 h76 M-40 32 h40" />
        </g>
      );
    default:
      return (
        <g stroke={s} strokeWidth="1.4" fill="none">
          <circle cx="0" cy="-14" r="13" fill={f("#fde68a")} />
          <circle cx="-34" cy="2" r="9" fill={f("#fecdd3")} />
          <circle cx="34" cy="2" r="9" fill={f("#fecdd3")} />
          <path d="M-22 40 a22 22 0 0 1 44 0 z" fill={f("#fde68a")} />
          <path d="M-44 36 a14 14 0 0 1 24 -8 M44 36 a14 14 0 0 0 -24 -8" />
        </g>
      );
  }
}

function LayerStack() {
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
  const rows = [90, 200, 310, 420, 530]; // vị trí dọc của từng tầng, trên xuống dưới

  return (
    <div ref={wrapRef} className="lg:h-[260vh]">
      <div className="lg:sticky lg:top-24">
        <div className="grid items-center gap-8 lg:grid-cols-12">
          {/* Cột trái: timeline + nội dung */}
          <div className="order-2 flex gap-6 lg:order-1 lg:col-span-5">
            <div className="relative flex flex-col items-center py-2">
              <div className="absolute bottom-3 top-3 w-px bg-[#0B1F44]/15" />
              {LAYERS.map((l) => {
                const on = l.level === active;
                return (
                  <button
                    key={l.level}
                    type="button"
                    onClick={() => setActive(l.level)}
                    aria-label={`Lớp ${l.level}: ${l.name}`}
                    className="relative z-10 my-3 flex size-6 items-center justify-center"
                  >
                    <span
                      className={`rounded-full transition-all duration-300 ${
                        on ? "size-3.5 bg-[#B31942] ring-4 ring-[#B31942]/20" : "size-2.5 bg-[#0B1F44]/30 hover:bg-[#0B1F44]/60"
                      }`}
                    />
                  </button>
                );
              })}
            </div>

            <div key={current.level} className="usas-pop min-h-[320px] flex-1 space-y-4" style={{ animationDuration: "0.5s" }}>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-[11px] font-bold uppercase tracking-[0.16em] text-[#B31942]">Lớp {current.level} · {current.name}</span>
                <span className="rounded-full bg-[#0B1F44]/8 px-2.5 py-0.5 text-[10px] font-semibold text-[#0B1F44]/70 ring-1 ring-[#0B1F44]/10">{current.tech}</span>
              </div>
              <h3 className="font-heading text-2xl font-bold leading-tight text-[#0B1F44] sm:text-3xl">{current.title}</h3>
              <p className="text-sm leading-relaxed text-ink-2 sm:text-base">{current.desc}</p>
              <div className="rounded-2xl border border-[#C9A227]/40 bg-[#C9A227]/10 px-4 py-3">
                <div className="text-[10px] font-bold uppercase tracking-wider text-[#8a6d0f]">Bạn nhận được</div>
                <div className="text-sm font-semibold text-[#0B1F44]">{current.gives}</div>
              </div>
              <ul className="space-y-2">
                {current.bullets.map((b) => (
                  <li key={b} className="flex items-start gap-2.5 text-sm text-ink-2">
                    <CheckCircle2Icon className="mt-0.5 size-4 shrink-0 text-[#B31942]" />
                    {b}
                  </li>
                ))}
              </ul>
              <div className="flex gap-1.5 pt-1">
                {[1, 2, 3, 4, 5].map((n) => (
                  <button
                    key={n}
                    type="button"
                    onClick={() => setActive(n)}
                    className={`h-1.5 rounded-full transition-all ${n === active ? "w-8 bg-[#B31942]" : "w-4 bg-[#0B1F44]/15 hover:bg-[#0B1F44]/30"}`}
                    aria-label={`Lớp ${n}`}
                  />
                ))}
              </div>
            </div>
          </div>

          {/* Cột phải: các tầng 3D */}
          <div className="order-1 flex justify-center lg:order-2 lg:col-span-7">
            <svg viewBox="0 0 600 640" className="max-h-[70vh] w-full max-w-[600px] overflow-visible" role="img" aria-label="Năm lớp của hệ thống USAS xếp chồng lên nhau">
              <defs>
                <pattern id="plateGrid" width="9" height="9" patternUnits="userSpaceOnUse">
                  <path d="M9 0H0V9" fill="none" stroke="#0B1F44" strokeOpacity="0.1" strokeWidth="0.8" />
                </pattern>
                <linearGradient id="plateOn" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stopColor="#12306a" />
                  <stop offset="1" stopColor="#0B1F44" />
                </linearGradient>
              </defs>

              {/* Trục dữ liệu chạy xuyên các tầng */}
              <line x1="250" y1="20" x2="250" y2="620" stroke="#B31942" strokeOpacity="0.5" strokeWidth="1.2" strokeDasharray="3 6">
                <animate attributeName="stroke-dashoffset" from="0" to="-18" dur="1.2s" repeatCount="indefinite" />
              </line>
              <circle r="4.5" cx="250" fill="#B31942">
                <animate attributeName="cy" values="20;620" dur="4s" repeatCount="indefinite" />
                <animate attributeName="opacity" values="0;1;1;0" dur="4s" repeatCount="indefinite" />
              </circle>

              {/* Vẽ từ tầng dưới lên để tầng trên che đúng chỗ */}
              {[...LAYERS].reverse().map((l) => {
                const idx = LAYERS.indexOf(l);
                const on = l.level === active;
                const y = rows[idx] - (on ? 18 : 0);
                const accent = PLATE_ACCENT[idx];
                return (
                  <g
                    key={l.level}
                    onClick={() => setActive(l.level)}
                    className="cursor-pointer"
                    style={{
                      transform: `translate(250px, ${y}px)`,
                      transition: "transform 0.6s cubic-bezier(.2,.8,.2,1)",
                      filter: on ? "drop-shadow(0 22px 22px rgba(179,25,66,0.28))" : "none",
                    }}
                  >
                    {/* Cạnh dày của tầng, giả lập bằng nhiều bản sao lệch xuống */}
                    {Array.from({ length: 11 }, (_, k) => 10 - k).map((k) => (
                      <g key={k} transform={`translate(0 ${k * 1.5})`}>
                        <g transform="matrix(0.866 0.5 -0.866 0.5 0 0)">
                          <rect
                            x="-65"
                            y="-65"
                            width="130"
                            height="130"
                            rx="14"
                            fill={on ? "#0B1F44" : "#ffffff"}
                            stroke={on ? "#1d3a73" : "#cbd5e1"}
                            strokeWidth="1"
                          />
                        </g>
                      </g>
                    ))}
                    {/* Mặt trên */}
                    <g transform="matrix(0.866 0.5 -0.866 0.5 0 0)">
                      <rect x="-65" y="-65" width="130" height="130" rx="14" fill={on ? "url(#plateOn)" : "#fbfcfd"} stroke={on ? accent : "#94a3b8"} strokeWidth={on ? 2 : 1} />
                      {!on && <rect x="-65" y="-65" width="130" height="130" rx="14" fill="url(#plateGrid)" />}
                      <rect x="-56" y="-56" width="112" height="112" rx="10" fill="none" stroke={on ? "#ffffff" : "#94a3b8"} strokeOpacity={on ? 0.25 : 0.5} strokeDasharray="2 4" />
                      <PlateDetail level={l.level} active={on} />
                    </g>
                    {/* Số thứ tự ở góc phải */}
                    <circle cx="112" cy="0" r="9" fill={on ? accent : "#e2e8f0"} />
                    <text x="112" y="3.5" textAnchor="middle" fontSize="10" fontWeight="700" fill={on ? "#fff" : "#64748b"}>
                      {l.level}
                    </text>

                    {/* Nhãn chú thích có đường dẫn */}
                    {on && (
                      <g className="usas-pop" style={{ animationDuration: "0.4s" }}>
                        <path d="M121 0 L150 -34 H165" fill="none" stroke="#0B1F44" strokeWidth="1" />
                        <rect x="165" y="-47" width={l.label.length * 6.3 + 20} height="26" rx="6" fill="#ffffff" stroke="#0B1F44" strokeOpacity="0.25" />
                        <text x="175" y="-30" fontSize="11" fontWeight="600" fill="#0B1F44">
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
    if (!isLoading && !isPublicPreview && isStaff) {
      router.replace("/admin");
    }
  }, [isStaff, isLoading, isPublicPreview, router]);

  if (!isLoading && isStaff && !isPublicPreview) {
    return (
      <div className="flex min-h-[45vh] flex-col items-center justify-center gap-3 text-sm text-muted-foreground">
        <Spinner />
        <p>{isModerator ? "Đang chuyển hướng thẳng đến khu vực Moderator..." : "Đang chuyển hướng thẳng đến trang Quản trị..."}</p>
      </div>
    );
  }

  const schools = schoolFilter === "all" ? SCHOOLS : SCHOOLS.filter((s) => s.type === schoolFilter);

  return (
    <div className="relative space-y-24 pb-20">
      {isStaff && isPublicPreview && (
        <Alert className="rounded-2xl border border-amber-500/30 bg-amber-500/10 text-amber-900 shadow-sm backdrop-blur-md dark:text-amber-200">
          <ShieldAlertIcon className="size-4 text-amber-600" />
          <AlertTitle className="font-semibold">Chế độ xem trước công khai ({isModerator ? "Moderator" : "Admin"})</AlertTitle>
          <AlertDescription className="mt-1 flex items-center justify-between gap-2">
            <span className="text-xs">Bạn đang xem giao diện trang chủ của học sinh / phụ huynh với tư cách {isModerator ? "Moderator" : "Quản trị viên"}.</span>
            <Link
              href="/admin"
              className={btn({ variant: "outline", size: "sm", className: "h-7 shrink-0 gap-1 rounded-full bg-white text-xs" })}
            >
              {isModerator ? "Vào khu vực Moderator" : "Vào trang Quản trị"}
              <ExternalLinkIcon className="size-3" />
            </Link>
          </AlertDescription>
        </Alert>
      )}

      {/* ============================== HERO ============================== */}
      <section className="relative -mt-2 overflow-hidden rounded-[36px] bg-[#0B1F44] text-white shadow-[0_30px_80px_rgba(11,31,68,0.35)]">
        <div className="absolute inset-0">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={HERO_IMG} alt="" className="h-full w-full object-cover opacity-40" onError={(e) => (e.currentTarget.style.display = "none")} />
          <div className="absolute inset-0 bg-gradient-to-r from-[#0B1F44] via-[#0B1F44]/85 to-[#0B1F44]/30" />
          <div className="absolute inset-0 bg-gradient-to-t from-[#0B1F44]/80 via-transparent to-transparent" />
        </div>
        <StarsPattern />
        <div className="pointer-events-none absolute -right-20 -top-20 size-96 rounded-full bg-[#B31942]/30 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-32 left-1/3 size-96 rounded-full bg-[#C9A227]/15 blur-3xl" />

        <div className="relative grid gap-10 px-6 py-14 sm:px-12 sm:py-20 lg:grid-cols-12 lg:items-center">
          <div className="space-y-7 lg:col-span-7">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-1.5 text-xs font-medium backdrop-blur-md">
              <StarIcon className="size-3.5 fill-[#e7c85a] text-[#e7c85a]" />
              Du học Mỹ · Có hệ thống đồng hành từ hồ sơ đến trung tâm tư vấn
            </div>

            <h1 className="font-heading text-4xl font-extrabold leading-[1.08] tracking-tight sm:text-6xl">
              Giấc mơ Mỹ
              <br />
              <span className="bg-gradient-to-r from-[#f3d56b] via-[#e7c85a] to-[#ff8aa0] bg-clip-text text-transparent">
                bắt đầu từ một hồ sơ đúng
              </span>
            </h1>

            <p className="max-w-xl text-base leading-relaxed text-white/80 sm:text-lg">
              Tìm trường hợp với điểm số và ngân sách, biết ngay cơ hội học bổng, và kết nối với trung tâm tư vấn uy tín khi bạn cần người đi cùng. Tất cả trong một nơi, bằng tiếng Việt.
            </p>

            <div className="flex flex-wrap items-center gap-3">
              <Link
                href="/analysis"
                className={btn({
                  size: "lg",
                  className:
                    "h-13 gap-2 rounded-full bg-[#B31942] px-8 text-base font-semibold text-white shadow-[0_10px_30px_rgba(179,25,66,0.45)] transition-all hover:-translate-y-0.5 hover:bg-[#c71f4b]",
                })}
              >
                Phân tích hồ sơ của tôi
                <ArrowRightIcon className="size-4" />
              </Link>
              <Link
                href="/centers"
                className={btn({
                  variant: "outline",
                  size: "lg",
                  className:
                    "h-13 gap-2 rounded-full border-white/30 bg-white/10 px-7 text-base font-medium text-white backdrop-blur-md transition-all hover:-translate-y-0.5 hover:bg-white/20 hover:text-white",
                })}
              >
                <Building2Icon className="size-4" />
                Tìm trung tâm tư vấn
              </Link>
            </div>

            <div className="flex flex-wrap gap-2 pt-1">
              {["GPA 3.5, SAT 1350 nên nộp trường nào?", "Học bổng cho sinh viên quốc tế?"].map((q) => (
                <button
                  key={q}
                  type="button"
                  onClick={() => openPanel(q)}
                  className="group inline-flex items-center gap-1.5 rounded-full border border-white/20 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-white/85 backdrop-blur transition-all hover:border-white/50 hover:bg-white/15"
                >
                  <MessageSquareTextIcon className="size-3.5 text-[#e7c85a]" />
                  {q}
                </button>
              ))}
            </div>
          </div>

          {/* Thẻ kính nổi bên phải */}
          <div className="relative lg:col-span-5">
            <div className="relative mx-auto max-w-sm space-y-3 lg:ml-auto">
              <div className="rounded-3xl border border-white/20 bg-white/10 p-5 shadow-2xl backdrop-blur-xl">
                <div className="mb-3 flex items-center justify-between">
                  <div className="text-sm font-bold">Gợi ý trường cho bạn</div>
                  <span className="rounded-full bg-white/15 px-2.5 py-0.5 text-[10px] font-semibold text-white/80">Minh họa</span>
                </div>
                <div className="space-y-2">
                  {DEMO_TIERS.map((t) => (
                    <div key={t.name} className="flex items-center justify-between rounded-2xl bg-black/20 px-3.5 py-2.5">
                      <div className="flex items-center gap-2.5">
                        <span className={`size-2 rounded-full ${t.dot}`} />
                        <span className="text-xs font-semibold">{t.name}</span>
                      </div>
                      <span className={`rounded-full border px-2.5 py-0.5 text-[10px] font-semibold ${t.badge}`}>{t.tag}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="ml-6 flex items-center gap-3 rounded-2xl border border-white/20 bg-white/95 p-3.5 text-[#0B1F44] shadow-xl sm:ml-12">
                <span className="flex size-10 items-center justify-center rounded-xl bg-[#C9A227]/20 text-[#9a7a10]">
                  <HandCoinsIcon className="size-5" />
                </span>
                <div>
                  <div className="text-xs font-bold">Có thể được học bổng</div>
                  <div className="text-[11px] text-slate-500">Need-based · Merit-based</div>
                </div>
              </div>

              <div className="mr-4 flex items-center gap-3 rounded-2xl border border-white/20 bg-white/95 p-3.5 text-[#0B1F44] shadow-xl sm:mr-10">
                <span className="flex size-10 items-center justify-center rounded-xl bg-[#B31942]/10 text-[#B31942]">
                  <Building2Icon className="size-5" />
                </span>
                <div>
                  <div className="text-xs font-bold">Kết nối trung tâm tư vấn</div>
                  <div className="text-[11px] text-slate-500">Gửi yêu cầu chỉ với một lần bấm</div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <Stripes />
      </section>

      {/* ============== Dải tên trường (chạy ngang) ============== */}
      <section aria-label="Một số trường phổ biến" className="-mt-12 overflow-hidden">
        <div className="mb-3 text-center text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-3">
          Học sinh Việt Nam quan tâm những trường như
        </div>
        <div className="relative">
          <div className="pointer-events-none absolute inset-y-0 left-0 z-10 w-16 bg-gradient-to-r from-ground to-transparent" />
          <div className="pointer-events-none absolute inset-y-0 right-0 z-10 w-16 bg-gradient-to-l from-ground to-transparent" />
          <div className="flex w-max animate-[usas-marquee_40s_linear_infinite] gap-10 whitespace-nowrap font-heading text-xl font-bold text-[#0B1F44]/35">
            {[...TICKER, ...TICKER].map((n, i) => (
              <span key={i} className="flex items-center gap-10">
                {n}
                <StarIcon className="size-3 fill-[#B31942]/50 text-[#B31942]/50" />
              </span>
            ))}
          </div>
        </div>
        <style>{`@keyframes usas-marquee{from{transform:translateX(0)}to{transform:translateX(-50%)}}`}</style>
      </section>

      {/* ===================== HUB CÁ NHÂN (đã đăng nhập) ===================== */}
      {isStudentOrParent && (
        <section className="relative overflow-hidden rounded-3xl border border-[#0B1F44]/10 bg-gradient-to-r from-[#FAF6EE] via-white to-white p-6 shadow-sm sm:p-8">
          <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
            <div className="space-y-1.5">
              <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-[#B31942]">
                <CheckCircle2Icon className="size-4" />
                <span>Không gian của bạn</span>
              </div>
              <h2 className="text-xl font-bold text-[#0B1F44] sm:text-2xl">Chào mừng trở lại, {user?.fullName || "bạn"}!</h2>
              <p className="max-w-xl text-xs text-ink-2 sm:text-sm">
                Tài khoản {user?.role === "parent" ? "Phụ huynh" : "Học sinh"} đã sẵn sàng. Tiếp tục từ nơi bạn đã dừng:
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2.5">
              <Link href="/analysis" className={btn({ size: "sm", className: "h-10 gap-1.5 rounded-full bg-[#0B1F44] px-5 font-medium hover:bg-[#12306a]" })}>
                <SlidersHorizontalIcon className="size-3.5" />
                Tiếp tục phân tích
              </Link>
              <Link href="/target-schools" className={btn({ variant: "outline", size: "sm", className: "h-10 gap-1.5 rounded-full bg-white px-5 font-medium" })}>
                <GraduationCapIcon className="size-3.5 text-[#B31942]" />
                Trường mục tiêu
              </Link>
              <Link href="/centers" className={btn({ variant: "outline", size: "sm", className: "h-10 gap-1.5 rounded-full bg-white px-5 font-medium" })}>
                <Building2Icon className="size-3.5 text-[#B31942]" />
                Trung tâm tư vấn
              </Link>
            </div>
          </div>
        </section>
      )}

      {/* ======================= MINH HỌA TRÒ CHUYỆN ======================= */}
      <section className="grid gap-12 lg:grid-cols-12 lg:items-center">
        <div className="space-y-6 lg:col-span-5">
          <div className="inline-flex items-center gap-1.5 rounded-full bg-[#B31942]/10 px-3.5 py-1 text-[11px] font-bold uppercase tracking-[0.14em] text-[#B31942]">
            <MessageSquareTextIcon className="size-3" />
            Trợ lý AI
          </div>
          <h2 className="font-heading text-3xl font-bold leading-tight tracking-tight text-[#0B1F44] sm:text-4xl">
            Hỏi như đang nhắn tin với một người tư vấn
          </h2>
          <p className="text-sm leading-relaxed text-ink-2 sm:text-base">
            Bạn hỏi bằng tiếng Việt, một câu có thể chứa nhiều ý. Trợ lý trả lời gọn, đúng trọng tâm và gợi ý luôn bước tiếp theo.
          </p>
          <ul className="space-y-3">
            {[
              "So sánh hai trường chỉ trong một câu hỏi",
              "Hỏi học phí, học bổng, điều kiện tuyển sinh",
              "Gợi ý nút bấm nhanh cho bước tiếp theo",
              "Cần người thật? Chuyển thẳng sang trung tâm tư vấn",
            ].map((t) => (
              <li key={t} className="flex items-center gap-3 text-sm text-[#0B1F44]">
                <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-[#B31942] text-white">
                  <CheckCircle2Icon className="size-3.5" />
                </span>
                {t}
              </li>
            ))}
          </ul>
          <OpenAssistantButton
            variant="outline"
            size="lg"
            className="h-12 rounded-full border-[#0B1F44]/20 bg-white px-6 font-semibold text-[#0B1F44] hover:bg-[#0B1F44] hover:text-white"
          >
            <BotIcon className="mr-2 size-4" />
            Thử hỏi ngay
          </OpenAssistantButton>
        </div>

        <div className="relative lg:col-span-7">
          <div className="pointer-events-none absolute -inset-6 rounded-[40px] bg-gradient-to-br from-[#B31942]/15 via-[#C9A227]/10 to-[#0B1F44]/15 blur-2xl" />
          <style>{`@keyframes usas-pop{from{opacity:0;transform:translateY(14px) scale(.98)}to{opacity:1;transform:none}}.usas-pop{opacity:0;animation:usas-pop .6s ease forwards}`}</style>
          <div className="relative overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-[0_30px_70px_rgba(11,31,68,0.18)]">
            <div className="flex items-center justify-between bg-[#0B1F44] px-5 py-3.5 text-white">
              <div className="flex items-center gap-3">
                <span className="flex size-9 items-center justify-center rounded-full bg-white/15">
                  <BotIcon className="size-5 text-[#e7c85a]" />
                </span>
                <div>
                  <div className="text-sm font-bold">Trợ lý USAS</div>
                  <div className="flex items-center gap-1.5 text-[11px] text-white/70">
                    <span className="size-1.5 rounded-full bg-emerald-400" />
                    Sẵn sàng trả lời
                  </div>
                </div>
              </div>
              <span className="rounded-full bg-white/15 px-2.5 py-0.5 text-[10px] font-semibold">Minh họa</span>
            </div>

            <div className="space-y-4 bg-[#F6F8FB] p-5 sm:p-6">
              <div className="usas-pop flex justify-end" style={{ animationDelay: "0.2s" }}>
                <div className="max-w-[80%] rounded-2xl rounded-br-md bg-[#0B1F44] px-4 py-2.5 text-sm text-white shadow">
                  So sánh giúp em University of Washington và Purdue, em muốn học Khoa học Máy tính
                </div>
              </div>

              <div className="usas-pop flex gap-2.5" style={{ animationDelay: "1.1s" }}>
                <span className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full bg-[#B31942] text-white">
                  <BotIcon className="size-4" />
                </span>
                <div className="max-w-[88%] space-y-2 rounded-2xl rounded-tl-md border border-slate-200 bg-white px-4 py-3 text-sm leading-relaxed text-slate-700 shadow-sm">
                  <p className="font-semibold text-[#0B1F44]">Hai trường khác nhau khá rõ ở môi trường học:</p>
                  <ul className="space-y-1.5">
                    <li className="flex gap-2">
                      <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[#B31942]" />
                      <span><b>University of Washington</b>: ở Seattle, gần các công ty công nghệ, thuận lợi cho thực tập.</span>
                    </li>
                    <li className="flex gap-2">
                      <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[#B31942]" />
                      <span><b>Purdue</b>: khuôn viên rộng, thiên về kỹ thuật, chi phí công lập dễ tiếp cận hơn.</span>
                    </li>
                  </ul>
                  <p className="text-[11px] text-slate-400">Số liệu lấy từ dữ liệu hệ thống</p>
                </div>
              </div>

              <div className="usas-pop flex justify-end" style={{ animationDelay: "2.2s" }}>
                <div className="max-w-[80%] rounded-2xl rounded-br-md bg-[#0B1F44] px-4 py-2.5 text-sm text-white shadow">
                  Em muốn xin học bổng thì sao?
                </div>
              </div>

              <div className="usas-pop flex gap-2.5" style={{ animationDelay: "3.1s" }}>
                <span className="mt-1 flex size-7 shrink-0 items-center justify-center rounded-full bg-[#B31942] text-white">
                  <BotIcon className="size-4" />
                </span>
                <div className="max-w-[88%] space-y-3 rounded-2xl rounded-tl-md border border-slate-200 bg-white px-4 py-3 text-sm leading-relaxed text-slate-700 shadow-sm">
                  <p>
                    Cả hai đều có học bổng theo thành tích cho sinh viên quốc tế. Mức hỗ trợ cụ thể phụ thuộc vào hồ sơ của em, nên bước hợp lý là xem hồ sơ em đang ở nhóm nào.
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {["Hồ sơ em hợp trường nào?", "Nhập ngân sách của em", "Kết nối trung tâm tư vấn"].map((c, i) => (
                      <span
                        key={c}
                        className={`inline-flex items-center gap-1 rounded-full border px-3 py-1.5 text-xs font-semibold ${
                          i === 2 ? "border-[#B31942]/30 bg-[#B31942]/10 text-[#B31942]" : "border-[#0B1F44]/15 bg-[#0B1F44]/5 text-[#0B1F44]"
                        }`}
                      >
                        {c}
                        <ChevronRightIcon className="size-3.5 opacity-60" />
                      </span>
                    ))}
                  </div>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-3 border-t border-slate-200 bg-white px-5 py-3.5">
              <div className="flex-1 rounded-full bg-slate-100 px-4 py-2.5 text-sm text-slate-400">Nhập câu hỏi của bạn…</div>
              <span className="flex size-10 items-center justify-center rounded-full bg-[#B31942] text-white shadow">
                <ArrowRightIcon className="size-4" />
              </span>
            </div>
          </div>
        </div>
      </section>

      {/* ===================== HỆ THỐNG LÀM ĐƯỢC GÌ ===================== */}
      <section className="space-y-12">
        <SectionHead tag="Hệ thống làm được gì" title="Mọi thứ bạn cần để chọn trường, trong một nơi" desc="Từ đọc bảng điểm đến gặp trung tâm tư vấn, mỗi bước có công cụ riêng và nối liền với nhau." />
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {CAPABILITIES.map((c) => (
            <Link
              key={c.title}
              href={c.href}
              className="group relative flex flex-col overflow-hidden rounded-3xl border border-slate-200/70 bg-white p-6 shadow-[0_6px_24px_rgba(11,31,68,0.05)] transition-all duration-300 hover:-translate-y-1.5 hover:shadow-[0_22px_50px_rgba(11,31,68,0.14)]"
            >
              <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${c.accent}`} />
              <span className={`mb-5 flex size-12 items-center justify-center rounded-2xl bg-gradient-to-br text-white shadow-lg ${c.accent}`}>
                <c.icon className="size-6" />
              </span>
              <h3 className="font-heading text-lg font-bold text-[#0B1F44]">{c.title}</h3>
              <p className="mt-2 flex-1 text-sm leading-relaxed text-ink-2">{c.desc}</p>
              <span className="mt-5 inline-flex items-center gap-1 text-sm font-semibold text-[#B31942]">
                {c.cta}
                <ArrowRightIcon className="size-4 transition-transform group-hover:translate-x-1" />
              </span>
            </Link>
          ))}
        </div>
      </section>

      {/* ===================== TRƯỜNG TIÊU BIỂU (có ảnh) ===================== */}
      <section className="space-y-10">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-xl space-y-3">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-[#B31942]/10 px-3.5 py-1 text-[11px] font-bold uppercase tracking-[0.14em] text-[#B31942]">
              <StarIcon className="size-3 fill-current" />
              Khám phá trường
            </div>
            <h2 className="font-heading text-2xl font-bold tracking-tight text-[#0B1F44] sm:text-4xl">Những ngôi trường nhiều bạn quan tâm</h2>
            <p className="text-sm text-ink-2 sm:text-base">Bấm vào một trường để hỏi trợ lý hoặc kiểm tra mức phù hợp với hồ sơ của bạn.</p>
          </div>
          <div className="flex flex-wrap gap-1.5 rounded-full border border-slate-200 bg-white p-1.5 shadow-sm">
            {SCHOOL_FILTERS.map((f) => (
              <button
                key={f.key}
                type="button"
                onClick={() => setSchoolFilter(f.key)}
                className={`rounded-full px-4 py-1.5 text-xs font-semibold transition-all ${
                  schoolFilter === f.key ? "bg-[#0B1F44] text-white shadow" : "text-slate-500 hover:text-[#0B1F44]"
                }`}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {schools.map((s) => (
            <article
              key={s.name}
              className="group flex flex-col overflow-hidden rounded-3xl border border-slate-200/70 bg-white shadow-[0_6px_24px_rgba(11,31,68,0.06)] transition-all duration-300 hover:-translate-y-1.5 hover:shadow-[0_26px_60px_rgba(11,31,68,0.18)]"
            >
              <div className="relative h-56 overflow-hidden bg-[#0B1F44]">
                <SchoolImage src={s.image} alt={s.name} />
                <div className="absolute inset-0 bg-gradient-to-t from-[#0B1F44]/90 via-[#0B1F44]/20 to-transparent" />
                <span className="absolute left-4 top-4 rounded-full bg-white/90 px-3 py-1 text-[11px] font-bold text-[#0B1F44] shadow backdrop-blur">
                  {s.typeLabel}
                </span>
                <div className="absolute inset-x-4 bottom-4 text-white">
                  <h3 className="font-heading text-xl font-bold leading-tight">{s.name}</h3>
                  <div className="mt-1 flex items-center gap-1.5 text-xs text-white/85">
                    <MapPinIcon className="size-3.5" />
                    {s.city}
                  </div>
                </div>
              </div>
              <div className="flex flex-1 flex-col gap-4 p-5">
                <p className="text-sm leading-relaxed text-ink-2">{s.highlight}</p>
                <div className="flex flex-wrap gap-1.5">
                  {s.majors.map((m) => (
                    <span key={m} className="rounded-full bg-[#FAF6EE] px-2.5 py-1 text-[11px] font-medium text-[#0B1F44]">
                      {m}
                    </span>
                  ))}
                </div>
                <div className="mt-auto flex items-center justify-between border-t border-slate-100 pt-4">
                  <Link href={`/analysis?school=${encodeURIComponent(s.name)}`} className="inline-flex items-center gap-1 text-sm font-semibold text-[#B31942] hover:underline">
                    Kiểm tra hồ sơ của tôi
                    <ChevronRightIcon className="size-4" />
                  </Link>
                  <button
                    type="button"
                    onClick={() => openPanel(`Cho em biết điều kiện tuyển sinh và học bổng của ${s.name}`)}
                    className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-[#0B1F44] hover:text-white"
                  >
                    <MessageSquareTextIcon className="size-3.5" />
                    Hỏi AI
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
      </section>

      {/* ============================ HỌC BỔNG ============================ */}
      <section className="relative overflow-hidden rounded-[36px] bg-[#0B1F44] px-6 py-14 text-white shadow-[0_30px_80px_rgba(11,31,68,0.3)] sm:px-12 sm:py-20">
        <StarsPattern />
        <div className="pointer-events-none absolute -left-24 top-0 size-96 rounded-full bg-[#C9A227]/20 blur-3xl" />
        <div className="relative space-y-12">
          <SectionHead dark tag="Học bổng" title="Học phí là rào cản? Có nhiều hướng đi hơn bạn nghĩ" desc="Mỗi trường có chính sách riêng. USAS giúp bạn biết nên tìm loại học bổng nào và trường nào hợp với ngân sách." />
          <div className="grid gap-5 md:grid-cols-3">
            {SCHOLARSHIPS.map((s) => (
              <div key={s.title} className="flex flex-col rounded-3xl border border-white/15 bg-white/[0.06] p-6 backdrop-blur-sm transition-all hover:-translate-y-1 hover:bg-white/10">
                <div className="mb-4 flex items-center justify-between">
                  <span className="flex size-12 items-center justify-center rounded-2xl bg-gradient-to-br from-[#e7c85a] to-[#C9A227] text-[#0B1F44] shadow-lg">
                    <s.icon className="size-6" />
                  </span>
                  <span className="rounded-full bg-white/10 px-3 py-1 text-[11px] font-semibold text-[#e7c85a]">{s.tag}</span>
                </div>
                <h3 className="font-heading text-lg font-bold">{s.title}</h3>
                <p className="mt-2 flex-1 text-sm leading-relaxed text-white/70">{s.desc}</p>
                <div className="mt-4 border-t border-white/10 pt-3 text-xs text-white/60">
                  <span className="font-semibold text-white/80">Ví dụ: </span>
                  {s.examples}
                </div>
              </div>
            ))}
          </div>
          <div className="flex flex-col items-center gap-3 text-center">
            <div className="flex flex-wrap justify-center gap-3">
              <Link
                href="/profile/financial"
                className={btn({ size: "lg", className: "h-12 gap-2 rounded-full bg-[#e7c85a] px-7 font-semibold text-[#0B1F44] hover:bg-[#f3d56b]" })}
              >
                Nhập ngân sách để lọc trường
                <ArrowRightIcon className="size-4" />
              </Link>
              <button
                type="button"
                onClick={() => openPanel("Học bổng nào phù hợp với sinh viên quốc tế ngân sách khoảng 25.000 USD một năm?")}
                className="inline-flex h-12 items-center gap-2 rounded-full border border-white/30 bg-white/10 px-6 text-sm font-medium backdrop-blur transition-all hover:bg-white/20"
              >
                <MessageSquareTextIcon className="size-4" />
                Hỏi trợ lý về học bổng
              </button>
            </div>
            <p className="text-[11px] text-white/50">Thông tin mang tính tham khảo. Chính sách học bổng thay đổi theo từng năm, hãy kiểm tra trang chính thức của trường.</p>
          </div>
        </div>
      </section>

      {/* ===================== KẾT NỐI TRUNG TÂM ===================== */}
      <section className="relative overflow-hidden rounded-[36px] border border-[#0B1F44]/10 bg-[#FAF6EE] p-6 sm:p-12">
        <div className="pointer-events-none absolute -right-24 -top-24 size-80 rounded-full bg-[#B31942]/10 blur-3xl" />
        <div className="relative grid gap-12 lg:grid-cols-12 lg:items-center">
          <div className="space-y-6 lg:col-span-5">
            <div className="inline-flex items-center gap-1.5 rounded-full bg-[#B31942]/10 px-3.5 py-1 text-[11px] font-bold uppercase tracking-[0.14em] text-[#B31942]">
              <Building2Icon className="size-3" />
              Trung tâm tư vấn
            </div>
            <h2 className="font-heading text-3xl font-bold leading-tight tracking-tight text-[#0B1F44] sm:text-4xl">
              Cần người đi cùng? Chúng tôi nối bạn với trung tâm phù hợp
            </h2>
            <p className="text-sm leading-relaxed text-ink-2 sm:text-base">
              USAS giúp bạn hiểu hồ sơ và chọn trường. Những việc cần người đồng hành trực tiếp như bài luận, visa hay luyện thi sẽ do trung tâm tư vấn đảm nhận, và bạn gửi yêu cầu ngay trên hệ thống.
            </p>
            <ol className="space-y-4">
              {CENTER_STEPS.map((s, i) => (
                <li key={s.title} className="flex gap-4">
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-[#0B1F44] text-sm font-bold text-white">{i + 1}</span>
                  <div>
                    <div className="text-sm font-bold text-[#0B1F44]">{s.title}</div>
                    <div className="text-xs leading-relaxed text-ink-2">{s.desc}</div>
                  </div>
                </li>
              ))}
            </ol>
            <Link
              href="/centers"
              className={btn({ size: "lg", className: "h-12 gap-2 rounded-full bg-[#B31942] px-7 font-semibold text-white shadow-[0_10px_30px_rgba(179,25,66,0.3)] hover:bg-[#c71f4b]" })}
            >
              Xem các trung tâm
              <ArrowRightIcon className="size-4" />
            </Link>
          </div>

          <div className="grid gap-4 sm:grid-cols-2 lg:col-span-7">
            {CENTER_SERVICES.map((s) => (
              <div key={s.title} className="flex items-start gap-4 rounded-2xl border border-white bg-white p-5 shadow-[0_6px_20px_rgba(11,31,68,0.06)] transition-all hover:-translate-y-1 hover:shadow-[0_16px_36px_rgba(11,31,68,0.12)]">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-[#0B1F44] text-[#e7c85a]">
                  <s.icon className="size-5" />
                </span>
                <div>
                  <div className="text-sm font-bold text-[#0B1F44]">{s.title}</div>
                  <div className="mt-0.5 text-xs leading-relaxed text-ink-2">{s.desc}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ============================ HÀNH TRÌNH ============================ */}
      <section className="space-y-12">
        <SectionHead tag="Hành trình" title="Năm bước từ hồ sơ đến quyết định" />
        <div className="relative">
          <div className="absolute left-0 right-0 top-7 hidden h-0.5 bg-gradient-to-r from-transparent via-[#B31942]/30 to-transparent md:block" />
          <ol className="relative grid gap-8 md:grid-cols-5">
            {[
              { icon: FileScanIcon, t: "Khai báo hồ sơ", d: "Nhập thông tin hoặc tải bảng điểm." },
              { icon: TargetIcon, t: "Nhận danh sách trường", d: "Chia 4 nhóm, có lý do rõ ràng." },
              { icon: SlidersHorizontalIcon, t: "Thử kịch bản", d: "Xem điều gì thay đổi nếu điểm tăng." },
              { icon: MessageSquareTextIcon, t: "Hỏi trợ lý", d: "Giải đáp thắc mắc bằng tiếng Việt." },
              { icon: Building2Icon, t: "Gặp trung tâm", d: "Chuyển phần cần người hỗ trợ." },
            ].map((s, i) => (
              <li key={s.t} className="flex flex-col items-center text-center">
                <span className="relative flex size-14 items-center justify-center rounded-full bg-[#0B1F44] text-white shadow-[0_10px_24px_rgba(11,31,68,0.3)] ring-8 ring-ground">
                  <s.icon className="size-6" />
                  <span className="absolute -right-1 -top-1 flex size-6 items-center justify-center rounded-full bg-[#B31942] text-[11px] font-bold">{i + 1}</span>
                </span>
                <h3 className="mt-4 text-sm font-bold text-[#0B1F44]">{s.t}</h3>
                <p className="mt-1 max-w-[180px] text-xs leading-relaxed text-ink-2">{s.d}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {/* ======================= CÁC LỚP CỦA HỆ THỐNG ======================= */}
      <section className="relative rounded-[36px] border border-[#0B1F44]/10 bg-[#FAF6EE] px-6 pt-12 sm:px-12 sm:pt-16">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-0 rounded-[36px] opacity-60"
          style={{ backgroundImage: "repeating-linear-gradient(90deg, rgba(11,31,68,0.045) 0 1px, transparent 1px 112px)" }}
        />
        <div className="relative">
          <SectionHead tag="Bên trong USAS" title="Năm lớp làm việc cùng nhau để bạn được trả lời đúng" desc="Cuộn xuống hoặc bấm vào từng lớp để xem mỗi lớp làm gì và mang lại điều gì cho bạn." />
          <div className="mt-10">
            <LayerStack />
          </div>
          <div className="flex flex-wrap items-center justify-center gap-x-8 gap-y-2 border-t border-[#0B1F44]/10 py-8 text-xs text-ink-2">
            {["Không có số liệu thì nói rõ là chưa có", "Không hứa hẹn kết quả trúng tuyển", "Việc cần người thì chuyển trung tâm"].map((t) => (
              <span key={t} className="inline-flex items-center gap-1.5">
                <ShieldCheckIcon className="size-4 text-ok" />
                {t}
              </span>
            ))}
          </div>
        </div>
      </section>

      {/* ================================ FAQ ================================ */}
      <section className="mx-auto max-w-4xl space-y-10">
        <SectionHead tag="Giải đáp" title="Câu hỏi thường gặp" />
        <div className="rounded-3xl border border-slate-200/70 bg-white p-6 shadow-[0_4px_25px_rgba(11,31,68,0.05)] sm:p-8">
          <Accordion className="space-y-3">
            {FAQS.map((f, i) => (
              <AccordionItem key={f.q} value={f.q} className={i < FAQS.length - 1 ? "border-b border-slate-100 pb-3" : "pb-1"}>
                <AccordionTrigger className="text-sm font-bold text-[#0B1F44] hover:no-underline sm:text-base">{f.q}</AccordionTrigger>
                <AccordionContent className="pt-2 text-xs leading-relaxed text-ink-2 sm:text-sm">{f.a}</AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        </div>
      </section>

      {/* ================================ CTA ================================ */}
      <section className="relative overflow-hidden rounded-[36px] bg-[#0B1F44] text-center text-white shadow-[0_30px_80px_rgba(11,31,68,0.35)]">
        <StarsPattern />
        <div className="pointer-events-none absolute -right-24 -top-24 size-80 rounded-full bg-[#B31942]/40 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-24 -left-24 size-80 rounded-full bg-[#C9A227]/20 blur-3xl" />
        <div className="relative mx-auto max-w-2xl space-y-6 px-6 py-16 sm:py-20">
          <h2 className="font-heading text-3xl font-extrabold leading-tight sm:text-5xl">Bắt đầu hành trình du học Mỹ của bạn</h2>
          <p className="text-sm leading-relaxed text-white/75 sm:text-base">Chỉ vài phút để có danh sách trường đầu tiên. Trợ lý AI và trung tâm tư vấn luôn sẵn sàng khi bạn cần.</p>
          <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
            <Link
              href="/analysis"
              className={btn({ size: "lg", className: "h-13 gap-2 rounded-full bg-[#B31942] px-8 text-base font-semibold text-white shadow-[0_10px_30px_rgba(179,25,66,0.45)] hover:bg-[#c71f4b]" })}
            >
              Phân tích hồ sơ
              <ArrowRightIcon className="size-4" />
            </Link>
            <OpenAssistantButton
              variant="outline"
              size="lg"
              className="h-13 rounded-full border-white/30 bg-white/10 px-7 text-base font-medium text-white backdrop-blur-md hover:bg-white/20 hover:text-white"
            >
              <BotIcon className="mr-2 size-4" />
              Hỏi trợ lý
            </OpenAssistantButton>
          </div>
        </div>
        <Stripes />
      </section>
    </div>
  );
}

export default function HomePage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-[45vh] items-center justify-center gap-2 text-sm text-muted-foreground">
          <Spinner />
          <span>Đang tải thông tin hệ thống...</span>
        </div>
      }
    >
      <HomeView />
    </Suspense>
  );
}
