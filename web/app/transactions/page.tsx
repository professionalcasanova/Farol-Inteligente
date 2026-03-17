"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import {
  ApiError,
  createTransaction,
  listAccounts,
  listCategories,
  listTransactions,
  transactionTypeOptions,
  type AccountResponse,
  type CategoryResponse,
  type TransactionResponse,
  type TransactionType,
} from "@/lib/api";
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getCurrentDateInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

type TransactionFormState = {
  financialAccountId: string;
  categoryId: string;
  type: TransactionType;
  amount: string;
  description: string;
  occurredOn: string;
};

const defaultFormState: TransactionFormState = {
  financialAccountId: "",
  categoryId: "",
  type: 2,
  amount: "",
  description: "",
  occurredOn: getCurrentDateInputValue(),
};

export default function TransactionsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [categories, setCategories] = useState<CategoryResponse[]>([]);
  const [transactions, setTransactions] = useState<TransactionResponse[]>([]);
  const [form, setForm] = useState<TransactionFormState>(defaultFormState);
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const visibleCategories = useMemo(
    () => categories.filter((category) => category.type === form.type),
    [categories, form.type],
  );

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
        const [accountsResponse, categoriesResponse, transactionsResponse] =
          await Promise.all([
            listAccounts(accessToken),
            listCategories(accessToken),
            listTransactions(accessToken),
          ]);

        if (isCancelled) {
          return;
        }

        setAccounts(accountsResponse);
        setCategories(categoriesResponse);
        setTransactions(transactionsResponse);
        setForm((current) => ({
          ...current,
          financialAccountId:
            current.financialAccountId || accountsResponse[0]?.id || "",
        }));
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          logout();
          return;
        }

        if (!isCancelled) {
          setLoadError(
            caughtError instanceof Error
              ? caughtError.message
              : "Nao foi possivel carregar as transacoes.",
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

  useEffect(() => {
    if (
      form.categoryId &&
      !visibleCategories.some((category) => category.id === form.categoryId)
    ) {
      setForm((current) => ({
        ...current,
        categoryId: "",
      }));
    }
  }, [form.categoryId, visibleCategories]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      await createTransaction(accessToken, {
        financialAccountId: form.financialAccountId,
        categoryId: form.categoryId || undefined,
        type: form.type,
        amount: Number(form.amount),
        description: form.description,
        occurredOn: form.occurredOn,
      });

      const transactionsResponse = await listTransactions(accessToken);
      setTransactions(transactionsResponse);
      setSuccess("Transacao criada com sucesso.");
      setForm((current) => ({
        ...defaultFormState,
        financialAccountId: current.financialAccountId,
        occurredOn: getCurrentDateInputValue(),
      }));
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout();
        return;
      }

      setFormError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel criar a transacao.",
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
      description="Registre entradas e saidas sem sair do MVP. A lista usa exatamente os dados ja persistidos pela API."
      onLogout={logout}
      session={session}
      title="Transacoes"
    >
      {formError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {formError}
        </div>
      ) : null}

      {success ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
          {success}
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando contas, categorias e transacoes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar as transacoes"
        />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Nova transacao
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Lancamento rapido
            </h2>

            {accounts.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                Voce precisa de ao menos uma conta financeira. Crie a primeira no
                dashboard e volte aqui.
              </div>
            ) : (
              <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Conta financeira</span>
                  <select
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        financialAccountId: event.target.value,
                      }))
                    }
                    value={form.financialAccountId}
                  >
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </label>

                <div className="grid gap-4 md:grid-cols-2">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Tipo</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          type: Number(event.target.value) as TransactionType,
                        }))
                      }
                      value={form.type}
                    >
                      {transactionTypeOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Valor</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      min="0.01"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          amount: event.target.value,
                        }))
                      }
                      placeholder="120.50"
                      step="0.01"
                      type="number"
                      value={form.amount}
                    />
                  </label>
                </div>

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Descricao</span>
                  <input
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        description: event.target.value,
                      }))
                    }
                    placeholder="Mercado, salario, almoco..."
                    value={form.description}
                  />
                </label>

                <div className="grid gap-4 md:grid-cols-2">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Data</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          occurredOn: event.target.value,
                        }))
                      }
                      type="date"
                      value={form.occurredOn}
                    />
                  </label>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Categoria</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          categoryId: event.target.value,
                        }))
                      }
                      value={form.categoryId}
                    >
                      <option value="">Sem categoria</option>
                      {visibleCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>

                <button
                  className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {isSubmitting ? "Salvando..." : "Criar transacao"}
                </button>
              </form>
            )}
          </section>

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Historico
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Transacoes registradas
                </h2>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                {transactions.length} itens
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {transactions.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma transacao encontrada. O primeiro lancamento ja atualiza
                  o dashboard e o insight de dinheiro livre.
                </div>
              ) : (
                transactions.map((transaction) => {
                  const accountName =
                    accounts.find(
                      (account) => account.id === transaction.financialAccountId,
                    )?.name ?? "Conta";
                  const categoryName =
                    categories.find((category) => category.id === transaction.categoryId)
                      ?.name ?? "Sem categoria";

                  return (
                    <article
                      className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                      key={transaction.id}
                    >
                      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                        <div>
                          <div className="text-sm font-semibold text-[var(--color-foreground)]">
                            {transaction.description}
                          </div>
                          <div className="mt-2 flex flex-wrap gap-2 text-xs text-[var(--color-muted)]">
                            <span>{accountName}</span>
                            <span>|</span>
                            <span>{categoryName}</span>
                            <span>|</span>
                            <span>{formatDate(transaction.occurredOn)}</span>
                          </div>
                        </div>
                        <div className="text-right">
                          <div
                            className={`text-base font-semibold ${
                              transaction.type === 1
                                ? "text-[var(--color-success)]"
                                : "text-[var(--color-foreground)]"
                            }`}
                          >
                            {transaction.type === 1 ? "+" : "-"}
                            {formatCurrency(transaction.amount)}
                          </div>
                          <div className="mt-2 text-xs text-[var(--color-muted)]">
                            criado em {formatDateTime(transaction.createdAtUtc)}
                          </div>
                        </div>
                      </div>
                    </article>
                  );
                })
              )}
            </div>
          </section>
        </div>
      )}
    </AppShell>
  );
}
