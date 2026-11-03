import { ApiError } from "@/lib/api";
import type { AdvisorChatResponse, AdvisorStreamEvent } from "@/types/api";

/** Tin báo tiến độ hoặc câu trả lời của một ý (dòng final trả về qua kết quả của hàm, không qua onEvent). */
export type AdvisorProgressEvent = Exclude<AdvisorStreamEvent, { type: "final" | "error" }>;

/**
 * Chat với advisor và nhận câu trả lời theo từng ý khi xong (POST /api/v1/advisor/chat/stream, mỗi dòng một JSON).
 * onEvent nhận tin tiến độ và câu trả lời từng ý ngay khi tới. Kết quả trả về là dòng final: câu cuối cùng
 * (có thể khác phần đã hiện, vì gợi ý trường / giải thích trường thay câu ở bước cuối), kèm sessionId, nút, widget.
 * Lỗi thì ném ApiError như apiFetch, nên toAssistantError xử lý giống nhau.
 */
export async function streamAdvisorChat(
  body: unknown,
  onEvent: (event: AdvisorProgressEvent) => void,
): Promise<AdvisorChatResponse> {
  const res = await fetch("/api/v1/advisor/chat/stream", {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    cache: "no-store",
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new ApiError(res.status, err?.detail ?? err?.title ?? err?.error ?? err?.message ?? res.statusText);
  }
  const reader = res.body?.getReader();
  if (!reader) throw new ApiError(502, "Trình duyệt không đọc được luồng trả lời.");

  // Đặt trong object vì TypeScript không theo dõi phép gán bên trong hàm con
  const result: { final: AdvisorChatResponse | null } = { final: null };
  const handle = (line: string) => {
    if (!line.trim()) return;
    const event = JSON.parse(line) as AdvisorStreamEvent;
    if (event.type === "final") result.final = event.response;
    else if (event.type === "error") throw new ApiError(503, event.message);
    else onEvent(event);
  };

  // Dữ liệu tới theo từng mảnh, một mảnh có thể cắt giữa dòng: giữ phần dở lại cho lần đọc sau
  const decoder = new TextDecoder();
  let buffer = "";
  for (;;) {
    const { done, value } = await reader.read();
    buffer += decoder.decode(value, { stream: !done });
    let newline = buffer.indexOf("\n");
    while (newline >= 0) {
      handle(buffer.slice(0, newline));
      buffer = buffer.slice(newline + 1);
      newline = buffer.indexOf("\n");
    }
    if (done) break;
  }
  handle(buffer);

  if (!result.final) throw new ApiError(502, "Trợ lý không trả kết quả cuối.");
  return result.final;
}
