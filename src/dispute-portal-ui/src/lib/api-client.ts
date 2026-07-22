import axios from "axios";
import { tokenStorage } from "@/auth/token-storage";

/**
 * The single axios instance every feature uses to talk to the backend. A request interceptor
 * attaches the Bearer token; a response interceptor clears the session and redirects to /login
 * on 401 (expired/invalid JWT, AC-AUTH-01). A 403 is left untouched — it means "authenticated
 * but wrong role" and must NOT log the user out (TDP-FE-01 §4).
 */
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api/v1",
  headers: { "Content-Type": "application/json" },
});

api.interceptors.request.use((config) => {
  const token = tokenStorage.get();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      tokenStorage.clear();
      if (window.location.pathname !== "/login") {
        const from = encodeURIComponent(window.location.pathname + window.location.search);
        window.location.assign(`/login?redirect=${from}`);
      }
    }
    return Promise.reject(error);
  },
);
