"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import type { StoredSession } from "@/lib/auth";

type AppShellProps = {
  session: StoredSession;
  title: string;
  description: string;
  onLogout: () => void;
  actions?: ReactNode;
  children: ReactNode;
};

const navItems = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/transactions", label: "Transacoes" },
  { href: "/bills", label: "Bills" },
  { href: "/budget", label: "Orcamento" },
  { href: "/imports", label: "Importar CSV" },
];

export function AppShell({
  session,
  title,
  description,
  onLogout,
  actions,
  children,
}: AppShellProps) {
  const pathname = usePathname();

  return (
    <div className="min-h-screen px-6 py-5 sm:px-8 xl:px-10">
      <div className="mx-auto flex min-h-[calc(100vh-2.5rem)] w-full max-w-[1360px] flex-col rounded-[32px] border border-[var(--color-line)] bg-[color:rgba(255,250,242,0.9)] shadow-[0_40px_120px_rgba(20,37,51,0.12)] backdrop-blur">
        <header className="border-b border-[var(--color-line)] px-7 py-6 sm:px-8 lg:px-10">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div className="space-y-3">
              <div className="flex items-center gap-3">
                <div className="rounded-full bg-[var(--color-accent)] px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.28em] text-white">
                  Farol
                </div>
                <span className="text-sm text-[var(--color-muted)]">
                  MVP financeiro para demo
                </span>
              </div>
              <div>
                <h1 className="text-3xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  {title}
                </h1>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
                  {description}
                </p>
              </div>
            </div>

            <div className="flex flex-col gap-3 lg:items-end">
              <div className="flex flex-wrap items-center gap-2 lg:justify-end">
                {navItems.map((item) => {
                  const isActive = pathname === item.href;

                  return (
                    <Link
                      className={`inline-flex min-h-11 items-center rounded-full px-4 py-2 text-sm font-medium transition ${
                        isActive
                          ? "bg-[var(--color-foreground)] text-white"
                          : "bg-white text-[var(--color-foreground)] hover:bg-[var(--color-accent-soft)]"
                      }`}
                      href={item.href}
                      key={item.href}
                    >
                      {item.label}
                    </Link>
                  );
                })}
              </div>
              <div className="flex flex-wrap items-center gap-3 lg:justify-end">
                <div className="text-right">
                  <div className="text-sm font-medium text-[var(--color-foreground)]">
                    {session.name}
                  </div>
                  <div className="text-xs text-[var(--color-muted)]">
                    {session.email}
                  </div>
                </div>
                <button
                  className="inline-flex min-h-11 items-center rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                  onClick={onLogout}
                  type="button"
                >
                  Sair
                </button>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 px-7 py-7 sm:px-8 lg:px-10">
          <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
            <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[var(--color-accent)]">
                visao do mes
              </div>
              <div className="mt-2 text-sm leading-6 text-[var(--color-foreground)]">
                Receitas, despesas, orçamento e dinheiro livre no mesmo fluxo.
              </div>
            </div>
            {actions}
          </div>
          {children}
        </main>
      </div>
    </div>
  );
}
