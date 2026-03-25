import { createContext, useContext, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { authApi } from "../../features/auth/api/authApi";
import type { AuthResponse, AuthUser, LoginPayload, RegisterPayload } from "../../features/auth/api/types";
import { ApiError } from "../../shared/lib/api/client";

type AuthStatus = "loading" | "authenticated" | "anonymous";

type AuthContextValue = {
  status: AuthStatus;
  user: AuthUser | null;
  accessToken: string | null;
  login: (payload: LoginPayload) => Promise<void>;
  signup: (payload: RegisterPayload) => Promise<void>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function applyAuthResponse(response: AuthResponse, setUser: (value: AuthUser | null) => void, setAccessToken: (value: string | null) => void, setStatus: (value: AuthStatus) => void) {
  setUser(response.user);
  setAccessToken(response.accessToken);
  setStatus("authenticated");
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [user, setUser] = useState<AuthUser | null>(null);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const refreshPromiseRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    void refreshSession();
  }, []);

  async function refreshSession() {
    if (refreshPromiseRef.current) {
      return refreshPromiseRef.current;
    }

    const refreshOperation = (async () => {
      try {
        const response = await authApi.refresh();
        applyAuthResponse(response, setUser, setAccessToken, setStatus);
      } catch (error) {
        if (error instanceof ApiError && error.status === 429) {
          setStatus((current) => current === "loading" ? "anonymous" : current);
          return;
        }

        setUser(null);
        setAccessToken(null);
        setStatus("anonymous");
      } finally {
        refreshPromiseRef.current = null;
      }
    })();

    refreshPromiseRef.current = refreshOperation;
    return refreshOperation;
  }

  async function login(payload: LoginPayload) {
    const response = await authApi.login(payload);
    applyAuthResponse(response, setUser, setAccessToken, setStatus);
  }

  async function signup(payload: RegisterPayload) {
    const response = await authApi.signup(payload);
    applyAuthResponse(response, setUser, setAccessToken, setStatus);
  }

  async function logout() {
    try {
      await authApi.logout();
    } finally {
      setUser(null);
      setAccessToken(null);
      setStatus("anonymous");
    }
  }

  return (
    <AuthContext.Provider
      value={{
        status,
        user,
        accessToken,
        login,
        signup,
        logout,
        refreshSession,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }

  return context;
}
