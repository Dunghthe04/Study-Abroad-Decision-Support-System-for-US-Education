import Link from "next/link";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

const features = [
  {
    href: "/advisor",
    title: "Tư vấn AI",
    description: "Hỏi về visa F-1, hồ sơ, chi phí, học bổng cho mọi bậc học. Câu trả lời có trích nguồn.",
  },
  {
    href: "/centers",
    title: "Tìm trung tâm",
    description: "Lọc trung tâm tư vấn du học theo dịch vụ, khu vực và bậc học.",
  },
  {
    href: "/account",
    title: "Hồ sơ cá nhân",
    description: "Đăng ký hoặc đăng nhập để quản lý lộ trình tư vấn và bảo mật thông tin hồ sơ du học.",
  },
  {
    href: "/profile/academic",
    title: "Hồ sơ học thuật & Bảng điểm",
    description: "Nhập điểm từng môn theo kỳ (3 năm gần nhất), chọn thang điểm và chứng chỉ IELTS/SAT để AI phân tích năng lực.",
  },
];

export default function HomePage() {
  return (
    <section className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-display">
          Study Abroad Decision Support System
        </h1>
        <p className="text-body text-muted-foreground">
          Nền tảng hỗ trợ học sinh, sinh viên và phụ huynh ra quyết định du học Mỹ, từ THPT, đại học đến thạc sĩ,
          tiến sĩ.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {features.map((f) => (
          <Link key={f.href} href={f.href} className="block">
            <Card className="h-full">
              <CardHeader>
                <CardTitle>
                  <h2>{f.title}</h2>
                </CardTitle>
                <CardDescription>{f.description}</CardDescription>
              </CardHeader>
            </Card>
          </Link>
        ))}
      </div>
    </section>
  );
}
