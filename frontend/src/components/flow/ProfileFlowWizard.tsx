"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertCircleIcon, Loader2Icon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { ProfileFlowHeader } from "./ProfileFlowHeader";
import { ProfileFlowStepper } from "./ProfileFlowStepper";
import { Step1Goal } from "./Step1Goal";
import { Step2Transcript } from "./Step2Transcript";
import { Step3Certifications } from "./Step3Certifications";
import { Step4Financial } from "./Step4Financial";
import { Step5Extracurricular } from "./Step5Extracurricular";
import { Step6Review } from "./Step6Review";
import { profileFlowApi } from "@/lib/profile-flow-api";
import type {
  GoalDraft,
  TranscriptDraft,
  CertificationDraft,
  FinancialDraft,
  ActivityDraft,
  DraftTranscriptFile,
  ProfileFlowDraft,
  ProfileMetadataResponse,
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
    englishTestType: "none",
    otherTests: [],
  },
  step4: {
    annualBudgetTier: null,
    needFinancialAid: null,
  },
  step5: {
    activities: [],
  },
};

export function ProfileFlowWizard() {
  const router = useRouter();
  const { user } = useAuth();

  const [currentStep, setCurrentStep] = useState(1);
  const [completedSteps, setCompletedSteps] = useState<number[]>([]);
  const [draft, setDraft] = useState<ProfileFlowDraft>(INITIAL_DRAFT);
  const [draftTranscript, setDraftTranscript] = useState<DraftTranscriptFile | null>(null);
  const [metadata, setMetadata] = useState<ProfileMetadataResponse | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [saveStatus, setSaveStatus] = useState<"saved" | "saving" | "error" | "idle">("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const saveTimerRef = useRef<NodeJS.Timeout | null>(null);
  const isInitialMount = useRef(true);
  const isUploadingRef = useRef(false);
  const completedStepsRef = useRef<number[]>([]);
  useEffect(() => {
    completedStepsRef.current = completedSteps;
  }, [completedSteps]);
  const lastCompletedStep = completedSteps.length ? Math.max(...completedSteps) : 0;

  // 1. Tải siêu dữ liệu và trạng thái nháp khi vào trang
  useEffect(() => {
    let isMounted = true;

    async function loadInitialData() {
      try {
        // Tải metadata động song song với trạng thái nháp
        const [metaData, stateData] = await Promise.all([
          profileFlowApi.getMetadata().catch(() => null),
          user ? profileFlowApi.getFlowState().catch(() => null) : null,
        ]);

        if (!isMounted) return;

        if (metaData) setMetadata(metaData);

        if (stateData) {
          setCurrentStep(stateData.currentStep || 1);
          setCompletedSteps(stateData.completedSteps ?? []);
          setDraftTranscript(stateData.draftTranscript ?? null);
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
      } catch (err: unknown) {
        console.error("Lỗi khi tải dữ liệu ban đầu cho luồng hồ sơ:", err);
      } finally {
        if (isMounted) {
          setIsLoading(false);
          isInitialMount.current = false;
        }
      }
    }

    loadInitialData();

    return () => {
      isMounted = false;
    };
  }, [user]);

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

  // 2. Tự động lưu nháp (Debounce 1000ms)
  const triggerAutoSave = useCallback(
    (step: number, currentDraft: ProfileFlowDraft) => {
      if (isInitialMount.current) return;

      if (saveTimerRef.current) {
        clearTimeout(saveTimerRef.current);
      }

      setSaveStatus("saving");

      saveTimerRef.current = setTimeout(async () => {
        if (isUploadingRef.current) return;
        try {
          await persistDraft(step, completedStepsRef.current, currentDraft);
          setSaveStatus("saved");
        } catch (err: unknown) {
          console.error("Lưu nháp thất bại:", err);
          setSaveStatus("error");
        }
      }, 1000);
    },
    [persistDraft]
  );

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
    const nextStep = Math.min(6, currentStep + 1);
    const nextCompleted = completedSteps.includes(currentStep)
      ? completedSteps
      : [...completedSteps, currentStep].sort((a, b) => a - b);

    setCompletedSteps(nextCompleted);
    setCurrentStep(nextStep);

    // Lưu nháp ngay khi đổi bước; huỷ lượt auto-save đang chờ để nó không ghi đè bước cũ
    if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    setSaveStatus("saving");
    persistDraft(nextStep, nextCompleted, draft)
      .then(() => setSaveStatus("saved"))
      .catch(() => setSaveStatus("error"));

    if (typeof window !== "undefined") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  // Điều hướng về bước trước
  const handlePrev = () => {
    const prevStep = Math.max(1, currentStep - 1);
    setCurrentStep(prevStep);
    if (typeof window !== "undefined") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  // Nhảy tới bước cụ thể từ stepper hoặc nút Sửa
  const handleSelectStep = (stepNumber: number) => {
    setCurrentStep(stepNumber);
    if (typeof window !== "undefined") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  // Thoát khỏi luồng
  const handleExit = () => {
    router.push("/");
  };

  // Xác nhận commit ở bước 6
  const handleSubmitCommit = async () => {
    setIsSubmitting(true);
    setErrorMessage(null);

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

      const res = await profileFlowApi.commitFlow(draft);
      const isSuccessful = Boolean(res.success || res.succeeded);
      if (isSuccessful) {
        if (typeof window !== "undefined") {
          localStorage.removeItem("usas_flow_draft");
        }
        router.push("/recommendations?run=1"); // hồ sơ vừa đổi: lọc lại, không hiện kết quả cũ
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
        <p className="text-sm text-muted-foreground">Đang khôi phục hồ sơ nháp của bạn...</p>
      </div>
    );
  }

  return (
    <div className="-mx-4 -mt-8 min-h-screen bg-ground/40 pb-16">
      {/* Top Wizard Header */}
      <ProfileFlowHeader saveStatus={saveStatus} onExit={handleExit} />

      {/* Main Split-View Layout */}
      <main className="mx-auto max-w-5xl px-4 pt-6 sm:pt-8">
        {errorMessage && (
          <Alert variant="destructive" className="mb-6">
            <AlertCircleIcon className="size-4" />
            <AlertTitle>Không thể lưu hồ sơ</AlertTitle>
            <AlertDescription className="text-xs">{errorMessage}</AlertDescription>
          </Alert>
        )}

        <div className="grid gap-8 lg:grid-cols-12 items-start">
          {/* Left Column: Active Step Form (65% width on desktop) */}
          <div className="lg:col-span-8 rounded-2xl border border-line bg-surface p-6 sm:p-8 shadow-xs">
            {currentStep === 1 && (
              <Step1Goal
                data={draft.step1}
                metadata={metadata}
                onUpdate={updateStep1}
                onNext={handleNext}
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
                onUpdate={updateStep3}
                onPrev={handlePrev}
                onNext={handleNext}
              />
            )}

            {currentStep === 4 && (
              <Step4Financial
                data={draft.step4}
                metadata={metadata}
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
                isSubmitting={isSubmitting}
              />
            )}
          </div>

          {/* Right Column: Stepper Sidebar (35% width on desktop) */}
          <div className="lg:col-span-4">
            <ProfileFlowStepper
              currentStep={currentStep}
              lastCompletedStep={lastCompletedStep}
              onSelectStep={handleSelectStep}
            />
          </div>
        </div>
      </main>
    </div>
  );
}

// Gộp draft nhận về (BE hoặc localStorage) lên khung mặc định để không thiếu field nào
function mergeDraft(base: ProfileFlowDraft, incoming: Partial<ProfileFlowDraft>): ProfileFlowDraft {
  return {
    step1: { ...base.step1, ...incoming.step1 },
    step2: { ...base.step2, ...incoming.step2 },
    step3: { ...base.step3, ...incoming.step3 },
    step4: { ...base.step4, ...incoming.step4 },
    step5: { activities: incoming.step5?.activities ?? base.step5.activities },
  };
}
