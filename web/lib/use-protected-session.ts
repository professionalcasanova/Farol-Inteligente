"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  addSessionChangeListener,
  clearStoredSession,
  readStoredSession,
  writeAuthNotice,
  writeStoredSession,
  type StoredSession,
} from "@/lib/auth";
import { logoutSession, refreshSession } from "@/lib/api";

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

export async function resolveProtectedSessionAsync(router: SessionRouter) {
  const storedSession = readStoredSession();

  if (storedSession) {
    return storedSession;
  }

  try {
    const refreshedSession = await refreshSession();
    writeStoredSession(refreshedSession);
    return refreshedSession;
  } catch {
    clearStoredSession();
    router.replace("/login");
    return null;
  }
}

export function logoutProtectedSession(
  router: SessionRouter,
  reason: LogoutReason = "manual",
) {
  void logoutSession().catch(() => {});
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
      setSession(readStoredSession());
    }

    let isMounted = true;

    async function resolveSession() {
      const storedSession = await resolveProtectedSessionAsync(router);

      if (!isMounted) {
        return;
      }

      setSession(storedSession);
      setIsLoading(false);
    }

    void resolveSession();
    const removeSessionChangeListener = addSessionChangeListener(syncSessionFromStorage);

    return () => {
      isMounted = false;
      removeSessionChangeListener();
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
