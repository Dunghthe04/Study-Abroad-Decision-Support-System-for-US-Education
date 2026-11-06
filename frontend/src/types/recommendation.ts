// Mirrors RecommendationsController và RecommendationService (StudyAbroad.Api / StudyAbroad.Application).
// Backend lưu kết quả thành JSON camelCase; các trường số có thể là null khi thiếu dữ liệu.

// [chi phí, học bổng, chất lượng], mỗi số 0–4
export type Pref = [number, number, number];

// R khó vào · RF khó vào xa · M vừa sức · S an toàn hơn · NA thiếu dữ liệu · O nhận gần như tất cả
export type SchoolGroup = "R" | "RF" | "M" | "S" | "NA" | "O";

// Mã lý do backend suy ra từ số đã tính cho từng trường
export type WhyCode =
  | "LOW_RATE"
  | "RATE_CAP"
  | "WEAK_CAP"
  | "GPA_HIGH"
  | "GPA_MID"
  | "GPA_LOW"
  | "SAT_ABOVE"
  | "SAT_IN_RANGE"
  | "SAT_BELOW"
  | "BUDGET_OK"
  | "NEED_AID"
  | "INTL_LOW"
  | "EC_PLUS"
  | "EC_MINUS";

// Cờ thiếu dữ liệu do lọc cứng gắn (mục 1.3)
export type FilterFlag =
  | "MAJOR_UNKNOWN"
  | "CONTROL_UNKNOWN"
  | "COST_UNKNOWN"
  | "NEED_BIG_AID"
  | "AID_UNKNOWN"
  | "ENGLISH_MIN_UNKNOWN"
  | "ENGLISH_NOT_COMPARABLE"
  | "ENGLISH_NEAR"
  // Hai cờ cũ: backend không còn gắn, nhưng lượt gợi ý đã lưu trước đây vẫn có thể chứa
  | "ENGLISH_NO_SCORE"
  | "ENGLISH_BELOW_MIN";

// Một trường đã chấm. Nhóm và số liệu do backend tính bằng code (không qua AI).
export interface ScoredSchool {
  universityId: string;
  code: string;
  name: string;
  state: string | null;
  control: string | null; // public | private
  website?: string | null; // trang chủ của trường; lần gợi ý cũ chưa có
  logoUrl?: string | null; // logo (Cloudinary); lần gợi ý cũ hoặc trường chưa có ảnh thì không có
  coverUrl?: string | null; // ảnh bìa
  cost: number | null; // tổng chi phí USD/năm
  outcome: "Keep" | "AidBlock";
  flags: FilterFlag[];
  g2: SchoolGroup;
  s: number | null;
  f: number | null;
  h: number | null;
  q: number | null;
  gap: number | null; // USD còn thiếu khi vượt ngân sách
  pg: number | null;
  ps: number | null;
  adj: number | null;
  r: number | null;
  acceptRate?: number | null; // tỷ lệ nhận chung 0–1; lần gợi ý cũ chưa có
}

// Một trường được xếp vào khối
export interface PickedSchool {
  code: string;
  x: number | null; // điểm xếp hạng; khối aid/other có thể null
  why: WhyCode[];
}

export type PickBlockName = "reach" | "match" | "safety" | "aid" | "other";

export interface RecommendationItems {
  scored: ScoredSchool[];
  failed: string[]; // mã trường chấm lỗi
  reach: PickedSchool[];
  match: PickedSchool[];
  safety: PickedSchool[];
  aid: PickedSchool[];
  other: PickedSchool[];
}

// Một nấc của bậc thang mở khóa: nếu đạt IELTS này thì có bấy nhiêu trường đủ điều kiện nộp
export interface EnglishLadderStep {
  ielts: number;
  eligible: number;
}

// Thông báo tiếng Anh của một lượt gợi ý (backend tính)
export interface EnglishNotice {
  hidden: number; // số trường bị ẩn chỉ vì tiếng Anh
  noCertificate: boolean; // chưa có chứng chỉ tiếng Anh nào
  ladder: EnglishLadderStep[];
}

export interface RecommendationCriteria {
  major: string | null;
  states: string[];
  budget: number | null;
  scholarshipNeed: string | null;
  pref: Pref;
  tier: number; // bậc ngoại khóa 1–5
  algorithm?: string; // phiên bản công thức; lần gợi ý cũ chưa có
  status: "OK" | "FAILED" | null; // null khi không còn trường nào để chọn
  english?: EnglishNotice | null; // null khi không có trường nào bị ẩn chỉ vì tiếng Anh; lần gợi ý cũ chưa có
}

export interface Recommendation {
  id: string;
  createdAt: string;
  studyLevel: string;
  algorithmVersion: string | null;
  criteria: RecommendationCriteria;
  items: RecommendationItems;
}

// ---- Trình mô phỏng hồ sơ (POST /recommendations/simulate) ----

export type ScholarshipNeed = "optional" | "preferred" | "mandatory";

// Giá trị giả định gửi lên. Trường nào bỏ trống (null hoặc không gửi) thì giữ nguyên hồ sơ thật.
export interface SimulationOverrides {
  gpa?: number | null;
  sat?: number | null;
  act?: number | null;
  ielts?: number | null;
  toefl?: number | null;
  duolingo?: number | null;
  budgetUsd?: number | null;
  scholarshipNeed?: ScholarshipNeed | null;
  ecTier?: number | null;
  pref?: Pref | null;
}

// Giá trị của hồ sơ thật, để thanh kéo hiện "hồ sơ" cạnh "giả định"
export interface SimulationProfile {
  gpa: number | null;
  sat: number | null;
  act: number | null;
  ielts: number | null;
  toefl?: number | null; // bản backend cũ chưa trả
  duolingo?: number | null;
  budgetUsd: number | null;
  scholarshipNeed: string | null;
  ecTier: number; // bậc tính từ các hoạt động trong hồ sơ
  activityCount?: number; // số hoạt động đã khai
  awardCount?: number; // số giải thưởng đã khai
}

// Số trường mỗi nhóm của một lượt tính
export interface GroupSummary {
  kept: number;
  reach: number;
  match: number;
  safety: number;
  aidBlock: number;
  noData: number;
}

// R khó vào · M vừa sức · S an toàn · NA chưa đủ dữ liệu · AID cần học bổng lớn · OUT bị loại
export type ChangeStatus = "R" | "M" | "S" | "NA" | "AID" | "OUT";

export interface SchoolChange {
  code: string;
  name: string;
  from: ChangeStatus;
  to: ChangeStatus;
}

export interface SimulationResponse {
  profile: SimulationProfile;
  baseline: GroupSummary;
  simulated: GroupSummary;
  changes: SchoolChange[];
  criteria: RecommendationCriteria;
  items: RecommendationItems; // cùng khuôn với lượt gợi ý đã lưu
}

// Một trường trong ô chọn trường của chat (GET /recommendations/schools/names)
export interface SchoolOption {
  code: string;
  name: string;
  state: string | null;
}

// ---- "Cần gì để vào trường này": kết quả goal-seeking (backend gửi trong widget của chat) ----

export type GoalTarget = "reach" | "match" | "safety";
export type GoalFactor = "sat" | "gpa" | "ecTier";
export type GoalDifficulty = "easy" | "hard" | "veryHard";
export type GoalOutcome = "already_reached" | "reachable" | "blocked" | "no_data";
export type GoalBudgetStatus = "ok" | "need_aid" | "over_budget" | "unknown_cost" | "no_budget";

export interface GoalChange {
  factor: GoalFactor;
  from: number | null; // null nếu hồ sơ chưa có
  to: number;
}

export interface GoalPlan {
  changes: GoalChange[];
  effort: number;
  difficulty: GoalDifficulty;
}

export interface GoalBudget {
  status: GoalBudgetStatus;
  costUsd: number | null;
  budgetUsd: number | null;
  minBudgetUsd: number | null; // ngân sách mỗi năm tối thiểu để trường không bị chặn
  shortfallUsd: number | null;
  intlAidAvgUsd: number | null;
}

export interface GoalAlternative {
  code: string;
  name: string;
  state: string | null;
  control: string | null;
  costUsd: number | null;
  group: string;
}

// Mức tiếng Anh tối thiểu của trường (null nếu không công bố bài thi đó); shortfallBand: học sinh còn thiếu bao nhiêu band
export interface GoalEnglish {
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
  shortfallBand: number | null;
}

// Vị trí của trường với hồ sơ thật. status: in_list (có trong danh sách) | excluded (bị bộ lọc cứng loại).
// group: nhóm học thuật R | M | S | NA, tính độc lập với bộ lọc nên trường bị loại vẫn có nhóm "nếu đủ điều kiện".
// excludeReason (khi excluded): MAJOR | STATE | CONTROL | ENGLISH | ENGLISH_NO_CERT | BUDGET. needAid: vượt ngân sách, cần học bổng lớn.
export interface GoalPosition {
  status: "in_list" | "excluded";
  group: string;
  excludeReason: string | null;
  needAid: boolean;
}

// Số liệu tuyển sinh dùng trong các luật chặn (tỉ lệ là 0..1) và các mốc của luật, để lời giải thích nói được con số cụ thể.
// intlRatio = intlAcceptRate / acceptRate; dưới alwaysReachRate luôn Khó vào; dưới safetyMinRate không được An toàn; intlRatio dưới intlRatioCut thì hạ một nhóm.
export interface GoalSchoolStats {
  acceptRate: number | null;
  intlAcceptRate: number | null;
  intlRatio: number | null;
  alwaysReachRate: number;
  safetyMinRate: number;
  intlRatioCut: number;
}

// Kết quả cho MỘT mức đích (nhóm cao hơn nhóm hiện tại): target M | S
export interface GoalTargetResult {
  target: string;
  outcome: "reachable" | "blocked" | "no_data";
  best: string;
  reasons: string[];
  cheapest: GoalPlan | null;
  recommended: GoalPlan | null;
  alone: GoalPlan[];
  noEffect: GoalFactor[];
}

// Current, target, best: R | M | S | NA
export interface SchoolGoalResult {
  code: string;
  name: string;
  current: string;
  target: string;
  outcome: GoalOutcome;
  best: string;
  reasons: string[]; // mã lý do xếp nhóm
  cheapest: GoalPlan | null; // mức tối thiểu ít công sức nhất
  recommended: GoalPlan | null; // mức nên đặt mục tiêu (có đệm)
  alone: GoalPlan[];
  noEffect: GoalFactor[];
  budget: GoalBudget;
  filterReason: "MAJOR" | "STATE" | "CONTROL" | "ENGLISH" | "ENGLISH_NO_CERT" | null; // trường không nằm trong danh sách vì bộ lọc ngoài ngân sách
  english?: GoalEnglish | null; // có khi bị loại vì tiếng Anh
  alternatives: GoalAlternative[];
  position?: GoalPosition | null; // bản backend cũ chưa trả
  stats?: GoalSchoolStats | null; // số liệu tuyển sinh và mốc của các luật chặn
  targets?: GoalTargetResult[] | null; // các nhóm cao hơn nhóm hiện tại: Khó vào có Vừa sức và An toàn, Vừa sức có An toàn, An toàn rỗng
}

// Thẻ giải thích một trường do code dựng (số đã lưu và mã lý do của lần gợi ý gần nhất), không có AI.
// g2: R | RF | M | S | NA | O. blk: MAIN | AID. acc: tỉ lệ nhận chung 0..1. pg, ps: vị trí GPA và SAT của học sinh 0..100.
export interface ExplainCardData {
  name: string;
  state: string | null;
  control: string | null;
  g2: string;
  blk: "MAIN" | "AID";
  why: string[];
  acc: number | null;
  sat25: number | null;
  sat75: number | null;
  pg: number | null;
  ps: number | null;
  cost: number | null;
  gap: number | null;
}

// Gốc của lời giải thích: hồ sơ thật, hoặc giả định học sinh đang xem (label: "GPA 3.6, SAT 1500")
export interface ExplainBasis {
  kind: "real" | "simulated";
  label: string | null;
}

// Lời tư vấn một trường (ChatService gọi ExplainAsync, hiện trong chat)
export interface SchoolExplanation {
  code: string;
  name: string;
  status: "OK" | "FAILED";
  summary: string;
  strengths: string[];
  improvements: string[];
}
