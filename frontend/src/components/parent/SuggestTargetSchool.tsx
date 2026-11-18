"use client";

// Phụ huynh gợi ý một trường cho con: tìm trường, thêm lời nhắn, gửi.
// Gợi ý nằm ở trang Trường mục tiêu của con, con tự chọn "Thêm vào danh sách" hoặc "Bỏ qua".

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { searchUniversities } from "@/lib/university-api";
import type { UniversityListItem } from "@/types/university";

const MAX_NOTE = 1000; // cùng giới hạn với backend

export function SuggestTargetSchool({
  studentId,
  childName,
  onSuggested,
}: {
  studentId: string;
  childName: string;
  onSuggested: () => void;
}) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<UniversityListItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [picked, setPicked] = useState<UniversityListItem | null>(null);
  const [note, setNote] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Tìm sau khi ngừng gõ 300ms; gõ tiếp thì bỏ lần tìm cũ
  useEffect(() => {
    const text = query.trim();
    if (picked || text.length < 2) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setSearching(true);
      searchUniversities({ search: text, pageSize: 6 }, controller.signal)
        .then((page) => setResults(page.items))
        .catch(() => {
          if (!controller.signal.aborted) setResults([]);
        })
        .finally(() => {
          if (!controller.signal.aborted) setSearching(false);
        });
    }, 300);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, picked]);

  const reset = () => {
    setQuery("");
    setResults([]);
    setPicked(null);
    setNote("");
    setError(null);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!picked) return;
    setSending(true);
    setError(null);
    try {
      await parentChildrenApi.suggestTargetSchool(studentId, {
        universityId: picked.id,
        note: note.trim() || undefined,
      });
      toast.add({
        type: "success",
        title: "Đã gửi gợi ý",
        description: `${childName} sẽ thấy ${picked.name} ở trang Trường mục tiêu.`,
      });
      reset();
      onSuggested();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Chưa gửi được gợi ý. Vui lòng thử lại.");
    } finally {
      setSending(false);
    }
  };

  const showResults = !picked && query.trim().length >= 2;

  return (
    <form onSubmit={submit} className="space-y-3 rounded-2xl border border-line bg-surface p-4">
      <div>
        <h3 className="text-sm font-semibold text-foreground">Gợi ý trường cho con</h3>
        <p className="text-xs text-muted-foreground">
          Trường bạn gợi ý chưa vào danh sách của {childName}. Con sẽ chọn thêm vào danh sách hoặc bỏ qua.
        </p>
      </div>

      {picked ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line bg-ground px-3 py-2">
          <div>
            <p className="text-sm font-semibold text-foreground">{picked.name}</p>
            <p className="text-xs text-muted-foreground">
              {[picked.city, picked.state].filter(Boolean).join(", ") || "Chưa rõ địa điểm"}
            </p>
          </div>
          <Button type="button" variant="ghost" size="sm" onClick={() => setPicked(null)} disabled={sending}>
            Chọn trường khác
          </Button>
        </div>
      ) : (
        <div className="space-y-2">
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Nhập tên trường, ví dụ Boston University"
            aria-label="Tìm trường để gợi ý"
          />
          {showResults && (
            <div className="rounded-xl border border-line">
              {searching ? (
                <div className="flex items-center gap-2 p-3 text-xs text-muted-foreground">
                  <Spinner /> Đang tìm...
                </div>
              ) : results.length === 0 ? (
                <p className="p-3 text-xs text-muted-foreground">Không tìm thấy trường nào.</p>
              ) : (
                <ul className="divide-y divide-line">
                  {results.map((u) => (
                    <li key={u.id}>
                      <button
                        type="button"
                        onClick={() => setPicked(u)}
                        className="w-full px-3 py-2 text-left hover:bg-muted"
                      >
                        <span className="block text-sm text-foreground">{u.name}</span>
                        <span className="block text-xs text-muted-foreground">
                          {[u.city, u.state].filter(Boolean).join(", ") || "Chưa rõ địa điểm"}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      )}

      {picked && (
        <Textarea
          value={note}
          onChange={(e) => setNote(e.target.value.slice(0, MAX_NOTE))}
          placeholder={`Lời nhắn cho ${childName} (không bắt buộc), ví dụ lý do bạn thích trường này`}
          aria-label="Lời nhắn cho con"
          rows={3}
        />
      )}

      {error && <p className="text-xs text-destructive">{error}</p>}

      {picked && (
        <div className="flex gap-2">
          <Button type="submit" disabled={sending}>
            {sending && <Spinner />}
            Gửi gợi ý
          </Button>
          <Button type="button" variant="outline" onClick={reset} disabled={sending}>
            Hủy
          </Button>
        </div>
      )}
    </form>
  );
}
