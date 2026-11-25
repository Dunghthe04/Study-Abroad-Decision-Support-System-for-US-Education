"use client";

// Bảng "Bạn so với trường" trong khung chat: từng tiêu chí bộ chấm dùng (điểm thi, GPA, ngoại khóa, tỉ lệ nhận, chi phí, tiếng Anh)
// của học sinh đặt cạnh số của trường, kèm nhận xét thiếu, đủ hay vượt. Dựng hoàn toàn từ kết quả code đã tính (không có AI),
// nên số luôn khớp với danh sách gợi ý.

import { formatGpa, formatPct, formatUsd } from "@/lib/recommendation-labels";
import { cn } from "@/lib/utils";
import { BasisBadge } from "./BasisBadge";
import type { GoalCompareRow, GoalEnglish, SchoolGoalResult } from "@/types/recommendation";

type Tone = "good" | "bad" | "muted";

export interface CompareLine {
  label: string;
  student: string;
  school: string;
  // Dòng chữ nhỏ dưới ô "Trường" (mức trường coi trọng tiêu chí)
  note?: string;
  diff: string;
  tone: Tone;
}

const TONE_CLASS: Record<Tone, string> = { good: "text-ok", bad: "text-risk", muted: "text-ink-3" };

const MISSING_SCHOOL = "Trường chưa công bố";
const MISSING_STUDENT = "Hồ sơ chưa có";

const IMPORTANCE_TEXT: Record<string, string> = {
  very_important: "rất quan trọng",
  important: "quan trọng",
  considered: "được xem xét",
  not_considered: "không xét",
};

function points(value: number): string {
  return Math.round(Math.abs(value)).toLocaleString("vi-VN");
}

// "(hơn khoảng 85% sinh viên của trường)"
function positionText(position: number | null | undefined): string {
  if (position == null) return "";
  return ` (hơn khoảng ${Math.min(100, Math.max(0, Math.round(position * 100)))}% sinh viên của trường)`;
}

function importanceNote(importance: string | null | undefined): string | undefined {
  const text = importance ? IMPORTANCE_TEXT[importance] : null;
  return text ? `Trường coi là ${text}` : undefined;
}

// SAT, ACT: khoảng 25–75% sinh viên của trường
function testLine(row: GoalCompareRow): CompareLine {
  const label = row.factor === "act" ? "ACT" : "SAT";
  const student = row.student != null ? String(Math.round(row.student)) : MISSING_STUDENT;
  const school = row.low != null && row.high != null ? `${Math.round(row.low)}–${Math.round(row.high)}` : MISSING_SCHOOL;
  const note = importanceNote(row.importance);
  const gap = row.gap ?? 0;
  switch (row.status) {
    case "below":
      return { label, student, school, note, diff: `Thấp hơn mốc dưới ${points(gap)} điểm${positionText(row.position)}`, tone: "bad" };
    case "above":
      return { label, student, school, note, diff: `Cao hơn mốc trên ${points(gap)} điểm${positionText(row.position)}`, tone: "good" };
    case "within":
      return { label, student, school, note, diff: `Nằm trong khoảng của trường${positionText(row.position)}`, tone: "good" };
    default:
      return { label, student, school, note, diff: "Chưa so sánh được", tone: "muted" };
  }
}

// GPA: trường công bố trung bình; vị trí tính theo phân phối GPA của trường nên có thể khác cảm giác "chênh so với trung bình"
function gpaLine(row: GoalCompareRow): CompareLine {
  const student = row.student != null ? formatGpa(row.student) : MISSING_STUDENT;
  const school = row.low != null ? `${formatGpa(row.low)} (trung bình)` : MISSING_SCHOOL;
  const note = importanceNote(row.importance);
  const gap = Math.abs(row.gap ?? 0);
  switch (row.status) {
    case "below":
      return { label: "GPA", student, school, note, diff: `Thấp hơn trung bình ${formatGpa(gap)}${positionText(row.position)}`, tone: "bad" };
    case "above":
      return { label: "GPA", student, school, note, diff: `Cao hơn trung bình ${formatGpa(gap)}${positionText(row.position)}`, tone: "good" };
    case "within":
      return { label: "GPA", student, school, note, diff: `Ngang mức trung bình${positionText(row.position)}`, tone: "good" };
    default:
      return { label: "GPA", student, school, note, diff: "Chưa so sánh được", tone: "muted" };
  }
}

// Ngoại khóa: bậc 3 là trung bình; trường không xét thì bậc không đổi kết quả
function activitiesLine(row: GoalCompareRow): CompareLine {
  const student = row.student != null ? `Bậc ${Math.round(row.student)}/5` : MISSING_STUDENT;
  const base = { label: "Ngoại khóa", student, school: "Bậc trung bình là 3", note: importanceNote(row.importance) };
  switch (row.status) {
    case "below":
      return { ...base, diff: "Thấp hơn mức trung bình, kéo điểm vị trí xuống", tone: "bad" };
    case "above":
      return { ...base, diff: "Cao hơn mức trung bình, cộng thêm vào điểm vị trí", tone: "good" };
    case "within":
      return { ...base, diff: "Mức trung bình, không cộng không trừ", tone: "muted" };
    default:
      return { label: "Ngoại khóa", student, school: "Trường không xét ngoại khóa", diff: "Không ảnh hưởng đến nhóm", tone: "muted" };
  }
}

// Tỉ lệ nhận: luật "dưới 15% luôn Khó vào" và luật "dưới 50% không được An toàn", xét tỉ lệ du học sinh nếu trường có, không thì tỉ lệ chung (UC-22)
function acceptLine(row: GoalCompareRow): CompareLine {
  const school =
    row.low != null
      ? `Chung ${formatPct(row.low)}${row.high != null ? `, du học sinh ${formatPct(row.high)}` : ""}`
      : MISSING_SCHOOL;
  const base = { label: "Tỷ lệ nhận", student: "Không áp dụng", school };
  switch (row.status) {
    case "always_reach":
      return { ...base, diff: "Dưới 15%: trường luôn xếp Khó vào, điểm cao đến đâu cũng không đổi", tone: "bad" };
    case "capped":
      return { ...base, diff: "Dưới 50%: trường chỉ có thể là Vừa sức, không bao giờ là An toàn hơn", tone: "muted" };
    case "ok":
      return { ...base, diff: "Không bị chặn bởi tỉ lệ nhận", tone: "good" };
    default:
      return { ...base, diff: "Chưa so sánh được", tone: "muted" };
  }
}

function lineOf(row: GoalCompareRow): CompareLine {
  switch (row.factor) {
    case "gpa":
      return gpaLine(row);
    case "ec":
      return activitiesLine(row);
    case "accept":
      return acceptLine(row);
    default:
      return testLine(row);
  }
}

function costLine(result: SchoolGoalResult): CompareLine {
  const b = result.budget;
  const school = b.costUsd != null ? `${formatUsd(b.costUsd)} mỗi năm` : MISSING_SCHOOL;
  const student = b.budgetUsd != null ? `Ngân sách ${formatUsd(b.budgetUsd)}` : MISSING_STUDENT;
  switch (b.status) {
    case "ok":
      return { label: "Chi phí", student, school, diff: "Nằm trong ngân sách", tone: "good" };
    case "need_aid":
      return { label: "Chi phí", student, school, diff: `Vượt trần ${formatUsd(b.shortfallUsd)}, cần học bổng lớn`, tone: "bad" };
    case "over_budget":
      return { label: "Chi phí", student, school, diff: `Vượt trần ${formatUsd(b.shortfallUsd)}`, tone: "bad" };
    case "no_budget":
      return { label: "Chi phí", student, school, diff: "Hồ sơ chưa đặt ngân sách", tone: "muted" };
    default:
      return { label: "Chi phí", student, school, diff: "Chưa so sánh được", tone: "muted" };
  }
}

// "IELTS 7,5, TOEFL 100 hoặc Duolingo 130"
function englishNeed(english: GoalEnglish): string {
  const parts = [
    english.minIelts != null ? `IELTS ${String(english.minIelts).replace(".", ",")}` : null,
    english.minToefl != null ? `TOEFL ${english.minToefl}` : null,
    english.minDuolingo != null ? `Duolingo ${english.minDuolingo}` : null,
  ].filter(Boolean) as string[];
  if (parts.length === 0) return MISSING_SCHOOL;
  return parts.length === 1 ? parts[0] : `${parts.slice(0, -1).join(", ")} hoặc ${parts[parts.length - 1]}`;
}

// Chỉ có khi trường bị loại vì tiếng Anh (backend chỉ trả English khi đó)
function englishLine(english: GoalEnglish): CompareLine {
  const noCert = english.shortfallBand == null;
  return {
    label: "Tiếng Anh",
    student: noCert ? "Chưa có chứng chỉ" : "Chưa đủ mức",
    school: `Tối thiểu ${englishNeed(english)}`,
    diff: noCert ? "Cần có chứng chỉ" : `Thiếu ${String(english.shortfallBand).replace(".", ",")} band`,
    tone: "bad",
  };
}

export function goalCompareLines(result: SchoolGoalResult): CompareLine[] {
  const rows = result.comparison ?? [];
  const lines = rows.map(lineOf);
  lines.push(costLine(result));
  if (result.english) lines.push(englishLine(result.english));
  return lines;
}

// basisLabel: mô tả giá trị giả định đã dùng, chỉ có khi result.basis là simulated
export function GoalCompareTable({ result, basisLabel }: { result: SchoolGoalResult; basisLabel?: string | null }) {
  // Bản backend cũ chưa trả bảng: không dựng bảng thiếu nửa
  if (!result.comparison || result.comparison.length === 0) return null;
  const lines = goalCompareLines(result);
  const simulated = result.basis === "simulated";
  return (
    <div className="mt-3 space-y-2 rounded-xl border border-[var(--control-border)]/60 bg-[var(--surface-2)] p-3.5 text-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] font-bold text-foreground">Bạn so với {result.name}</p>
        <BasisBadge kind={simulated ? "simulated" : "real"} label={simulated ? basisLabel : null} />
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[480px] border-collapse text-left text-xs">
          <thead>
            <tr className="border-b border-line text-ink-3">
              <th className="py-1.5 pr-3 font-medium">Tiêu chí</th>
              <th className="py-1.5 pr-3 font-medium">Bạn</th>
              <th className="py-1.5 pr-3 font-medium">Trường</th>
              <th className="py-1.5 font-medium">Nhận xét</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => (
              <tr key={line.label} className="border-b border-line/60 align-top last:border-0">
                <th scope="row" className="py-2 pr-3 font-semibold text-foreground">{line.label}</th>
                <td className="py-2 pr-3 text-foreground">{line.student}</td>
                <td className="py-2 pr-3 text-ink-2">
                  {line.school}
                  {line.note && <span className="block text-[11px] text-ink-3">{line.note}</span>}
                </td>
                <td className={cn("py-2 font-medium", TONE_CLASS[line.tone])}>{line.diff}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
