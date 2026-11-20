"use client";

import React, { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { actingApi } from "@/lib/acting-api";
import { authApi } from "@/lib/api";
import { cancelScheduledRefresh, scheduleRefresh, SESSION_EXPIRED_EVENT } from "@/lib/auth-refresh";
import type { LoginRequest, RegisterRequest, UserDto } from "@/types/api";

interface AuthContextType {
  user: UserDto | null;
  isLoading: boolean;
  login: (data: LoginRequest) => Promise<UserDto>;
  register: (data: RegisterRequest) => Promise<{ message: string }>;
  verifyEmail: (data: { email: string; otpCode: string }) => Promise<UserDto>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  /** 1.5b: phụ huynh đang nhập hồ sơ thay con managed nào. null = không làm thay. */
  acting: ActingChild | null;
  /** Vào làm thay rồi mở trang hồ sơ của con (tải lại cả trang). */
  /** next: trang mở sau khi vào làm thay, mặc định trang nhập hồ sơ. Chat (1.5e) truyền "/advisor" để mở khung chat. */
  startActing: (childId: string, next?: string) => Promise<void>;
  /** Thoát làm thay rồi về "Con của tôi" (tải lại cả trang). */
  stopActing: () => Promise<void>;
}

export interface ActingChild {
  childId: string;
  childName: string;
}

/** Chỉ phụ huynh mới làm thay được. Lỗi thì coi như không làm thay để không chặn trang. */
async function loadActing(user: UserDto | null): Promise<ActingChild | null> {
  if (user?.role !== "parent") return null;
  try {
    const res = await actingApi.get();
    return res.acting && res.childId ? { childId: res.childId, childName: res.childName ?? "" } : null;
  } catch {
    return null;
  }
}

/**
 * Đổi phiên giữa phụ huynh và con thì tải lại cả trang: dữ liệu đã tải và lưu tạm (useFetched, chat, nháp hồ sơ)
 * thuộc người trước sẽ bị bỏ hết, không lẫn hồ sơ phụ huynh với hồ sơ con.
 */
function reloadAt(path: string) {
  window.location.assign(path);
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [acting, setActing] = useState<ActingChild | null>(null);

  // [Bảo mật cao - Không dùng localStorage]:
  // Khi tải ứng dụng: gọi trực tiếp /api/v1/auth/me với HttpOnly Cookie được trình duyệt gửi tự động.
  useEffect(() => {
    async function initAuth() {
      try {
        const profile = await authApi.getMe();
        setActing(await loadActing(profile));
        setUser(profile);
      } catch {
        // Chưa đăng nhập, token hết hạn hoặc phiên đã bị thu hồi
        setUser(null);
        setActing(null);
      } finally {
        setIsLoading(false);
      }
    }

    initAuth();
  }, []);

  // Refresh token hết hạn (quá 7 ngày) hoặc phiên bị thu hồi: auth-refresh báo, client coi như đã đăng xuất
  useEffect(() => {
    const onExpired = () => {
      setUser(null);
      setActing(null);
    };
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired);
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired);
  }, []);

  // 1.5b (R5): tab khác vừa vào hoặc thoát làm thay thì cookie đã đổi. Quay lại tab này mà thấy khác thì tải lại,
  // để tab cũ không ghi hồ sơ vào nhầm người.
  const userId = user?.id;
  const isParent = user?.role === "parent";
  const actingChildId = acting?.childId ?? null;
  useEffect(() => {
    if (!isParent) return;
    const onVisible = async () => {
      if (document.visibilityState !== "visible") return;
      try {
        const res = await actingApi.get();
        const nowChildId = res.acting ? (res.childId ?? null) : null;
        if (nowChildId !== actingChildId) window.location.reload();
      } catch {
        // Lỗi mạng: giữ nguyên, lần quay lại sau kiểm tra tiếp
      }
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => document.removeEventListener("visibilitychange", onVisible);
  }, [isParent, userId, actingChildId]);

  // [AC-2] Đăng nhập: Backend đặt HttpOnly Cookie, Client chỉ lưu User state trong RAM
  const login = async (data: LoginRequest): Promise<UserDto> => {
    const res = await authApi.login(data);
    scheduleRefresh(res.expiresAt);
    setUser(res.user);
    return res.user;
  };

  // [AC-1] Đăng ký: Gọi API tạo tài khoản học sinh / phụ huynh
  const register = async (data: RegisterRequest): Promise<{ message: string }> => {
    return await authApi.register(data);
  };

  // [USAS-12] Xác thực email OTP: Nhận AuthResponse và cập nhật user state
  const verifyEmail = async (data: { email: string; otpCode: string }): Promise<UserDto> => {
    const res = await authApi.verifyEmail(data);
    scheduleRefresh(res.expiresAt);
    setUser(res.user);
    return res.user;
  };

  const refreshUser = async () => {
    try {
      const profile = await authApi.getMe();
      setActing(await loadActing(profile));
      setUser(profile);
    } catch {
      setUser(null);
      setActing(null);
    }
  };

  const startActing = async (childId: string, next = "/profile/academic") => {
    await actingApi.start(childId);
    reloadAt(next);
  };

  const stopActing = async () => {
    await actingApi.stop();
    reloadAt("/parent/children");
  };

  // [AC-3] Đăng xuất: Thu hồi phiên trên máy chủ và xóa HttpOnly Cookie
  const logout = async () => {
    try {
      await authApi.logout();
    } catch {
      // Dù có lỗi mạng thì Client vẫn reset trạng thái
    } finally {
      cancelScheduledRefresh();
      setUser(null);
      setActing(null);
    }
  };

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, verifyEmail, logout, refreshUser, acting, startActing, stopActing }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
