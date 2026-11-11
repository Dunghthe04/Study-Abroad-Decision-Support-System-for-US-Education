// Mục tiêu học sinh tích ở đầu form: quyết định ô nào bắt buộc và bấm nút cuối thì đi đâu.
// Mã mục tiêu phải khớp FORM_GOALS trong advisor/app/services/intake.py (chat gửi qua link ?goal=)
export type FormGoal = "danh_gia" | "lo_trinh" | "goi_y_truong";

export const FORM_GOAL_OPTIONS: { value: FormGoal; label: string; description: string }[] = [
  { value: "danh_gia", label: "Đánh giá hồ sơ", description: "Hồ sơ đang mạnh, yếu ở đâu" },
  { value: "lo_trinh", label: "Lên lộ trình", description: "Cần làm gì, khi nào" },
  { value: "goi_y_truong", label: "Gợi ý trường", description: "Danh sách trường hợp với bạn" },
];

// Mục tiêu cần ngành thật (không được "Chưa chọn ngành"): lộ trình và gợi ý trường lọc theo ngành
const NEEDS_MAJOR: FormGoal[] = ["lo_trinh", "goi_y_truong"];

const ALL_GOALS = FORM_GOAL_OPTIONS.map((o) => o.value);

// "danh_gia,lo_trinh" → ["danh_gia", "lo_trinh"]; mã lạ bỏ qua, giữ thứ tự của FORM_GOAL_OPTIONS
export function parseGoals(text: string | null | undefined): FormGoal[] {
  const picked = new Set((text ?? "").split(",").map((s) => s.trim()));
  return ALL_GOALS.filter((g) => picked.has(g));
}

export function goalsNeedMajor(goals: FormGoal[]): boolean {
  return goals.some((g) => NEEDS_MAJOR.includes(g));
}

export function onlySchoolList(goals: FormGoal[]): boolean {
  return goals.length === 1 && goals[0] === "goi_y_truong";
}

// Câu gửi sang chat khi vào form trực tiếp: "Đánh giá hồ sơ, lên lộ trình và gợi ý trường cho em"
export function goalsQuestion(goals: FormGoal[]): string {
  const labels = FORM_GOAL_OPTIONS.filter((o) => goals.includes(o.value)).map((o, i) =>
    i === 0 ? o.label : o.label.charAt(0).toLowerCase() + o.label.slice(1)
  );
  const joined = labels.length > 1 ? `${labels.slice(0, -1).join(", ")} và ${labels.at(-1)}` : (labels[0] ?? "");
  return `${joined} cho em`;
}
