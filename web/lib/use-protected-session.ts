"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  clearStoredSession,
  readStoredSession,
  type StoredSession,
} from "@/lib/auth";

type SessionRouter = {
  replace: (href: string) => void;
};

export function resolveProtectedSession(router: SessionRouter) {
  const storedSession = readStoredSession();

  if (!storedSession) {
    clearStoredSession();
    router.replace("/login");
    return null;
  }

  return storedSession;
}

export function logoutProtectedSession(router: SessionRouter) {
  clearStoredSession();
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

  const logout = useCallback(() => {
    logoutProtectedSession(router);
    setSession(null);
  }, [router]);

  return {
    session,
    isLoading,
    logout,
  };
}
