"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlertCircleIcon, RefreshCwIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAssistant, useAssistantPageContext } from "@/components/assistant/AssistantProvider";
import { recommendationApi } from "@/lib/recommendation-api";
import { whyText } from "@/lib/recommendation-labels";
import { RESET_SIMULATION_EVENT, SIMULATE_EVENT, describeSimulation } from "@/lib/simulation-link";
import { useProfileSimulation, type ProfileSimulation } from "@/lib/use-profile-simulation";
import type {
  PickBlockName,
  Pref,
  Recommendation,
  SavedScenario,
  SchoolChange,
  ScoredSchool,
  SimulationOverrides,
} from "@/types/recommendation";
import { ProfileSimulator } from "./ProfileSimulator";
import { ProfileSummary } from "./ProfileSummary";
import { EnglishNoticeBanner } from "./EnglishNoticeBanner";
import { SchoolCard } from "./SchoolCard";
import { SaveScenarioButton, SavedScenariosButton, scenarioOverrides, useSavedScenarios } from "./SavedScenarios";

// Bốn khối chính theo đúng thứ tự trên màn. "other" để riêng ở cuối.
const BLOCKS: { key: PickBlockName; title: string; note: string }[] = [
  { key: "reach", title: "Khó vào", note: "Tối đa 4 trường" },
  { key: "match", title: "Vừa sức", note: "Tối đa 4 trường" },
  { key: "safety", title: "An toàn hơn", note: "Tối đa 4 trường" },
  { key: "aid", title: "Chỉ đi được nếu có học bổng lớn", note: "Xếp theo điểm phù hợp" },
];

const RESULT_SUGGESTIONS = [
  "Hồ sơ của em mạnh ở đâu?",
  "Nên nộp bao nhiêu trường mỗi nhóm?",
  "Làm sao để lên nhóm Vừa sức?",
];

const BALANCED_PREF: Pref = [2, 2, 2];

interface RecommendationViewProps {
  autoRun: boolean; // true khi vừa gửi hồ sơ xong: chạy gợi ý mới thay vì đọc lần cũ
  initialOverrides?: SimulationOverrides; // giá trị giả định chat vừa thử, điền sẵn vào thanh kéo
}

export function RecommendationView({ autoRun, initialOverrides }: RecommendationViewProps) {
  const router = useRouter();
  const [rec, setRec] = useState<Recommendation | null>(null);
  // Vừa gửi hồ sơ thì bắt đầu ở trạng thái "running", còn lại là "loading" (đọc lần đã lưu)
  const [phase, setPhase] = useState<"loading" | "running" | "ready">(autoRun ? "running" : "loading");
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false); // dev mode chạy useEffect 2 lần; không được tạo 2 lần gợi ý
  const { openPanel, sendAction } = useAssistant();
  // Nút trên thẻ trường gửi câu hỏi vào khung chat kèm mã trường (không qua SLM): chat trả thẻ kết quả rồi lời tư vấn
  const nameOf = (code: string) => rec?.items.scored.find((s) => s.code === code)?.name ?? code;
  const askGoal = (code: string) => sendAction(`Cần gì để vào ${nameOf(code)}?`, { type: "goal_seek", schoolCode: code });
  const askExplain = (code: string) => sendAction(`Vì sao ${nameOf(code)} xếp ở nhóm này?`, { type: "explain", schoolCode: code });

  // Trình mô phỏng chỉ chạy khi đã có lần gợi ý (cần hồ sơ đã gửi)
  const basePref = rec?.criteria.pref ?? BALANCED_PREF;
  const sim = useProfileSimulation(phase === "ready" && rec !== null, basePref, initialOverrides);

  // Đang kéo thanh thì danh sách hiện kết quả mô phỏng, không kéo thì hiện lần gợi ý đã lưu
  const view = useMemo<Recommendation | null>(() => {
    if (!rec) return null;
    return sim.isSimulating && sim.result ? { ...rec, criteria: sim.result.criteria, items: sim.result.items } : rec;
  }, [rec, sim.isSimulating, sim.result]);
  const simulating = sim.isSimulating && sim.result !== null;

  // [UC-86] Kịch bản đã lưu. Lưu kèm ưu tiên đang dùng (kể cả khi chưa kéo thanh ưu tiên),
  // để bản chụp backend tính khớp với danh sách đang xem.
  const scenarios = useSavedScenarios(rec !== null);
  const saveScenario = (title: string) =>
    scenarios.save(title, { ...sim.overrides, pref: sim.overrides.pref ?? basePref });
  // Mở lại: đặt thanh kéo theo kịch bản. Ưu tiên trùng ưu tiên hiện tại thì bỏ, để dải giả định không hiện thừa.
  const openScenario = (s: SavedScenario) => {
    const next = scenarioOverrides(s);
    if (next.pref && next.pref.every((n, i) => n === basePref[i])) delete next.pref;
    sim.replace(next);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  // Ngữ cảnh trợ lý bên phải: lời chào theo số trường, chip hỏi về trường Khó vào đầu tiên
  const firstReach = view ? view.items.scored.find((s) => s.code === view.items.reach[0]?.code) : undefined;
  useAssistantPageContext({
    label: "Trang đề xuất",
    suggestions: firstReach ? [`Vì sao ${firstReach.name} Khó vào?`, ...RESULT_SUGGESTIONS] : RESULT_SUGGESTIONS,
    greeting: rec ? `Mình là hệ thống tư vấn du học. Bạn cần mình hỗ trợ gì không?:` : undefined,
    // Đang kéo thanh giả định thì chat giải thích theo giả định đó (khớp danh sách đang xem)
    simulation: simulating ? sim.overrides : null,
  });

  // Chat vừa thử một kịch bản what-if: đặt lại thanh kéo tại chỗ, không tải lại trang
  const { replace: replaceSimulation, resetAll: resetSimulation } = sim;
  useEffect(() => {
    const onSimulate = (event: Event) => replaceSimulation((event as CustomEvent<SimulationOverrides>).detail);
    const onReset = () => resetSimulation();
    window.addEventListener(SIMULATE_EVENT, onSimulate);
    window.addEventListener(RESET_SIMULATION_EVENT, onReset);
    return () => {
      window.removeEventListener(SIMULATE_EVENT, onSimulate);
      window.removeEventListener(RESET_SIMULATION_EVENT, onReset);
    };
  }, [replaceSimulation, resetSimulation]);

  // Có kết quả thì mở trợ lý bên phải một lần như bản demo (màn hẹp thì khung phủ kín nên không tự mở)
  const autoOpened = useRef(false);
  useEffect(() => {
    if (!rec || autoOpened.current) return;
    autoOpened.current = true;
    if (window.matchMedia("(min-width: 900px)").matches) openPanel();
  }, [rec, openPanel]);

  // Chạy cả quy trình (lọc cứng, đọc ngoại khóa, chấm nhóm bằng code, lưu)
  const runNew = async () => {
    setPhase("running");
    setError(null);
    sim.resetAll();
    try {
      setRec(await recommendationApi.create(rec?.criteria.pref));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Chưa tạo được gợi ý.");
    } finally {
      setPhase("ready");
    }
  };

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    // Bỏ ?run=1 khỏi URL để tải lại trang chỉ đọc lần đã lưu, không chạy lại
    if (autoRun) router.replace("/recommendations");

    // Chỉ gọi setState trong .then/.catch, không gọi ngay trong effect
    const load = autoRun ? recommendationApi.create() : recommendationApi.getLatest();
    load
      .then(setRec)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Chưa tải được gợi ý."))
      .finally(() => setPhase("ready"));
  }, [autoRun, router]);

  if (phase !== "ready") {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 text-center">
        <Spinner className="size-8 text-brand" />
        <p className="text-sm text-muted-foreground">
          {phase === "running"
            ? "Đang lọc trường, đọc hoạt động ngoại khóa và xếp nhóm..."
            : "Đang tải gợi ý của bạn..."}
        </p>
      </div>
    );
  }

  const main = (
    <div className="min-w-0 flex-1 space-y-6 px-4 py-6 lg:px-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-h1 text-foreground">Trường gợi ý cho bạn</h1>
        {!rec && (
          <Button variant="outline" size="sm" onClick={runNew} className="gap-1">
            <RefreshCwIcon className="size-3.5" />
            Nhận gợi ý
          </Button>
        )}
        {/* [UC-86] popup danh sách kịch bản đã lưu: mở lại, đổi tên, xóa */}
        {rec && <SavedScenariosButton store={scenarios} onOpen={openScenario} />}
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircleIcon className="size-4" />
          <AlertTitle>Có lỗi</AlertTitle>
          <AlertDescription className="text-xs">{error}</AlertDescription>
        </Alert>
      )}

      {!rec && !error && (
        <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
          Bạn chưa có danh sách trường. Bấm &quot;Nhận gợi ý&quot; hoặc{" "}
          <Link href="/analysis?edit=1" className="font-semibold text-brand underline-offset-4 hover:underline">
            hoàn thành hồ sơ
          </Link>{" "}
          trước.
        </div>
      )}

      {rec && view && (
        <>
          <ProfileSummary rec={rec} onRerun={runNew} />
          {simulating && <SimulationBanner sim={sim} onSave={saveScenario} />}
          <ResultBody
            rec={view}
            changes={simulating ? sim.result!.changes : []}
            simulated={simulating}
            onExplain={askExplain}
            onGoal={askGoal}
          />
        </>
      )}

      <p className="text-xs text-ink-3">
        Kết quả chỉ để tham khảo, không phải xác suất đỗ. Số liệu lấy từ Common Data Set của từng trường.
      </p>

    </div>
  );

  // Có kết quả: thanh mô phỏng dính mép trái cao hết khung (như CollegeVine), phần còn lại chiếm hết bề rộng tới khung chat
  if (!rec || !view) return main;
  return (
    <div className="flex flex-col lg:flex-row lg:items-start">
      <ProfileSimulator sim={sim} basePref={basePref} />
      {main}
    </div>
  );
}

// Dải thông báo khi đang xem hồ sơ giả định: số trường mỗi nhóm trước và sau, các trường đổi nhóm
function SimulationBanner({ sim, onSave }: { sim: ProfileSimulation; onSave: (title: string) => Promise<void> }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-brand/30 bg-brand-wash px-4 py-2.5">
      <p className="text-sm font-semibold text-brand">Đang xem giả định: {describeSimulation(sim.overrides)}</p>
      <div className="flex flex-wrap items-center gap-2">
        {/* [UC-86] chỉ lưu khi bấm nút, không lưu mỗi lần kéo thanh */}
        <SaveScenarioButton overrides={sim.overrides} onSave={onSave} />
        <Button variant="outline" size="sm" onClick={sim.resetAll}>
          Quay lại hồ sơ thật
        </Button>
      </div>
    </div>
  );
}

interface ResultBodyProps {
  rec: Recommendation;
  changes: SchoolChange[]; // trường đổi nhóm khi đang mô phỏng
  simulated: boolean; // đang xem kết quả giả định
  onExplain?: (code: string) => void; // bỏ trống khi đang mô phỏng
  onGoal?: (code: string) => void;
}

function ResultBody({ rec, changes, simulated, onExplain, onGoal }: ResultBodyProps) {
  const { criteria, items } = rec;
  const byCode = new Map<string, ScoredSchool>(items.scored.map((s) => [s.code, s]));
  const changeByCode = new Map<string, SchoolChange>(changes.map((c) => [c.code, c]));

  // Không còn trường nào sau lọc cứng: lượt chọn không chạy
  if (criteria.status === null) {
    return (
      <div className="space-y-4">
        {criteria.english && <EnglishNoticeBanner notice={criteria.english} empty />}
        <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
          Không có trường nào qua bộ lọc. Hãy thử bỏ bớt bang, đổi loại trường hoặc mức ngân sách trong{" "}
          <Link href="/analysis?edit=1" className="font-semibold text-brand underline-offset-4 hover:underline">
            hồ sơ
          </Link>
          .
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {criteria.english && <EnglishNoticeBanner notice={criteria.english} empty={false} />}

      {criteria.status === "FAILED" && (
        <Alert>
          <AlertCircleIcon className="size-4" />
          <AlertTitle>Chưa xếp được danh sách</AlertTitle>
          <AlertDescription className="text-xs">
            Chưa xếp được danh sách trường. Bấm &quot;Chạy lại từ hồ sơ&quot; để thử lại.
          </AlertDescription>
        </Alert>
      )}

      {items.failed.length > 0 && (
        <p className="text-xs text-warn">
          {items.failed.length} trường chưa chấm được nên không có trong danh sách.
        </p>
      )}

      {BLOCKS.map((block) => {
        const list = items[block.key];
        if (list.length === 0) return null;
        return (
          <section key={block.key} className="space-y-3">
            <div className="flex items-baseline gap-2">
              <h2 className="text-h3 text-foreground">{block.title}</h2>
              <span className="text-xs text-muted-foreground">{block.note}</span>
            </div>
            <div className="space-y-3">
              {list.map((p) => (
                <div key={p.code} className="space-y-2">
                  <SchoolCard
                    picked={p}
                    school={byCode.get(p.code)}
                    change={changeByCode.get(p.code)}
                    simulated={simulated}
                    onExplain={onExplain}
                    onGoal={onGoal}
                    student={{ sat: criteria.studentSat ?? null, gpa: criteria.studentGpa ?? null }}
                  />
                </div>
              ))}
            </div>
          </section>
        );
      })}

      {items.other.length > 0 && (
        <details className="rounded-2xl border border-line bg-surface p-4 text-xs text-muted-foreground">
          <summary className="cursor-pointer font-semibold text-foreground">
            {items.other.length} trường chưa xếp được nhóm Khó vào / Vừa sức / An toàn
          </summary>
          <p className="mt-2">Hệ thống không đoán khi thiếu số liệu. Các trường này vẫn phù hợp bộ lọc của bạn, chỉ là chưa đủ dữ liệu để so với hồ sơ.</p>
          <ul className="mt-2 space-y-1.5">
            {items.other.map((p) => (
              <li key={p.code}>
                <b className="text-foreground">{byCode.get(p.code)?.name ?? p.code}</b>
                {p.why.length > 0 && <>: {p.why.map(whyText).join(" ")}</>}
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  );
}
