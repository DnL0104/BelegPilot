"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
} from "react";
import { useRouter, usePathname } from "next/navigation";
import type { AuthUser, AuthResponse } from "@/types/api";
import {
  setAccessToken,
  login as apiLogin,
  register as apiRegister,
  logout as apiLogout,
} from "@/lib/api-client";

interface AuthContextValue {
  user: AuthUser | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, displayName: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const PUBLIC_PATHS = ["/login", "/register", "/impressum", "/datenschutz", "/agb", "/widerruf"];

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();
  const pathname = usePathname();

  // On mount, restore session from localStorage
  useEffect(() => {
    const stored = localStorage.getItem("user");
    const refreshToken = localStorage.getItem("refreshToken");

    if (stored && refreshToken) {
      try {
        const parsed = JSON.parse(stored) as AuthUser;
        setUser(parsed);
        // The Axios interceptor will handle getting a fresh access token
        // via the refresh token on the first 401.
      } catch {
        localStorage.removeItem("user");
        localStorage.removeItem("refreshToken");
      }
    }
    setIsLoading(false);
  }, []);

  // Redirect unauthenticated users away from protected routes
  useEffect(() => {
    if (isLoading) return;
    if (!user && !PUBLIC_PATHS.includes(pathname)) {
      router.replace("/login");
    }
  }, [user, isLoading, pathname, router]);

  const handleAuthResponse = useCallback(
    (response: AuthResponse) => {
      setAccessToken(response.accessToken);
      localStorage.setItem("refreshToken", response.refreshToken);
      localStorage.setItem("user", JSON.stringify(response.user));
      setUser(response.user);
      router.replace("/");
    },
    [router]
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await apiLogin(email, password);
      handleAuthResponse(response);
    },
    [handleAuthResponse]
  );

  const register = useCallback(
    async (email: string, displayName: string, password: string) => {
      const response = await apiRegister(email, displayName, password);
      handleAuthResponse(response);
    },
    [handleAuthResponse]
  );

  const logout = useCallback(() => {
    apiLogout();
    setUser(null);
    router.replace("/login");
  }, [router]);

  return (
    <AuthContext value={{ user, isLoading, login, register, logout }}>
      {children}
    </AuthContext>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
}
