"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { LoadingScreen } from "@/components/loading-screen";
import { readStoredSession } from "@/lib/auth";

export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    const session = readStoredSession();
    router.replace(session ? "/dashboard" : "/login");
  }, [router]);

  return <LoadingScreen message="Direcionando para o Farol..." />;
}
