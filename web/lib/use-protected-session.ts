"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  clearStoredSession,
  readStoredSession,
  writeAuthNotice,
  type StoredSession,
} from "@/lib/auth";

type SessionRouter = {
  replace: (href: string) => void;
};

type LogoutReason = "manual" | "session-expired";

export function resolveProtectedSession(router: SessionRouter) {
  const storedSession = readStoredSession();

  if (!storedSession) {
    clearStoredSession();
    router.replace("/login");
    return null;
  }

  return storedSession;
}

export function logoutProtectedSession(
  router: SessionRouter,
  reason: LogoutReason = "manual",
) {
  clearStoredSession();

  if (reason === "session-expired") {
    writeAuthNotice("session-expired");
  }

  router.replace("/login");
}

export function useProtectedSession() {
  const router = useRouter();
  const [session, setSession] = useState<StoredSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    function syncSessionFromStorage() {
      const storedSession = resolveProtectedSession(router);

      if (!storedSession) {
        setSession(null);
        setIsLoading(false);
        return;
      }

      setSession(storedSession);
      setIsLoading(false);
    }

    syncSessionFromStorage();
    window.addEventListener("storage", syncSessionFromStorage);

    return () => {
      window.removeEventListener("storage", syncSessionFromStorage);
    };
  }, [router]);

  const logout = useCallback((reason: LogoutReason = "manual") => {
    logoutProtectedSession(router, reason);
    setSession(null);
  }, [router]);

  return {
    session,
    isLoading,
    logout,
  };
}
