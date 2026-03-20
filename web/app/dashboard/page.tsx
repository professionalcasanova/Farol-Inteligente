"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  accountTypeOptions,
  createAccount,
  getAlerts,
  getBillsSummary,
  getFriendlyApiMessage,
  getFreeMoney,
  getMonthlyBudget,
  getMonthlySummary,
  isUnauthorizedApiError,
  listBills,
  listAccounts,
  listTransactions,
  type AccountResponse,
  type AlertsResponse,
  type BillsSummaryResponse,
  type FreeMoneyResponse,
  type MonthlyBudgetResponse,
  type MonthlySummaryResponse,
} from "@/lib/api";
import {
  formatDate,
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
  alerts: AlertsResponse;
  billsSummary: BillsSummaryResponse;
  budget: MonthlyBudgetResponse;
  freeMoney: FreeMoneyResponse;
  onboarding: {
    hasAccount: boolean;
    hasBill: boolean;
    hasTransaction: boolean;
  };
  summary: MonthlySummaryResponse;
};

const defaultAccountForm: AccountFormState = {
  name: "",
  type: 2,
};

const alertSeverityLabels = {
  high: "Alto",
  medium: "Medio",
} as const;

const alertSeverityStyles = {
  high: "bg-[color:rgba(185,28,28,0.1)] text-red-700 border-[color:rgba(185,28,28,0.14)]",
  medium:
    "bg-[color:rgba(217,119,6,0.12)] text-[var(--color-warm)] border-[color:rgba(217,119,6,0.14)]",
} as const;

const checklistItemStyles = {
  completed:
    "border-[color:rgba(29,130,93,0.14)] bg-[color:rgba(220,252,231,0.76)]",
  pending: "border-[var(--color-line)] bg-white",
} as const;

export default function DashboardPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [data, setData] = useState<DashboardData | null>(null);
  const [loadError, setLoadError] = useState("");
  const [accountError, setAccountError] = useState("");
  const [accountSuccess, setAccountSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isCreatingAccount, setIsCreatingAccount] = useState(false);
  const [accountForm, setAccountForm] = useState<AccountFormState>(defaultAccountForm);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );

  const checklistItems = data
    ? [
        {
          completed: data.onboarding.hasAccount,
          description: "Crie a base para organizar entradas, saidas e importacoes.",
          href: "/dashboard#quick-account",
          label: "Criar sua primeira conta",
        },
        {
          completed: data.onboarding.hasTransaction,
          description: "Registre uma entrada para liberar saldo, resumo mensal e dinheiro livre.",
          href: "/transactions",
          label: "Registrar uma entrada (salario)",
        },
        {
          completed: data.onboarding.hasBill,
          description: "Adicione um vencimento para ativar bills, agenda e alertas.",
          href: "/bills",
          label: "Adicionar uma conta a pagar",
        },
      ]
    : [];

  const completedChecklistItemsCount = checklistItems.filter(
    (item) => item.completed,
  ).length;
  const shouldShowOnboarding = checklistItems.some((item) => !item.completed);

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
        const [
          summary,
          alerts,
          billsSummary,
          freeMoney,
          budget,
          accounts,
          transactions,
          bills,
        ] = await Promise.all([
          getMonthlySummary(
            accessToken,
            monthAndYear.month,
            monthAndYear.year,
          ),
          getAlerts(accessToken, monthAndYear.month, monthAndYear.year),
          getBillsSummary(accessToken, monthAndYear.month, monthAndYear.year),
          getFreeMoney(accessToken, monthAndYear.month, monthAndYear.year),
          getMonthlyBudget(
            accessToken,
            monthAndYear.month,
            monthAndYear.year,
          ),
          listAccounts(accessToken),
          listTransactions(accessToken),
          listBills(accessToken),
        ]);

        if (!isCancelled) {
          setLoadError("");
          setData({
            accounts,
            alerts,
            billsSummary,
            budget,
            freeMoney,
            onboarding: {
              hasAccount: accounts.length > 0,
              hasBill: bills.length > 0,
              hasTransaction: transactions.length > 0,
            },
            summary,
          });
        }
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel carregar o dashboard agora. Confira se a API local esta ativa e tente novamente.",
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
  }, [logout, monthAndYear.month, monthAndYear.year, reloadKey, session]);

  async function handleCreateAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    setIsCreatingAccount(true);
    setAccountError("");
    setAccountSuccess("");

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
              onboarding: {
                ...current.onboarding,
                hasAccount: accounts.length > 0,
              },
            }
          : current,
      );
      setAccountForm(defaultAccountForm);
      setAccountSuccess(
        "Conta criada com sucesso. Agora voce ja pode registrar transacoes, bills ou importar um CSV.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setAccountError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel criar a conta agora. Revise os dados e tente novamente.",
          {
            messageMap: {
              "Name is required.": "Informe o nome da conta para continuar.",
              "Financial account name is required.":
                "Informe o nome da conta para continuar.",
              "Financial account name cannot exceed 120 characters.":
                "O nome da conta ficou longo demais. Tente um nome menor.",
              "Financial account type is invalid.":
                "Selecione um tipo de conta valido.",
            },
          },
        ),
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
      {accountSuccess ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
          <div>{accountSuccess}</div>
          <div className="mt-3 flex flex-wrap gap-3">
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/transactions"
            >
              Registrar transacao
            </Link>
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/imports"
            >
              Importar CSV
            </Link>
          </div>
        </div>
      ) : null}

      {accountError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {accountError}
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Atualizando o resumo do mes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel abrir o dashboard"
        />
      ) : !data ? (
        <LoadErrorState
          message="O dashboard nao retornou dados para este mes."
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Dashboard indisponivel"
        />
      ) : (
        <div className="space-y-6">
          {shouldShowOnboarding ? (
            <section className="rounded-[28px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    Comece por aqui
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Primeiro valor em poucos passos
                  </h2>
                  <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
                    O checklist some automaticamente quando voce conclui os tres passos principais.
                  </p>
                </div>
                <div className="text-sm font-medium text-[var(--color-foreground)]">
                  {completedChecklistItemsCount} de 3 concluidos
                </div>
              </div>

              <div className="mt-6 grid gap-3 lg:grid-cols-3">
                {checklistItems.map((item) => (
                  <Link
                    className={`rounded-[24px] border px-5 py-4 transition hover:border-[var(--color-accent)] ${item.completed ? checklistItemStyles.completed : checklistItemStyles.pending}`}
                    href={item.href}
                    key={item.label}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          [{item.completed ? "x" : " "}] {item.label}
                        </div>
                        <div className="mt-2 text-sm leading-6 text-[var(--color-muted)]">
                          {item.description}
                        </div>
                      </div>
                      <div className="shrink-0 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                        {item.completed ? "Concluido" : "Abrir"}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          ) : null}

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Acoes rapidas
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Proximo passo da demonstracao
                </h2>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                Escolha um fluxo e volte para acompanhar o impacto no dashboard.
              </div>
            </div>

            <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {[
                {
                  href: "/transactions",
                  label: "Nova transacao",
                  description: "Registre receita ou despesa e atualize o saldo na hora.",
                },
                {
                  href: "/bills",
                  label: "Nova bill",
                  description: "Cadastre vencimentos e veja alertas aparecerem no mes.",
                },
                {
                  href: "/budget",
                  label: "Montar orcamento",
                  description: "Defina limites por categoria e acompanhe o restante.",
                },
                {
                  href: "/imports",
                  label: "Importar CSV",
                  description: "Puxe varias transacoes de uma vez para acelerar a demo.",
                },
              ].map((item) => (
                <Link
                  className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 transition hover:border-[var(--color-accent)]"
                  href={item.href}
                  key={item.href}
                >
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    {item.label}
                  </div>
                  <div className="mt-2 text-sm leading-6 text-[var(--color-muted)]">
                    {item.description}
                  </div>
                </Link>
              ))}
            </div>
          </section>

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Alertas do mes
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              O que merece atencao agora
            </h2>

            <div className="mt-6 grid gap-3">
              {data.alerts.alerts.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhum alerta importante para este mes no momento. Se quiser
                  avancar a demo agora, registre uma transacao, monte um
                  orcamento ou adicione uma bill.
                </div>
              ) : (
                data.alerts.alerts.map((alert, index) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                    key={`${alert.type}-${index}`}
                  >
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div>
                        <div className="flex flex-wrap items-center gap-3">
                          <span
                            className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[alert.severity]}`}
                          >
                            Prioridade {alertSeverityLabels[alert.severity]}
                          </span>
                        </div>
                        <div className="mt-3 text-sm font-semibold text-[var(--color-foreground)]">
                          {alert.message}
                        </div>
                      </div>
                      <div className="flex flex-col items-start gap-3 md:items-end">
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(alert.amount)}
                        </div>
                        {alert.actionUrl ? (
                          <Link
                            className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
                            href={alert.actionUrl}
                          >
                            Resolver agora
                          </Link>
                        ) : null}
                      </div>
                    </div>
                  </article>
                ))
              )}
            </div>
          </section>

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
                    Ainda nao existe orcamento cadastrado para este mes. Monte
                    seu primeiro planejamento em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/budget">
                      Orcamento
                    </Link>{" "}
                    e depois volte aqui para acompanhar o restante.
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

            <article
              className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6"
              id="quick-account"
            >
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
                    liberar transacoes, bills, orcamento e importacao CSV.
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

          <section className="grid gap-6 xl:grid-cols-[0.92fr_1.08fr]">
            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Bills do mes
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Vencimentos em destaque
              </h2>

              <div className="mt-6 grid gap-4 md:grid-cols-3">
                {[
                  {
                    label: "A pagar",
                    total: data.billsSummary.totalPending,
                    count: data.billsSummary.countPending,
                    tone: "bg-[color:rgba(217,119,6,0.12)] text-[var(--color-warm)]",
                  },
                  {
                    label: "Vencido",
                    total: data.billsSummary.totalOverdue,
                    count: data.billsSummary.countOverdue,
                    tone: "bg-[color:rgba(185,28,28,0.1)] text-red-700",
                  },
                  {
                    label: "Pago",
                    total: data.billsSummary.totalPaid,
                    count: data.billsSummary.countPaid,
                    tone: "bg-[color:rgba(29,130,93,0.12)] text-[var(--color-success)]",
                  },
                ].map((item) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white p-4"
                    key={item.label}
                  >
                    <div className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${item.tone}`}>
                      {item.label}
                    </div>
                    <div className="mt-4 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                      {formatCurrency(item.total)}
                    </div>
                    <div className="mt-2 text-xs text-[var(--color-muted)]">
                      {item.count} {item.count === 1 ? "conta" : "contas"}
                    </div>
                  </article>
                ))}
              </div>
            </article>

            <article className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Proximas contas
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Ate 5 vencimentos pendentes
              </h2>

              <div className="mt-6 space-y-3">
                {data.billsSummary.upcoming.length === 0 ? (
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                    Nenhuma conta pendente para este mes. Se quiser demonstrar
                    vencimentos e alertas, crie uma nova bill em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/bills">
                      Bills
                    </Link>
                    .
                  </div>
                ) : (
                  data.billsSummary.upcoming.map((bill) => (
                    <div
                      className="flex flex-col gap-3 rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 md:flex-row md:items-center md:justify-between"
                      key={bill.id}
                    >
                      <div>
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          {bill.description}
                        </div>
                        <div className="mt-1 text-xs text-[var(--color-muted)]">
                          Vence em {formatDate(bill.dueOn)}
                        </div>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="rounded-full border border-[color:rgba(217,119,6,0.14)] bg-[color:rgba(217,119,6,0.12)] px-3 py-1 text-xs font-semibold text-[var(--color-warm)]">
                          Pendente
                        </span>
                        <div className="text-sm font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(bill.amount)}
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </div>
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
                  Ainda nao ha transacoes registradas neste mes. Crie uma
                  receita ou despesa em{" "}
                  <Link className="font-semibold text-[var(--color-accent)]" href="/transactions">
                    Transacoes
                  </Link>{" "}
                  para alimentar o dashboard.
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
