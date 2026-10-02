import { ChatBox } from "@/components/advisor/ChatBox";

export const metadata = { title: "Tư vấn AI – USAS" };

export default function AdvisorPage() {
  return (
    <section className="space-y-4">
      <h1 className="text-2xl font-bold">Tư vấn du học Mỹ bằng AI</h1>
      <p className="text-sm text-slate-600">
        Hỗ trợ mọi bậc: THCS/THPT, cao đẳng, đại học, thạc sĩ, tiến sĩ. Chọn bậc học để câu trả lời sát hơn.
      </p>
      <ChatBox />
    </section>
  );
}
