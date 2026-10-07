import Link from "next/link";

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
];

export default function HomePage() {
  return (
    <section className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-3xl font-bold tracking-tight text-slate-900">
          Study Abroad Decision Support System
        </h1>
        <p className="text-slate-600">
          Nền tảng hỗ trợ học sinh, sinh viên và phụ huynh ra quyết định du học Mỹ, từ THPT, đại học đến thạc sĩ,
          tiến sĩ.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        {features.map((f) => (
          <Link
            key={f.href}
            href={f.href}
            className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-blue-400 hover:shadow-md"
          >
            <h2 className="font-semibold text-slate-900">{f.title}</h2>
            <p className="mt-2 text-sm text-slate-600">{f.description}</p>
          </Link>
        ))}
      </div>
    </section>
  );
}
