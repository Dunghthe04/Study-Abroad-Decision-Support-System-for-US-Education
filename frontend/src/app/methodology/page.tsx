import type { Metadata } from "next";
import Link from "next/link";
import {
  ArrowRightIcon,
  AwardIcon,
  CheckCircle2Icon,
  DollarSignIcon,
  GraduationCapIcon,
  LayersIcon,
  ScaleIcon,
  ShieldCheckIcon,
  SparklesIcon,
  TrendingUpIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = {
  title: "Giới thiệu phương pháp gợi ý trường – USAS",
  description:
    "Phương pháp khoa học hỗ trợ ra quyết định du học Mỹ: mô hình tuyển sinh toàn diện (Holistic Review), thuật toán AHP & SAW và phân tầng 4 nhóm trường.",
};

const tiers = [
  {
    name: "Khó vào (Reach)",
    badge: "Mục tiêu cao",
    badgeVariant: "destructive" as const,
    colorClass: "border-risk/30 bg-risk-wash/30 text-risk",
    description: "Hồ sơ hiện tại thấp hơn mức thường nhận của trường. Nên xem như mục tiêu thử thách cao.",
    criteria: "GPA ≤ GPA TB trường − 0.3 HOẶC SAT < mốc 25% percentile của trường.",
    strategy: "Khuyến nghị chiếm khoảng 20–25% danh sách ứng tuyển. Cần bài luận xuất sắc và ngoại khóa bứt phá.",
  },
  {
    name: "Vừa sức (Match)",
    badge: "Trọng tâm",
    badgeVariant: "brand" as const,
    colorClass: "border-brand/30 bg-brand-wash/30 text-brand-ink",
    description: "Hồ sơ nằm trong khoảng thường nhận của trường. Đây là nhóm trọng tâm cần đầu tư kỹ lưỡng nhất.",
    criteria: "Hồ sơ nằm trong khoảng 25% – 75% percentile của trường, chênh lệch GPA trong biên độ ±0.3.",
    strategy: "Khuyến nghị chiếm 45–50% danh sách ứng tuyển. Nhóm có xác suất trúng tuyển và phù hợp môi trường cao nhất.",
  },
  {
    name: "An toàn hơn (Safety)",
    badge: "Vững chắc",
    badgeVariant: "secondary" as const,
    colorClass: "border-ok/30 bg-ok-wash/30 text-ok",
    description: "Hồ sơ cao hơn mức thường nhận của trường, khả năng được nhận rất cao.",
    criteria: "GPA ≥ GPA TB trường + 0.3 HOẶC SAT ≥ mốc 75% percentile. Cao đẳng cộng đồng mở tuyển sinh mặc định an toàn.",
    strategy: "Khuyến nghị chiếm 25–30% danh sách ứng tuyển để bảo đảm học sinh luôn có phương án nhập học chắc chắn.",
  },
  {
    name: "Cần học bổng lớn (Financial Reach)",
    badge: "Cân nhắc chi phí",
    badgeVariant: "outline" as const,
    colorClass: "border-warn/30 bg-warn-wash/30 text-warn",
    description: "Trường phù hợp về học thuật nhưng tổng chi phí vượt quá ngân sách gia đình nếu không có học bổng.",
    criteria: "Tổng chi phí/năm > 110% ngân sách gia đình tự chi trả.",
    strategy: "Cần tập trung săn học bổng tài năng (Merit-based) hoặc hỗ trợ tài chính (Financial Aid) để hiện thực hóa ước mơ.",
  },
];

const pillars = [
  {
    icon: GraduationCapIcon,
    title: "1. Năng lực học thuật (50%)",
    subtitle: "Academic Competence",
    items: [
      "GPA 3 năm gần nhất: Quy đổi tuyến tính chính xác thang 4.0 (GPA 4.0 = GPA 10 / 10 × 4.0), đối chiếu trung vị điểm số tuyển sinh.",
      "Bài thi chuẩn hóa (SAT/ACT): So sánh phân vị 25% – 75% của khóa sinh viên năm nhất trường đã nhận.",
      "Chứng chỉ tiếng Anh (IELTS, TOEFL, Duolingo): Điều kiện sàn tối thiểu (Threshold Gate) để đảm bảo năng lực học thuật.",
    ],
  },
  {
    icon: DollarSignIcon,
    title: "2. Khả năng tài chính (30%)",
    subtitle: "Financial Affordability",
    items: [
      "Tổng chi phí thực tế (Total Cost of Attendance): Tính gồm học phí, bảo hiểm y tế và sinh hoạt phí theo từng bang.",
      "Ngân sách gia đình: Lọc cứng nếu chi phí vượt quá 10% ngân sách tự túc khi không có học bổng.",
      "Chính sách học bổng: Phân loại cơ hội nhận học bổng cho sinh viên quốc tế (Merit-based & Need-based).",
    ],
  },
  {
    icon: AwardIcon,
    title: "3. Ngoại khóa & Thành tích (20%)",
    subtitle: "Holistic Review (Thang 0–4)",
    items: [
      "Quy mô & Cấp độ: Trường, Tỉnh/Thành phố, Quốc gia, hoặc Quốc tế.",
      "Vai trò & Cống hiến: Vị trí lãnh đạo (Chủ nhiệm, Đội trưởng) so với thành viên tham gia, số năm duy trì cam kết.",
      "Thành tích & Giải thưởng: Ghi nhận giải thưởng chuyên môn, dự án vì cộng đồng và năng khiếu đặc thù.",
    ],
  },
];

const presets = [
  {
    icon: ScaleIcon,
    name: "Cân bằng (Mặc định)",
    weights: "50% Học thuật · 30% Tài chính · 20% Ngoại khóa",
    desc: "Cân đối đồng đều giữa chất lượng đào tạo, khả năng chi trả và mức độ cạnh tranh của hồ sơ.",
  },
  {
    icon: DollarSignIcon,
    name: "Tối ưu chi phí",
    weights: "50% Tài chính · 35% Học thuật · 15% Ngoại khóa",
    desc: "Ưu tiên các trường có tổng chi phí học phí và ăn ở thấp nhất, giảm thiểu gánh nặng tài chính.",
  },
  {
    icon: AwardIcon,
    name: "Săn học bổng nhiều",
    weights: "45% Học bổng & Tài chính · 35% Học thuật · 20% Ngoại khóa",
    desc: "Tìm kiếm các trường có gói hỗ trợ tài chính và học bổng hào phóng cho du học sinh Việt Nam.",
  },
  {
    icon: TrendingUpIcon,
    name: "Ưu tiên danh tiếng",
    weights: "65% Học thuật & Xếp hạng · 20% Tài chính · 15% Ngoại khóa",
    desc: "Hướng tới các trường đại học top đầu có thứ hạng cao và danh tiếng học thuật nổi bật.",
  },
];

export default function MethodologyPage() {
  return (
    <div className="space-y-12 pb-16">
      {/* Hero Section */}
      <section className="relative overflow-hidden rounded-2xl border border-line bg-gradient-to-b from-brand-wash/40 via-surface to-surface p-8 sm:p-12">
        <div className="max-w-3xl space-y-4">
          <Badge variant="brand" className="px-3 py-1 text-xs">
            Phương pháp khoa học · Minh bạch · Không hộp đen
          </Badge>
          <h1 className="text-display text-brand-ink">
            Phương pháp tư vấn và gợi ý trường của USAS
          </h1>
          <p className="text-body text-muted-foreground text-base sm:text-lg leading-relaxed">
            USAS hỗ trợ học sinh và phụ huynh ra quyết định du học Mỹ dựa trên mô hình tuyển sinh
            toàn diện (<strong className="text-foreground">Holistic Review</strong>), thuật toán
            ra quyết định đa tiêu chí (<strong className="text-foreground">AHP & SAW</strong>) và
            trí tuệ nhân tạo có kiểm định (<strong className="text-foreground">Responsible AI</strong>).
          </p>
          <div className="flex flex-wrap items-center gap-3 pt-2">
            <Link
              href="/analysis"
              className={buttonVariants({ variant: "default", size: "lg" })}
            >
              Bắt đầu phân tích hồ sơ ngay
              <ArrowRightIcon className="size-4 ml-1.5" />
            </Link>
            <Link
              href="/advisor"
              className={buttonVariants({ variant: "outline", size: "lg" })}
            >
              <SparklesIcon className="size-4 mr-1.5 text-brand" />
              Chat với AI tư vấn
            </Link>
          </div>
        </div>
      </section>

      {/* 4 Nhóm trường */}
      <section className="space-y-6">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <LayersIcon className="size-5 text-brand" />
            <h2 className="text-h1 text-foreground">1. Mô hình phân tầng 4 nhóm trường</h2>
          </div>
          <p className="text-body text-muted-foreground">
            Hệ thống không xếp chung mọi trường vào một bảng duy nhất mà chia thành 4 nhóm chiến lược,
            giúp gia đình xây dựng danh sách nộp hồ sơ an toàn và thông minh:
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {tiers.map((tier) => (
            <Card key={tier.name} className="h-full transition-shadow hover:shadow-sm">
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between gap-2">
                  <CardTitle className="text-h3 text-foreground">{tier.name}</CardTitle>
                  <Badge variant={tier.badgeVariant}>{tier.badge}</Badge>
                </div>
                <CardDescription className="text-sm">{tier.description}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <div className="rounded-lg bg-muted/60 p-3">
                  <div className="text-label text-xs mb-1">Điều kiện phân nhóm</div>
                  <div className="font-medium text-foreground">{tier.criteria}</div>
                </div>
                <div className="text-body-s">
                  <strong className="text-foreground">Chiến lược đề xuất: </strong>
                  {tier.strategy}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      {/* 3 Trục tiêu chí */}
      <section className="space-y-6">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <ScaleIcon className="size-5 text-brand" />
            <h2 className="text-h1 text-foreground">2. Đánh giá đa tiêu chí (Multi-Criteria Evaluation)</h2>
          </div>
          <p className="text-body text-muted-foreground">
            Tuyển sinh đại học Mỹ không chỉ dựa vào điểm thi mà đánh giá toàn diện năng lực học thuật,
            hoàn cảnh tài chính và phẩm chất cá nhân:
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-3">
          {pillars.map((p) => {
            const Icon = p.icon;
            return (
              <Card key={p.title} className="flex flex-col justify-between">
                <CardHeader>
                  <div className="mb-2 flex size-10 items-center justify-center rounded-lg bg-brand-wash text-brand">
                    <Icon className="size-5" />
                  </div>
                  <CardTitle className="text-h3 text-foreground">{p.title}</CardTitle>
                  <CardDescription className="text-xs uppercase tracking-wide">
                    {p.subtitle}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-3 pt-0">
                  <ul className="space-y-2 text-sm text-muted-foreground">
                    {p.items.map((it, idx) => (
                      <li key={idx} className="flex items-start gap-2">
                        <CheckCircle2Icon className="size-4 shrink-0 text-ok mt-0.5" />
                        <span>{it}</span>
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </section>

      {/* Phương pháp AHP & SAW */}
      <section className="space-y-6 rounded-2xl border border-line bg-surface p-6 sm:p-8">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <ShieldCheckIcon className="size-5 text-brand" />
            <h2 className="text-h1 text-foreground">3. Thuật toán AHP & SAW</h2>
          </div>
          <p className="text-body text-muted-foreground">
            Để tránh việc gán trọng số tùy tiện, USAS áp dụng phương pháp phân tích thứ bậc{" "}
            <strong>AHP (Analytic Hierarchy Process)</strong> của Giáo sư Thomas L. Saaty để xác định
            bộ trọng số khoa học, và giải thuật <strong>SAW (Simple Additive Weighting)</strong> để
            tính điểm độ phù hợp cho từng trường.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-2">
          <div className="space-y-4 rounded-xl border border-line bg-ground/50 p-5">
            <h3 className="text-h3 text-foreground font-semibold">
              Kiểm định tính nhất quán của trọng số (AHP)
            </h3>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Dựa trên ma trận so sánh cặp giữa Học thuật, Tài chính và Ngoại khóa, hệ thống thu được
              tỉ số nhất quán:
            </p>
            <div className="flex items-center justify-around rounded-lg bg-surface p-3 text-center border">
              <div>
                <div className="text-xs text-muted-foreground">Chỉ số nhất quán (CI)</div>
                <div className="text-numeric text-base text-foreground font-semibold">0.0091</div>
              </div>
              <div className="h-8 w-px bg-line" />
              <div>
                <div className="text-xs text-muted-foreground">Tỉ số nhất quán (CR)</div>
                <div className="text-numeric text-base text-ok font-bold">0.016 &lt; 0.10</div>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              ✓ Tỉ số CR &lt; 0.10 chứng minh bộ trọng số toán học hoàn toàn nhất quán và đáng tin cậy,
              không tồn tại mâu thuẫn logic trong đánh giá.
            </p>
          </div>

          <div className="space-y-4 rounded-xl border border-line bg-ground/50 p-5">
            <h3 className="text-h3 text-foreground font-semibold">
              Công thức xếp hạng SAW
            </h3>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Mỗi trường trong từng nhóm được chấm điểm độ phù hợp (0 – 100%) bằng công thức cộng có
              trọng số chuẩn hóa:
            </p>
            <div className="rounded-lg bg-surface p-3 text-center border font-mono text-sm text-brand-ink">
              Điểm phù hợp = Σ (Trọng số_k × Điểm chuẩn hóa_k) / Σ Trọng số_k
            </div>
            <p className="text-xs text-muted-foreground">
              ✓ Nếu học sinh thiếu điểm ở một tiêu chí (ví dụ chưa thi SAT), hệ thống tự động tái chuẩn
              hóa trên các tiêu chí còn lại, không bao giờ đánh tụt điểm về 0 vô lý.
            </p>
          </div>
        </div>

        {/* Các chế độ ưu tiên */}
        <div className="pt-4 space-y-3">
          <h3 className="text-h3 text-foreground font-semibold">
            4 Chế độ ưu tiên linh hoạt trên màn hình kết quả
          </h3>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {presets.map((ps) => {
              const Icon = ps.icon;
              return (
                <div
                  key={ps.name}
                  className="rounded-xl border border-line bg-surface p-4 space-y-2 transition-colors hover:border-brand/40"
                >
                  <div className="flex items-center gap-2">
                    <div className="flex size-7 items-center justify-center rounded-md bg-brand-wash text-brand">
                      <Icon className="size-4" />
                    </div>
                    <span className="font-semibold text-sm text-foreground">{ps.name}</span>
                  </div>
                  <div className="text-xs font-medium text-brand">{ps.weights}</div>
                  <p className="text-xs text-muted-foreground leading-normal">{ps.desc}</p>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* AI có trách nhiệm */}
      <section className="space-y-6">
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <SparklesIcon className="size-5 text-brand" />
            <h2 className="text-h1 text-foreground">4. Trí tuệ nhân tạo có kiểm soát (Responsible AI)</h2>
          </div>
          <p className="text-body text-muted-foreground">
            AI đóng vai trò là trợ lý thông minh đắc lực, nhưng tuyệt đối không được phép tự tạo số liệu
            hay quyết định thay bạn:
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <Card>
            <CardHeader>
              <CardTitle className="text-h3 text-foreground">OCR Bảng điểm thông minh</CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground space-y-2">
              <p>
                Mô hình Vision tự động đọc ảnh chụp học bạ hoặc bảng điểm PDF, nhận diện học kỳ và từng môn học
                để tự điền dữ liệu.
              </p>
              <p className="text-xs text-foreground font-medium">
                ✓ Bạn luôn xem và chỉnh sửa lại từng ô điểm trước khi xác nhận.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-h3 text-foreground">Fact-Check Guardrail</CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground space-y-2">
              <p>
                Lời giải thích cho từng gợi ý trường do AI viết đều được kiểm tra đối chiếu tự động với số liệu
                tuyển sinh thực tế của trường.
              </p>
              <p className="text-xs text-foreground font-medium">
                ✓ Loại bỏ 100% hiện tượng thông tin ảo (hallucination).
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-h3 text-foreground">Bảo mật & Tự động lưu nháp</CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground space-y-2">
              <p>
                Mọi bước làm trong quy trình 6 bước được tự động lưu nháp vào tài khoản của bạn. Bạn có thể
                rời đi và quay lại tiếp tục bất cứ lúc nào.
              </p>
              <p className="text-xs text-foreground font-medium">
                ✓ Dữ liệu hồ sơ học sinh được bảo mật theo tiêu chuẩn cao nhất.
              </p>
            </CardContent>
          </Card>
        </div>
      </section>

      {/* CTA Footer */}
      <section className="rounded-2xl border border-brand/20 bg-gradient-to-r from-brand-ink to-brand p-8 text-white sm:p-10">
        <div className="max-w-2xl space-y-4">
          <h2 className="text-display font-semibold text-white">
            Sẵn sàng tìm kiếm ngôi trường phù hợp với bạn?
          </h2>
          <p className="text-white/85 text-sm sm:text-base leading-relaxed">
            Chỉ mất khoảng 4 phút để hoàn thành hồ sơ 6 bước trực quan. Hệ thống sẽ tự động phân tích
            và sắp xếp danh sách trường học tối ưu theo đúng mục tiêu của bạn.
          </p>
          <div className="pt-2 flex flex-wrap gap-3">
            <Link
              href="/analysis"
              className={buttonVariants({
                variant: "secondary",
                size: "lg",
                className: "bg-white text-brand-ink hover:bg-white/90 font-semibold",
              })}
            >
              Bắt đầu quy trình phân tích hồ sơ
              <ArrowRightIcon className="size-4 ml-1.5" />
            </Link>
            <Link
              href="/advisor"
              className={buttonVariants({
                variant: "outline",
                size: "lg",
                className: "border-white/30 text-white hover:bg-white/10 hover:text-white",
              })}
            >
              Hỏi đáp cùng Chat AI
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
