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
  intlAcceptRate?: number | null; // tỷ lệ nhận du học sinh 0–1
  effectiveRate?: number | null; // tỷ lệ dùng cho luật chặn (UC-22): du học sinh nếu có, không thì chung; lần gợi ý cũ chưa có
  rateSource?: "international" | "overall" | null;
  gpaTopShare?: number | null; // tỷ lệ sinh viên trúng tuyển có GPA đúng 4,0 (0–1)
  aidAvg?: number | null; // học bổng trung bình (USD/năm) của người được nhận; không phải mức bạn chắc chắn nhận
  aidPolicy?: string | null; // none | need_only | merit_only | need_and_merit
  sat25?: number | null; // khoảng SAT tổng của sinh viên trúng tuyển (null nếu trường chưa có hoặc dữ liệu sai thang)
  sat75?: number | null;
  fit?: number | null; // điểm phù hợp 0–1, dùng xếp thứ tự trong nhóm; lần gợi ý cũ chưa có
  why?: WhyCode[]; // mã lý do của riêng trường này; lần gợi ý cũ chưa có thì thẻ "Xem thêm" không ghi lý do
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
  studentSat?: number | null; // SAT của học sinh trong lượt này (giả định thì theo giả định); lần gợi ý cũ chưa có
  studentGpa?: number | null; // GPA thang 4
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
  // UC-77: bang (mã hai chữ), ngành (tên tiếng Anh như lưu trong hồ sơ) thay điều kiện lọc; tỉ giá giả định (VNĐ mỗi USD hoặc % tăng, chỉ nhận mức tăng)
  states?: string[] | null;
  major?: string | null;
  exchangeRateVnd?: number | null;
  exchangeRatePercent?: number | null;
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

// ---- [UC-86] Kịch bản what-if đã lưu (/api/v1/scenarios) ----

// Giá trị giả định lúc bấm "Lưu" và bản chụp kết quả lúc đó (backend tự tính, không phải kết quả mới nhất)
export interface SavedScenario {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string | null;
  kind: "what_if"; // UC-85 (độ nhạy) sau này thêm loại khác
  overrides: Omit<SimulationOverrides, "pref">; // backend trả pref riêng ở dưới
  pref: Pref | null;
  baseline: GroupSummary | null;
  simulated: GroupSummary | null;
  changes: SchoolChange[];
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
// effectiveRate là tỉ lệ dùng cho các luật chặn (UC-22): của du học sinh nếu có, không thì chung; rateSource nói đó là số nào.
// Dưới alwaysReachRate luôn Khó vào; dưới safetyMinRate không được An toàn. intlRatio = intlAcceptRate / acceptRate chỉ để tham khảo.
export interface GoalSchoolStats {
  acceptRate: number | null;
  intlAcceptRate: number | null;
  intlRatio: number | null;
  alwaysReachRate: number;
  safetyMinRate: number;
  effectiveRate?: number | null;
  rateSource?: "international" | "overall" | null;
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
  englishRequirement?: GoalEnglish | null; // UC-155: mức tối thiểu của trường kể cả khi chưa bị loại vì tiếng Anh; shortfallBand 0 là đạt
  alternatives: GoalAlternative[];
  position?: GoalPosition | null; // bản backend cũ chưa trả
  stats?: GoalSchoolStats | null; // số liệu tuyển sinh và mốc của các luật chặn
  targets?: GoalTargetResult[] | null; // các nhóm cao hơn nhóm hiện tại: Khó vào có Vừa sức và An toàn, Vừa sức có An toàn, An toàn rỗng
  // Cơ sở đã tính: real (hồ sơ thật) hoặc simulated (giả định học sinh đang xem); bản backend cũ chưa trả thì coi là real
  basis?: "real" | "simulated";
  comparison?: GoalCompareRow[] | null; // bảng "bạn so với trường" về học thuật; bản backend cũ chưa trả
}

// Một dòng "bạn so với trường", mỗi dòng là một tiêu chí bộ chấm nhóm dùng. factor: sat | act | gpa | ec | accept.
// status: sat/act/gpa: below | within | above | unknown (trường chưa công bố) | no_profile (hồ sơ chưa có);
//   ec: below | within | above so với bậc trung bình 3, unknown khi trường không xét ngoại khóa;
//   accept: always_reach (nhận dưới 15%, luôn Khó vào) | capped (nhận dưới 50%, tối đa Vừa sức) | ok | unknown.
// student: số của học sinh (ec: bậc 1 đến 5). low, high: khoảng của trường (GPA chỉ có trung bình nên low = high);
//   riêng accept: low = tỉ lệ nhận chung, high = tỉ lệ nhận du học sinh (0..1).
// gap: âm là thiếu, dương là vượt, 0 là trong khoảng; ec: bậc trừ 3; accept: du học sinh chia chung.
// position: vị trí trong sinh viên của trường 0..1 (sat, act, gpa). importance: mức trường coi trọng tiêu chí (CDS C7).
export interface GoalCompareRow {
  factor: "sat" | "act" | "gpa" | "ec" | "accept";
  status: "below" | "within" | "above" | "unknown" | "no_profile" | "always_reach" | "downgrade" | "capped" | "ok";
  student: number | null;
  low: number | null;
  high: number | null;
  gap: number | null;
  position?: number | null;
  importance?: string | null;
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
  /** Điểm phù hợp và năm thành phần (UC-24). Thẻ của lần gợi ý cũ không có thì không hiện phần này. */
  fit?: FitBreakdown | null;
}

// UC-24: năm thành phần của điểm phù hợp theo thứ tự đặc tả. score 0..1, null là thiếu dữ liệu nên bị bỏ khỏi tổng; weight là trọng số đã dùng cho lần gợi ý đó.
export type FitPartKey = "position" | "budget" | "quality" | "aid" | "english";

export interface FitPart {
  key: FitPartKey;
  score: number | null;
  weight: number;
}

export interface FitBreakdown {
  total: number | null;
  /** Trọng số đã đổi theo nút ưu tiên (UC-25), khác trọng số mặc định. */
  priorityAdjusted: boolean;
  parts: FitPart[];
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

// ---- Thông tin một trường trong chat (widget "school_info"; backend: SchoolInfoDto). Số liệu null = hệ thống chưa có.
export interface SchoolRankingData {
  listName: string;
  group: string | null;
  year: number | null;
  rank: number;
  rankDisplay: string;
  isTie: boolean;
}

export interface SchoolInfoData {
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  control: string | null;
  website: string | null;
  cost: { tuition: number | null; living: number | null; fees: number | null; other: number | null; total: number | null };
  english: { minIelts: number | null; minToefl: number | null; minDuolingo: number | null };
  test: { satPolicy: string | null; sat25: number | null; sat75: number | null; act25: number | null; act75: number | null };
  admission: { acceptRate: number | null; intlAcceptRate: number | null };
  aid: { policy: string | null; avgUsd: number | null; recipients: number | null };
  outcomes: { retentionRate1Yr: number | null; gradRate6Yr: number | null };
  gpa?: { avg: number | null; shareFour: number | null; shareAbove375: number | null } | null;
  deadlines: { round: string; date: string }[];
  majors: string[];
  rankings: SchoolRankingData[];
  sourceUrl: string | null;
  retrievedAt: string | null;
}

export type SchoolInfoField =
  | "cost"
  | "aid"
  | "english"
  | "test"
  | "gpa"
  | "acceptance"
  | "deadline"
  | "outcomes"
  | "majors"
  | "ranking"
  | "location"
  | "website";

// ---- So sánh từ hai trường trong chat (widget "school_compare"; backend: ChatSchoolInfo.WidgetOfCompare).
// Mỗi hàng đã định dạng sẵn ở backend, values cùng thứ tự với schools.
export interface SchoolCompareRow {
  field: string;
  label: string;
  values: string[];
  gap: string | null; // câu chênh lệch chi phí (chỉ hàng chi phí)
}

export interface SchoolCompareData {
  schools: { code: string; name: string; website: string | null }[];
  rows: SchoolCompareRow[];
  aiNote: string | null; // nhận xét do AI viết; null khi AI lỗi hoặc không qua kiểm tra
  verify: boolean; // có chi phí hoặc hạn nộp: nhắc kiểm tra lại trên trang chính thức
  coveredText: string | null;
}

// ---- So sánh theo độ phù hợp với hồ sơ (UC-154; widget "fit_compare", backend: ChatFitCompare.WidgetOf).
// status: in_list | aid | no_data | excluded. group: R | M | S | NA (null khi bị loại). total và score theo thang 0 đến 1; parts đủ năm thành phần kèm trọng số.
export interface FitCompareSchool {
  code: string;
  name: string;
  state: string | null;
  status: "in_list" | "aid" | "no_data" | "excluded";
  group: string | null;
  excludeReason: string | null;
  total: number | null;
  parts: FitPart[];
  why: string[];
}

export interface FitCompareData {
  schools: FitCompareSchool[];
  priorityAdjusted: boolean;
  aiNote?: string | null; // nhận xét do AI viết; vắng khi AI lỗi hoặc không qua kiểm tra
  coveredText?: string | null; // đoạn chữ thẻ đã nói đủ: giao diện bỏ đoạn đó và chỉ hiện thẻ
}

// ---- Tìm trường theo điều kiện trong chat (widget "school_search"; backend: ChatSchoolInfo.WidgetOfSearch).
export interface SchoolSearchItem {
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  control: string | null;
  costUsd: number | null; // tổng chi phí một năm (gồm ăn ở, sinh hoạt)
  tuitionUsd?: number | null; // chỉ học phí
  acceptRate: number | null;
  aidPolicy: string | null;
  aidAvgUsd: number | null;
  quality: number | null;
  bestRank: number | null;
  // Mức trường coi trọng điểm thi (CDS C7): very_important | important | considered | not_considered
  testImportance?: string | null;
}

export interface SchoolSearchData {
  total: number;
  summary: string; // điều kiện đã hiểu, nói lại bằng lời
  sortLabel: string;
  visible: number; // số trường hiện sẵn, phần còn lại mở bằng "Xem thêm"
  items: SchoolSearchItem[];
  // Điều kiện và cách xếp đã dùng (camelCase như bên .NET), để nút "Xem tất cả" mở trang danh sách đúng bộ lọc
  filters?: {
    states?: string[] | null;
    control?: string | null;
    major?: string | null;
    maxCostUsd?: number | null;
    maxIelts?: number | null;
    testPolicy?: string | null;
    ivy?: boolean;
  } | null;
  sort?: string | null;
  coveredText: string | null;
  // Điều kiện đã bỏ vì không đọc ra (vd "top" không có số), để học sinh biết danh sách không lọc theo điều đó
  note?: string | null;
}
