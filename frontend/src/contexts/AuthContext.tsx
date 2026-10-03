"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { apiFetch } from "@/lib/api";
import {
  USE_MOCK_AUTH,
  mockGetMe,
  mockLogin,
  mockLogout,
  mockRegister,
} from "@/lib/mock-auth";
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  User,
} from "@/types/api";

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  login: (data: LoginRequest) => Promise<AuthResponse>;
  register: (data: RegisterRequest) => Promise<AuthResponse>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Khôi phục phiên làm việc khi tải trang lần đầu
  useEffect(() => {
    let isMounted = true;

    async function initializeAuth() {
      try {
        let current: AuthResponse | null = null;
        if (USE_MOCK_AUTH) {
          current = await mockGetMe();
        } else {
          current = await apiFetch<AuthResponse>("/api/v1/auth/me", {
            credentials: "include",
          });
        }
        if (isMounted) {
          setUser(current);
        }
      } catch {
        if (isMounted) {
          setUser(null);
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void initializeAuth();

    return () => {
      isMounted = false;
    };
  }, []);

  // Gọi thủ công khi cần làm mới thông tin phiên
  const refreshUser = useCallback(async () => {
    try {
      let current: AuthResponse | null = null;
      if (USE_MOCK_AUTH) {
        current = await mockGetMe();
      } else {
        current = await apiFetch<AuthResponse>("/api/v1/auth/me", {
          credentials: "include",
        });
      }
      setUser(current);
    } catch {
      setUser(null);
    }
  }, []);

  // Đăng nhập
  const login = async (data: LoginRequest): Promise<AuthResponse> => {
    let res: AuthResponse;
    if (USE_MOCK_AUTH) {
      res = await mockLogin(data);
    } else {
      res = await apiFetch<AuthResponse>("/api/v1/auth/login", {
        method: "POST",
        body: JSON.stringify(data),
        credentials: "include",
      });
    }
    setUser(res);
    return res;
  };

  // Đăng ký
  const register = async (data: RegisterRequest): Promise<AuthResponse> => {
    if (USE_MOCK_AUTH) {
      return await mockRegister(data);
    }
    return await apiFetch<AuthResponse>("/api/v1/auth/register", {
      method: "POST",
      body: JSON.stringify(data),
      credentials: "include",
    });
  };

  // Đăng xuất
  const logout = async (): Promise<void> => {
    try {
      if (USE_MOCK_AUTH) {
        await mockLogout();
      } else {
        await apiFetch("/api/v1/auth/logout", {
          method: "POST",
          credentials: "include",
        });
      }
    } finally {
      setUser(null);
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        login,
        register,
        logout,
        refreshUser,
      }}
    >
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
