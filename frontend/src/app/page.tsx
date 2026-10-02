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
];

export default function HomePage() {
  return (
    <section className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-3xl font-bold">Study Abroad Decision Support System</h1>
        <p className="text-slate-600">
          Nền tảng hỗ trợ học sinh, sinh viên và phụ huynh ra quyết định du học Mỹ, từ THPT, đại học đến thạc sĩ,
          tiến sĩ.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        {features.map((f) => (
          <Link
            key={f.href}
            href={f.href}
            className="rounded-lg border border-slate-200 bg-white p-5 transition hover:border-slate-400"
          >
            <h2 className="font-semibold">{f.title}</h2>
            <p className="mt-1 text-sm text-slate-600">{f.description}</p>
          </Link>
        ))}
      </div>
    </section>
  );
}
