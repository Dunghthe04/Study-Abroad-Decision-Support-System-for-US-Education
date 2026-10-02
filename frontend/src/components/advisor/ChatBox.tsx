"use client";

import { useState } from "react";
import { apiFetch } from "@/lib/api";
import { STUDY_LEVELS, STUDY_LEVEL_LABELS, type StudyLevel } from "@/lib/study-levels";
import type { AdvisorChatResponse, ChatMessage, Citation } from "@/types/api";

type Turn = ChatMessage & { citations?: Citation[]; disclaimer?: string | null };

export function ChatBox() {
  const [turns, setTurns] = useState<Turn[]>([]);
  const [input, setInput] = useState("");
  const [studyLevel, setStudyLevel] = useState<StudyLevel | "">("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function send(e: React.FormEvent) {
    e.preventDefault();
    const content = input.trim();
    if (!content || loading) return;

    const history: Turn[] = [...turns, { role: "user", content }];
    setTurns(history);
    setInput("");
    setLoading(true);
    setError(null);

    try {
      const res = await apiFetch<AdvisorChatResponse>("/api/v1/advisor/chat", {
        method: "POST",
        body: JSON.stringify({
          messages: history.map(({ role, content }) => ({ role, content })),
          studyLevel: studyLevel || null,
        }),
      });
      setTurns([
        ...history,
        { role: "assistant", content: res.answer, citations: res.citations, disclaimer: res.disclaimer },
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Có lỗi xảy ra");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-slate-200 bg-white p-4">
      <label className="flex items-center gap-2 text-sm text-slate-600">
        Bậc học quan tâm:
        <select
          value={studyLevel}
          onChange={(e) => setStudyLevel(e.target.value as StudyLevel | "")}
          className="rounded border border-slate-300 px-2 py-1"
        >
          <option value="">Chưa chọn</option>
          {STUDY_LEVELS.map((level) => (
            <option key={level} value={level}>
              {STUDY_LEVEL_LABELS[level]}
            </option>
          ))}
        </select>
      </label>
      <div className="flex min-h-64 flex-col gap-3">
        {turns.length === 0 && (
          <p className="text-sm text-slate-500">Ví dụ: &quot;Xin visa F-1 cần những giấy tờ gì?&quot;</p>
        )}
        {turns.map((t, i) => (
          <div
            key={i}
            className={
              t.role === "user"
                ? "self-end rounded-lg bg-slate-900 px-3 py-2 text-white"
                : "self-start rounded-lg bg-slate-100 px-3 py-2"
            }
          >
            <p className="whitespace-pre-wrap">{t.content}</p>
            {t.citations && t.citations.length > 0 && (
              <ol className="mt-2 list-decimal pl-5 text-xs text-slate-600">
                {t.citations.map((c, j) => (
                  <li key={j}>
                    {c.url ? (
                      <a href={c.url} target="_blank" rel="noreferrer" className="underline">
                        {c.title}
                      </a>
                    ) : (
                      c.title
                    )}
                  </li>
                ))}
              </ol>
            )}
            {t.disclaimer && <p className="mt-2 text-xs italic text-slate-500">{t.disclaimer}</p>}
          </div>
        ))}
        {loading && <p className="text-sm text-slate-500">Đang trả lời...</p>}
        {error && <p className="text-sm text-red-600">{error}</p>}
      </div>
      <form onSubmit={send} className="flex gap-2">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Nhập câu hỏi..."
          className="flex-1 rounded border border-slate-300 px-3 py-2 outline-none focus:border-slate-500"
        />
        <button
          type="submit"
          disabled={loading}
          className="rounded bg-slate-900 px-4 py-2 text-white disabled:opacity-50"
        >
          Gửi
        </button>
      </form>
    </div>
  );
}
