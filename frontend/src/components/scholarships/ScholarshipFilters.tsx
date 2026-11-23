import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { COVERAGE_OPTIONS, KIND_OPTIONS } from "@/lib/admin-scholarship-display";
import type { ScholarshipSearchParams } from "@/types/scholarship";

const LEVELS = [
  { value: "", label: "Mọi bậc học" },
  { value: "undergraduate", label: "Đại học" },
  { value: "graduate", label: "Sau đại học" },
];

/** Form GET thuần: bấm Lọc thì trang tải lại theo query string, không cần state phía client. */
export function ScholarshipFilters({ params }: { params: ScholarshipSearchParams }) {
  return (
    <form method="get" className="grid gap-3 rounded-xl border bg-card p-4 sm:grid-cols-2 lg:grid-cols-4">
      <Input name="search" placeholder="Tên học bổng, đơn vị cấp, trường" defaultValue={params.search ?? ""} className="lg:col-span-2" />
      <NativeSelect name="studyLevel" defaultValue={params.studyLevel ?? ""} className="w-full" aria-label="Bậc học">
        {LEVELS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </NativeSelect>
      <NativeSelect name="coverageType" defaultValue={params.coverageType ?? ""} className="w-full" aria-label="Loại hỗ trợ">
        {COVERAGE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.value ? o.label : "Mọi loại hỗ trợ"}</option>)}
      </NativeSelect>
      <NativeSelect name="kind" defaultValue={params.kind ?? ""} className="w-full" aria-label="Loại học bổng">
        {KIND_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.value ? o.label : "Mọi loại học bổng"}</option>)}
      </NativeSelect>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" name="vnEligible" value="true" defaultChecked={params.vnEligible === true} />
        Chỉ học bổng mở cho người Việt Nam
      </label>
      <Input name="minAmountUsd" type="number" min={0} placeholder="Giá trị từ (USD)" defaultValue={params.minAmountUsd ?? ""} />
      <Input name="maxAmountUsd" type="number" min={0} placeholder="Giá trị đến (USD)" defaultValue={params.maxAmountUsd ?? ""} />
      <Input name="maxMinGpa4" type="number" step="0.1" min={0} max={4} placeholder="GPA của bạn (thang 4)" defaultValue={params.maxMinGpa4 ?? ""} />
      <Input name="maxMinIelts" type="number" step="0.5" min={0} max={9} placeholder="IELTS của bạn" defaultValue={params.maxMinIelts ?? ""} />
      <div className="flex gap-2 sm:col-span-2 lg:col-span-4">
        <Button type="submit">Lọc</Button>
        <Button type="button" variant="outline" render={<a href="/scholarships" />}>Xóa lọc</Button>
      </div>
    </form>
  );
}
