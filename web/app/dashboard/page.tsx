"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  accountTypeOptions,
  ApiError,
  createAccount,
  getFreeMoney,
  getMonthlyBudget,
  getMonthlySummary,
  listAccounts,
  type AccountResponse,
  type FreeMoneyResponse,
  type MonthlyBudgetResponse,
  type MonthlySummaryResponse,
} from "@/lib/api";
import {
  formatCurrency,
  getCurrentMonthInputValue,
  parseMonthInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

type AccountFormState = {
  name: string;
  type: 1 | 2 | 3 | 4;
};

type DashboardData = {
  accounts: AccountResponse[];
  budget: MonthlyBudgetResponse;
  freeMoney: FreeMoneyResponse;
  summary: MonthlySummaryResponse;
};

const defaultAccountForm: AccountFormState = {
  name: "",
  type: 2,
};

export default function DashboardPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [data, setData] = useState<DashboardData | null>(null);
  const [error, setError] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isCreatingAccount, setIsCreatingAccount] = useState(false);
  const [accountForm, setAccountForm] = useState<AccountFormState>(defaultAccountForm);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );

  useEffect(() => {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    let isCancelled = false;

    async function load() {
      setIsFetching(true);
      setError("");

      try {
        const [summary, freeMoney, budget, accounts] = await Promise.all([
          getMonthlySummary(
            accessToken,
            monthAndYear.month,
            monthAndYear.year,
          ),
          getFreeMoney(accessToken, monthAndYear.month, monthAndYear.year),
          getMonthlyBudget(
            accessToken,
            monthAndYear.month,
            monthAndYear.year,
          ),
          listAccounts(accessToken),
        ]);

        if (!isCancelled) {
          setData({
            accounts,
            budget,
            freeMoney,
            summary,
          });
        }
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          logout();
          return;
        }

        if (!isCancelled) {
          setError(
            caughtError instanceof Error
              ? caughtError.message
              : "Nao foi possivel carregar o dashboard.",
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
  }, [logout, monthAndYear.month, monthAndYear.year, session]);

  async function handleCreateAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    setIsCreatingAccount(true);
    setError("");

    try {
      await createAccount(accessToken, {
        name: accountForm.name,
        type: accountForm.type,
      });

      const accounts = await listAccounts(accessToken);

      setData((current) =>
        current
          ? {
              ...current,
              accounts,
            }
          : current,
      );
      setAccountForm(defaultAccountForm);
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout();
        return;
      }

      setError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel criar a conta agora.",
      );
    } finally {
      setIsCreatingAccount(false);
    }
  }

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  return (
    <AppShell
      actions={
        <MonthPicker
          label="Mes de referencia"
          onChange={setMonthValue}
          value={monthValue}
        />
      }
      description="Acompanhe o mes com uma leitura rapida de receitas, despesas, orcamento e dinheiro livre."
      onLogout={logout}
      session={session}
      title="Dashboard financeiro"
    >
      {error ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      {isFetching || !data ? (
        <LoadingScreen message="Atualizando o resumo do mes..." />
      ) : (
        <div className="space-y-6">
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {[
              {
                label: "Receitas",
                value: formatCurrency(data.freeMoney.totalIncome),
                tone: "bg-[color:rgba(15,118,110,0.08)] text-[var(--color-accent)]",
              },
              {
                label: "Despesas",
                value: formatCurrency(data.freeMoney.totalExpense),
                tone: "bg-[color:rgba(209,123,15,0.12)] text-[var(--color-warm)]",
              },
              {
                label: "Saldo",
                value: formatCurrency(data.freeMoney.balance),
                tone: "bg-[color:rgba(17,37,51,0.08)] text-[var(--color-foreground)]",
              },
              {
                label: "Dinheiro livre",
                value: formatCurrency(data.freeMoney.freeToSpend),
                tone: "bg-[color:rgba(41,128,90,0.12)] text-[var(--color-success)]",
              },
            ].map((item) => (
              <article
                className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-5"
                key={item.label}
              >
                <div
                  className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${item.tone}`}
                >
                  {item.label}
                </div>
                <div className="mt-6 text-3xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  {item.value}
                </div>
              </article>
            ))}
          </section>

          <section className="grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    Orcamento do mes
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Reserva e execucao
                  </h2>
                </div>
                <div className="grid gap-3 md:grid-cols-3">
                  <div>
                    <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                      Planejado
                    </div>
                    <div className="mt-2 text-lg font-semibold text-[var(--color-foreground)]">
                      {formatCurrency(data.budget.totalPlanned)}
                    </div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                      Gasto
                    </div>
                    <div className="mt-2 text-lg font-semibold text-[var(--color-foreground)]">
                      {formatCurrency(data.budget.totalSpent)}
                    </div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                      Restante
                    </div>
                    <div className="mt-2 text-lg font-semibold text-[var(--color-foreground)]">
                      {formatCurrency(data.budget.totalRemaining)}
                    </div>
                  </div>
                </div>
              </div>

              <div className="mt-6 grid gap-3">
                {data.budget.categories.length === 0 ? (
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                    Ainda nao existe orcamento cadastrado para este mes.
                  </div>
                ) : (
                  data.budget.categories.map((item) => (
                    <div
                      className="grid gap-3 rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 md:grid-cols-[1.2fr_0.8fr_0.8fr_0.8fr]"
                      key={item.categoryId}
                    >
                      <div>
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          {item.categoryName}
                        </div>
                        <div className="mt-1 text-xs text-[var(--color-muted)]">
                          Categoria acompanhada no orcamento.
                        </div>
                      </div>
                      <div>
                        <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                          Planejado
                        </div>
                        <div className="mt-2 text-sm font-medium text-[var(--color-foreground)]">
                          {formatCurrency(item.planned)}
                        </div>
                      </div>
                      <div>
                        <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                          Gasto
                        </div>
                        <div className="mt-2 text-sm font-medium text-[var(--color-foreground)]">
                          {formatCurrency(item.spent)}
                        </div>
                      </div>
                      <div>
                        <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                          Restante
                        </div>
                        <div className="mt-2 text-sm font-medium text-[var(--color-foreground)]">
                          {formatCurrency(item.remaining)}
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </article>

            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Contas e preparacao
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Base para transacoes e importacao
              </h2>

              <div className="mt-6 space-y-3">
                {data.accounts.length === 0 ? (
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-5 text-sm text-[var(--color-muted)]">
                    Nenhuma conta encontrada. Crie a primeira conta abaixo para
                    liberar transacoes, orcamento e importacao CSV.
                  </div>
                ) : (
                  data.accounts.map((account) => (
                    <div
                      className="rounded-[22px] border border-[var(--color-line)] bg-white px-4 py-3"
                      key={account.id}
                    >
                      <div className="text-sm font-semibold text-[var(--color-foreground)]">
                        {account.name}
                      </div>
                      <div className="mt-1 text-xs text-[var(--color-muted)]">
                        {accountTypeOptions.find((item) => item.value === account.type)?.label ??
                          "Conta"}
                      </div>
                    </div>
                  ))
                )}
              </div>

              <form className="mt-6 space-y-4" onSubmit={handleCreateAccount}>
                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Nome da conta</span>
                  <input
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setAccountForm((current) => ({
                        ...current,
                        name: event.target.value,
                      }))
                    }
                    placeholder="Conta principal"
                    value={accountForm.name}
                  />
                </label>

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Tipo</span>
                  <select
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setAccountForm((current) => ({
                        ...current,
                        type: Number(event.target.value) as 1 | 2 | 3 | 4,
                      }))
                    }
                    value={accountForm.type}
                  >
                    {accountTypeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>

                <button
                  className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isCreatingAccount}
                  type="submit"
                >
                  {isCreatingAccount ? "Criando conta..." : "Criar conta rapida"}
                </button>
              </form>
            </article>
          </section>

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Leitura por categoria
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Onde o mes esta concentrado
            </h2>

            <div className="mt-6 grid gap-3 md:grid-cols-2">
              {data.summary.byCategory.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Ainda nao ha transacoes registradas neste mes.
                </div>
              ) : (
                data.summary.byCategory.map((item, index) => (
                  <div
                    className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                    key={`${item.categoryName}-${index}`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          {item.categoryName}
                        </div>
                        <div className="mt-1 text-xs text-[var(--color-muted)]">
                          {item.type === 1 ? "Receita" : "Despesa"}
                        </div>
                      </div>
                      <div className="text-sm font-semibold text-[var(--color-foreground)]">
                        {formatCurrency(item.total)}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </section>
        </div>
      )}
    </AppShell>
  );
}
