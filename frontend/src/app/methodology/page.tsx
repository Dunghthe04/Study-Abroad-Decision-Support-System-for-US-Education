import type { Metadata } from "next";
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
  ScanTextIcon,
  ShieldAlertIcon,
} from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { groupLabel, groupVariant, statusLabel } from "@/lib/recommendation-labels";
import { OpenAssistantButton } from "@/components/assistant/OpenAssistantButton";
import { cn } from "@/lib/utils";

export const metadata: Metadata = {
  title: "Cách hệ thống xếp trường – USAS",
  description:
    "Hệ thống so hồ sơ của bạn với sinh viên từng trường đã nhận rồi xếp vào nhóm Khó vào, Vừa sức, An toàn hơn. Có hình minh họa và ví dụ có số.",
};

// Nội dung trang này mô tả đúng công thức trong backend (SchoolGrouper, SchoolRanker, HardFilter).
// Các số trong ví dụ được kiểm bằng test backend (MethodologyExampleTests): đổi công thức thì test báo và trang này phải sửa theo.

const steps = [
  { icon: FilterIcon, title: "Lọc", body: "Bỏ trường không hợp ngành, bang, tiếng Anh hoặc ngân sách của bạn." },
  { icon: LayersIcon, title: "Xếp nhóm", body: "So hồ sơ của bạn với sinh viên trường đã nhận để ra Khó vào, Vừa sức hay An toàn hơn." },
  { icon: ListOrderedIcon, title: "Sắp thứ tự", body: "Trong mỗi nhóm, trường hợp với bạn hơn đứng trước. Mỗi nhóm hiện tối đa 4 trường." },
];

// Trường mẫu: GPA trung bình 3,6 · SAT 1100–1350 · cả GPA và điểm thi được coi trọng như nhau · ngoại khoá bậc trung bình · nhận 55% hồ sơ
const examples = [
  { id: "A", profile: "GPA 3,5 · SAT 1250", position: 46, group: "M" },
  { id: "B", profile: "GPA 3,2 · SAT 1150", position: 22, group: "R" },
  { id: "C", profile: "GPA 3,9 · SAT 1400", position: 83, group: "S" },
];

const downgrades = [
  { title: "Trường rất chọn lọc", body: "Nhận dưới 15% hồ sơ", effect: "Luôn Khó vào", variant: "R" },
  { title: "Trường nhận ít hơn nửa số hồ sơ", body: "Nhận dưới 50%", effect: "Không được An toàn hơn", variant: "M" },
  { title: "Một chỉ số quá yếu", body: "GPA hoặc điểm thi dưới 25%", effect: "Chỉ được Vừa sức", variant: "M" },
  { title: "Trường nhận ít du học sinh", body: "Tỉ lệ nhận du học sinh thấp hơn nửa tỉ lệ chung", effect: "Hạ một nhóm", variant: "M" },
];

const fitParts = [
  { name: "Độ vừa tầm", weight: 30, body: "Cao nhất khi hồ sơ của bạn ở giữa nhóm sinh viên của trường.", tone: "bg-brand text-white" },
  { name: "Ngân sách", weight: 25, body: "Càng rẻ hơn ngân sách càng cao; vượt thì bằng 0.", tone: "bg-brand/80 text-white" },
  { name: "Chất lượng", weight: 20, body: "Tính từ vị trí của trường trong các bảng xếp hạng.", tone: "bg-brand/60 text-white" },
  { name: "Học bổng", weight: 15, body: "Phần trăm chi phí cả năm mà học bổng trung bình chi trả.", tone: "bg-brand/40 text-brand-ink" },
  { name: "Tiếng Anh", weight: 10, body: "Đạt mức tối thiểu của trường thì tối đa.", tone: "bg-brand/25 text-brand-ink" },
];

const limits = [
  { title: "Không phải xác suất đỗ", body: "Trường còn xét bài luận, thư giới thiệu và nhiều thứ hệ thống không thấy." },
  { title: "Thiếu số liệu thì không đoán", body: "Trường thiếu số liệu được ghi “Chưa đủ dữ liệu”, không bị xếp bừa." },
  { title: "Có giả định", body: "Trường chỉ công bố mốc 25% và 75% của điểm thi nên hệ thống giả định điểm phân bố hình chuông quanh hai mốc đó." },
];

const aiRules = [
  { icon: CalculatorIcon, title: "Nhóm do code tính", body: "Cùng hồ sơ và cùng số liệu trường thì luôn ra cùng kết quả. AI không quyết định nhóm." },
  { icon: BotIcon, title: "AI viết lời giải thích", body: "Các con số trong lời giải thích được đối chiếu tự động với số liệu tuyển sinh của trường." },
  { icon: ScanTextIcon, title: "Đọc bảng điểm từ ảnh", body: "Mô hình điền sẵn từ ảnh học bạ. Bạn luôn xem và sửa trước khi xác nhận." },
];

const rules = [
  { rule: "Tỉ lệ nhận của trường dưới 15%", effect: "Luôn là Khó vào, dù hồ sơ mạnh đến đâu." },
  { rule: "Vị trí học thuật đủ cao nhưng tỉ lệ nhận dưới 50%", effect: "Chỉ là Vừa sức, không được An toàn hơn." },
  { rule: "Vị trí học thuật đủ cao nhưng GPA hoặc điểm thi riêng lẻ dưới 25%", effect: "Chỉ là Vừa sức: chỉ số mạnh không che được chỉ số yếu." },
  { rule: "Tỉ lệ nhận du học sinh thấp hơn một nửa tỉ lệ nhận chung", effect: "Hạ một nhóm." },
  { rule: "Dưới 50% sinh viên của trường nộp SAT", effect: "SAT chỉ đại diện nhóm có nộp (thường điểm cao) nên trọng số điểm thi giảm một nửa." },
  { rule: "Trường bắt buộc SAT mà bạn chưa có điểm thi", effect: "Chưa đủ dữ liệu để xếp nhóm." },
];

function SectionCard({ title, note, children, className }: { title: string; note?: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={cn("rounded-xl border border-line bg-surface p-5 sm:p-6", className)}>
      <h2 className="font-sans text-lg font-semibold text-foreground">{title}</h2>
      {note && <p className="mt-1 text-sm text-muted-foreground">{note}</p>}
      <div className="mt-5">{children}</div>
    </section>
  );
}

// Thang vị trí học thuật 0–100%: ba vùng theo ngưỡng 25% và 75%, các hồ sơ ví dụ là chấm có chữ cái (không chỉ dựa vào màu)
function PositionScale() {
  return (
    <div
      role="img"
      aria-label={`Thang vị trí học thuật từ 0 đến 100%. Dưới 25% là Khó vào, từ 25% đến dưới 75% là Vừa sức, từ 75% là An toàn hơn. ${examples
        .map((e) => `Hồ sơ ${e.id} ở ${e.position}%, nhóm ${groupLabel(e.group)}`)
        .join(". ")}.`}
      className="px-3"
    >
      <div className="relative h-12">
        <div className="absolute inset-0 flex overflow-hidden rounded-lg">
          <div className="w-1/4 bg-reach-wash" />
          <div className="w-1/2 bg-brand-wash" />
          <div className="w-1/4 bg-ok-wash" />
        </div>
        {examples.map((e) => (
          <span
            key={e.id}
            style={{ left: `${e.position}%` }}
            className="absolute top-1/2 flex size-8 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-foreground text-sm font-semibold text-background ring-2 ring-surface"
          >
            {e.id}
          </span>
        ))}
      </div>
      <div className="relative mt-1.5 h-4 text-xs text-muted-foreground" aria-hidden>
        <span className="absolute left-0">0%</span>
        <span className="absolute -translate-x-1/2" style={{ left: "25%" }}>25%</span>
        <span className="absolute -translate-x-1/2" style={{ left: "75%" }}>75%</span>
        <span className="absolute right-0">100%</span>
      </div>
      <div className="mt-2 flex text-center text-sm font-medium" aria-hidden>
        <span className="w-1/4 text-reach">Khó vào</span>
        <span className="w-1/2 text-brand-ink">Vừa sức</span>
        <span className="w-1/4 text-ok">An toàn hơn</span>
      </div>
    </div>
  );
}

export default function MethodologyPage() {
  return (
    <div className="mx-auto max-w-[900px] space-y-6 pb-16">
      <div className="space-y-2 pt-5">
        <h1 className="text-display text-foreground">Cách hệ thống xếp trường</h1>
        <p className="max-w-[62ch] text-muted-foreground">
          Hệ thống trả lời một câu hỏi: <span className="font-medium text-foreground">hồ sơ của bạn đứng ở đâu so với sinh viên trường đã nhận?</span>{" "}
          Kết quả là nhóm để tham khảo, không phải xác suất đỗ.
        </p>
      </div>

      <section aria-label="Ba bước" className="grid gap-3 md:grid-cols-3">
        {steps.map((step, index) => {
          const Icon = step.icon;
          return (
            <div key={step.title} className="relative rounded-xl border border-line bg-surface p-5">
              <div className="flex items-center gap-3">
                <span className="flex size-10 items-center justify-center rounded-full bg-brand-wash text-brand">
                  <Icon aria-hidden className="size-5" />
                </span>
                <span className="text-numeric text-sm text-muted-foreground">Bước {index + 1}</span>
              </div>
              <h2 className="mt-3 font-sans text-base font-semibold text-foreground">{step.title}</h2>
              <p className="mt-1 text-sm text-muted-foreground">{step.body}</p>
            </div>
          );
        })}
      </section>

      <SectionCard
        title="Bước 2: hồ sơ của bạn đứng ở đâu"
        note="Mỗi hồ sơ được cho một vị trí từ 0% đến 100%. Vị trí 46% nghĩa là 46% sinh viên trường đã nhận có điểm thấp hơn bạn."
      >
        <PositionScale />
        <div className="mt-6 grid gap-3 sm:grid-cols-3">
          {examples.map((e) => (
            <div key={e.id} className="rounded-lg border border-line p-3">
              <div className="flex items-center gap-2">
                <span className="flex size-6 items-center justify-center rounded-full bg-foreground text-xs font-semibold text-background">{e.id}</span>
                <span className="text-sm font-medium text-foreground">{e.profile}</span>
              </div>
              <div className="mt-2 flex items-center justify-between gap-2">
                <span className="text-sm text-muted-foreground">
                  Vị trí <span className="text-numeric font-semibold text-foreground">{e.position}%</span>
                </span>
                <Badge variant={groupVariant(e.group)} className="h-6 px-2.5 text-[13px] font-semibold">
                  {groupLabel(e.group)}
                </Badge>
              </div>
            </div>
          ))}
        </div>
        <p className="mt-4 text-xs text-muted-foreground">
          Trường mẫu: GPA trung bình 3,6 · SAT từ 1100 (mốc 25%) đến 1350 (mốc 75%) · nhận 55% hồ sơ.
        </p>
      </SectionCard>

      <SectionCard title="Khi nào hệ thống hạ nhóm" note="Điểm cao chưa chắc là An toàn hơn. Bốn trường hợp sau làm nhóm thấp đi, dù vị trí học thuật của bạn cao.">
        <div className="grid gap-3 sm:grid-cols-2">
          {downgrades.map((d) => (
            <div key={d.title} className="flex gap-3 rounded-lg border border-line p-4">
              <ShieldAlertIcon aria-hidden className="mt-0.5 size-5 shrink-0 text-warn" />
              <div className="space-y-1">
                <div className="text-sm font-semibold text-foreground">{d.title}</div>
                <div className="text-sm text-muted-foreground">{d.body}</div>
                <Badge variant={groupVariant(d.variant)} className="mt-1 h-6 px-2.5 text-[13px] font-semibold">
                  {d.effect}
                </Badge>
              </div>
            </div>
          ))}
        </div>
        <p className="mt-4 text-sm text-muted-foreground">
          Ví dụ: hồ sơ C (vị trí 83%) ở trường nhận 55% là An toàn hơn, nhưng cũng hồ sơ đó ở trường chỉ nhận 40% thì là Vừa sức, và ở trường nhận 12% thì là Khó vào.
        </p>
      </SectionCard>

      <SectionCard title="Còn chi phí thì sao" note="Chi phí không tạo thêm nhóm. Nó quyết định trường nằm trong ba nhóm, vào khối riêng, hay bị loại.">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-3 rounded-lg border border-line p-3">
            <CheckCircle2Icon aria-hidden className="size-5 shrink-0 text-ok" />
            <span className="min-w-0 flex-1 text-sm text-foreground">Chi phí một năm không vượt ngân sách quá 10%</span>
            <ArrowRightIcon aria-hidden className="size-4 text-muted-foreground" />
            <span className="text-sm font-medium text-foreground">Nằm trong ba nhóm</span>
          </div>
          <div className="flex flex-wrap items-center gap-3 rounded-lg border border-line p-3">
            <ArrowDownIcon aria-hidden className="size-5 shrink-0 text-warn" />
            <span className="min-w-0 flex-1 text-sm text-foreground">Vượt quá 10%, bạn chọn cần học bổng, và trường có học bổng cho du học sinh</span>
            <ArrowRightIcon aria-hidden className="size-4 text-muted-foreground" />
            <Badge variant={groupVariant("AID")} className="h-6 px-2.5 text-[13px] font-semibold">
              Khối {statusLabel("AID")}
            </Badge>
          </div>
          <div className="flex flex-wrap items-center gap-3 rounded-lg border border-line p-3">
            <BanIcon aria-hidden className="size-5 shrink-0 text-muted-foreground" />
            <span className="min-w-0 flex-1 text-sm text-foreground">Vượt quá 10% trong các trường hợp còn lại</span>
            <ArrowRightIcon aria-hidden className="size-4 text-muted-foreground" />
            <span className="text-sm font-medium text-foreground">Bị loại</span>
          </div>
        </div>
        <p className="mt-4 text-sm text-muted-foreground">
          Tiếng Anh cũng không tạo nhóm: chưa có chứng chỉ, hoặc thiếu từ 1 band so với mức tối thiểu của trường thì trường bị loại ở bước lọc.
        </p>
      </SectionCard>

      <SectionCard title="Bước 3: sắp thứ tự trong nhóm" note="Trường nào hợp với bạn hơn thì đứng trước. Điểm phù hợp gồm năm phần, theo tỉ lệ dưới đây.">
        <div role="img" aria-label={`Tỉ lệ năm phần của điểm phù hợp: ${fitParts.map((p) => `${p.name} ${p.weight}%`).join(", ")}.`} className="flex overflow-hidden rounded-lg">
          {fitParts.map((p) => (
            <div key={p.name} style={{ width: `${p.weight}%` }} className={cn("flex h-11 items-center justify-center text-sm font-semibold", p.tone)}>
              {p.weight}%
            </div>
          ))}
        </div>
        <ul className="mt-4 grid gap-2.5 sm:grid-cols-2">
          {fitParts.map((p) => (
            <li key={p.name} className="flex gap-3 text-sm">
              <span className={cn("mt-1 size-3 shrink-0 rounded-sm", p.tone.split(" ")[0])} aria-hidden />
              <span>
                <span className="font-semibold text-foreground">{p.name} {p.weight}%.</span>{" "}
                <span className="text-muted-foreground">{p.body}</span>
              </span>
            </li>
          ))}
        </ul>
        <p className="mt-4 text-sm text-muted-foreground">
          Ba thanh ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng) chỉ đổi thứ tự trong nhóm, không đổi nhóm của trường.
        </p>
      </SectionCard>

      <SectionCard title="Điều cần biết trước khi dùng kết quả">
        <div className="grid gap-3 sm:grid-cols-3">
          {limits.map((item) => (
            <div key={item.title} className="rounded-lg bg-ground p-4">
              <div className="text-sm font-semibold text-foreground">{item.title}</div>
              <p className="mt-1 text-sm text-muted-foreground">{item.body}</p>
            </div>
          ))}
        </div>
      </SectionCard>

      <SectionCard title="AI dùng ở đâu" note="AI hỗ trợ, không tự tạo số liệu và không quyết định thay bạn.">
        <div className="grid gap-4 sm:grid-cols-3">
          {aiRules.map((r) => {
            const Icon = r.icon;
            return (
              <div key={r.title} className="space-y-1.5">
                <Icon aria-hidden className="size-5 text-brand" />
                <div className="text-sm font-semibold text-foreground">{r.title}</div>
                <p className="text-sm text-muted-foreground">{r.body}</p>
              </div>
            );
          })}
        </div>
      </SectionCard>

      <details className="rounded-xl border border-line bg-surface">
        <summary className="cursor-pointer px-5 py-4 font-sans text-lg font-semibold text-foreground sm:px-6">
          Quy tắc chi tiết cho người muốn kiểm chứng
        </summary>
        <div className="space-y-6 border-t border-line p-5 sm:p-6">
          <div>
            <h3 className="text-sm font-semibold text-foreground">Cách tính vị trí học thuật</h3>
            <ul className="mt-2 list-disc space-y-1.5 pl-4 text-sm text-muted-foreground">
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
            <h3 className="text-sm font-semibold text-foreground">Ngưỡng và các quy tắc có thể hạ nhóm</h3>
            <div className="mt-2 overflow-x-auto">
              <table className="w-full min-w-[520px] text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-xs text-muted-foreground">
                    <th className="py-2.5 pr-3 font-semibold">Điều kiện</th>
                    <th className="py-2.5 font-semibold">Kết quả</th>
                  </tr>
                </thead>
                <tbody>
                  <tr className="border-b border-line">
                    <td className="py-3 pr-3 text-foreground">Vị trí học thuật dưới 25%</td>
                    <td className="py-3 text-muted-foreground">Khó vào</td>
                  </tr>
                  <tr className="border-b border-line">
                    <td className="py-3 pr-3 text-foreground">Vị trí học thuật từ 25% đến dưới 75%</td>
                    <td className="py-3 text-muted-foreground">Vừa sức</td>
                  </tr>
                  <tr className="border-b border-line">
                    <td className="py-3 pr-3 text-foreground">Vị trí học thuật từ 75%</td>
                    <td className="py-3 text-muted-foreground">An toàn hơn (nếu không vướng các quy tắc dưới đây)</td>
                  </tr>
                  {rules.map((r) => (
                    <tr key={r.rule} className="border-b border-line last:border-0">
                      <td className="py-3 pr-3 text-foreground">{r.rule}</td>
                      <td className="py-3 text-muted-foreground">{r.effect}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-foreground">Điểm phù hợp</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              Mỗi thành phần có giá trị từ 0 đến 1, nhân với trọng số rồi cộng lại. Thành phần nào thiếu số liệu thì bị bỏ ra và trọng số được chia lại cho các thành phần còn lại. Ba thanh ưu tiên, mỗi thanh từ 0 đến 4 với 2 là cân bằng, nhân trọng số của ngân sách, học bổng và chất lượng với mức chọn chia 2.
            </p>
          </div>
        </div>
      </details>

      <div className="flex flex-wrap gap-3">
        <Link href="/analysis" className={buttonVariants({ size: "lg", className: "h-11 px-5" })}>
          Bắt đầu phân tích
          <ArrowRightIcon className="ml-1.5 size-4" />
        </Link>
        <OpenAssistantButton className="h-11 px-5" question="Vì sao chia trường thành 3 nhóm?" />
      </div>
    </div>
  );
}
