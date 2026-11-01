"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlertCircleIcon, RefreshCwIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAssistant, useAssistantPageContext } from "@/components/assistant/AssistantProvider";
import { recommendationApi } from "@/lib/recommendation-api";
import type { PickBlockName, Pref, Recommendation, ScoredSchool } from "@/types/recommendation";
import { ExplainSheet } from "./ExplainSheet";
import { PrefButtons } from "./PrefButtons";
import { ProfileSummary } from "./ProfileSummary";
import { SchoolCard } from "./SchoolCard";

// Bốn khối chính theo đúng thứ tự trên màn. "other" để riêng ở cuối.
const BLOCKS: { key: PickBlockName; title: string; note: string }[] = [
  { key: "reach", title: "Khó vào", note: "Tối đa 3 trường" },
  { key: "match", title: "Vừa sức", note: "Tối đa 5 trường" },
  { key: "safety", title: "An toàn hơn", note: "Tối đa 4 trường" },
  { key: "aid", title: "Chỉ đi được nếu có học bổng lớn", note: "Xếp theo số tiền còn thiếu" },
];

const RESULT_SUGGESTIONS = [
  "Hồ sơ của em mạnh ở đâu?",
  "Nên nộp bao nhiêu trường mỗi nhóm?",
  "Làm sao để lên nhóm Vừa sức?",
];

interface RecommendationViewProps {
  autoRun: boolean; // true khi vừa gửi hồ sơ xong: chạy gợi ý mới thay vì đọc lần cũ
}

export function RecommendationView({ autoRun }: RecommendationViewProps) {
  const router = useRouter();
  const [rec, setRec] = useState<Recommendation | null>(null);
  // Vừa gửi hồ sơ thì bắt đầu ở trạng thái "running", còn lại là "loading" (đọc lần đã lưu)
  const [phase, setPhase] = useState<"loading" | "running" | "ready">(autoRun ? "running" : "loading");
  const [repicking, setRepicking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [explainCode, setExplainCode] = useState<string | null>(null);
  const started = useRef(false); // dev mode chạy useEffect 2 lần; không được tạo 2 lần gợi ý
  const { openPanel } = useAssistant();

  // Ngữ cảnh trợ lý bên phải: lời chào theo số trường, chip hỏi về trường Khó vào đầu tiên
  const firstReach = rec ? rec.items.scored.find((s) => s.code === rec.items.reach[0]?.code) : undefined;
  const shownCount = rec ? BLOCKS.reduce((n, b) => n + rec.items[b.key].length, 0) : 0;
  useAssistantPageContext({
    label: "Trang đề xuất",
    suggestions: firstReach ? [`Vì sao ${firstReach.name} Khó vào?`, ...RESULT_SUGGESTIONS] : RESULT_SUGGESTIONS,
    greeting: rec ? `Mình là hệ thống tư vấn du học. Bạn cần mình hỗ trợ gì không?:` : undefined,
  });

  // Có kết quả thì mở trợ lý bên phải một lần như bản demo (màn hẹp thì khung phủ kín nên không tự mở)
  const autoOpened = useRef(false);
  useEffect(() => {
    if (!rec || autoOpened.current) return;
    autoOpened.current = true;
    if (window.matchMedia("(min-width: 900px)").matches) openPanel();
  }, [rec, openPanel]);

  // Chạy cả quy trình (lọc → Qwen → Gemini). Mất khoảng nửa phút.
  const runNew = async () => {
    setPhase("running");
    setError(null);
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

    // Bỏ ?run=1 khỏi URL để tải lại trang chỉ đọc lần đã lưu, không chạy lại AI
    if (autoRun) router.replace("/recommendations");

    // Chỉ gọi setState trong .then/.catch, không gọi ngay trong effect
    const load = autoRun ? recommendationApi.create() : recommendationApi.getLatest();
    load
      .then(setRec)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "Chưa tải được gợi ý."))
      .finally(() => setPhase("ready"));
  }, [autoRun, router]);

  // Đổi nút ưu tiên: chỉ gọi lại lượt chọn
  const handleRepick = async (pref: Pref) => {
    setRepicking(true);
    setError(null);
    try {
      setRec(await recommendationApi.repick(pref));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Chưa sắp xếp lại được.");
    } finally {
      setRepicking(false);
    }
  };

  if (phase !== "ready") {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 text-center">
        <Spinner className="size-8 text-brand" />
        <p className="text-sm text-muted-foreground">
          {phase === "running"
            ? "Đang lọc trường, đọc hoạt động ngoại khóa và chấm điểm. Việc này mất khoảng nửa phút..."
            : "Đang tải gợi ý của bạn..."}
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-8">
      <div className="flex flex-wrap items-end justify-between gap-4 border-b border-line pb-4">
        <div className="space-y-1">
          <h1 className="text-h1 text-foreground">Trường gợi ý cho bạn</h1>
          {rec && (
            <p className="text-xs text-muted-foreground">
              Tạo lúc {new Date(rec.createdAt).toLocaleString("vi-VN")} · Bậc ngoại khóa {rec.criteria.tier}/5
              {!rec.criteria.ecAiUsed && " (tính từ số bạn khai)"}
            </p>
          )}
        </div>
        <Button variant="outline" size="sm" onClick={runNew} className="gap-1">
          <RefreshCwIcon className="size-3.5" />
          {rec ? "Chạy lại từ hồ sơ" : "Nhận gợi ý"}
        </Button>
      </div>

      <ProfileSummary rec={rec} />

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

      {rec && <ResultBody rec={rec} repicking={repicking} onRepick={handleRepick} onExplain={setExplainCode} />}

      <p className="text-[11px] text-ink-3">
        Kết quả chỉ để tham khảo, không phải xác suất đỗ. Số liệu lấy từ Common Data Set của từng trường.
      </p>

      <ExplainSheet code={explainCode} onClose={() => setExplainCode(null)} />
    </div>
  );
}

interface ResultBodyProps {
  rec: Recommendation;
  repicking: boolean;
  onRepick: (pref: Pref) => void;
  onExplain: (code: string) => void;
}

function ResultBody({ rec, repicking, onRepick, onExplain }: ResultBodyProps) {
  const { criteria, items } = rec;
  const byCode = new Map<string, ScoredSchool>(items.scored.map((s) => [s.code, s]));

  // Không còn trường nào sau lọc cứng: lượt chọn không chạy
  if (criteria.status === null) {
    return (
      <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
        Không có trường nào qua bộ lọc. Hãy thử bỏ bớt bang, đổi loại trường hoặc mức ngân sách trong{" "}
        <Link href="/analysis?edit=1" className="font-semibold text-brand underline-offset-4 hover:underline">
          hồ sơ
        </Link>
        .
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <PrefButtons current={criteria.pref} disabled={repicking} onSelect={onRepick} />

      {criteria.status === "FAILED" && (
        <Alert>
          <AlertCircleIcon className="size-4" />
          <AlertTitle>Chưa xếp được danh sách</AlertTitle>
          <AlertDescription className="text-xs">
            Dịch vụ AI đang hết lượt hoặc trả kết quả lỗi. Trường đã được chấm và lưu lại; bấm một nút ưu tiên ở
            trên để thử xếp lại sau.
          </AlertDescription>
        </Alert>
      )}

      {items.failed.length > 0 && (
        <p className="text-xs text-warn">
          {items.failed.length} trường chưa chấm được nên không có trong danh sách.
        </p>
      )}

      {repicking && (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <Spinner />
          Đang sắp xếp lại theo ưu tiên mới...
        </div>
      )}

      {BLOCKS.map((block) => {
        const list = items[block.key];
        if (list.length === 0) return null;
        return (
          <section key={block.key} className="space-y-3">
            <div className="flex items-baseline gap-2">
              <h2 className="text-sm font-bold uppercase tracking-wide text-foreground">{block.title}</h2>
              <span className="text-xs text-muted-foreground">{block.note}</span>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              {list.map((p) => (
                <SchoolCard key={p.code} picked={p} school={byCode.get(p.code)} onExplain={onExplain} />
              ))}
            </div>
          </section>
        );
      })}

      {items.other.length > 0 && (
        <details className="rounded-2xl border border-line bg-surface p-4 text-xs text-muted-foreground">
          <summary className="cursor-pointer font-semibold text-foreground">
            {items.other.length} trường chưa xếp nhóm (thiếu dữ liệu hoặc nhận gần như tất cả)
          </summary>
          <ul className="mt-2 space-y-1">
            {items.other.map((p) => (
              <li key={p.code}>{byCode.get(p.code)?.name ?? p.code}</li>
            ))}
          </ul>
        </details>
      )}
    </div>
  );
}
