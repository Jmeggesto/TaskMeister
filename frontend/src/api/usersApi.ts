import { request } from "./apiClient";
import type { AuthResponse } from "../types/AuthResponse";

export const signup = (name: string, email: string, password: string) =>
  request<AuthResponse>("/users/signup", { method: "POST", body: { name, email, password } });

export const login = (email: string, password: string) =>
  request<AuthResponse>("/users/login", { method: "POST", body: { email, password } });

export const logout = (token: string) =>
  request<void>("/users/logout", { method: "POST", token });
