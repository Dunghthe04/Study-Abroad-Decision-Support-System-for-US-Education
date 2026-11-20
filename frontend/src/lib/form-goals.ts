// Mục tiêu học sinh tích ở đầu form: quyết định ô nào bắt buộc và bấm nút cuối thì đi đâu.
// Mã mục tiêu phải khớp FORM_GOALS trong advisor/app/services/intake.py (chat gửi qua link ?goal=).
// Riêng "chi_sua": chỉ lưu hồ sơ, không làm việc gì khác. Mở từ link "Sửa hồ sơ" thì lưu xong quay về trang cũ;
// chat gửi khi học sinh sửa hồ sơ mà không có câu tư vấn nào đang chờ, lưu xong về chat báo đã lưu
export type FormGoal = "danh_gia" | "lo_trinh" | "goi_y_truong" | "chi_sua";

export const FORM_GOAL_OPTIONS: { value: FormGoal; label: string; description: string }[] = [
  { value: "danh_gia", label: "Đánh giá hồ sơ", description: "Hồ sơ đang mạnh, yếu ở đâu" },
  { value: "lo_trinh", label: "Lên lộ trình", description: "Cần làm gì, khi nào" },
  { value: "goi_y_truong", label: "Gợi ý trường", description: "Danh sách trường hợp với bạn" },
  { value: "chi_sua", label: "Chỉ sửa hồ sơ", description: "Lưu hồ sơ, không làm việc gì khác" },
];

export const EDIT_ONLY: FormGoal = "chi_sua";

// Mục tiêu cần ngành thật (không được "Chưa chọn ngành"): lộ trình và gợi ý trường lọc theo ngành
const NEEDS_MAJOR: FormGoal[] = ["lo_trinh", "goi_y_truong"];

const ALL_GOALS = FORM_GOAL_OPTIONS.map((o) => o.value);

// Mục tiêu đã tích ở form, hoặc việc học sinh đã hỏi khi điền hồ sơ trong chat (sessionStorage).
// Không lưu vào nháp hồ sơ vì đây là việc muốn làm, không phải thông tin hồ sơ
export const GOALS_KEY = "usas_flow_goals";
// Báo form đang mở biết mục tiêu đã lưu vừa đổi (chat gửi thêm việc học sinh hỏi)
export const GOALS_CHANGED_EVENT = "usas:form-goals-changed";

// "danh_gia,lo_trinh" → ["danh_gia", "lo_trinh"]; mã lạ bỏ qua, giữ thứ tự của FORM_GOAL_OPTIONS
export function parseGoals(text: string | null | undefined): FormGoal[] {
  const picked = new Set((text ?? "").split(",").map((s) => s.trim()));
  return ALL_GOALS.filter((g) => picked.has(g));
}

export function goalsNeedMajor(goals: FormGoal[]): boolean {
  return goals.some((g) => NEEDS_MAJOR.includes(g));
}

// "Chỉ sửa hồ sơ" không đi cùng việc khác: tích nó thì bỏ các việc kia, tích việc khác thì bỏ nó
export function toggleGoal(goals: FormGoal[], goal: FormGoal): FormGoal[] {
  if (goals.includes(goal)) return goals.filter((g) => g !== goal);
  if (goal === EDIT_ONLY) return [EDIT_ONLY];
  return parseGoals([...goals.filter((g) => g !== EDIT_ONLY), goal].join(","));
}

export function onlyEdit(goals: FormGoal[]): boolean {
  return goals.includes(EDIT_ONLY);
}

// Link mở form để sửa hồ sơ, tích sẵn "Chỉ sửa hồ sơ" (chỉ khi đã có hồ sơ). Lưu xong quay về trang `back`
export function editProfileHref(back: string): string {
  return `/analysis?edit=1&goal=${EDIT_ONLY}&back=${encodeURIComponent(back)}`;
}

// Trang quay về sau khi lưu: chỉ nhận đường dẫn trong trang web (bắt đầu bằng một dấu /)
export function safeBackPath(back: string | null): string | null {
  return back && back.startsWith("/") && !back.startsWith("//") && !back.startsWith("/\\") ? back : null;
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

// Chat gửi việc học sinh đã hỏi khi điền hồ sơ trong chat: gộp với mục tiêu đã lưu để mở form từ menu vẫn tích sẵn
export function rememberChatGoals(codes: string[]): void {
  try {
    const saved = parseGoals(sessionStorage.getItem(GOALS_KEY));
    const next = parseGoals([...saved.filter((g) => g !== EDIT_ONLY), ...codes].join(","));
    if (next.length === saved.length) return; // không có mục mới
    sessionStorage.setItem(GOALS_KEY, next.join(","));
  } catch {
    return; // trình duyệt chặn storage → bỏ qua
  }
  window.dispatchEvent(new Event(GOALS_CHANGED_EVENT));
}
