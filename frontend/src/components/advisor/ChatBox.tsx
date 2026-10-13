"use client";

import { useState } from "react";
import { apiFetch } from "@/lib/api";
import { STUDY_LEVELS, STUDY_LEVEL_LABELS, type StudyLevel } from "@/lib/study-levels";
import type { AdvisorChatResponse, ChatMessage, Citation } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

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
    <Card>
      <CardContent className="flex flex-col gap-4">
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          Bậc học quan tâm:
          <NativeSelect value={studyLevel} onChange={(e) => setStudyLevel(e.target.value as StudyLevel | "")}>
            <NativeSelectOption value="">Chưa chọn</NativeSelectOption>
            {STUDY_LEVELS.map((level) => (
              <NativeSelectOption key={level} value={level}>
                {STUDY_LEVEL_LABELS[level]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </label>
        <div className="flex min-h-64 flex-col gap-3">
          {turns.length === 0 && (
            <p className="text-sm text-muted-foreground">Ví dụ: &quot;Xin visa F-1 cần những giấy tờ gì?&quot;</p>
          )}
          {turns.map((t, i) => (
            <div
              key={i}
              className={cn(
                "rounded-lg px-3 py-2",
                t.role === "user" ? "self-end bg-primary text-primary-foreground" : "self-start bg-muted",
              )}
            >
              <p className="whitespace-pre-wrap">{t.content}</p>
              {t.citations && t.citations.length > 0 && (
                <ol className="mt-2 list-decimal pl-5 text-body-s">
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
              {t.disclaimer && <p className="mt-2 text-body-s italic">{t.disclaimer}</p>}
            </div>
          ))}
          {loading && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Spinner />
              Đang trả lời...
            </p>
          )}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </div>
        <form onSubmit={send} className="flex gap-2">
          <Input
            value={input}
            maxLength={1000}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Nhập câu hỏi (tối đa 1000 ký tự)..."
            className="flex-1"
          />
          <Button type="submit" disabled={loading}>
            Gửi
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
