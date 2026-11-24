"use client";

// ponytail: temporary floating debugger to preview every AI Chat UI case (panel states + all response widgets). Remove after AI Chat UI polish.

import { useState } from "react";
import { BugIcon, RotateCcwIcon, XIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import type { SubscriptionSummaryDto } from "@/types/billing";
import type { SchoolGoalResult, SchoolSearchData } from "@/types/recommendation";
import { setPeekDismissed } from "@/components/assistant/AssistantPanel";
import {
  useAssistant,
  type AssistantDebugOverrides,
  type AssistantError,
  type AssistantStreaming,
  type AssistantTurn,
  type DebugComposerMode,
} from "@/components/assistant/AssistantProvider";

const MOCK_SUB_BASE: SubscriptionSummaryDto = {
  tier: "premium",
  chargingPool: "personal",
  personal: {
    planCode: "PERSONAL_MONTHLY",
    planName: "Gói Cá nhân",
    startsAt: "2026-11-01T00:00:00Z",
    endsAt: "2026-12-01T00:00:00Z",
    usedPercent: 85,
    exhausted: false,
    lowQuota: true,
    expiringSoon: false,
  },
  family: null,
  free: { usedPercent: 100, resetsAt: "2026-12-01T00:00:00Z" },
  pendingOrderCode: null,
  lowQuota: false,
  expiringSoon: false,
};

const MOCK_GOAL_RESULT: SchoolGoalResult = {
  code: "166027",
  name: "Boston University",
  current: "R",
  target: "M",
  outcome: "reachable",
  best: "M",
  reasons: ["SAT_BELOW_25", "COST_OVER_BUDGET"],
  cheapest: null,
  recommended: null,
  alone: [],
  noEffect: [],
  budget: {
    status: "over_budget",
    costUsd: 86200,
    budgetUsd: 55000,
    minBudgetUsd: 65000,
    shortfallUsd: 31200,
    intlAidAvgUsd: 18000,
  },
  filterReason: "ENGLISH",
  english: { minIelts: 7.0, minToefl: 100, minDuolingo: 130, shortfallBand: 0.5 },
  alternatives: [],
  basis: "real",
  comparison: [
    { factor: "sat", status: "below", student: 1340, low: 1420, high: 1520, gap: -80, position: 0.18, importance: "very_important" },
    { factor: "gpa", status: "above", student: 3.92, low: 3.85, high: 3.85, gap: 0.07, position: 0.72, importance: "very_important" },
    { factor: "ec", status: "within", student: 3, low: 3, high: 3, gap: 0, importance: "important" },
    { factor: "accept", status: "always_reach", student: null, low: 0.11, high: 0.06, gap: 0.55 },
  ],
};

const MOCK_SCHOOL_SEARCH: SchoolSearchData = {
  total: 18,
  summary: "ngành Computer Science ở California có học phí dưới 50.000 USD",
  sortLabel: "tổng chi phí thấp nhất",
  visible: 3,
  sort: "lowestCost",
  coveredText: null,
  note: "Đã bỏ điều kiện 'top' vì chưa có mốc thứ hạng cụ thể.",
  filters: { states: ["CA"], major: "Computer Science", maxCostUsd: 50000, testPolicy: "optional" },
  items: [
    {
      code: "110635",
      name: "San Jose State University",
      city: "San Jose",
      state: "CA",
      control: "public",
      tuitionUsd: 19800,
      costUsd: 38400,
      acceptRate: 0.79,
      bestRank: 165,
      aidPolicy: "limited",
      aidAvgUsd: null,
      quality: 0.9,
      testImportance: "not_considered",
    },
    {
      code: "110583",
      name: "California State University, Long Beach",
      city: "Long Beach",
      state: "CA",
      control: "public",
      tuitionUsd: 18900,
      costUsd: 39100,
      acceptRate: 0.47,
      bestRank: 137,
      aidPolicy: "limited",
      aidAvgUsd: null,
      quality: 0.9,
      testImportance: "very_important",
    },
    {
      code: "110662",
      name: "University of California, Riverside",
      city: "Riverside",
      state: "CA",
      control: "public",
      tuitionUsd: 46300,
      costUsd: 68200,
      acceptRate: 0.68,
      bestRank: 76,
      aidPolicy: "merit",
      aidAvgUsd: 12000,
      quality: 0.9,
      testImportance: "important",
    },
  ],
};

interface ResponsePreset {
  id: string;
  label: string;
  turns: AssistantTurn[];
}

const RESPONSE_PRESETS: ResponsePreset[] = [
  {
    id: "rich-text",
    label: "Rich ChatText",
    turns: [
      { role: "user", content: "I-20 là gì và cần chuẩn bị những gì để xin visa F-1?" },
      {
        role: "assistant",
        content:
          "**Form I-20** (Certificate of Eligibility for Nonimmigrant Student Status) là giấy xác nhận do trường đại học Mỹ được SEVP công nhận cấp sau khi bạn trúng tuyển và chứng minh đủ tài chính.\n\nCác giấy tờ cần chuẩn bị:\n- **Hộ chiếu** còn hạn ít nhất 6 tháng sau ngày dự kiến nhập học.\n- **Thư mời nhập học** và biên lai đóng phí **SEVIS I-901** ($350).\n- **Chứng minh tài chính** khớp với tổng chi phí năm đầu ghi trên I-20.\n\nLưu ý quan trọng:\nBạn có thể phỏng vấn visa F-1 sớm nhất **365 ngày** trước ngày khai giảng, nhưng chỉ được nhập cảnh Mỹ trước tối đa 30 ngày.",
      },
    ],
  },
  {
    id: "citations",
    label: "Citations + Disclaimer",
    turns: [
      { role: "user", content: "Học phí trung bình đại học công lập Mỹ cho sinh viên quốc tế?" },
      {
        role: "assistant",
        content:
          "Học phí đại học công lập Mỹ cho sinh viên quốc tế (mức **out-of-state**) thường dao động từ **25.000 đến 45.000 USD/năm**, chưa tính chi phí ăn ở (khoảng 12.000 – 18.000 USD/năm).",
        citations: [
          { title: "College Board — Trends in College Pricing 2025", url: "https://research.collegeboard.org", snippet: "Average out-of-state tuition at public four-year institutions.", retrievedAt: "2026-10-15T00:00:00Z" },
          { title: "USAS University Tuition Dataset (IPEDS 2025)", url: null, snippet: "Tổng hợp học phí sinh viên quốc tế từ IPEDS.", retrievedAt: "2026-11-01T00:00:00Z" },
        ],
        disclaimer: "Số liệu mang tính tham khảo từ kỳ tuyển sinh gần nhất và có thể thay đổi theo từng trường.",
      },
    ],
  },
  {
    id: "quick-replies",
    label: "Quick Replies",
    turns: [
      { role: "user", content: "Mình muốn cập nhật hồ sơ để gợi ý trường" },
      {
        role: "assistant",
        content: "Bạn muốn điền hồ sơ theo cách nào để hệ thống gợi ý danh sách trường phù hợp nhất?",
        quickReplies: [
          { label: "Trả lời trong chat", value: "Trả lời trong chat" },
          { label: "Điền bằng form", value: "Điền bằng form", href: "/analysis?from=chat" },
          { label: "Xem theo hồ sơ thật", value: "Xem theo hồ sơ thật", action: { type: "explain", schoolCode: "166027", basis: "real" } },
          { label: "Gợi ý trường luôn", value: "Gợi ý trường luôn" },
        ],
      },
    ],
  },
  {
    id: "locked-premium",
    label: "Locked: Premium",
    turns: [
      { role: "user", content: "Phân tích chi tiết cơ hội vào MIT và gợi ý cách nâng hồ sơ giúp mình" },
      {
        role: "assistant",
        content: "Mình có thể giải đáp thông tin chung về MIT (tỷ lệ nhận ~4%, cần SAT/ACT bắt buộc).",
        locked: { reason: "premium_required", resetsAt: null },
      },
    ],
  },
  {
    id: "locked-quota",
    label: "Locked: Quota",
    turns: [
      { role: "user", content: "So sánh giúp mình UT Austin và Purdue ngành CS" },
      {
        role: "assistant",
        content: "",
        locked: { reason: "quota_exhausted", resetsAt: "2026-12-01T00:00:00Z" },
      },
    ],
  },
  {
    id: "goal-seek",
    label: "Widget: GoalCompare",
    turns: [
      { role: "user", content: "Cần gì để vào Boston University ở nhóm Vừa sức?" },
      {
        role: "assistant",
        content:
          "Với hồ sơ hiện tại, **Boston University** đang nằm ở nhóm **Khó vào**.\n\nĐiểm cần cải thiện:\n- Nâng **SAT thêm khoảng 80–100 điểm** (lên mức 1420+).\n- Bổ sung **IELTS 7.0** (hiện thiếu 0.5 band).\n- Chuẩn bị thêm ngân sách hoặc săn học bổng vì chi phí đang vượt trần **31.200 USD/năm**.",
        widget: {
          type: "goal_seek",
          data: { code: "166027", result: MOCK_GOAL_RESULT, basisLabel: "GPA 3.92 · SAT 1340" },
        },
        quickReplies: [
          { label: "Thử nâng SAT lên 1480", value: "Nếu SAT mình lên 1480 thì sao?" },
          { label: "Tìm trường vừa sức hơn", value: "Gợi ý trường tương tự vừa sức hơn" },
        ],
      },
    ],
  },
  {
    id: "goal-seek-sim",
    label: "Widget: GoalCompare (Sim)",
    turns: [
      { role: "user", content: "Với giả định SAT 1480 thì Boston University thế nào?" },
      {
        role: "assistant",
        content: "Với mức giả định **SAT 1480**, điểm thi của bạn đã nằm gọn trong khoảng 25–75% của trường.",
        widget: {
          type: "goal_seek",
          data: {
            code: "166027",
            result: { ...MOCK_GOAL_RESULT, basis: "simulated" },
            basisLabel: "SAT 1480 · GPA 3.92",
          },
        },
      },
    ],
  },
  {
    id: "school-search",
    label: "Widget: SchoolSearch",
    turns: [
      { role: "user", content: "Tìm trường ngành Computer Science ở California chi phí thấp" },
      {
        role: "assistant",
        content: "Dưới đây là các trường tiêu biểu khớp điều kiện bạn vừa hỏi:",
        widget: { type: "school_search", data: MOCK_SCHOOL_SEARCH as unknown as Record<string, unknown> },
      },
    ],
  },
  {
    id: "centers",
    label: "Widget: CenterCards",
    turns: [
      { role: "user", content: "Giới thiệu trung tâm tư vấn du học Mỹ uy tín ở Hà Nội" },
      {
        role: "assistant",
        content: "Mình tìm thấy 4 trung tâm đã xác minh tại Hà Nội hỗ trợ hồ sơ đại học Mỹ:\n- A\n- B",
        widget: {
          type: "centers",
          data: {
            firstPage: 3,
            centers: [
              { id: "c1", name: "VietAbroad Consulting", city: "Hà Nội", website: "https://vietabroad.example.com", reason: "Chuyên săn học bổng đại học Mỹ nhóm Top 50 và sửa bài luận." },
              { id: "c2", name: "AMVNX Education", city: "Hà Nội", website: "https://amvnx.example.com", reason: "Có gói hỗ trợ chọn trường, luyện phỏng vấn visa F-1 trọn gói." },
              { id: "c3", name: "Capstone Vietnam", city: "Hà Nội", website: null, reason: "Đối tác nhiều trường công lập và cao đẳng cộng đồng Mỹ." },
              { id: "c4", name: "Summit Study Abroad", city: "Hà Nội", website: "https://summit.example.com", reason: "Mạnh về luyện SAT/IELTS kết hợp cố vấn chiến lược nộp hồ sơ." },
            ],
            cities: [
              { city: "TP. Hồ Chí Minh", count: 6, ask: "Tìm trung tâm tư vấn du học Mỹ ở TP. Hồ Chí Minh" },
              { city: "Đà Nẵng", count: 2, ask: "Tìm trung tâm tư vấn du học Mỹ ở Đà Nẵng" },
            ],
          },
        },
      },
    ],
  },
  {
    id: "review",
    label: "Widget: Review + Current",
    turns: [
      { role: "user", content: "Kiểm tra lại hồ sơ hiện tại của mình" },
      {
        role: "assistant",
        content: "Đây là tóm tắt hồ sơ bạn đã cung cấp. Một vài mục bắt buộc vẫn còn thiếu — bạn kiểm tra rồi xác nhận nhé.",
        widget: {
          type: "review",
          data: {
            current: {
              major: { value: "Computer Science", status: "confirmed" },
              budget: { value: "30k_50k", status: "confirmed" },
            },
            profile: {
              current_level: { value: "high_school", status: "confirmed" },
              current_grade: { value: "Lớp 11", status: "confirmed" },
              target_level: { value: "undergraduate", status: "confirmed" },
              major: { value: "Computer Science", status: "confirmed" },
              gpa: { value: "8.8 / 10", status: "confirmed" },
              certifications: {
                value: [
                  { type: "ielts", overall: "7.0", testDate: "2026-03" },
                  { type: "sat", overall: "1450", testDate: "2026-05" },
                ],
                status: "confirmed",
              },
              budget: { value: "30k_50k", status: "confirmed" },
              scholarship: { value: "preferred", status: "confirmed" },
              activities: { value: [{ name: "CLB Robotics", role: "president", impactLevel: "school" }], status: "confirmed" },
              awards: { status: "none" },
              visa_refused: { value: false, status: "confirmed" },
            },
            missing: ["intake", "preferred_states"],
          },
        },
        quickReplies: [
          { label: "Lưu hồ sơ & gợi ý trường", value: "Xác nhận lưu hồ sơ" },
          { label: "Sửa lại thông tin", value: "Mình muốn sửa thông tin" },
        ],
      },
    ],
  },
  {
    id: "select",
    label: "Widget: Select + GPA/Upload",
    turns: [
      { role: "user", content: "Nhập GPA giúp mình" },
      {
        role: "assistant",
        content: "Bạn chọn thang điểm và nhập GPA hiện tại, hoặc tải ảnh học bạ lên để hệ thống tự đọc nhé.",
        widget: {
          type: "select",
          data: {
            upload: "transcript",
            format: "GPA {gpa} (thang {scale})",
            fields: [
              {
                name: "scale",
                label: "Thang điểm",
                kind: "select",
                options: [
                  { value: "10", label: "Thang 10" },
                  { value: "4", label: "Thang 4.0" },
                ],
              },
              { name: "gpa", label: "Điểm GPA", kind: "number", placeholder: "VD: 8.6", step: 0.1 },
            ],
          },
        },
      },
    ],
  },
  {
    id: "major",
    label: "Widget: Major",
    turns: [
      { role: "user", content: "Mình chưa chọn ngành học" },
      {
        role: "assistant",
        content: "Bạn đang quan tâm đến ngành học nào tại Mỹ?",
        widget: { type: "major", data: {} },
      },
    ],
  },
  {
    id: "school-picker",
    label: "Widget: School Picker",
    turns: [
      { role: "user", content: "Giải thích cho mình một trường trong danh sách" },
      {
        role: "assistant",
        content: "Bạn chọn trường muốn mình giải thích chi tiết nhé:",
        widget: { type: "school", data: { kind: "explain" } },
      },
    ],
  },
  {
    id: "budget-tier",
    label: "Widget: Budget (Tier)",
    turns: [
      { role: "user", content: "Ngân sách du học tính thế nào?" },
      {
        role: "assistant",
        content: "Gia đình bạn dự kiến chi trả tối đa khoảng bao nhiêu mỗi năm cho du học Mỹ?",
        widget: { type: "budget", data: { rate: 25500 } },
      },
    ],
  },
  {
    id: "budget-exact",
    label: "Widget: Budget (Exact)",
    turns: [
      { role: "user", content: "Phụ huynh muốn nhập mức tối đa chính xác" },
      {
        role: "assistant",
        content: "Vui lòng nhập số tiền tối đa gia đình có thể chi trả mỗi năm (VNĐ):",
        widget: { type: "budget", data: { exact: true, rate: 25500, initial: { amountVnd: 1_200_000_000 } } },
      },
    ],
  },
  {
    id: "scholarship",
    label: "Widget: Scholarship",
    turns: [
      { role: "user", content: "Mức độ cần học bổng" },
      {
        role: "assistant",
        content: "Học bổng quan trọng ở mức nào với kế hoạch du học của bạn?",
        widget: { type: "scholarship", data: { initial: { need: "preferred" } } },
      },
    ],
  },
  {
    id: "ctas",
    label: "CTAs: Form / Rec / Sim",
    turns: [
      { role: "user", content: "Cho mình mở form hồ sơ" },
      {
        role: "assistant",
        content: "Bạn có thể mở form để điền đầy đủ thông tin, sau đó quay lại đây xem kết quả ngay.",
        widget: { type: "form", data: { goals: ["danh_gia", "goi_y_truong"] } },
      },
      { role: "user", content: "Gợi ý trường xong rồi thì xem ở đâu?" },
      {
        role: "assistant",
        content: "Danh sách trường phù hợp với hồ sơ của bạn đã sẵn sàng.",
        widget: { type: "recommend", data: {} },
      },
      { role: "user", content: "Nếu mình nâng IELTS lên 7.5 và SAT lên 1500 thì sao?" },
      {
        role: "assistant",
        content: "Với giả định **SAT 1500 · IELTS 7.5**, có 4 trường tăng lên nhóm **Vừa sức**.",
        widget: { type: "simulate", data: { sat: 1500, ielts: 7.5 } },
      },
    ],
  },
];

const ALL_KITCHEN_SINK: AssistantTurn[] = RESPONSE_PRESETS.flatMap((p) => p.turns);

type BannerMode = "none" | "real" | "sim";
type QuotaMode = "none" | "lowQuota" | "expiringSoon" | "freePool";
type HistoryMode = "none" | "opening" | "hasOlder" | "loadingOlder" | "start";
type LoadingMode = "none" | "typing" | "progress" | "streaming" | "streamingCard" | "slowRec";
type ErrorMode = "none" | "auth" | "unavailable" | "other";

export function AssistantDebugDrawer() {
  const { open, wide, openPanel, closePanel, toggleWide, debugOverrides, setDebugOverrides } = useAssistant();
  const [expanded, setExpanded] = useState(false);
  const [appendMode, setAppendMode] = useState(false);

  const [banner, setBanner] = useState<BannerMode>("none");
  const [quota, setQuota] = useState<QuotaMode>("none");
  const [history, setHistory] = useState<HistoryMode>("none");
  const [loadingMode, setLoadingMode] = useState<LoadingMode>("none");
  const [errorMode, setErrorMode] = useState<ErrorMode>("none");
  const [composer, setComposer] = useState<DebugComposerMode>("auto");

  const activeCount = [
    debugOverrides?.turns !== undefined,
    banner !== "none",
    quota !== "none",
    history !== "none",
    loadingMode !== "none",
    errorMode !== "none",
    composer !== "auto",
  ].filter(Boolean).length;

  const patch = (next: Partial<AssistantDebugOverrides>) => {
    setDebugOverrides((prev) => ({ ...(prev ?? {}), ...next }));
  };

  const applyBanner = (mode: BannerMode) => {
    setBanner(mode);
    if (!open) openPanel();
    if (mode === "none") patch({ page: { simulation: undefined } });
    else if (mode === "real") patch({ page: { simulation: null } });
    else patch({ page: { simulation: { gpa: 3.85, sat: 1480, ielts: 7.5, budgetUsd: 45000 } } });
  };

  const applyQuota = (mode: QuotaMode) => {
    setQuota(mode);
    if (!open) openPanel();
    if (mode === "none") patch({ subscription: undefined });
    else if (mode === "lowQuota") patch({ subscription: { ...MOCK_SUB_BASE, lowQuota: true, expiringSoon: false } });
    else if (mode === "expiringSoon") patch({ subscription: { ...MOCK_SUB_BASE, lowQuota: false, expiringSoon: true } });
    else patch({ subscription: { ...MOCK_SUB_BASE, tier: "free", chargingPool: "free", lowQuota: false, expiringSoon: false } });
  };

  const applyHistory = (mode: HistoryMode) => {
    setHistory(mode);
    if (!open) openPanel();
    if (mode === "none") patch({ openingSession: false, resumed: false, hasOlder: false, loadingOlder: false });
    else if (mode === "opening") patch({ openingSession: true, resumed: false, hasOlder: false, loadingOlder: false });
    else if (mode === "hasOlder") patch({ openingSession: false, resumed: true, hasOlder: true, loadingOlder: false });
    else if (mode === "loadingOlder") patch({ openingSession: false, resumed: true, hasOlder: true, loadingOlder: true });
    else patch({ openingSession: false, resumed: true, hasOlder: false, loadingOlder: false });
  };

  const applyLoading = (mode: LoadingMode) => {
    setLoadingMode(mode);
    if (!open) openPanel();
    const map: Record<LoadingMode, { loading: boolean; streaming: AssistantStreaming | null; turns?: AssistantTurn[] }> = {
      none: { loading: false, streaming: null },
      typing: { loading: true, streaming: null },
      progress: { loading: true, streaming: { answers: 3, done: 1, text: "1. **I-20** là giấy xác nhận nhập học từ trường Mỹ..." } },
      streaming: {
        loading: true,
        streaming: {
          answers: 2,
          done: 1,
          text: "Đang tổng hợp câu trả lời theo từng ý:\n- **Học phí**: khoảng 35.000 USD/năm.\n- **Sinh hoạt phí**: khoảng 15.000 USD/năm.",
        },
      },
      streamingCard: {
        loading: true,
        streaming: {
          answers: 1,
          done: 0,
          text: "Đang viết lời giải thích chi tiết cho bảng so sánh ở trên...",
          widget: { type: "goal_seek", data: { code: "166027", result: MOCK_GOAL_RESULT } },
        },
      },
      slowRec: {
        loading: true,
        streaming: null,
        turns: [
          { role: "assistant", content: "Mình sẽ chạy đề xuất lộ trình và danh sách trường cho hồ sơ này nhé?" },
          { role: "user", content: "Đồng ý, chạy gợi ý trường luôn đi" },
        ],
      },
    };
    const cfg = map[mode];
    patch({ loading: cfg.loading, streaming: cfg.streaming, ...(cfg.turns ? { turns: cfg.turns } : {}) });
  };

  const applyError = (mode: ErrorMode) => {
    setErrorMode(mode);
    if (!open) openPanel();
    const map: Record<ErrorMode, AssistantError | null> = {
      none: null,
      auth: { kind: "auth", message: "Bạn cần đăng nhập để hỏi trợ lý." },
      unavailable: { kind: "unavailable", message: "Trợ lý đang tạm ngưng, bạn thử lại sau." },
      other: { kind: "other", message: "Bạn đang có một câu hỏi đang xử lý, vui lòng đợi xong rồi hỏi tiếp." },
    };
    patch({ error: map[mode] });
  };

  const applyComposer = (mode: DebugComposerMode) => {
    setComposer(mode);
    if (!open) openPanel();
    patch({ composerMode: mode });
  };

  const injectTurns = (nextTurns: AssistantTurn[]) => {
    if (!open) openPanel();
    setDebugOverrides((prev) => {
      const base = prev ?? {};
      const turns = appendMode && base.turns ? [...base.turns, ...nextTurns] : nextTurns;
      return { ...base, turns, composerMode: base.composerMode ?? "normal" };
    });
    if (composer === "auto") setComposer("normal");
  };

  const resetAll = () => {
    setBanner("none");
    setQuota("none");
    setHistory("none");
    setLoadingMode("none");
    setErrorMode("none");
    setComposer("auto");
    setDebugOverrides(null);
  };

  if (!expanded) {
    return (
      <button
        type="button"
        onClick={() => setExpanded(true)}
        className="fixed bottom-4 left-4 z-[60] inline-flex cursor-pointer items-center gap-1.5 rounded-full border border-amber-500/50 bg-amber-950/90 px-3 py-1.5 text-xs font-semibold text-amber-100 shadow-lg backdrop-blur transition-transform hover:scale-105 print:hidden"
        title="Mở bộ debug UI cho AI Chat (tạm thời)"
      >
        <BugIcon className="size-3.5 text-amber-300" />
        Chat UI Debug
        {activeCount > 0 && (
          <span className="rounded-full bg-amber-400 px-1.5 py-0.2 text-[10px] font-bold text-amber-950">
            {activeCount}
          </span>
        )}
      </button>
    );
  }

  return (
    <div className="fixed bottom-4 left-4 z-[60] flex max-h-[82vh] w-[350px] max-w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-2xl border border-line bg-surface text-xs text-ink shadow-2xl print:hidden">
      {/* Header */}
      <div className="flex items-center justify-between gap-2 border-b border-line bg-ground px-3 py-2">
        <div className="flex items-center gap-1.5 font-semibold">
          <BugIcon className="size-3.5 text-amber-600" />
          <span>AI Chat UI Debugger</span>
          <span className="rounded bg-amber-500/15 px-1.5 py-0.5 text-[10px] font-medium text-amber-700 dark:text-amber-300">
            TEMP
          </span>
        </div>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={resetAll}
            title="Reset về dữ liệu thật"
            className="inline-flex cursor-pointer items-center gap-1 rounded-md border border-line bg-surface px-2 py-1 text-[11px] font-medium hover:bg-ground"
          >
            <RotateCcwIcon className="size-3" />
            Live
          </button>
          <button
            type="button"
            onClick={() => setExpanded(false)}
            aria-label="Thu gọn debugger"
            className="cursor-pointer rounded-md p-1 text-ink-2 hover:bg-surface hover:text-ink"
          >
            <XIcon className="size-4" />
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="space-y-3 overflow-y-auto p-3">
        {/* Panel & Launcher controls */}
        <Section title="Panel & Launcher">
          <div className="flex flex-wrap gap-1.5">
            <Chip active={open} onClick={() => (open ? closePanel() : openPanel())}>
              {open ? "Close Panel" : "Open Panel"}
            </Chip>
            <Chip active={wide} onClick={toggleWide}>
              {wide ? "Wide ✓" : "Narrow"}
            </Chip>
            <Chip
              onClick={() => {
                closePanel();
                setPeekDismissed(false);
              }}
            >
              Launcher: Peek Card
            </Chip>
            <Chip
              onClick={() => {
                closePanel();
                setPeekDismissed(true);
              }}
            >
              Launcher: FAB
            </Chip>
          </div>
        </Section>

        {/* Panel Chrome States */}
        <Section title="Top Banner (Simulation)">
          <div className="flex flex-wrap gap-1">
            {(["none", "real", "sim"] as BannerMode[]).map((m) => (
              <Chip key={m} active={banner === m} onClick={() => applyBanner(m)}>
                {m === "none" ? "None" : m === "real" ? "Hồ sơ thật" : "Giả định"}
              </Chip>
            ))}
          </div>
        </Section>

        <Section title="Quota / Plan Banner">
          <div className="flex flex-wrap gap-1">
            {(
              [
                ["none", "None"],
                ["lowQuota", ">80% Quota"],
                ["expiringSoon", "Expiring"],
                ["freePool", "Free Pool"],
              ] as [QuotaMode, string][]
            ).map(([m, lbl]) => (
              <Chip key={m} active={quota === m} onClick={() => applyQuota(m)}>
                {lbl}
              </Chip>
            ))}
          </div>
        </Section>

        <Section title="History / Resumed State">
          <div className="flex flex-wrap gap-1">
            {(
              [
                ["none", "None"],
                ["opening", "Opening..."],
                ["hasOlder", "Has Older"],
                ["loadingOlder", "Loading Older"],
                ["start", "Đầu phiên"],
              ] as [HistoryMode, string][]
            ).map(([m, lbl]) => (
              <Chip key={m} active={history === m} onClick={() => applyHistory(m)}>
                {lbl}
              </Chip>
            ))}
          </div>
        </Section>

        <Section title="Loading & Streaming">
          <div className="flex flex-wrap gap-1">
            {(
              [
                ["none", "None"],
                ["typing", "Typing"],
                ["progress", "Progress 1/3"],
                ["streaming", "Stream Text"],
                ["streamingCard", "Stream + Card"],
                ["slowRec", "Slow Rec (8s)"],
              ] as [LoadingMode, string][]
            ).map(([m, lbl]) => (
              <Chip key={m} active={loadingMode === m} onClick={() => applyLoading(m)}>
                {lbl}
              </Chip>
            ))}
          </div>
        </Section>

        <Section title="Error Banner">
          <div className="flex flex-wrap gap-1">
            {(
              [
                ["none", "None"],
                ["auth", "401 Auth"],
                ["unavailable", "503 Down"],
                ["other", "429 Busy"],
              ] as [ErrorMode, string][]
            ).map(([m, lbl]) => (
              <Chip key={m} active={errorMode === m} onClick={() => applyError(m)}>
                {lbl}
              </Chip>
            ))}
          </div>
        </Section>

        <Section title="Composer State">
          <div className="flex flex-wrap gap-1">
            {(
              [
                ["auto", "Auto"],
                ["normal", "Force Active"],
                ["needLogin", "Need Login"],
                ["ageBlock", "Age <16 Block"],
              ] as [DebugComposerMode, string][]
            ).map(([m, lbl]) => (
              <Chip key={m} active={composer === m} onClick={() => applyComposer(m)}>
                {lbl}
              </Chip>
            ))}
          </div>
        </Section>

        {/* Chat Responses */}
        <Section
          title="Chat Response Cases"
          right={
            <label className="inline-flex cursor-pointer items-center gap-1 text-[11px] text-ink-2">
              <input
                type="checkbox"
                checked={appendMode}
                onChange={(e) => setAppendMode(e.target.checked)}
                className="accent-brand"
              />
              Append
            </label>
          }
        >
          <div className="mb-1.5 flex flex-wrap gap-1">
            <Chip
              accent
              onClick={() => {
                setAppendMode(false);
                if (!open) openPanel();
                setDebugOverrides((prev) => ({
                  ...(prev ?? {}),
                  turns: ALL_KITCHEN_SINK,
                  loading: false,
                  streaming: null,
                  error: null,
                  composerMode: prev?.composerMode ?? "normal",
                }));
                if (composer === "auto") setComposer("normal");
              }}
            >
              ★ All Cases (Kitchen Sink)
            </Chip>
            <Chip
              onClick={() => {
                if (!open) openPanel();
                patch({ turns: [], loading: false, streaming: null, error: null, openingSession: false, resumed: false });
              }}
            >
              Empty State
            </Chip>
          </div>
          <div className="flex flex-wrap gap-1">
            {RESPONSE_PRESETS.map((p) => (
              <Chip key={p.id} onClick={() => injectTurns(p.turns)}>
                {p.label}
              </Chip>
            ))}
          </div>
        </Section>
      </div>
    </div>
  );
}

function Section({ title, right, children }: { title: string; right?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <p className="text-[11px] font-semibold tracking-wide text-ink-3 uppercase">{title}</p>
        {right}
      </div>
      {children}
    </div>
  );
}

function Chip({
  active,
  accent,
  onClick,
  children,
}: {
  active?: boolean;
  accent?: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "cursor-pointer rounded-md border px-2 py-1 text-[11px] font-medium transition-colors",
        active
          ? "border-brand bg-brand text-white"
          : accent
            ? "border-brand/40 bg-brand-wash text-brand hover:bg-brand hover:text-white"
            : "border-line bg-surface text-ink hover:border-brand/40 hover:bg-brand-wash",
      )}
    >
      {children}
    </button>
  );
}
