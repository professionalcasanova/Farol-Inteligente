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
  financialAccountId: string;
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
  financialAccountId: "",
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

function getComparisonBarWidth(value: number, max: number) {
  if (value <= 0 || max <= 0) {
    return "0%";
  }

  return `${Math.max(10, Math.min(100, (value / max) * 100))}%`;
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
  const [showQuickEntryDetails, setShowQuickEntryDetails] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );
  const firstUseState = data ? isFirstUseState(data) : false;
  const activationMessage = data ? getActivationMessage(data) : null;
  const visibleQuickEntryCategories = useMemo(
    () =>
      (data?.categories ?? []).filter(
        (category) => category.type === quickEntryForm.type,
      ),
    [data?.categories, quickEntryForm.type],
  );
  const quickEntrySubmitLabel =
    quickEntryForm.type === 1 ? "Registrar entrada" : "Registrar saída";
  const visibleInsightCount = data?.monthHealth?.insights.length ?? 0;
  const fallbackAlertCount = data?.alerts.alerts.length ?? 0;
  const notificationCount = visibleInsightCount || fallbackAlertCount;
  const monthlyFlowMax = data
    ? Math.max(1, data.freeMoney.totalIncome, data.freeMoney.totalExpense)
    : 1;

  const isCriticalHealth = data?.monthHealth?.status === "critical";
  const criticalReasons = data?.monthHealth
    ? data.monthHealth.reasons?.slice(0, 2) ??
      data.monthHealth.insights?.slice(0, 2).map((insight) => insight.cause) ??
      []
    : [];
  const criticalActions = data?.monthHealth
    ? data.monthHealth.actions?.slice(0, 2) ??
      [data.monthHealth.summary?.action].filter(Boolean)
    : [];
  const budgetFlowMax = data
    ? Math.max(1, data.budget.totalPlanned, data.budget.totalSpent)
    : 1;

  useEffect(() => {
    const accounts = data?.accounts ?? [];

    setQuickEntryForm((current) => {
      const hasSelectedAccount = accounts.some(
        (account) => account.id === current.financialAccountId,
      );

      if (accounts.length === 0) {
        return current.financialAccountId
          ? { ...current, financialAccountId: "" }
          : current;
      }

      if (hasSelectedAccount) {
        return current;
      }

      if (accounts.length === 1) {
        return {
          ...current,
          financialAccountId: accounts[0].id,
        };
      }

      return current.financialAccountId
        ? { ...current, financialAccountId: "" }
        : current;
    });
  }, [data?.accounts]);

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
              "Nao foi possivel carregar o dashboard agora. Tente novamente em alguns instantes.",
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

    if (!quickEntryForm.financialAccountId) {
      setQuickEntryError(
        "Escolha a conta em que essa movimentação deve ser registrada.",
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
        financialAccountId: quickEntryForm.financialAccountId,
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
      setQuickEntryForm((current) => ({
        ...defaultQuickEntryForm,
        financialAccountId: current.financialAccountId,
      }));
      setShowQuickEntryDetails(false);
      setQuickEntrySuccess("Registrado.");
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
        <div className="flex flex-wrap items-center gap-3">
          <Link
            aria-label="Ver notificações do mês"
            className="relative inline-flex min-h-11 min-w-11 items-center justify-center rounded-full border border-[var(--color-line)] bg-white px-3 py-2 text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
            href="/bills"
          >
            <svg
              aria-hidden="true"
              className="h-5 w-5"
              fill="none"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="1.8"
              viewBox="0 0 24 24"
            >
              <path d="M15 17h5l-1.4-1.4A2 2 0 0 1 18 14.2V11a6 6 0 1 0-12 0v3.2a2 2 0 0 1-.6 1.4L4 17h5" />
              <path d="M10 17a2 2 0 0 0 4 0" />
            </svg>
            {notificationCount > 0 ? (
              <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-[var(--color-foreground)] px-1 text-[11px] font-semibold text-white">
                {notificationCount > 9 ? "9+" : notificationCount}
              </span>
            ) : null}
          </Link>

          <MonthPicker
            label="Mês de referência"
            onChange={setMonthValue}
            value={monthValue}
          />
        </div>
      }
      description="Acompanhe o mês, veja o que pede atenção e registre o que entrou ou saiu sem sair da página."
      onLogout={logout}
      session={session}
      title="Visão do Mês"
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
          <section
            className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6"
            id="quick-account"
          >
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Ação rápida
                </div>
                <h2 className="mt-3 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                  O que você fez hoje com seu dinheiro?
                </h2>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
                  Digite o valor, diga se entrou ou saiu e registre na hora.
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
              <div className="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.76fr)]">
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                  Você ainda não tem uma conta cadastrada. Crie a primeira aqui
                  para começar a registrar o que entrou ou saiu sem sair da
                  página.
                </div>

                <form className="space-y-4 rounded-[24px] border border-[var(--color-line)] bg-white p-5" onSubmit={handleCreateAccount}>
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Criar primeira conta
                  </div>

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
                    {isCreatingAccount ? "Criando conta..." : "Criar conta"}
                  </button>
                </form>
              </div>
            ) : (
              <form className="mt-6 space-y-5" onSubmit={handleQuickEntrySubmit}>
                <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)]">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Conta financeira</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-4 text-base font-medium text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)] disabled:cursor-not-allowed disabled:bg-[var(--color-panel)]"
                      disabled={data.accounts.length === 1}
                      onChange={(event) =>
                        setQuickEntryForm((current) => ({
                          ...current,
                          financialAccountId: event.target.value,
                        }))
                      }
                      value={quickEntryForm.financialAccountId}
                    >
                      {data.accounts.length > 1 ? (
                        <option value="">Escolha a conta para registrar</option>
                      ) : null}
                      {data.accounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.name}
                        </option>
                      ))}
                    </select>
                  </label>

                  <div className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 text-sm leading-6 text-[var(--color-muted)]">
                    {data.accounts.length === 1
                      ? "Essa movimentação será registrada na sua conta disponível."
                      : "Escolha explicitamente a conta para evitar lançar a movimentação no lugar errado."}
                  </div>
                </div>

                <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_auto] xl:items-end">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Quanto foi?</span>
                    <input
                      autoFocus
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
                      {isRegisteringNow ? "Registrando..." : quickEntrySubmitLabel}
                    </button>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="text-sm font-medium text-[var(--color-foreground)]">
                      Categoria
                    </div>
                    <div className="text-xs text-[var(--color-muted)]">
                      As opções mudam quando você troca entre entrada e saída.
                    </div>
                  </div>
                  <div className="flex gap-2 overflow-x-auto pb-2">
                    <button
                      className={`shrink-0 rounded-full border px-4 py-2 text-sm font-medium transition ${quickEntryForm.categoryId === "" ? "border-[var(--color-foreground)] bg-[var(--color-foreground)] text-white" : "border-[var(--color-line)] bg-white text-[var(--color-foreground)] hover:bg-[var(--color-accent-soft)]"}`}
                      onClick={() =>
                        setQuickEntryForm((current) => ({
                          ...current,
                          categoryId: "",
                        }))
                      }
                      type="button"
                    >
                      Sem categoria
                    </button>
                    {visibleQuickEntryCategories.map((category) => {
                      const isSelected = quickEntryForm.categoryId === category.id;

                      return (
                        <button
                          className={`shrink-0 rounded-full border px-4 py-2 text-sm font-medium transition ${isSelected ? "border-[var(--color-foreground)] bg-[var(--color-foreground)] text-white" : "border-[var(--color-line)] bg-white text-[var(--color-foreground)] hover:bg-[var(--color-accent-soft)]"}`}
                          key={category.id}
                          onClick={() =>
                            setQuickEntryForm((current) => ({
                              ...current,
                              categoryId: category.id,
                            }))
                          }
                          type="button"
                        >
                          {category.name}
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div>
                  <button
                    aria-expanded={showQuickEntryDetails}
                    className="text-sm font-medium text-[var(--color-muted)] transition hover:text-[var(--color-foreground)]"
                    onClick={() => setShowQuickEntryDetails((current) => !current)}
                    type="button"
                  >
                    {showQuickEntryDetails
                      ? "Esconder descrição"
                      : "Adicionar descrição"}
                  </button>
                </div>

                {showQuickEntryDetails ? (
                  <div className="rounded-[24px] border border-[var(--color-line)] bg-[color:rgba(255,255,255,0.68)] p-4">
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
                  </div>
                ) : null}
              </form>
            )}
          </section>

          {data.monthHealth ? (
            <>
              {isCriticalHealth ? (
                <section className="rounded-[28px] border border-[color:rgba(185,28,28,0.3)] bg-[color:rgba(254,226,226,0.8)] p-6">
                  <div className="text-sm font-semibold uppercase tracking-[0.18em] text-red-700">
                    Risco financeiro crítico
                  </div>
                  <div className="mt-3 text-xl font-bold text-red-800">
                    {data.monthHealth.message ?? data.monthHealth.summary.message}
                  </div>
                  {criticalReasons.length > 0 ? (
                    <ul className="mt-3 list-disc pl-5 text-sm text-[var(--color-foreground)]">
                      {criticalReasons.map((reason, index) => (
                        <li key={`reason-${index}`}>{reason}</li>
                      ))}
                    </ul>
                  ) : null}
                  {criticalActions.length > 0 ? (
                    <div className="mt-3 text-sm text-[var(--color-muted)]">
                      {criticalActions.slice(0, 2).map((action, index) => (
                        <p key={`action-${index}`} className="mt-1">
                          • {action}
                        </p>
                      ))}
                    </div>
                  ) : null}
                  <div className="mt-4">
                    <Link
                      href="/bills"
                      className="rounded-xl bg-red-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-red-800"
                    >
                      Ver contas a pagar
                    </Link>
                  </div>
                </section>
              ) : null}

              <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
                <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1.18fr)_minmax(320px,0.82fr)]">
                  <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                      {firstUseState ? "Primeiro uso" : "Visão do mês"}
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
                            href="/dashboard#quick-account"
                          >
                            Registrar agora
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
          </>
          ) : null}

          <section className="space-y-8">
            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Resumo financeiro
              </div>
              <div className="mt-4 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <h2 className="text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                    Base do mês
                  </h2>
                  <p className="mt-3 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                    {getFinancialSupportText(data)}
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
                    detail: "O quanto ainda sobra sem furar o que já foi planejado.",
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

              <div className="mt-6 grid gap-4 xl:grid-cols-2">
                <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-5">
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Entradas x saídas
                  </div>
                  <div className="mt-4 space-y-4">
                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Entradas</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.freeMoney.totalIncome)}
                        </span>
                      </div>
                      <div className="mt-2 h-3 rounded-full bg-[color:rgba(15,118,110,0.08)]">
                        <div
                          className="h-3 rounded-full bg-[var(--color-accent)]"
                          style={{
                            width: getComparisonBarWidth(
                              data.freeMoney.totalIncome,
                              monthlyFlowMax,
                            ),
                          }}
                        />
                      </div>
                    </div>

                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Saídas</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.freeMoney.totalExpense)}
                        </span>
                      </div>
                      <div className="mt-2 h-3 rounded-full bg-[color:rgba(217,119,6,0.12)]">
                        <div
                          className="h-3 rounded-full bg-[var(--color-warm)]"
                          style={{
                            width: getComparisonBarWidth(
                              data.freeMoney.totalExpense,
                              monthlyFlowMax,
                            ),
                          }}
                        />
                      </div>
                    </div>
                  </div>
                </article>

                <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-5">
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Planejado x gasto
                  </div>
                  <div className="mt-4 space-y-4">
                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Planejado</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.budget.totalPlanned)}
                        </span>
                      </div>
                      <div className="mt-2 h-3 rounded-full bg-[color:rgba(15,118,110,0.08)]">
                        <div
                          className="h-3 rounded-full bg-[var(--color-accent)]"
                          style={{
                            width: getComparisonBarWidth(
                              data.budget.totalPlanned,
                              budgetFlowMax,
                            ),
                          }}
                        />
                      </div>
                    </div>

                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Gasto</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.budget.totalSpent)}
                        </span>
                      </div>
                      <div className="mt-2 h-3 rounded-full bg-[color:rgba(217,119,6,0.12)]">
                        <div
                          className="h-3 rounded-full bg-[var(--color-warm)]"
                          style={{
                            width: getComparisonBarWidth(
                              data.budget.totalSpent,
                              budgetFlowMax,
                            ),
                          }}
                        />
                      </div>
                    </div>
                  </div>
                </article>
              </div>
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
