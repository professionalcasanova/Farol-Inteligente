"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  clearStoredSession,
  readStoredSession,
  type StoredSession,
} from "@/lib/auth";

export function useProtectedSession() {
  const router = useRouter();
  const [session, setSession] = useState<StoredSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const storedSession = readStoredSession();

    if (!storedSession) {
      setIsLoading(false);
      router.replace("/login");
      return;
    }

    setSession(storedSession);
    setIsLoading(false);
  }, [router]);

  const logout = useCallback(() => {
    clearStoredSession();
    setSession(null);
    router.replace("/login");
  }, [router]);

  return {
    session,
    isLoading,
    logout,
  };
}
