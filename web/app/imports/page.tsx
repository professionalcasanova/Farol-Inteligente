"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import {
  ApiError,
  importTransactionsCsv,
  listAccounts,
  type AccountResponse,
  type ImportTransactionsCsvResponse,
} from "@/lib/api";
import { useProtectedSession } from "@/lib/use-protected-session";

export default function ImportsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [accountId, setAccountId] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportTransactionsCsvResponse | null>(null);
  const [loadError, setLoadError] = useState("");
  const [importError, setImportError] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    let isCancelled = false;

    async function load() {
      setIsFetching(true);
      setLoadError("");

      try {
        const response = await listAccounts(accessToken);

        if (isCancelled) {
          return;
        }

        setAccounts(response);
        setAccountId(response[0]?.id ?? "");
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            caughtError instanceof Error
              ? caughtError.message
              : "Nao foi possivel carregar as contas.",
          );
        }
      } finally {
        if (!isCancelled) {
          setIsFetching(false);
        }
      }
    }

    void load();

    return () => {
      isCancelled = true;
    };
  }, [logout, reloadKey, session]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session || !file) {
      setImportError("Selecione um arquivo CSV antes de importar.");
      return;
    }

    if (!accountId) {
      setImportError("Selecione uma conta financeira para a importacao.");
      return;
    }

    const accessToken = session.accessToken;
    setIsSubmitting(true);
    setImportError("");

    try {
      const response = await importTransactionsCsv(accessToken, {
        financialAccountId: accountId,
        file,
      });

      setResult(response);
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout("session-expired");
        return;
      }

      setImportError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel importar o arquivo.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  return (
    <AppShell
      actions={
        <Link
          className="rounded-full border border-[var(--color-line)] px-4 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
          href="/dashboard"
        >
          Voltar ao dashboard
        </Link>
      }
      description="Envie um CSV simples para uma conta existente e veja na hora quantas linhas entraram, quantas foram ignoradas e por que."
      onLogout={logout}
      session={session}
      title="Importacao CSV"
    >
      {importError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {importError}
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando contas para importacao..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar a importacao"
        />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Upload
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Importar transacoes
            </h2>

            {accounts.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                Nenhuma conta financeira encontrada. Crie uma conta no dashboard
                antes de importar. Se precisar, volte para{" "}
                <Link className="font-semibold text-[var(--color-accent)]" href="/dashboard#quick-account">
                  Dashboard
                </Link>
                .
              </div>
            ) : (
              <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Conta de destino</span>
                  <select
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) => setAccountId(event.target.value)}
                    value={accountId}
                  >
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Arquivo CSV</span>
                  <input
                    accept=".csv,text/csv"
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] file:mr-4 file:rounded-full file:border-0 file:bg-[var(--color-accent-soft)] file:px-3 file:py-2 file:text-sm file:font-medium file:text-[var(--color-foreground)]"
                    onChange={(event) =>
                      setFile(event.target.files?.[0] ?? null)
                    }
                    type="file"
                  />
                </label>

                <button
                  className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {isSubmitting ? "Importando..." : "Enviar CSV"}
                </button>
              </form>
            )}
          </section>

          <section className="space-y-6">
            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Formato esperado
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                CSV simples e explicito
              </h2>
              <pre className="mt-6 overflow-x-auto rounded-[24px] border border-[var(--color-line)] bg-white p-4 text-sm leading-7 text-[var(--color-foreground)]">
occurredOn,description,amount,type,categoryName
2026-03-01,Salario,3000.00,Income,Salario
2026-03-02,Mercado,120.50,Expense,Alimentacao
2026-03-03,Uber viagem,42.00,Expense,
              </pre>
            </article>

            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Resultado
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Resumo da importacao
              </h2>

              {!result ? (
                <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  O resumo aparece aqui depois do primeiro upload. Assim que a
                  importacao terminar, voce ja pode revisar as transacoes e
                  voltar ao dashboard.
                </div>
              ) : (
                <>
                  <div className="mt-6 grid gap-4 md:grid-cols-3">
                    <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                      <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                        Linhas totais
                      </div>
                      <div className="mt-3 text-xl font-semibold text-[var(--color-foreground)]">
                        {result.totalRows}
                      </div>
                    </article>
                    <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                      <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                        Importadas
                      </div>
                      <div className="mt-3 text-xl font-semibold text-[var(--color-success)]">
                        {result.importedRows}
                      </div>
                    </article>
                    <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                      <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                        Ignoradas
                      </div>
                      <div className="mt-3 text-xl font-semibold text-[var(--color-warm)]">
                        {result.skippedRows}
                      </div>
                    </article>
                  </div>

                  <div className="mt-6 space-y-3">
                    {result.errors.length === 0 ? (
                      <div className="rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
                        Nenhum erro encontrado no arquivo enviado.
                      </div>
                    ) : (
                      result.errors.map((item) => (
                        <div
                          className="rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700"
                          key={`${item.rowNumber}-${item.message}`}
                        >
                          Linha {item.rowNumber}: {item.message}
                        </div>
                      ))
                    )}
                  </div>

                  <div className="mt-6 flex flex-wrap gap-3">
                    <Link
                      className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                      href="/transactions"
                    >
                      Revisar transacoes
                    </Link>
                    <Link
                      className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                      href="/dashboard"
                    >
                      Voltar ao dashboard
                    </Link>
                  </div>
                </>
              )}
            </article>
          </section>
        </div>
      )}
    </AppShell>
  );
}
