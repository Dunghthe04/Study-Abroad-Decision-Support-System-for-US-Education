"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertCircleIcon, CheckIcon, HomeIcon, Loader2Icon, MessageCircleIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  useAssistant,
  useAssistantBeforeSend,
  useAssistantPageContext,
  useAssistantProfileChanged,
} from "@/components/assistant/AssistantProvider";
import { AssistantLauncher } from "@/components/assistant/AssistantPanel";
import { FLOW_STEPS, ProfileFlowStepper, type NeedsEntry } from "./ProfileFlowStepper";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { FlowChromeProvider } from "./StepChrome";
import { NeedsIntro } from "./NeedsIntro";
import { Step1Goal } from "./Step1Goal";
import { Step2Transcript } from "./Step2Transcript";
import { Step3Certifications } from "./Step3Certifications";
import { Step4Financial } from "./Step4Financial";
import { Step5Extracurricular, getItemProblem } from "./Step5Extracurricular";
import { Step6Review } from "./Step6Review";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { migrateLegacyStep3 } from "@/lib/certification-utils";
import { recommendationApi } from "@/lib/recommendation-api";
import {
  EDIT_ONLY,
  FORM_GOAL_OPTIONS,
  GOALS_CHANGED_EVENT,
  GOALS_KEY,
  goalsNeedMajor,
  goalsQuestion,
  onlyEdit,
  onlySchoolList,
  parseGoals,
  safeBackPath,
  type FormGoal,
} from "@/lib/form-goals";
import type {
  GoalDraft,
  TranscriptDraft,
  CertificationDraft,
  FinancialDraft,
  ActivityDraft,
  DraftTranscriptFile,
  ProfileFlowDraft,
  ProfileMetadataResponse,
  LegacyCertificationDraft,
} from "@/types/profile-flow";

// Không chọn sẵn cấp học / ngân sách / học bổng — học sinh phải tự chọn (BE validate khi commit)
const INITIAL_DRAFT: ProfileFlowDraft = {
  step1: {
    currentEducationLevel: null,
    currentGrade: null,
    targetDegreeLevel: "undergraduate",
    targetStates: [],
    targetSchoolType: "all",
  },
  step2: {
    academicYears: [],
    calculatedGpa4Scale: null,
    manualGpa10Scale: null,
  },
  step3: {
    certifications: [],
    englishPlanned: false,
    standardizedPlanned: false,
  },
  step4: {
    annualBudgetTier: null,
    needFinancialAid: null,
  },
  step5: {
    activities: [],
  },
};

// Nháp của lần nộp gần nhất, để nộp lại y hệt thì không lọc trường và gửi AI lần nữa
const LAST_COMMITTED_KEY = "usas_last_committed_draft";

// Câu gợi ý riêng cho từng bước trong khung trợ lý
const STEP_SUGGESTIONS: Record<number, string> = {
  1: "Chưa biết chọn ngành nào thì sao?",
  2: "Học kỳ chưa có điểm thì để thế nào?",
  3: "Chứng chỉ của em không có trong danh sách?",
  4: "Ngân sách có gồm vé máy bay không?",
  5: "Phạm vi ảnh hưởng chọn thế nào?",
  6: "Hồ sơ của em đủ chưa?",
};

// Mục 7: trang đã đủ ô bắt buộc chưa (giống nút "Tiếp tục" của từng trang) để chat được chuyển form qua.
// Trang 1 không xét ngành: chat tự bắt buộc ngành (hoặc "Chưa rõ"). Trang 2 chỉ cần đã có GPA
function stepReady(step: number, draft: ProfileFlowDraft, goals: FormGoal[]): boolean {
  switch (step) {
    case 1:
      return Boolean(
        draft.step1.currentEducationLevel &&
          draft.step1.targetDegreeLevel &&
          (!goalsNeedMajor(goals) || draft.step1.targetMajor)
      );
    case 2:
      return draft.step2.calculatedGpa4Scale != null || draft.step2.manualGpa10Scale != null;
    case 3: {
      // tiếng Anh và bài thi học thuật: mỗi mục có điểm hoặc tích "chưa thi" (giống nút Tiếp tục của bước 3)
      // Thẻ sửa trực tiếp nên có thể còn thẻ trống (chưa chọn loại): chưa tính là đã điền
      const certs = (draft.step3.certifications ?? []).filter((c) => Boolean(c.type));
      const english = certs.some((c) => c.category === "english") || draft.step3.englishPlanned;
      const tests =
        certs.some((c) => ["standardized", "graduate"].includes(c.category)) || draft.step3.standardizedPlanned;
      return Boolean(english && tests);
    }
    case 4:
      return Boolean(draft.step4.annualBudgetTier && draft.step4.needFinancialAid);
    case 5:
      return draft.step5.activities.every((a) => !getItemProblem(a));
    default:
      return true;
  }
}

// Phải khớp CONTINUE_CHAT trong advisor/app/services/intake.py
const CONTINUE_CHAT = "Tiếp tục bằng chat";
const railAction = "w-full justify-start gap-2 px-3 text-white/80 hover:bg-white/10 hover:text-white";

function scrollToTop() {
  if (typeof window === "undefined") return;
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const behavior = reduced ? "auto" : "smooth";
  window.scrollTo({ top: 0, behavior });
  // Từ 1024px chỉ vùng câu hỏi cuộn
  document.getElementById("flow-scroll")?.scrollTo({ top: 0, behavior });
}

export function ProfileFlowWizard() {
  const router = useRouter();
  const { user } = useAuth();
  const { open: assistantOpen, openPanel: openAssistant } = useAssistant();

  const [currentStep, setCurrentStep] = useState(1);
  // intro: màn chọn nhu cầu lúc mới vào; flow: sidebar + các bước. editingNeeds: sửa nhu cầu giữa chừng
  const [phase, setPhase] = useState<"intro" | "flow">("intro");
  const [editingNeeds, setEditingNeeds] = useState(false);
  const [hasCommittedProfile, setHasCommittedProfile] = useState(false);
  const [completedSteps, setCompletedSteps] = useState<number[]>([]);
  const [draft, setDraft] = useState<ProfileFlowDraft>(INITIAL_DRAFT);
  const [draftTranscript, setDraftTranscript] = useState<DraftTranscriptFile | null>(null);
  const [metadata, setMetadata] = useState<ProfileMetadataResponse | null>(null);
  // UC-94: ngân sách phụ huynh đã nhập (USD), cố định với học sinh
  const [budgetLockedUsd, setBudgetLockedUsd] = useState<number | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [, setSaveStatus] = useState<"saved" | "saving" | "error" | "idle">("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  // Mở form từ chat (?from=chat): bước 6 lưu hồ sơ rồi quay về chat trả lời câu học sinh đã hỏi, không gợi ý trường
  const [fromChat, setFromChat] = useState(false);
  // Mục tiêu ở trang 1. Mở từ chat có ?goal= thì lấy theo câu hỏi trong chat và khóa lại
  const [goals, setGoals] = useState<FormGoal[]>([]);
  const goalsLocked = fromChat && goals.length > 0;
  // "Chỉ sửa hồ sơ": lưu xong quay về trang đã mở form (?back=), không có thì về Tổng quan
  const editOnly = onlyEdit(goals);
  const [backPath, setBackPath] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);
  const goalsRef = useRef<FormGoal[]>([]);
  useEffect(() => {
    goalsRef.current = goals;
  }, [goals]);
  const updateGoals = (next: FormGoal[]) => {
    setGoals(next);
    try {
      // "Chỉ sửa hồ sơ" chỉ dùng cho lần mở này, không giữ lại cho lần mở form sau
      sessionStorage.setItem(GOALS_KEY, next.filter((g) => g !== EDIT_ONLY).join(","));
    } catch {
      // trình duyệt chặn storage → chỉ giữ trong trang
    }
  };

  const saveTimerRef = useRef<NodeJS.Timeout | null>(null);
  const isInitialMount = useRef(true);
  const isUploadingRef = useRef(false);
  const redirecting = useRef(false);
  const completedStepsRef = useRef<number[]>([]);
  useEffect(() => {
    completedStepsRef.current = completedSteps;
  }, [completedSteps]);
  const currentStepRef = useRef(1);
  useEffect(() => {
    currentStepRef.current = currentStep;
  }, [currentStep]);
  // Trang 1 có ô mục tiêu thì phải tích ít nhất 1 (mở từ chat không có goal= thì ẩn ô này)
  const showGoals = !fromChat || goals.length > 0;
  // Bước nào đã đủ mục bắt buộc; bước đầu tiên còn thiếu (6 = đủ hết, mở "Xem lại và gửi")
  const readySteps = [1, 2, 3, 4, 5].filter((s) => stepReady(s, draft, goals) && !(s === 1 && showGoals && goals.length === 0));
  const firstNotReady = [1, 2, 3, 4, 5].find((s) => !readySteps.includes(s)) ?? 6;

  const stepTitle = FLOW_STEPS.find((s) => s.step === currentStep)?.title ?? "";
  const choosingNeeds = phase === "intro" || editingNeeds;
  useAssistantPageContext({
    label: choosingNeeds ? "Nhu cầu của bạn" : `Bước ${currentStep}/6 · ${stepTitle}`,
    suggestions: choosingNeeds
      ? ["Nên chọn nhu cầu nào?"]
      : ["Bước này điền thế nào?", "Kiểm tra bước này giúp mình", STEP_SUGGESTIONS[currentStep]].filter(Boolean),
    studyLevel: draft.step1.targetDegreeLevel ?? null,
  });

  // 1. Tải siêu dữ liệu và trạng thái nháp khi vào trang
  useEffect(() => {
    let isMounted = true;

    async function loadInitialData() {
      try {
        // Đã có kết quả gợi ý thì vào thẳng trang kết quả (khỏi gửi AI lại); "Sửa hồ sơ" mở ?edit=1 để vào form
        // Mở từ chat cũng vào form, không chuyển sang trang kết quả
        const params = new URLSearchParams(window.location.search);
        const openedFromChat = params.get("from") === "chat";
        setFromChat(openedFromChat);
        let savedGoals: string | null = null;
        if (!openedFromChat) {
          try {
            savedGoals = sessionStorage.getItem(GOALS_KEY);
          } catch {
            // bỏ qua
          }
        }
        // "Chỉ sửa hồ sơ" từ chat (sửa hồ sơ, không có việc chờ) thì giữ; ngoài chat chỉ tích khi đã có hồ sơ (bên dưới)
        let startGoals = parseGoals(openedFromChat ? params.get("goal") : savedGoals).filter(
          (g) => openedFromChat || g !== EDIT_ONLY
        );
        setBackPath(openedFromChat ? null : safeBackPath(params.get("back")));
        // Link "Sửa hồ sơ" (Tổng quan, Tài khoản...) gửi ?goal=chi_sua: tích sẵn "Chỉ sửa hồ sơ" khi đã có hồ sơ
        const wantEditOnly = !openedFromChat && parseGoals(params.get("goal")).includes(EDIT_ONLY);
        setGoals(startGoals);
        const editing = params.get("edit") === "1" || openedFromChat;
        // Tải metadata động song song với trạng thái nháp
        const [metaData, stateData, latest] = await Promise.all([
          profileFlowApi.getMetadata().catch(() => null),
          user ? profileFlowApi.getFlowState().catch(() => null) : null,
          user && !editing ? recommendationApi.getLatest().catch(() => null) : null,
        ]);

        if (!isMounted) return;
        if (latest) {
          redirecting.current = true;
          router.replace("/recommendations");
          return;
        }

        if (metaData) setMetadata(metaData);

        if (stateData) {
          setHasCommittedProfile(Boolean(stateData.hasCommittedProfile));
          if (wantEditOnly && stateData.hasCommittedProfile) {
            startGoals = [EDIT_ONLY];
            setGoals(startGoals);
          }
          setCurrentStep(stateData.currentStep || 1);
          setCompletedSteps(stateData.completedSteps ?? []);
          setDraftTranscript(stateData.draftTranscript ?? null);
          setBudgetLockedUsd(stateData.budgetLockedUsd ?? null);
          const incomingDraft = stateData.draft;
          if (incomingDraft) {
            setDraft((prev) => mergeDraft(prev, incomingDraft));
          }
        } else if (typeof window !== "undefined") {
          // Fallback lưu nháp LocalStorage cho khách chưa đăng nhập
          const localDraftStr = localStorage.getItem("usas_flow_draft");
          if (localDraftStr) {
            try {
              const parsed = JSON.parse(localDraftStr);
              if (parsed?.draft) setDraft(mergeDraft(INITIAL_DRAFT, parsed.draft));
              if (parsed?.currentStep) setCurrentStep(parsed.currentStep);
              if (Array.isArray(parsed?.completedSteps)) setCompletedSteps(parsed.completedSteps);
            } catch {
              // ignore
            }
          }
        }
        // Đã có nhu cầu (chat, cùng tab, sửa hồ sơ) thì vào thẳng các bước; không thì hỏi nhu cầu trước
        setPhase(openedFromChat || startGoals.length > 0 ? "flow" : "intro");
      } catch (err: unknown) {
        console.error("Lỗi khi tải dữ liệu ban đầu cho luồng hồ sơ:", err);
      } finally {
        if (isMounted && !redirecting.current) {
          setIsLoading(false);
          isInitialMount.current = false;
        }
      }
    }

    loadInitialData();

    return () => {
      isMounted = false;
    };
  }, [user, router]);

  // Lưu nháp: đã đăng nhập → BE, khách → localStorage
  const persistDraft = useCallback(
    async (step: number, steps: number[], currentDraft: ProfileFlowDraft) => {
      if (user) {
        await profileFlowApi.saveDraft(step, steps, currentDraft);
      } else if (typeof window !== "undefined") {
        localStorage.setItem(
          "usas_flow_draft",
          JSON.stringify({ currentStep: step, completedSteps: steps, draft: currentDraft })
        );
      }
    },
    [user]
  );

  // Lượt tự lưu đang chờ hết 1 giây (null = không có gì chờ lưu)
  const pendingSaveRef = useRef<{ step: number; draft: ProfileFlowDraft } | null>(null);

  // D2: chat vừa ghi vào nháp thì nạp lại form. Đang gõ dở một ô hoặc còn lượt lưu chờ
  // thì đợi (đánh dấu reloadWantedRef), để không xoá chữ đang gõ. Chỉ setDraft, không tự lưu lại
  const reloadWantedRef = useRef(false);
  const formRef = useRef<HTMLDivElement>(null);
  // Chỗ gắn thanh đầu / thanh cuối bước, nằm ngoài vùng cuộn để chỉ phần câu hỏi có thanh cuộn
  const [headerSlot, setHeaderSlot] = useState<HTMLDivElement | null>(null);
  const [navSlot, setNavSlot] = useState<HTMLDivElement | null>(null);
  const reloadFromChat = useCallback(async (_keys?: string[], formStep?: number | null) => {
    if (!user) return;
    // Chỉ xét ô của form; ô gõ chat của trợ lý luôn đang được chọn sau khi gửi
    const active = document.activeElement;
    const typing =
      Boolean(formRef.current?.contains(active)) &&
      (active instanceof HTMLInputElement ||
        active instanceof HTMLTextAreaElement ||
        active instanceof HTMLSelectElement);
    // Đang gõ thì chỉ nạp lại sau, không nhảy trang (lượt chat sau sẽ gửi trang mới)
    if (typing || pendingSaveRef.current) {
      reloadWantedRef.current = true;
      return;
    }
    reloadWantedRef.current = false;
    const state = await profileFlowApi.getFlowState().catch(() => null);
    const incomingDraft = state?.draft;
    if (!incomingDraft) return;
    // Trong lúc chờ mạng học sinh lại gõ thì để lần sau nạp
    if (pendingSaveRef.current) {
      reloadWantedRef.current = true;
      return;
    }
    const nextDraft = mergeDraft(INITIAL_DRAFT, incomingDraft);
    setDraft((prev) => mergeDraft(prev, incomingDraft));
    setDraftTranscript(state.draftTranscript ?? null);

    // Mục 7: chuyển sang trang chat đang hỏi. Lùi thì chuyển luôn; tiến thì dừng ở trang đầu tiên
    // còn thiếu ô bắt buộc. Tích các trang đã đủ rồi lưu như khi bấm "Tiếp tục"
    const from = currentStepRef.current;
    if (formStep == null || formStep === from) return;
    let to = formStep;
    for (let s = from; s < formStep; s++) {
      if (!stepReady(s, nextDraft, goalsRef.current)) {
        to = s;
        break;
      }
    }
    if (to === from) return;
    const ready = [1, 2, 3, 4, 5].filter((s) => s < to && stepReady(s, nextDraft, goalsRef.current));
    const nextCompleted = [...new Set([...completedStepsRef.current, ...ready])].sort((a, b) => a - b);
    setCompletedSteps(nextCompleted);
    setCurrentStep(to);
    setSaveStatus("saving");
    persistDraft(to, nextCompleted, nextDraft)
      .then(() => setSaveStatus("saved"))
      .catch(() => setSaveStatus("error"));
  }, [user, persistDraft]);
  useAssistantProfileChanged(reloadFromChat);

  // Form mở trực tiếp (không khóa mục tiêu) mà chat vừa gửi thêm việc học sinh hỏi: tích theo mục tiêu đã lưu
  useEffect(() => {
    if (goalsLocked) return;
    const onGoals = () => {
      try {
        setGoals(parseGoals(sessionStorage.getItem(GOALS_KEY)));
      } catch {
        // bỏ qua
      }
    };
    window.addEventListener(GOALS_CHANGED_EVENT, onGoals);
    return () => window.removeEventListener(GOALS_CHANGED_EVENT, onGoals);
  }, [goalsLocked]);

  // Rời khỏi ô đang gõ thì nạp phần chat đã đổi (đợi focus chuyển xong mới xét)
  useEffect(() => {
    const onFocusOut = () => {
      setTimeout(() => {
        if (reloadWantedRef.current) void reloadFromChat();
      }, 0);
    };
    document.addEventListener("focusout", onFocusOut);
    return () => document.removeEventListener("focusout", onFocusOut);
  }, [reloadFromChat]);

  const savePending = useCallback(async () => {
    const pending = pendingSaveRef.current;
    if (!pending || isUploadingRef.current) return;
    pendingSaveRef.current = null;
    try {
      await persistDraft(pending.step, completedStepsRef.current, pending.draft);
      setSaveStatus("saved");
      if (reloadWantedRef.current) void reloadFromChat();
    } catch (err: unknown) {
      console.error("Lưu nháp thất bại:", err);
      setSaveStatus("error");
    }
  }, [persistDraft, reloadFromChat]);

  // 2. Tự động lưu nháp (Debounce 1000ms)
  const triggerAutoSave = useCallback(
    (step: number, currentDraft: ProfileFlowDraft) => {
      if (isInitialMount.current) return;

      if (saveTimerRef.current) {
        clearTimeout(saveTimerRef.current);
      }

      setSaveStatus("saving");
      pendingSaveRef.current = { step, draft: currentDraft };
      saveTimerRef.current = setTimeout(savePending, 1000);
    },
    [savePending]
  );

  // D1: gửi tin nhắn chat thì lưu ngay nháp đang chờ, không đợi hết 1 giây
  const flushDraft = useCallback(async () => {
    if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    await savePending();
  }, [savePending]);
  useAssistantBeforeSend(flushDraft);

  // Helper cập nhật từng phần của bản nháp
  const updateStep1 = (val: Partial<GoalDraft>) => {
    const nextDraft: ProfileFlowDraft = {
      ...draft,
      step1: { ...draft.step1, ...val },
    };
    setDraft(nextDraft);
    triggerAutoSave(currentStep, nextDraft);
  };

  const updateStep2 = (val: Partial<TranscriptDraft>) => {
    const nextDraft: ProfileFlowDraft = {
      ...draft,
      step2: { ...draft.step2, ...val },
    };
    setDraft(nextDraft);
    triggerAutoSave(currentStep, nextDraft);
  };

  const updateStep3 = (val: Partial<CertificationDraft>) => {
    const nextDraft: ProfileFlowDraft = {
      ...draft,
      step3: { ...draft.step3, ...val },
    };
    setDraft(nextDraft);
    triggerAutoSave(currentStep, nextDraft);
  };

  const updateStep4 = (val: Partial<FinancialDraft>) => {
    const nextDraft: ProfileFlowDraft = {
      ...draft,
      step4: { ...draft.step4, ...val },
    };
    setDraft(nextDraft);
    triggerAutoSave(currentStep, nextDraft);
  };

  const updateStep5 = (activities: ActivityDraft[]) => {
    const nextDraft: ProfileFlowDraft = {
      ...draft,
      step5: { activities },
    };
    setDraft(nextDraft);
    triggerAutoSave(currentStep, nextDraft);
  };

  // Điều hướng tới bước tiếp theo
  const handleNext = () => {
    setErrorMessage(null);
    const nextStep = Math.min(6, currentStep + 1);
    const nextCompleted = completedSteps.includes(currentStep)
      ? completedSteps
      : [...completedSteps, currentStep].sort((a, b) => a - b);

    setCompletedSteps(nextCompleted);
    setCurrentStep(nextStep);

    // Lưu nháp ngay khi đổi bước; huỷ lượt auto-save đang chờ để nó không ghi đè bước cũ
    if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    pendingSaveRef.current = null;
    setSaveStatus("saving");
    persistDraft(nextStep, nextCompleted, draft)
      .then(() => setSaveStatus("saved"))
      .catch(() => setSaveStatus("error"));

    scrollToTop();
  };

  // Điều hướng về bước trước
  const handlePrev = () => {
    const prevStep = Math.max(1, currentStep - 1);
    setCurrentStep(prevStep);
    scrollToTop();
  };

  // Nhảy tới bước cụ thể từ stepper hoặc nút Sửa
  const handleSelectStep = (stepNumber: number) => {
    // Bước 1–5 nhảy tự do; "Xem lại và gửi" còn khoá thì đưa tới bước đầu tiên còn thiếu
    const to = stepNumber === 6 && firstNotReady < 6 ? firstNotReady : stepNumber;
    setCurrentStep(to);
    scrollToTop();
  };

  // Chọn nhu cầu lần đầu (intro) hoặc sửa giữa chừng. Chỉ đổi nhu cầu, không đụng nháp
  const handleConfirmNeeds = (next: FormGoal[]) => {
    updateGoals(next);
    setEditingNeeds(false);
    setPhase("flow");
    // Nhu cầu mới cần ngành thật mà chưa có ngành: đưa về bước 1 để chọn
    if (goalsNeedMajor(next) && !draft.step1.targetMajor && currentStep > 1) {
      setCurrentStep(1);
    }
  };

  // Đổi bước (hoặc vào/ra màn nhu cầu): đưa focus về h1 của bước để trình đọc màn hình đọc tiêu đề mới
  useEffect(() => {
    if (isLoading || choosingNeeds) return;
    document.querySelector<HTMLElement>("[data-step-heading]")?.focus({ preventScroll: true });
  }, [currentStep, isLoading, choosingNeeds]);

  // Xác nhận commit ở bước 6
  // Chỉ tích "Gợi ý trường" (hoặc chưa tích gì, như form cũ) thì sang trang gợi ý trường;
  // "Chỉ sửa hồ sơ" thì lưu rồi quay về trang cũ; còn lại về chat
  const backToChat = fromChat || (goals.length > 0 && !onlySchoolList(goals) && !editOnly);
  // Nhãn nút gửi nói đúng việc học sinh sẽ nhận: mở từ chat thì quay về chat, còn lại theo nhu cầu đã chọn
  const chatLabel = fromChat
    ? "Lưu và quay về chat"
    : goals.includes("danh_gia") && goals.includes("lo_trinh")
      ? "Lưu và xem đánh giá, lộ trình"
      : goals.includes("danh_gia")
        ? "Lưu và xem đánh giá hồ sơ"
        : goals.includes("lo_trinh")
          ? "Lưu và xem lộ trình"
          : "Lưu và quay về chat";
  const handleSubmitCommit = async () => {
    setErrorMessage(null);
    // Còn bước thiếu mục bắt buộc (vd. quay lại xoá rồi nhảy thẳng tới đây): đưa về bước đó
    if (firstNotReady < 6) {
      setErrorMessage(`Bước ${firstNotReady} còn thiếu mục bắt buộc, bạn điền nốt rồi tiếp tục nhé.`);
      handleSelectStep(firstNotReady);
      return;
    }
    setIsSubmitting(true);

    try {
      if (!user) {
        // Nếu là khách, chuyển hướng tới trang đăng nhập
        if (typeof window !== "undefined") {
          localStorage.setItem(
            "usas_flow_draft",
            JSON.stringify({ currentStep: 6, completedSteps: [1, 2, 3, 4, 5], draft })
          );
        }
        router.push("/login?redirect=/analysis");
        return;
      }

      // Hồ sơ y hệt lần nộp trước (đã có kết quả) thì xem lại kết quả cũ, không lọc và gửi AI lần nữa
      const draftJson = JSON.stringify({ user: user.id, draft });
      let lastCommitted: string | null = null;
      try {
        lastCommitted = localStorage.getItem(LAST_COMMITTED_KEY);
      } catch {
        // trình duyệt chặn storage → coi như hồ sơ mới
      }
      if (!backToChat && !editOnly && lastCommitted === draftJson && (await recommendationApi.getLatest().catch(() => null))) {
        router.push("/recommendations");
        return;
      }

      const res = await profileFlowApi.commitFlow(draft);
      const isSuccessful = Boolean(res.success || res.succeeded);
      if (isSuccessful) {
        try {
          localStorage.removeItem("usas_flow_draft");
          localStorage.setItem(LAST_COMMITTED_KEY, draftJson);
        } catch {
          // bỏ qua
        }
        // Mở từ chat: mở khung chat, advisor thấy hồ sơ đủ thì trả lời luôn câu học sinh đã hỏi
        // (chỉ sửa hồ sơ thì chat báo đã lưu thành công).
        // fromForm: form đã là bước rà soát, advisor bỏ bảng xác nhận đang treo trong chat
        if (fromChat) {
          openAssistant(CONTINUE_CHAT, { fromForm: true });
          return;
        }
        // Chỉ sửa hồ sơ: báo đã lưu rồi quay về trang cũ, không chạy gợi ý, không mở chat
        if (editOnly) {
          setSavedNotice(true);
          window.setTimeout(() => router.push(backPath ?? "/dashboard"), 1200);
          return;
        }
        // Vào form trực tiếp mà tích cả đánh giá hay lộ trình: gửi một câu gộp các việc đã tích, chat trả lời luôn
        if (backToChat) {
          openAssistant(goalsQuestion(goals), { fromForm: true });
          return;
        }
        router.push("/recommendations?run=1");
      } else {
        setErrorMessage(res.message || "Không thể hoàn tất lưu hồ sơ.");
      }
    } catch (err: unknown) {
      setErrorMessage(
        err instanceof Error ? err.message : "Đã có lỗi xảy ra khi xác nhận hồ sơ."
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center space-y-3">
        <Loader2Icon className="size-8 animate-spin text-brand" />
        <p className="text-sm text-muted-foreground">Đang mở hồ sơ…</p>
      </div>
    );
  }

  if (choosingNeeds) {
    return (
      <>
        <NeedsIntro
          key={editingNeeds ? "edit" : goals.join()}
          mode={editingNeeds ? "edit" : "start"}
          initialGoals={goals}
          draftStep={currentStep > 1 || completedSteps.length > 0 ? currentStep : null}
          hasCommittedProfile={hasCommittedProfile}
          onConfirm={handleConfirmNeeds}
          onCancel={() => setEditingNeeds(false)}
        />
        {/* Chọn nhu cầu chưa có thanh bước (nơi có nút chat) nên vẫn hiện nút chat nổi */}
        <AssistantLauncher />
      </>
    );
  }

  // Mục "Nhu cầu" ở thanh bước: mở từ chat thì chỉ đọc; chat không gửi nhu cầu thì ẩn
  const needs: NeedsEntry | null =
    goalsLocked || showGoals
      ? {
          labels: FORM_GOAL_OPTIONS.filter((o) => goals.includes(o.value))
            .map((o) => o.label)
            .join(" · "),
          ...(goalsLocked
            ? { note: goals.includes(EDIT_ONLY) ? "Bạn đang sửa hồ sơ từ chat" : "Theo câu bạn hỏi trong chat" }
            : { onEdit: () => setEditingNeeds(true) }),
        }
      : null;

  const stepperProps = {
    currentStep,
    readySteps,
    onSelectStep: handleSelectStep,
    needs,
    // Nháp tự lưu sau 1 giây; bấm nút thì lưu ngay phần còn chờ rồi về trang chủ
    footer: (
      <div className="flex flex-col gap-1">
        {/* Chuyển sang trả lời trong chat, chat hỏi tiếp các mục còn thiếu */}
        {user && (
          <Button type="button" variant="ghost" className={railAction} onClick={() => openAssistant(CONTINUE_CHAT)}>
            <MessageCircleIcon className="size-4" />
            {CONTINUE_CHAT}
          </Button>
        )}
        <Button
          type="button"
          variant="ghost"
          className={railAction}
          onClick={async () => {
            await savePending();
            router.push("/");
          }}
        >
          <HomeIcon className="size-4" />
          Lưu và về trang chủ
        </Button>
      </div>
    ),
  };

  const notice =
    savedNotice || (errorMessage && currentStep !== 6) ? (
      <>
        {savedNotice && (
          <Alert>
            <CheckIcon className="size-4" />
            <AlertTitle>Đã lưu hồ sơ</AlertTitle>
            <AlertDescription className="text-xs">Đang quay về trang trước...</AlertDescription>
          </Alert>
        )}
        {/* Lỗi khi gửi hồ sơ hiện ở bước 6, cạnh thẻ của từng bước */}
        {errorMessage && currentStep !== 6 && (
          <Alert variant="destructive">
            <AlertCircleIcon className="size-4" />
            <AlertTitle>Không thể lưu hồ sơ</AlertTitle>
            <AlertDescription className="text-xs">{errorMessage}</AlertDescription>
          </Alert>
        )}
      </>
    ) : null;

  // Khung trợ lý đóng: thanh bước bên trái từ 1024px (dưới đó là thanh dính); khung trợ lý mở: luôn là thanh dính
  return (
    <div data-flow-motion className="bg-surface lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:overflow-hidden lg:bg-brand">
      <div className="flex min-h-[calc(100dvh-var(--header-h))] lg:min-h-0 lg:flex-1">
        {!assistantOpen && <ProfileFlowStepper variant="sidebar" {...stepperProps} />}
        <section className={cn("flow-panel flex min-w-0 flex-1 flex-col bg-surface lg:my-3 lg:mr-3 lg:min-h-0 lg:overflow-hidden lg:rounded-2xl", assistantOpen && "lg:ml-3")}>
          <ProfileFlowStepper
            variant="compact"
            {...stepperProps}
            sheetSide={assistantOpen ? "left" : "bottom"}
            className={assistantOpen ? undefined : "lg:hidden"}
          />
          <div ref={setHeaderSlot} />
          <FlowChromeProvider value={{ headerSlot, navSlot, notice, title: stepTitle, compactBar: assistantOpen }}>
          <div id="flow-scroll" className="min-h-0 flex-1 lg:overflow-y-auto">
          <div ref={formRef} key={currentStep}>
            {currentStep === 1 && (
              <Step1Goal
                data={draft.step1}
                metadata={metadata}
                onUpdate={updateStep1}
                onNext={handleNext}
                goals={goals}
                onEditNeeds={goalsLocked ? undefined : () => setEditingNeeds(true)}
              />
            )}

            {currentStep === 2 && (
              <Step2Transcript
                data={draft.step2}
                currentEducationLevel={draft.step1.currentEducationLevel}
                currentGrade={draft.step1.currentGrade}
                draftTranscript={draftTranscript}
                onTranscriptUploaded={setDraftTranscript}
                onUploadingChange={(uploading) => {
                  isUploadingRef.current = uploading;
                }}
                onUpdate={updateStep2}
                onPrev={handlePrev}
                onNext={handleNext}
              />
            )}

            {currentStep === 3 && (
              <Step3Certifications
                data={draft.step3}
                certificationTypes={metadata?.certificationTypes ?? []}
                onUpdate={updateStep3}
                onPrev={handlePrev}
                onNext={handleNext}
              />
            )}

            {currentStep === 4 && (
              <Step4Financial
                data={draft.step4}
                metadata={metadata}
                lockedUsd={budgetLockedUsd}
                onUpdate={updateStep4}
                onPrev={handlePrev}
                onNext={handleNext}
              />
            )}

            {currentStep === 5 && (
              <Step5Extracurricular
                activities={draft.step5.activities}
                onUpdate={updateStep5}
                onPrev={handlePrev}
                onNext={handleNext}
              />
            )}

            {currentStep === 6 && (
              <Step6Review
                draft={draft}
                draftTranscript={draftTranscript}
                metadata={metadata}
                onEditStep={handleSelectStep}
                onPrev={handlePrev}
                onSubmit={handleSubmitCommit}
                backToChat={backToChat}
                editOnly={editOnly}
                chatLabel={chatLabel}
                isSubmitting={isSubmitting || savedNotice}
                submitError={errorMessage}
              />
            )}
          </div>
          </div>
          </FlowChromeProvider>
          <div ref={setNavSlot} className="sticky bottom-0 z-10 lg:static" />
        </section>
      </div>
    </div>
  );
}

// Gộp draft nhận về (BE hoặc localStorage) lên khung mặc định để không thiếu field nào
function mergeDraft(base: ProfileFlowDraft, incoming: Partial<ProfileFlowDraft>): ProfileFlowDraft {
  return {
    step1: { ...base.step1, ...incoming.step1 },
    step2: { ...base.step2, ...incoming.step2 },
    // Nháp cũ (englishTestType / otherTests) → danh sách chứng chỉ
    step3: incoming.step3
      ? migrateLegacyStep3(incoming.step3 as Partial<CertificationDraft> & LegacyCertificationDraft)
      : base.step3,
    step4: { ...base.step4, ...incoming.step4 },
    step5: { activities: incoming.step5?.activities ?? base.step5.activities },
  };
}
