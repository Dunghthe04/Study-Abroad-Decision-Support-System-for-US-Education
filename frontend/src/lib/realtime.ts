import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";

/**
 * [Chat realtime] Một kết nối SignalR duy nhất cho cả trình duyệt (chat yêu cầu liên hệ và thông báo tin mới).
 * Xác thực bằng cookie HttpOnly như các API khác. Gửi tin vẫn đi qua REST; kết nối này chỉ nhận sự kiện và gửi tín hiệu nhẹ
 * (vào / rời phòng, đang nhập, đã xem). Nếu WebSocket bị chặn, SignalR tự lùi về SSE hoặc long polling.
 */
export const LEAD_CHAT_HUB_URL = "/api/v1/hubs/lead-chat";

/** Sự kiện server đẩy xuống ("newMessage"...) và sự kiện vòng đời kết nối do module này phát. */
export type RealtimeEvent =
  | "newMessage"
  | "messagesRead"
  | "typing"
  | "presence"
  | "notification"
  | "connected"
  | "reconnected"
  | "disconnected";

const SERVER_EVENTS: RealtimeEvent[] = ["newMessage", "messagesRead", "typing", "presence", "notification"];

/** Thử nối lại: kết nối rớt thì SignalR tự thử theo các mốc này, hết mốc thì module tự thử tiếp với giãn cách tăng dần. */
const AUTO_RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];
const RETRY_BASE_MS = 15_000;
const RETRY_MAX_MS = 120_000;

type Listener = (payload?: unknown) => void;

const listeners = new Map<RealtimeEvent, Set<Listener>>();
const stateSubscribers = new Set<() => void>();

let connection: HubConnection | null = null;
let wanted = false;
let retryTimer: ReturnType<typeof setTimeout> | null = null;
let retryCount = 0;

function emit(event: RealtimeEvent, payload?: unknown) {
  listeners.get(event)?.forEach((listener) => {
    try {
      listener(payload);
    } catch {
      // Một người nghe lỗi không được làm hỏng những người nghe khác
    }
  });
}

function notifyState() {
  stateSubscribers.forEach((cb) => cb());
}

function scheduleRetry() {
  if (!wanted || retryTimer) return;
  const delay = Math.min(RETRY_BASE_MS * 2 ** retryCount, RETRY_MAX_MS);
  retryCount += 1;
  retryTimer = setTimeout(() => {
    retryTimer = null;
    void connect();
  }, delay);
}

function build(): HubConnection {
  const hub = new HubConnectionBuilder()
    .withUrl(LEAD_CHAT_HUB_URL, { withCredentials: true })
    .withAutomaticReconnect(AUTO_RECONNECT_DELAYS_MS)
    .configureLogging(LogLevel.None)
    .build();

  for (const event of SERVER_EVENTS) hub.on(event, (payload: unknown) => emit(event, payload));

  hub.onreconnecting(() => {
    notifyState();
    emit("disconnected");
  });
  hub.onreconnected(() => {
    retryCount = 0;
    notifyState();
    emit("reconnected");
  });
  hub.onclose(() => {
    notifyState();
    emit("disconnected");
    scheduleRetry();
  });
  return hub;
}

async function connect() {
  if (!wanted || typeof window === "undefined") return;
  connection ??= build();
  if (connection.state !== HubConnectionState.Disconnected) return;

  try {
    await connection.start();
    retryCount = 0;
    notifyState();
    emit("connected");
  } catch {
    // Chưa đăng nhập, mất mạng hoặc server chưa sẵn sàng: thử lại sau, REST vẫn dùng được
    notifyState();
    scheduleRetry();
  }
}

/** Bắt đầu giữ kết nối (gọi nhiều lần vẫn chỉ có một kết nối). */
export function startRealtime() {
  wanted = true;
  void connect();
}

/** Đóng kết nối, ví dụ khi đăng xuất. */
export function stopRealtime() {
  wanted = false;
  retryCount = 0;
  if (retryTimer) {
    clearTimeout(retryTimer);
    retryTimer = null;
  }
  const hub = connection;
  connection = null;
  if (hub) void hub.stop().catch(() => undefined);
  notifyState();
  emit("disconnected");
}

export function isRealtimeConnected(): boolean {
  return connection?.state === HubConnectionState.Connected;
}

/** Cho <c>useSyncExternalStore</c>: gọi lại mỗi khi trạng thái kết nối đổi. */
export function subscribeRealtimeState(callback: () => void): () => void {
  stateSubscribers.add(callback);
  return () => {
    stateSubscribers.delete(callback);
  };
}

/** Lắng nghe một sự kiện. Trả về hàm hủy đăng ký. */
export function onRealtime<T = undefined>(event: RealtimeEvent, listener: (payload: T) => void): () => void {
  let set = listeners.get(event);
  if (!set) {
    set = new Set();
    listeners.set(event, set);
  }
  const wrapped = listener as Listener;
  set.add(wrapped);
  return () => {
    set.delete(wrapped);
  };
}

/** Gọi một hàm trên hub. Ném lỗi nếu chưa kết nối hoặc server từ chối. */
export async function invokeRealtime<T = void>(method: string, ...args: unknown[]): Promise<T> {
  if (!connection || connection.state !== HubConnectionState.Connected) {
    throw new Error("realtime_not_connected");
  }
  return connection.invoke<T>(method, ...args);
}
