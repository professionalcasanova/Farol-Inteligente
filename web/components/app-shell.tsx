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
  utilityActions?: ReactNode;
  children: ReactNode;
};

const navItems = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/transactions", label: "Movimentações" },
  { href: "/bills", label: "Contas a pagar" },
  { href: "/budget", label: "Planejamento" },
  { href: "/imports", label: "Importar dados" },
];

export function AppShell({
  session,
  title,
  description,
  onLogout,
  actions,
  utilityActions,
  children,
}: AppShellProps) {
  const pathname = usePathname();

  return (
    <div className="min-h-screen px-6 py-5 sm:px-8 xl:px-10">
      <div className="mx-auto flex min-h-[calc(100vh-2.5rem)] w-full max-w-[1360px] flex-col rounded-[32px] border border-[var(--color-line)] bg-[color:rgba(255,250,242,0.9)] shadow-[0_40px_120px_rgba(20,37,51,0.12)] backdrop-blur">
        <header className="border-b border-[var(--color-line)] px-7 py-6 sm:px-8 lg:px-10">
          <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
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

              <nav aria-label="Navegação principal" className="min-w-0 lg:max-w-full">
                <div className="flex items-center gap-2 overflow-x-auto pb-1 md:overflow-visible lg:justify-end">
                  {navItems.map((item) => {
                    const isActive = pathname === item.href;

                    return (
                      <Link
                        aria-current={isActive ? "page" : undefined}
                        className={`inline-flex min-h-11 shrink-0 items-center whitespace-nowrap rounded-full border px-4 py-2 text-sm font-semibold transition ${
                          isActive
                            ? "border-[var(--color-foreground)] bg-[var(--color-foreground)] !text-white"
                            : "border-transparent bg-white text-[var(--color-foreground)] hover:border-[var(--color-line)] hover:bg-[var(--color-accent-soft)]"
                        }`}
                        href={item.href}
                        key={item.href}
                      >
                        <span className={isActive ? "text-white" : undefined}>{item.label}</span>
                      </Link>
                    );
                  })}
                </div>
              </nav>
            </div>

            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div className="min-w-0">{actions}</div>
              <div className="flex flex-wrap items-center gap-3 lg:justify-end">
                {utilityActions}
                <div className="min-w-0 rounded-[24px] border border-[var(--color-line)] bg-white px-4 py-3 text-right">
                  <div className="truncate text-sm font-semibold text-[var(--color-foreground)]">
                    {session.name}
                  </div>
                  <div className="truncate text-xs text-[var(--color-muted)]">
                    {session.email}
                  </div>
                </div>
                <button
                  className="inline-flex min-h-11 items-center whitespace-nowrap rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                  onClick={onLogout}
                  type="button"
                >
                  Sair
                </button>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 px-7 py-7 sm:px-8 lg:px-10">{children}</main>
      </div>
    </div>
  );
}
