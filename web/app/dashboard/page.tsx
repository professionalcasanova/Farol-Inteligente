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
  createTransaction,
  getAlerts,
  getBillsSummary,
  getFriendlyApiMessage,
  getFreeMoney,
  getMonthHealth,
  getMonthlyBudget,
  getMonthlySummary,
  isUnauthorizedApiError,
  listBills,
  listAccounts,
  listCategories,
  listTransactions,
  type AccountResponse,
  type AlertsResponse,
  type BillsSummaryResponse,
  type CategoryResponse,
  type FreeMoneyResponse,
  type MonthHealthResponse,
  type MonthlyBudgetResponse,
  type MonthlySummaryResponse,
  type TransactionType,
} from "@/lib/api";
import {
  formatDate,
  formatCurrency,
  getCurrentDateInputValue,
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
  categories: CategoryResponse[];
  freeMoney: FreeMoneyResponse;
  monthHealth: MonthHealthResponse | null;
  onboarding: {
    hasAccount: boolean;
    hasBill: boolean;
    hasTransaction: boolean;
  };
  summary: MonthlySummaryResponse;
};

type QuickEntryFormState = {
  amount: string;
  type: TransactionType;
  description: string;
  categoryId: string;
};

const defaultAccountForm: AccountFormState = {
  name: "",
  type: 2,
};

const defaultQuickEntryForm: QuickEntryFormState = {
  amount: "",
  type: 2,
  description: "",
  categoryId: "",
};

const quickEntryTypeOptions: Array<{
  value: TransactionType;
  label: string;
}> = [
  { value: 1, label: "Entrou dinheiro" },
  { value: 2, label: "Saiu dinheiro" },
];

const quickEntryMessageMap = {
  "Financial account was not found.": "A conta usada para registrar agora não foi encontrada.",
  "Category was not found.": "A categoria escolhida não foi encontrada.",
  "Transaction amount must be greater than zero.":
    "Informe um valor maior que zero para registrar agora.",
  "Transaction description is required.":
    "Não deu para entender essa movimentação. Tente descrever em poucas palavras.",
} as const;

const alertSeverityLabels = {
  high: "Alto",
  medium: "Médio",
} as const;

const alertSeverityStyles = {
  high: "bg-[color:rgba(185,28,28,0.1)] text-red-700 border-[color:rgba(185,28,28,0.14)]",
  medium:
    "bg-[color:rgba(217,119,6,0.12)] text-[var(--color-warm)] border-[color:rgba(217,119,6,0.14)]",
} as const;

const monthHealthStatusLabels = {
  healthy: "Saudável",
  attention: "Atenção",
  critical: "Crítico",
} as const;

const monthHealthStatusStyles = {
  healthy:
    "border-[color:rgba(29,130,93,0.18)] bg-[color:rgba(220,252,231,0.82)] text-[var(--color-success)]",
  attention:
    "border-[color:rgba(217,119,6,0.16)] bg-[color:rgba(255,247,237,0.9)] text-[var(--color-warm)]",
  critical:
    "border-[color:rgba(185,28,28,0.16)] bg-[color:rgba(254,226,226,0.82)] text-red-700",
} as const;

const checklistItemStyles = {
  completed:
    "border-[color:rgba(29,130,93,0.14)] bg-[color:rgba(220,252,231,0.76)]",
  pending: "border-[var(--color-line)] bg-white",
} as const;

function getDashboardHeadline(data: DashboardData) {
  if (data.billsSummary.countOverdue > 0) {
    return "Seu mês pede atenção imediata.";
  }

  if (data.alerts.alerts.length > 0) {
    return "Seu mês está sob controle, mas com pontos de atenção.";
  }

  if (
    data.summary.totalIncome === 0 &&
    data.summary.totalExpense === 0 &&
    data.billsSummary.countPending === 0 &&
    data.budget.totalPlanned === 0
  ) {
    return "Seu mês ainda está começando.";
  }

  return "Seu mês está organizado até aqui.";
}

function getDashboardSupportText(data: DashboardData) {
  if (data.billsSummary.countOverdue > 0) {
    return `Você tem ${data.billsSummary.countOverdue} conta${data.billsSummary.countOverdue === 1 ? "" : "s"} vencida${data.billsSummary.countOverdue === 1 ? "" : "s"} somando ${formatCurrency(data.billsSummary.totalOverdue)}. Vale resolver isso antes de olhar o restante.`;
  }

  if (
    data.summary.totalIncome === 0 &&
    data.summary.totalExpense === 0 &&
    data.billsSummary.countPending === 0 &&
    data.budget.totalPlanned === 0
  ) {
    return "Registre uma movimentação, monte um planejamento ou adicione uma conta a pagar para começar a leitura do mês.";
  }

  return `Seu saldo está em ${formatCurrency(data.summary.balance)} e o dinheiro livre em ${formatCurrency(data.freeMoney.freeToSpend)}. Esse valor já considera o que ainda ficou reservado no planejamento do mês. A partir daqui, os alertas e vencimentos mostram onde agir primeiro.`;
}

function getFinancialSupportText(data: DashboardData) {
  if (
    data.summary.totalIncome === 0 &&
    data.summary.totalExpense === 0 &&
    data.billsSummary.countPending === 0 &&
    data.budget.totalPlanned === 0
  ) {
    return "Este painel mostra a base do mês. Conforme você registrar movimentações, vencimentos e planejamento, a leitura fica mais precisa.";
  }

  return "Entradas, saídas, saldo e dinheiro livre ajudam a confirmar o contexto do mês antes de agir sobre planejamento e vencimentos. Aqui, o dinheiro livre já considera o que ainda ficou reservado no planejamento.";
}

function isFirstUseState(data: DashboardData) {
  return (
    data.summary.totalIncome === 0 &&
    data.summary.totalExpense === 0 &&
    data.billsSummary.countPending === 0 &&
    data.billsSummary.countOverdue === 0 &&
    data.budget.totalPlanned === 0 &&
    data.accounts.length <= 1 &&
    !data.onboarding.hasTransaction &&
    !data.onboarding.hasBill
  );
}

function getActivationMessage(data: DashboardData) {
  if (!data.onboarding.hasAccount) {
    return {
      message: "Comece criando sua primeira conta no Farol.",
      cause:
        "Sem uma conta financeira, ainda não dá para registrar movimentações, importar um arquivo ou acompanhar seu mês.",
      action:
        "Crie uma conta abaixo e depois registre uma entrada ou importe seus primeiros dados para liberar a leitura do mês.",
    };
  }

  return {
    message: "Seu mês ainda não tem dados suficientes.",
    cause:
      "Você já tem conta, mas ainda faltam movimentações ou vencimentos para o Farol montar seu primeiro estado do mês.",
    action:
      "Registre uma entrada agora ou importe um arquivo para chegar ao primeiro insight do mês.",
  };
}

export default function DashboardPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [data, setData] = useState<DashboardData | null>(null);
  const [loadError, setLoadError] = useState("");
  const [accountError, setAccountError] = useState("");
  const [accountSuccess, setAccountSuccess] = useState("");
  const [quickEntryError, setQuickEntryError] = useState("");
  const [quickEntrySuccess, setQuickEntrySuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isCreatingAccount, setIsCreatingAccount] = useState(false);
  const [isRegisteringNow, setIsRegisteringNow] = useState(false);
  const [accountForm, setAccountForm] = useState<AccountFormState>(defaultAccountForm);
  const [quickEntryForm, setQuickEntryForm] =
    useState<QuickEntryFormState>(defaultQuickEntryForm);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );

  const checklistItems = data
    ? [
        {
          completed: data.onboarding.hasAccount,
          description: "Crie a base para organizar entradas, saídas e importações.",
          href: "/dashboard#quick-account",
          label: "Criar sua primeira conta",
        },
        {
          completed: data.onboarding.hasTransaction,
          description: "Registre uma entrada para liberar saldo, resumo mensal e dinheiro livre.",
          href: "/transactions",
          label: "Registrar uma entrada (salário)",
        },
        {
          completed: data.onboarding.hasBill,
          description: "Adicione um vencimento para ativar contas a pagar, agenda e alertas.",
          href: "/bills",
          label: "Adicionar uma conta a pagar",
        },
      ]
    : [];

  const completedChecklistItemsCount = checklistItems.filter(
    (item) => item.completed,
  ).length;
  const shouldShowOnboarding = checklistItems.some((item) => !item.completed);
  const firstUseState = data ? isFirstUseState(data) : false;
  const activationMessage = data ? getActivationMessage(data) : null;
  const visibleQuickEntryCategories = useMemo(
    () =>
      (data?.categories ?? []).filter(
        (category) => category.type === quickEntryForm.type,
      ),
    [data?.categories, quickEntryForm.type],
  );
  const quickActionItems = [
    {
      href: "/transactions",
      label: "Registrar movimentação",
      description: "Atualize saldo, categorias e dinheiro livre na hora.",
    },
    {
      href: "/bills",
      label: "Criar conta a pagar",
      description: "Cadastre vencimentos e acompanhe alertas do mês.",
    },
    {
      href: "/budget",
      label: "Montar planejamento",
      description: "Defina limites por categoria e acompanhe o restante.",
    },
    {
      href: "/imports",
      label: "Importar dados de arquivo",
      description: "Traga várias movimentações de uma vez para acelerar o uso.",
    },
  ];

  useEffect(() => {
    if (
      quickEntryForm.categoryId &&
      !visibleQuickEntryCategories.some(
        (category) => category.id === quickEntryForm.categoryId,
      )
    ) {
      setQuickEntryForm((current) => ({
        ...current,
        categoryId: "",
      }));
    }
  }, [quickEntryForm.categoryId, visibleQuickEntryCategories]);

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
        const monthHealthPromise = getMonthHealth(
          accessToken,
          monthAndYear.month,
          monthAndYear.year,
        ).catch((caughtError) => {
          if (isUnauthorizedApiError(caughtError)) {
            throw caughtError;
          }

          return null;
        });
        const categoriesPromise = listCategories(accessToken).catch(
          (caughtError) => {
            if (isUnauthorizedApiError(caughtError)) {
              throw caughtError;
            }

            return [];
          },
        );

        const [
          monthHealth,
          summary,
          alerts,
          billsSummary,
          categories,
          freeMoney,
          budget,
          accounts,
          transactions,
          bills,
        ] = await Promise.all([
          monthHealthPromise,
          getMonthlySummary(
            accessToken,
            monthAndYear.month,
            monthAndYear.year,
          ),
          getAlerts(accessToken, monthAndYear.month, monthAndYear.year),
          getBillsSummary(accessToken, monthAndYear.month, monthAndYear.year),
          categoriesPromise,
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
            categories,
            freeMoney,
            monthHealth,
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
              "Não foi possível carregar o dashboard agora. Confira se a API local está ativa e tente novamente.",
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
        "Conta criada com sucesso. Agora você já pode registrar uma movimentação ou importar um arquivo.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setAccountError(
        getFriendlyApiMessage(
          caughtError,
          "Não foi possível criar a conta agora. Revise os dados e tente novamente.",
          {
            messageMap: {
              "Name is required.": "Informe o nome da conta para continuar.",
              "Financial account name is required.":
                "Informe o nome da conta para continuar.",
              "Financial account name cannot exceed 120 characters.":
                "O nome da conta ficou longo demais. Tente um nome menor.",
              "Financial account type is invalid.":
                "Selecione um tipo de conta válido.",
            },
          },
        ),
      );
    } finally {
      setIsCreatingAccount(false);
    }
  }

  async function handleQuickEntrySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session || !data) {
      return;
    }

    if (data.accounts.length === 0) {
      setQuickEntryError(
        "Crie sua primeira conta abaixo para começar a registrar movimentações.",
      );
      return;
    }

    const amount = Number(quickEntryForm.amount.replace(",", "."));

    if (!Number.isFinite(amount) || amount <= 0) {
      setQuickEntryError("Informe o valor para registrar agora.");
      return;
    }

    const accessToken = session.accessToken;
    setIsRegisteringNow(true);
    setQuickEntryError("");
    setQuickEntrySuccess("");

    try {
      await createTransaction(accessToken, {
        financialAccountId: data.accounts[0].id,
        categoryId: quickEntryForm.categoryId || undefined,
        type: quickEntryForm.type,
        amount,
        description:
          quickEntryForm.description.trim() ||
          (quickEntryForm.type === 1 ? "Entrada rápida" : "Saída rápida"),
        occurredOn: getCurrentDateInputValue(),
      });

      const monthHealthPromise = getMonthHealth(
        accessToken,
        monthAndYear.month,
        monthAndYear.year,
      ).catch((caughtError) => {
        if (isUnauthorizedApiError(caughtError)) {
          throw caughtError;
        }

        return null;
      });

      const [monthHealth, summary, alerts, billsSummary, freeMoney, budget, transactions, bills] =
        await Promise.all([
          monthHealthPromise,
          getMonthlySummary(accessToken, monthAndYear.month, monthAndYear.year),
          getAlerts(accessToken, monthAndYear.month, monthAndYear.year),
          getBillsSummary(accessToken, monthAndYear.month, monthAndYear.year),
          getFreeMoney(accessToken, monthAndYear.month, monthAndYear.year),
          getMonthlyBudget(accessToken, monthAndYear.month, monthAndYear.year),
          listTransactions(accessToken),
          listBills(accessToken),
        ]);

      setData((current) =>
        current
          ? {
              ...current,
              alerts,
              billsSummary,
              budget,
              freeMoney,
              monthHealth,
              onboarding: {
                hasAccount: current.accounts.length > 0,
                hasBill: bills.length > 0,
                hasTransaction: transactions.length > 0,
              },
              summary,
            }
          : current,
      );
      setQuickEntryForm(defaultQuickEntryForm);
      setQuickEntrySuccess("Registrado 👍");
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setQuickEntryError(
        getFriendlyApiMessage(
          caughtError,
          "Não foi possível registrar agora. Tente novamente em instantes.",
          { messageMap: quickEntryMessageMap },
        ),
      );
    } finally {
      setIsRegisteringNow(false);
    }
  }

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  return (
    <AppShell
      actions={
        <MonthPicker
          label="Mês de referência"
          onChange={setMonthValue}
          value={monthValue}
        />
      }
      description="Acompanhe o mês com uma leitura rápida de entradas, saídas, planejamento e dinheiro livre."
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
              Registrar movimentação
            </Link>
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/imports"
            >
              Importar dados de arquivo
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
        <LoadingScreen message="Atualizando o resumo do mês..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Não foi possível abrir o dashboard"
        />
      ) : !data ? (
        <LoadErrorState
          message="O dashboard não retornou dados para este mês."
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Dashboard indisponível"
        />
      ) : (
        <div className="space-y-8">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Ação rápida
                </div>
                <h2 className="mt-3 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                  O que você fez hoje com seu dinheiro?
                </h2>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
                  Registre uma movimentação em poucos segundos, sem sair do dashboard.
                </p>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                Ou, se preferir,{" "}
                <Link
                  className="font-semibold text-[var(--color-foreground)] transition hover:text-[var(--color-accent)]"
                  href="/imports"
                >
                  importar dados de arquivo
                </Link>
                .
              </div>
            </div>

            {quickEntryError ? (
              <div className="mt-5 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
                {quickEntryError}
              </div>
            ) : null}

            {quickEntrySuccess ? (
              <div className="mt-5 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
                {quickEntrySuccess}
              </div>
            ) : null}

            {data.accounts.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                Você ainda não tem uma conta cadastrada. Crie a primeira em{" "}
                <Link
                  className="font-semibold text-[var(--color-accent)]"
                  href="/dashboard#quick-account"
                >
                  Contas e preparação
                </Link>{" "}
                para começar a registrar suas movimentações.
              </div>
            ) : (
              <form className="mt-6 space-y-4" onSubmit={handleQuickEntrySubmit}>
                <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_auto] xl:items-end">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Quanto foi?</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-4 text-2xl font-semibold text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      inputMode="decimal"
                      min="0.01"
                      onChange={(event) =>
                        setQuickEntryForm((current) => ({
                          ...current,
                          amount: event.target.value,
                        }))
                      }
                      placeholder="120"
                      step="0.01"
                      type="number"
                      value={quickEntryForm.amount}
                    />
                  </label>

                  <div className="grid gap-3 sm:grid-cols-[auto_auto] xl:self-end">
                    <div
                      aria-label="Tipo da movimentação"
                      className="inline-flex rounded-2xl border border-[var(--color-line)] bg-white p-1"
                      role="group"
                    >
                      {quickEntryTypeOptions.map((option) => {
                        const isSelected = quickEntryForm.type === option.value;

                        return (
                          <button
                            aria-pressed={isSelected}
                            className={`rounded-[18px] px-4 py-3 text-sm font-semibold transition ${isSelected ? "bg-[var(--color-foreground)] text-white" : "text-[var(--color-muted)] hover:text-[var(--color-foreground)]"}`}
                            key={option.value}
                            onClick={() =>
                              setQuickEntryForm((current) => ({
                                ...current,
                                type: option.value,
                              }))
                            }
                            type="button"
                          >
                            {option.label}
                          </button>
                        );
                      })}
                    </div>

                    <button
                      className="w-full rounded-2xl bg-[var(--color-foreground)] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                      disabled={isRegisteringNow}
                      type="submit"
                    >
                      {isRegisteringNow ? "Registrando..." : "Registrar agora"}
                    </button>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(220px,0.8fr)]">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Descrição (se quiser)</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setQuickEntryForm((current) => ({
                          ...current,
                          description: event.target.value,
                        }))
                      }
                      placeholder="Mercado, salário, almoço..."
                      value={quickEntryForm.description}
                    />
                  </label>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Categoria (se quiser)</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setQuickEntryForm((current) => ({
                          ...current,
                          categoryId: event.target.value,
                        }))
                      }
                      value={quickEntryForm.categoryId}
                    >
                      <option value="">Deixar sem categoria</option>
                      {visibleQuickEntryCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>

                <div className="text-sm text-[var(--color-muted)]">
                  Se quiser, registre só o valor e toque em{" "}
                  <span className="font-semibold text-[var(--color-foreground)]">
                    Registrar agora
                  </span>
                  .
                </div>
              </form>
            )}
          </section>

          {data.monthHealth ? (
            <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1.18fr)_minmax(320px,0.82fr)]">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                      {firstUseState ? "Primeiro uso" : "Inteligência do mês"}
                    </div>
                    <span
                      className={`rounded-full border px-3 py-1 text-xs font-semibold ${monthHealthStatusStyles[data.monthHealth.status]}`}
                    >
                      {monthHealthStatusLabels[data.monthHealth.status]}
                    </span>
                  </div>

                  <h2 className="mt-4 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                    {firstUseState && activationMessage
                      ? activationMessage.message
                      : data.monthHealth.summary.message}
                  </h2>
                  <p className="mt-3 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                    {firstUseState && activationMessage
                      ? activationMessage.cause
                      : data.monthHealth.summary.cause}
                  </p>
                </div>

                <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-5">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                    O que fazer agora
                  </div>
                  <div className="mt-3 text-sm leading-6 text-[var(--color-foreground)]">
                    {firstUseState && activationMessage
                      ? activationMessage.action
                      : data.monthHealth.summary.action}
                  </div>
                  {firstUseState ? (
                    <div className="mt-4 flex flex-wrap gap-3">
                      {!data.onboarding.hasAccount ? (
                        <Link
                          className="rounded-full border border-[var(--color-line)] bg-white px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                          href="/dashboard#quick-account"
                        >
                          Criar primeira conta
                        </Link>
                      ) : (
                        <>
                          <Link
                            className="rounded-full border border-[var(--color-line)] bg-white px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                            href="/transactions"
                          >
                            Registrar entrada
                          </Link>
                          <Link
                            className="rounded-full border border-[var(--color-line)] bg-white px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                            href="/imports"
                          >
                            Importar dados de arquivo
                          </Link>
                        </>
                      )}
                    </div>
                  ) : null}
                </div>
              </div>

              {data.monthHealth.insights.length > 0 && !firstUseState ? (
                <div className="mt-6 grid gap-3 md:grid-cols-3">
                  {data.monthHealth.insights.map((insight) => (
                    <article
                      className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                      key={insight.type}
                    >
                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <span
                          className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[insight.severity]}`}
                        >
                          Prioridade {alertSeverityLabels[insight.severity]}
                        </span>
                        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-muted)]">
                          #{insight.priority}
                        </div>
                      </div>
                      <div className="mt-4 text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                        {insight.message}
                      </div>
                      <div className="mt-2 text-sm leading-6 text-[var(--color-muted)]">
                        {insight.action}
                      </div>
                    </article>
                  ))}
                </div>
              ) : null}
            </section>
          ) : null}

          <section
            className={
              data.monthHealth
                ? "space-y-8"
                : "grid items-start gap-8 xl:grid-cols-[minmax(0,1.16fr)_minmax(320px,0.84fr)]"
            }
          >
            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                {data.monthHealth ? "Números de apoio" : "Como está seu mês"}
              </div>
              <div className="mt-4 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <h2 className="text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                    {data.monthHealth
                      ? "Base financeira do mês"
                      : getDashboardHeadline(data)}
                  </h2>
                  <p className="mt-3 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                    {data.monthHealth
                      ? getFinancialSupportText(data)
                      : getDashboardSupportText(data)}
                  </p>
                </div>
                <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-4 text-sm text-[var(--color-foreground)]">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                    Mês em foco
                  </div>
                  <div className="mt-2 font-semibold">
                    {monthAndYear.month.toString().padStart(2, "0")}/{monthAndYear.year}
                  </div>
                </div>
              </div>

              <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {[
                  {
                    label: "Entradas",
                    value: formatCurrency(data.freeMoney.totalIncome),
                    detail: "Dinheiro que entrou no mês.",
                    tone: "bg-[color:rgba(15,118,110,0.08)] text-[var(--color-accent)]",
                  },
                  {
                    label: "Saídas",
                    value: formatCurrency(data.freeMoney.totalExpense),
                    detail: "Dinheiro que já saiu no mês.",
                    tone: "bg-[color:rgba(209,123,15,0.12)] text-[var(--color-warm)]",
                  },
                  {
                    label: "Saldo",
                    value: formatCurrency(data.freeMoney.balance),
                    detail: "Resultado entre entradas e saídas.",
                    tone: "bg-[color:rgba(17,37,51,0.08)] text-[var(--color-foreground)]",
                  },
                  {
                    label: "Dinheiro livre",
                    value: formatCurrency(data.freeMoney.freeToSpend),
                    detail: "O quanto ainda sobra depois do planejado.",
                    tone: "bg-[color:rgba(41,128,90,0.12)] text-[var(--color-success)]",
                  },
                ].map((item) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white p-5"
                    key={item.label}
                  >
                    <div
                      className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${item.tone}`}
                    >
                      {item.label}
                    </div>
                    <div className="mt-5 text-3xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                      {item.value}
                    </div>
                    <div className="mt-2 text-xs leading-5 text-[var(--color-muted)]">
                      {item.detail}
                    </div>
                  </article>
                ))}
              </div>
            </article>

            {!data.monthHealth ? (
              <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Mais crítico agora
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Alertas do mês
                </h2>

                <div className="mt-6 grid gap-3">
                  {data.alerts.alerts.length === 0 ? (
                    <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                      Nenhum alerta importante para este mês no momento. Se quiser avançar o uso agora, registre uma movimentação, monte um planejamento ou adicione uma conta a pagar.
                    </div>
                  ) : (
                    data.alerts.alerts.map((alert, index) => (
                      <article
                        className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                        key={`${alert.type}-${index}`}
                      >
                        <div className="flex flex-col gap-3">
                          <div className="flex flex-wrap items-center justify-between gap-3">
                            <span
                              className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[alert.severity]}`}
                            >
                              Prioridade {alertSeverityLabels[alert.severity]}
                            </span>
                            <div className="text-sm font-semibold text-[var(--color-foreground)]">
                              {formatCurrency(alert.amount)}
                            </div>
                          </div>
                          <div className="text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                            {alert.message}
                          </div>
                          {alert.actionUrl ? (
                            <div>
                              <Link
                                className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
                                href={alert.actionUrl}
                              >
                                Resolver agora
                              </Link>
                            </div>
                          ) : null}
                        </div>
                      </article>
                    ))
                  )}
                </div>
              </article>
            ) : null}
          </section>
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
                    O checklist some automaticamente quando você conclui os três passos principais.
                  </p>
                </div>
                <div className="text-sm font-medium text-[var(--color-foreground)]">
                  {completedChecklistItemsCount} de 3 concluídos
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
                        {item.completed ? "Concluído" : "Abrir"}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          ) : null}

          {!shouldShowOnboarding ? (
            <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    O que fazer em seguida
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Escolha o próximo ajuste do mês
                  </h2>
                </div>
                <div className="text-sm text-[var(--color-muted)]">
                  Use estes atalhos quando quiser agir rápido sem perder o contexto do dashboard.
                </div>
              </div>

              <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                {quickActionItems.map((item) => (
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
          ) : null}
          <section className="grid items-start gap-8 xl:grid-cols-[minmax(0,1.28fr)_minmax(320px,0.72fr)]">
            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    Planejamento do mês
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Reserva e execução
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
                    Ainda não existe planejamento cadastrado para este mês. Monte
                    seu primeiro planejamento em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/budget">
                      Planejamento
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
                          Categoria acompanhada no planejamento.
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
              className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6"
              id="quick-account"
            >
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Contas e preparação
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Base para movimentações e importação
              </h2>

              <div className="mt-6 space-y-3">
                {data.accounts.length === 0 ? (
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-5 text-sm text-[var(--color-muted)]">
                    Nenhuma conta encontrada. Crie a primeira conta abaixo para
                    liberar movimentações, contas a pagar, planejamento e importação de arquivo.
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
                  {isCreatingAccount ? "Criando conta..." : "Criar conta rápida"}
                </button>
              </form>
            </article>
          </section>

          <section className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.94fr)_minmax(0,1.06fr)]">
            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Contas a pagar do mês
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

            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Próximas contas
              </div>
              <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Até 5 vencimentos pendentes
              </h2>

              <div className="mt-6 space-y-3">
                {data.billsSummary.upcoming.length === 0 ? (
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                    Nenhuma conta pendente para este mês. Se quiser demonstrar
                    vencimentos e alertas, crie uma nova conta a pagar em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/bills">
                      Contas a pagar
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
              Onde o mês está concentrado
            </h2>

            <div className="mt-6 grid gap-3 md:grid-cols-2">
              {data.summary.byCategory.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Ainda não há movimentações registradas neste mês. Crie uma
                  entrada ou saída em{" "}
                  <Link className="font-semibold text-[var(--color-accent)]" href="/transactions">
                    Movimentações
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
                          {item.type === 1 ? "Entrada" : "Saída"}
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
