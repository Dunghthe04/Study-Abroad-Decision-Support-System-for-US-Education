/**
 * Tự gia hạn phiên đăng nhập bằng refresh token.
 *
 * Access token chỉ sống 15 phút, refresh token (cookie HttpOnly, chỉ gửi tới /api/v1/auth) sống tới hết 7 ngày của phiên.
 * - Request nào bị 401 thì gọi POST /api/v1/auth/refresh một lần rồi gửi lại request đó.
 *   Nhiều request cùng bị 401 thì dùng chung một lần gia hạn (server đổi refresh token mỗi lần, gọi song song sẽ bị 409).
 * - 409 từ refresh = tab khác vừa gia hạn trước, trình duyệt đã có cookie mới, chỉ cần gửi lại request.
 * - Biết hạn access token (sau đăng nhập / gia hạn) thì hẹn giờ gia hạn trước 1 phút để người dùng không gặp 401.
 * - Gia hạn thất bại (phiên hết 7 ngày, bị thu hồi, bị khóa) thì phát sự kiện SESSION_EXPIRED_EVENT để AuthContext đăng xuất.
 */

const REFRESH_PATH = "/api/v1/auth/refresh";
/** Các đường dẫn mà 401 không có nghĩa "phiên hết hạn": sai mật khẩu khi đăng nhập, hoặc chính lời gọi gia hạn. */
const NO_RETRY_PATHS = ["/api/v1/auth/login", REFRESH_PATH];
const REFRESH_BEFORE_MS = 60_000;
const MIN_DELAY_MS = 5_000;

export const SESSION_EXPIRED_EVENT = "usas:session-expired";

const isBrowser = typeof window !== "undefined";
let inflight: Promise<boolean> | null = null;
let timer: ReturnType<typeof setTimeout> | null = null;

function pathOf(input: string): string {
  try {
    return new URL(input, isBrowser ? window.location.origin : "http://localhost").pathname;
  } catch {
    return input;
  }
}

/** Gọi gia hạn, dùng chung một lời gọi cho mọi request đang chờ. Trả true nếu nên gửi lại request. */
export function refreshSession(): Promise<boolean> {
  if (!isBrowser) return Promise.resolve(false);
  inflight ??= (async () => {
    try {
      const res = await fetch(REFRESH_PATH, { method: "POST", credentials: "include", cache: "no-store" });
      if (res.ok) {
        const body = (await res.json().catch(() => null)) as { expiresAt?: string } | null;
        scheduleRefresh(body?.expiresAt);
        return true;
      }
      if (res.status === 409) return true;
      cancelScheduledRefresh();
      window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
      return false;
    } catch {
      // Lỗi mạng: không kết luận là hết phiên, để request gốc trả lỗi như cũ
      return false;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

/** Thay cho fetch ở các lời gọi API cần đăng nhập: gặp 401 thì gia hạn rồi gửi lại đúng một lần. */
export async function authFetch(input: string, init?: RequestInit): Promise<Response> {
  const res = await fetch(input, init);
  if (res.status !== 401 || !isBrowser || NO_RETRY_PATHS.includes(pathOf(input))) return res;
  if (init?.signal?.aborted) return res;
  if (!(await refreshSession())) return res;
  return fetch(input, init);
}

/** Hẹn giờ gia hạn trước khi access token hết hạn 1 phút. Gọi sau khi đăng nhập hoặc gia hạn thành công. */
export function scheduleRefresh(accessExpiresAt?: string | null): void {
  if (!isBrowser || !accessExpiresAt) return;
  const expiresAt = Date.parse(accessExpiresAt);
  if (Number.isNaN(expiresAt)) return;
  cancelScheduledRefresh();
  const delay = Math.max(expiresAt - Date.now() - REFRESH_BEFORE_MS, MIN_DELAY_MS);
  timer = setTimeout(() => {
    timer = null;
    void refreshSession();
  }, delay);
}

/** Bỏ hẹn giờ gia hạn (đăng xuất). */
export function cancelScheduledRefresh(): void {
  if (timer) clearTimeout(timer);
  timer = null;
}
