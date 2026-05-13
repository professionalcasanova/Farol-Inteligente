"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { LoadingScreen } from "@/components/loading-screen";
import { refreshSession } from "@/lib/api";
import { readStoredSession, writeStoredSession } from "@/lib/auth";

export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    async function redirect() {
      const session = readStoredSession();

      if (session) {
        router.replace("/dashboard");
        return;
      }

      try {
        const refreshedSession = await refreshSession();
        writeStoredSession(refreshedSession);
        router.replace("/dashboard");
      } catch {
        router.replace("/login");
      }
    }

    void redirect();
  }, [router]);

  return <LoadingScreen message="Direcionando para o Farol..." />;
}
