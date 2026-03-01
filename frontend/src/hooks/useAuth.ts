import { useState, useCallback } from "react";
import * as usersApi from "../api/usersApi";
import type { AuthResponse } from "../types/AuthResponse";

const STORAGE_KEY = "taskmeister_auth";

function loadStoredAuth(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as AuthResponse;
    if (new Date(parsed.expiresAt) < new Date()) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

function persistAuth(data: AuthResponse): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

function clearAuth(): void {
  localStorage.removeItem(STORAGE_KEY);
}

export function useAuth() {
  const [auth, setAuth] = useState<AuthResponse | null>(loadStoredAuth);

  const signup = useCallback(async (name: string, email: string, password: string): Promise<void> => {
    const data = await usersApi.signup(name, email, password);
    persistAuth(data);
    setAuth(data);
  }, []);

  const login = useCallback(async (email: string, password: string): Promise<void> => {
    const data = await usersApi.login(email, password);
    persistAuth(data);
    setAuth(data);
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    if (auth?.token) {
      await usersApi.logout(auth.token).catch(() => {});
    }
    clearAuth();
    setAuth(null);
  }, [auth]);

  return { auth, signup, login, logout };
}
