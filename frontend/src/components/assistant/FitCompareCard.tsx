"use client";

// UC-154: bảng so sánh 2 đến 4 trường theo độ phù hợp với hồ sơ: nhóm và điểm năm thành phần của từng trường, do code tính bằng công thức gợi ý.
// Phần nhận xét do AI viết nằm riêng, có nhãn, để học sinh phân biệt với số liệu hệ thống tính. Hệ thống không chọn trường thay học sinh.

import { SparklesIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { groupLabel, groupVariant } from "@/lib/recommendation-labels";
import type { FitCompareData, FitCompareSchool, FitPartKey } from "@/types/recommendation";
import { FIT_PART_INFO } from "./ExplainCard";

const FIT_ORDER: FitPartKey[] = ["position", "budget", "quality", "aid", "english"];

// Lý do trường không nằm trong gợi ý (mã lọc cứng của UC-22). Mã lạ giữ nguyên chữ, không đoán nghĩa
const EXCLUDE_TEXT: Record<string, string> = {
  MAJOR: "Trường không có ngành bạn chọn",
  STATE: "Trường nằm ngoài các bang bạn chọn",
  CONTROL: "Trường khác loại trường bạn chọn",
  ENGLISH: "Điểm tiếng Anh thấp hơn mức tối thiểu từ 1 band",
  ENGLISH_NO_CERT: "Bạn chưa có chứng chỉ tiếng Anh",
  BUDGET: "Chi phí vượt ngân sách",
};

const points = (score: number | null) => (score == null ? "Chưa có số liệu" : `${Math.round(score * 100)}`);

// Nhóm để hiện: trường cần học bổng lớn có nhãn riêng như trang kết quả
function groupOf(school: FitCompareSchool): { text: string; variant: ReturnType<typeof groupVariant> } {
  if (school.status === "excluded") return { text: "Không nằm trong gợi ý", variant: "neutral" };
  if (school.status === "aid") return { text: "Cần học bổng lớn", variant: groupVariant("AID") };
  return { text: groupLabel(school.group ?? "NA"), variant: groupVariant(school.group ?? "NA") };
}

export function FitCompareCard({ data }: { data: FitCompareData }) {
  const schools = data.schools;
  return (
    <div className="mt-2.5 space-y-3 rounded-xl border border-line bg-surface p-3 text-sm">
      <p className="text-[13px] font-bold text-brand">So sánh theo độ phù hợp: {schools.map((s) => s.name).join(" và ")}</p>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[320px] border-collapse text-left text-xs">
          <thead>
            <tr className="border-b border-line align-bottom">
              <th className="w-28 py-1.5 pr-2 text-xs font-medium text-ink-2">Mục</th>
              {schools.map((s) => (
                <th key={s.code} className="px-2 py-1.5 text-[12px] font-bold text-foreground">
                  {s.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr className="border-b border-line/60">
              <th className="py-1.5 pr-2 text-xs font-medium text-ink-2">Nhóm</th>
              {schools.map((s) => {
                const group = groupOf(s);
                return (
                  <td key={s.code} className="px-2 py-1.5">
                    <Badge variant={group.variant}>{group.text}</Badge>
                  </td>
                );
              })}
            </tr>
            <tr className="border-b border-line/60">
              <th className="py-1.5 pr-2 text-xs font-medium text-ink-2">Điểm phù hợp</th>
              {schools.map((s) => (
                <td key={s.code} className="px-2 py-1.5 font-bold text-foreground">
                  {s.status === "excluded" ? (
                    <span className="font-normal text-ink-3">{EXCLUDE_TEXT[s.excludeReason ?? ""] ?? s.excludeReason ?? "Không đạt điều kiện lọc"}</span>
                  ) : s.total == null ? (
                    <span className="font-normal text-ink-3">Chưa tính được</span>
                  ) : (
                    `${Math.round(s.total * 100)} / 100`
                  )}
                </td>
              ))}
            </tr>
            {FIT_ORDER.map((key) => (
              <tr key={key} className="border-b border-line/60 last:border-0">
                <th className="py-1.5 pr-2 align-top text-xs font-medium text-ink-2">{FIT_PART_INFO[key].label}</th>
                {schools.map((s) => {
                  const part = s.parts.find((p) => p.key === key);
                  return (
                    <td key={s.code} className="px-2 py-1.5 align-top text-ink-2">
                      {s.status === "excluded" || !part ? (
                        "–"
                      ) : (
                        <>
                          <span className="font-semibold text-foreground">{points(part.score)}</span>
                          {part.score != null && <span className="text-ink-3"> · trọng số {part.weight.toFixed(2).replace(".", ",")}</span>}
                        </>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {data.aiNote && (
        <div className="space-y-1.5 rounded-lg bg-brand-wash/50 p-2.5">
          <p className="inline-flex items-center gap-1 text-xs font-semibold text-brand">
            <SparklesIcon className="size-3.5" />
            AI nhận xét
          </p>
          <p className="text-xs leading-relaxed whitespace-pre-wrap text-foreground">{data.aiNote}</p>
          <p className="text-xs text-ink-3">Nhận xét do AI viết từ số liệu ở bảng trên, chỉ để tham khảo. AI không chọn trường và không đổi nhóm.</p>
        </div>
      )}

      <p className="text-xs text-ink-3">
        {data.priorityAdjusted ? "Trọng số đã đổi theo các nút ưu tiên của bạn. " : ""}
        Điểm phù hợp chỉ dùng để sắp xếp các trường trong cùng một nhóm; trường khác nhóm thì xem nhóm trước. Đây là mức tham chiếu, không phải
        cam kết đỗ.
      </p>
    </div>
  );
}
