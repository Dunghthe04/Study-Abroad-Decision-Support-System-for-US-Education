import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { formatConfidence, intentLabel, outOfScopeLabel } from "@/lib/chat-review-format";
import type { ChatReviewModelDetail } from "@/types/chat-review";

export function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-1.5">
      <h3 className="text-sm font-semibold text-ink">{title}</h3>
      <div className="flex flex-col gap-1.5 rounded-md bg-ground p-3">{children}</div>
    </section>
  );
}

/** Nội dung chat hiện dạng chữ thường (React tự thoát ký tự), giữ xuống dòng của người viết. */
export function Text({ children }: { children: string }) {
  return <p className="whitespace-pre-wrap break-words text-sm text-ink">{children}</p>;
}

export function ModelLabels({ model }: { model: ChatReviewModelDetail }) {
  const confidence = formatConfidence(model.confidence);
  return (
    <div className="mt-1 flex flex-col gap-1.5 border-t border-line pt-2">
      <p className="text-xs font-medium text-ink-2">SLM đã gán nhãn</p>
      <div className="flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
        {confidence ? <span>Độ tin cậy {confidence}</span> : null}
        {model.fallback ? <Badge variant="warn">Dùng dự phòng</Badge> : null}
        {model.outOfScope ? <Badge variant="neutral">{outOfScopeLabel(model.outOfScope)}</Badge> : null}
      </div>
      {model.modelIntents.length > 0 ? (
        <ul className="flex flex-col gap-1">
          {model.modelIntents.map((intent, index) => (
            <li key={index} className="text-xs text-ink-2">
              <span className="font-medium text-ink">{intentLabel(intent.type)}</span>
              {intent.span ? <span> · &ldquo;{intent.span}&rdquo;</span> : null}
              {intent.entities && Object.keys(intent.entities).length > 0 ? (
                <code className="mt-0.5 block break-words rounded bg-surface px-1.5 py-0.5 text-[13px] text-ink-3">{JSON.stringify(intent.entities)}</code>
              ) : null}
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-ink-3">Model không trả ý nào (SLM hỏng nên không có đầu ra thật).</p>
      )}
    </div>
  );
}
