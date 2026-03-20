"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { LoadingScreen } from "@/components/loading-screen";
import { getFriendlyApiMessage, register } from "@/lib/api";
import { readStoredSession, writeStoredSession } from "@/lib/auth";

export default function RegisterPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isCheckingSession, setIsCheckingSession] = useState(true);

  useEffect(() => {
    if (readStoredSession()) {
      router.replace("/dashboard");
      return;
    }

    setIsCheckingSession(false);
  }, [router]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");

    if (!name.trim() || !email.trim() || !password.trim()) {
      setError("Preencha nome, e-mail e senha para criar sua conta.");
      return;
    }

    setIsSubmitting(true);

    try {
      const session = await register(name.trim(), email.trim(), password);
      writeStoredSession(session);
      router.replace("/dashboard");
    } catch (caughtError) {
      setError(
        getFriendlyApiMessage(
          caughtError,
          "Não foi possível criar sua conta agora. Tente novamente em alguns instantes.",
          {
            messageMap: {
              "Email is already in use.":
                "Já existe uma conta com esse e-mail. Tente entrar ou use outro endereço.",
            },
          },
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isCheckingSession) {
    return <LoadingScreen message="Verificando sua sessão..." />;
  }

  return (
    <div className="grid min-h-screen bg-transparent lg:grid-cols-[1.1fr_0.9fr]">
      <section className="relative hidden overflow-hidden border-r border-[var(--color-line)] p-10 lg:flex lg:flex-col lg:justify-between">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(15,118,110,0.24),transparent_36%),radial-gradient(circle_at_bottom_right,rgba(209,123,15,0.18),transparent_30%)]" />
        <div className="relative">
          <div className="inline-flex rounded-full bg-[var(--color-foreground)] px-4 py-2 text-xs font-semibold uppercase tracking-[0.28em] text-white">
            Farol MVP
          </div>
          <h1 className="mt-8 max-w-xl text-5xl font-semibold tracking-[-0.05em] text-[var(--color-foreground)]">
            Comece com uma conta nova e veja valor logo no primeiro acesso.
          </h1>
          <p className="mt-6 max-w-xl text-lg leading-8 text-[var(--color-muted)]">
            Cadastre-se, registre suas primeiras movimentações e acompanhe o mês
            com alertas, orçamento e contas a pagar no mesmo fluxo.
          </p>
        </div>

        <div className="relative grid gap-4">
          {[
            "Crie sua conta e entre no Farol sem depender de setup manual.",
            "Veja dashboard, transações e orçamento no mesmo ambiente.",
            "Organize vencimentos e importações CSV com poucos passos.",
          ].map((item) => (
            <div
              className="rounded-[24px] border border-[var(--color-line)] bg-[color:rgba(255,255,255,0.76)] px-5 py-4 text-sm leading-6 text-[var(--color-foreground)]"
              key={item}
            >
              {item}
            </div>
          ))}
        </div>
      </section>

      <section className="flex items-center justify-center px-5 py-10 sm:px-8">
        <div className="w-full max-w-md rounded-[32px] border border-[var(--color-line)] bg-[var(--color-panel)] p-8 shadow-[0_30px_80px_rgba(20,37,51,0.1)]">
          <div className="mb-8">
            <div className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--color-accent)]">
              Criar conta
            </div>
            <h2 className="mt-3 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
              Abra seu acesso ao Farol
            </h2>
            <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
              Use seus dados básicos para começar. Depois do cadastro, você já
              entra direto no dashboard.
            </p>
          </div>

          <form className="space-y-5" onSubmit={handleSubmit}>
            <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
              <span>Nome</span>
              <input
                className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                onChange={(event) => setName(event.target.value)}
                placeholder="Maria Silva"
                type="text"
                value={name}
              />
            </label>

            <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
              <span>E-mail</span>
              <input
                className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                onChange={(event) => setEmail(event.target.value)}
                placeholder="maria@email.com"
                type="email"
                value={email}
              />
            </label>

            <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
              <span>Senha</span>
              <input
                className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                onChange={(event) => setPassword(event.target.value)}
                placeholder="123456"
                type="password"
                value={password}
              />
            </label>

            {error ? (
              <div className="rounded-2xl border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            ) : null}

            <button
              className="w-full rounded-2xl bg-[var(--color-foreground)] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
              disabled={isSubmitting}
              type="submit"
            >
              {isSubmitting ? "Criando conta..." : "Criar conta"}
            </button>
          </form>

          <p className="mt-6 text-center text-sm text-[var(--color-muted)]">
            Já tem conta?{" "}
            <Link
              className="font-semibold text-[var(--color-foreground)] transition hover:text-[var(--color-accent)]"
              href="/login"
            >
              Entrar
            </Link>
          </p>
        </div>
      </section>
    </div>
  );
}
