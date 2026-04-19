"use client";

import Link from "next/link";
import { FormEvent, useEffect, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { BoundedList } from "@/components/bounded-list";
import { CollapsibleHelp } from "@/components/collapsible-help";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { PrimaryActionCard } from "@/components/primary-action-card";
import { SecondarySupportPanel } from "@/components/secondary-support-panel";
import {
  getFriendlyApiMessage,
  importTransactionsCsv,
  isUnauthorizedApiError,
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
  const activeAccounts = accounts.filter((account) => account.isActive);

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
        setAccountId(response.find((account) => account.isActive)?.id ?? "");
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel preparar a importacao agora. Tente novamente em alguns instantes.",
            ),
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
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setImportError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel importar o arquivo agora. Revise o CSV e tente novamente.",
        ),
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
      description="Escolha a conta, envie o arquivo e veja em seguida o que entrou."
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
          title="Nao foi possivel abrir a importacao"
        />
      ) : (
        <div className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.98fr)_minmax(320px,0.88fr)]">
          <div className="space-y-6">
            <PrimaryActionCard
              description="Escolha para qual conta o arquivo deve ir e envie o CSV. O Farol mostra depois o que entrou e o que precisa de revisao."
              eyebrow="Upload"
              title="Importar transacoes"
            >
              {accounts.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma conta financeira encontrada. Crie uma conta no dashboard
                  antes de importar. Se precisar, volte para{" "}
                  <Link
                    className="font-semibold text-[var(--color-accent)]"
                    href="/dashboard#quick-account"
                  >
                    Dashboard
                  </Link>
                  .
                </div>
              ) : activeAccounts.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Todas as suas contas estao inativas. Reative ao menos uma em{" "}
                  <Link
                    className="font-semibold text-[var(--color-accent)]"
                    href="/accounts"
                  >
                    Contas
                  </Link>{" "}
                  para importar novas transacoes.
                </div>
              ) : (
                <form className="space-y-4" onSubmit={handleSubmit}>
                  <div className="rounded-[24px] border border-[var(--color-line)] bg-white px-4 py-4 text-sm leading-6 text-[var(--color-muted)]">
                    Se a conta certa nao aparecer aqui, reative em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/accounts">
                      Contas
                    </Link>{" "}
                    e volte para importar.
                  </div>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Conta de destino</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) => setAccountId(event.target.value)}
                      value={accountId}
                    >
                      {activeAccounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.name}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Arquivo CSV</span>
                    <span className="text-xs leading-5 text-[var(--color-muted)]">
                      Use o arquivo exportado do seu banco ou da sua planilha.
                    </span>
                    <input
                      accept=".csv,text/csv"
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] file:mr-4 file:rounded-full file:border-0 file:bg-[var(--color-accent-soft)] file:px-3 file:py-2 file:text-sm file:font-medium file:text-[var(--color-foreground)]"
                      onChange={(event) => setFile(event.target.files?.[0] ?? null)}
                      type="file"
                    />
                  </label>

                  <button
                    className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                    disabled={isSubmitting}
                    type="submit"
                  >
                    {isSubmitting ? "Importando..." : "Importar arquivo"}
                  </button>
                </form>
              )}
            </PrimaryActionCard>
          </div>

          <div className="min-w-0 space-y-6">
            <SecondarySupportPanel
              description="Quando a importacao terminar, o resumo aparece aqui para voce entender rapidamente o que entrou e se algo precisa de ajuste."
              eyebrow="Resultado"
              title="Resumo da importacao"
            >
              {!result ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  O resumo aparece aqui logo depois do envio do arquivo.
                </div>
              ) : (
                <>
                  <div className="grid gap-4 sm:grid-cols-3">
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

                  <div className="mt-6">
                    {result.totalRows === 0 ? (
                      <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.16)] bg-[color:rgba(204,251,241,0.7)] px-5 py-4 text-sm text-[var(--color-foreground)]">
                        O arquivo foi recebido, mas veio sem movimentacoes.
                        Adicione linhas com dados reais e tente de novo.
                      </div>
                    ) : result.errors.length === 0 ? (
                      <div className="rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
                        Importacao concluida. Seus dados ja podem ser revisados.
                      </div>
                    ) : (
                      <BoundedList
                        hasItems={result.errors.length > 0}
                        maxHeightClassName="max-h-[16rem]"
                        testId="import-errors-list"
                      >
                        {result.errors.map((item) => (
                          <div
                            className="rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700"
                            key={`${item.rowNumber}-${item.message}`}
                          >
                            Linha {item.rowNumber}: {item.message}
                          </div>
                        ))}
                      </BoundedList>
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
            </SecondarySupportPanel>

            <CollapsibleHelp
              summary="Abra so se precisar de ajuda com o arquivo."
              title="Ajuda com o arquivo"
            >
              <div className="space-y-3">
                <p>
                  Se a importacao nao funcionar, confira se voce enviou um CSV exportado
                  do banco ou da sua planilha.
                </p>
                <p>
                  Quando houver linhas com problema, o Farol mostra abaixo quais pontos
                  precisam de revisao.
                </p>
                <p>
                  Se a conta certa nao aparecer para selecao, reative em{" "}
                  <Link className="font-semibold text-[var(--color-accent)]" href="/accounts">
                    Contas
                  </Link>
                  .
                </p>
              </div>
            </CollapsibleHelp>
          </div>
        </div>
      )}
    </AppShell>
  );
}
