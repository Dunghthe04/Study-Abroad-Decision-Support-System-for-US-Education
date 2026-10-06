"use client";

import React, { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { authApi } from "@/lib/api";
import type { LoginRequest, RegisterRequest, UserDto } from "@/types/api";

interface AuthContextType {
  user: UserDto | null;
  isLoading: boolean;
  login: (data: LoginRequest) => Promise<UserDto>;
  register: (data: RegisterRequest) => Promise<UserDto>;
  verifyEmail: (data: { email: string; otpCode: string }) => Promise<UserDto>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // [Bảo mật cao - Không dùng localStorage]:
  // Khi tải ứng dụng: gọi trực tiếp /api/v1/auth/me với HttpOnly Cookie được trình duyệt gửi tự động.
  useEffect(() => {
    async function initAuth() {
      try {
        const profile = await authApi.getMe();
        setUser(profile);
      } catch {
        // Chưa đăng nhập, token hết hạn hoặc phiên đã bị thu hồi
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    }

    initAuth();
  }, []);

  // [AC-2] Đăng nhập: Backend đặt HttpOnly Cookie, Client chỉ lưu User state trong RAM
  const login = async (data: LoginRequest): Promise<UserDto> => {
    const res = await authApi.login(data);
    setUser(res.user);
    return res.user;
  };

  // [AC-1] Đăng ký: Gọi API tạo tài khoản học sinh / phụ huynh
  const register = async (data: RegisterRequest): Promise<UserDto> => {
    return await authApi.register(data);
  };

  // [USAS-12] Xác thực email OTP: Nhận AuthResponse và cập nhật user state
  const verifyEmail = async (data: { email: string; otpCode: string }): Promise<UserDto> => {
    const res = await authApi.verifyEmail(data);
    setUser(res.user);
    return res.user;
  };

  const refreshUser = async () => {
    try {
      const profile = await authApi.getMe();
      setUser(profile);
    } catch {
      setUser(null);
    }
  };

  // [AC-3] Đăng xuất: Thu hồi phiên trên máy chủ và xóa HttpOnly Cookie
  const logout = async () => {
    try {
      await authApi.logout();
    } catch {
      // Dù có lỗi mạng thì Client vẫn reset trạng thái
    } finally {
      setUser(null);
    }
  };

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, verifyEmail, logout, refreshUser }}>
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
