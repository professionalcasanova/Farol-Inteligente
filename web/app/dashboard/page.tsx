"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { loadDashboardData, refreshDashboardData } from "./_lib/dashboard-data";
import type { DashboardData, QuickEntryFormState } from "./_lib/dashboard-types";
import {
  buildNotificationItems,
  getActivationMessage,
  getComparisonBarWidth,
  getCriticalPriorityReason,
  getCriticalSupportMessage,
  getFinancialSupportText,
  getPredictableBillLabel,
  getProjectionSupportMessage,
  isActionNavigableFromDashboard,
  isFirstUseState,
} from "./_lib/dashboard-view-model";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  createTransaction,
  getFriendlyApiMessage,
  isUnauthorizedApiError,
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

const dashboardStandardGridClass =
  "grid items-start gap-6 xl:grid-cols-[minmax(0,1.12fr)_minmax(320px,0.88fr)]";
const dashboardHeroGridClass =
  "grid items-start gap-5 xl:grid-cols-[minmax(0,1.72fr)_minmax(360px,1.08fr)]";
const dashboardSectionStackClass = "space-y-6";

export default function DashboardPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [data, setData] = useState<DashboardData | null>(null);
  const [loadError, setLoadError] = useState("");
  const [quickEntryError, setQuickEntryError] = useState("");
  const [quickEntrySuccess, setQuickEntrySuccess] = useState("");
  const [quickEntryCreatedTransactionId, setQuickEntryCreatedTransactionId] = useState<
    string | null
  >(null);
  const [isFetching, setIsFetching] = useState(true);
  const [isRegisteringNow, setIsRegisteringNow] = useState(false);
  const [quickEntryForm, setQuickEntryForm] =
    useState<QuickEntryFormState>(defaultQuickEntryForm);
  const [showQuickEntryDetails, setShowQuickEntryDetails] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );
  const activeAccounts = useMemo(
    () => data?.accounts.filter((account) => account.isActive) ?? [],
    [data?.accounts],
  );
  const firstUseState = data ? isFirstUseState(data) : false;
  const monthHealth = data?.monthHealth;
  const showSecondarySections = data ? !firstUseState || !data.monthHealth : false;
  const activationMessage = data ? getActivationMessage(data) : null;
  const prioritizedRecommendedActions = data?.monthHealth?.recommendedActions?.slice(0, 3) ?? [];
  const visibleQuickEntryCategories = useMemo(
    () =>
      (data?.categories ?? []).filter(
        (category) => category.type === quickEntryForm.type,
      ),
    [data?.categories, quickEntryForm.type],
  );
  const quickEntrySubmitLabel =
    quickEntryForm.type === 1 ? "Registrar entrada" : "Registrar saída";
  const monthlyFlowMax = data
    ? Math.max(1, data.freeMoney.totalIncome, data.freeMoney.totalExpense)
    : 1;
  const freeMoneyReserveRows = data
    ? [
        {
          label: "Reservado no planejamento",
          value: data.freeMoney.plannedReserve,
        },
        {
          label: "Em contas em aberto",
          value: data.freeMoney.unpaidBillsReserve,
        },
      ].filter((item) => item.value > 0)
    : [];
  const freeMoneyReserveNote = data
    ? data.freeMoney.predictableObligationsReserve > 0
      ? `Desse total em contas abertas, ${formatCurrency(data.freeMoney.predictableObligationsReserve)} ja vem de contas recorrentes e parcelas.`
      : data.freeMoney.unpaidBillsReserve > 0
      ? "Esse valor já desconta as contas em aberto do mês."
      : data.freeMoney.plannedReserve > 0
        ? "Esse valor já desconta o que segue reservado no planejamento."
        : ""
    : "";
  const monthReserveTotal = data
    ? data.freeMoney.plannedReserve + data.freeMoney.unpaidBillsReserve
    : 0;
  const monthStateCards = data
    ? [
        {
          label: "Saldo realizado",
          value: formatCurrency(data.freeMoney.balance),
          detail: "Entradas menos saídas já registradas no mês.",
          tone: "bg-[color:rgba(17,37,51,0.08)] text-[var(--color-foreground)]",
        },
        {
          label: "Dinheiro livre",
          value: formatCurrency(data.freeMoney.freeToSpend),
          detail: "O que ainda sobra depois do planejamento e das contas em aberto do mês.",
          tone: "bg-[color:rgba(41,128,90,0.12)] text-[var(--color-success)]",
          composition: freeMoneyReserveRows,
          note: freeMoneyReserveNote,
        },
        {
          label: "Reservado",
          value: formatCurrency(monthReserveTotal),
          detail: "Planejamento restante e contas abertas que ainda consomem o mês.",
          tone: "bg-[color:rgba(15,118,110,0.08)] text-[var(--color-accent)]",
        },
      ]
    : [];

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
  const primaryRecommendedAction =
    prioritizedRecommendedActions.find((action) =>
      isActionNavigableFromDashboard(action.target),
    ) ?? prioritizedRecommendedActions[0] ?? null;
  const visibleAlertHighlights = data?.alerts.alerts.slice(0, 3) ?? [];
  const showFallbackAlertHighlights = !data?.monthHealth && visibleAlertHighlights.length > 0;
  const criticalPriorityReason = getCriticalPriorityReason(data?.monthHealth);
  const criticalSupportMessage = getCriticalSupportMessage(data?.monthHealth);
  const dashboardFocusHref = isCriticalHealth
    ? "#foco-do-mes"
    : showFallbackAlertHighlights
      ? "#alertas-do-mes"
      : "#resumo-financeiro";
  const notificationItems = buildNotificationItems(
    data?.monthHealth,
    data?.alerts.alerts ?? [],
    dashboardFocusHref,
  );
  const notificationCount = notificationItems.length;
  const budgetFlowMax = data
    ? Math.max(1, data.budget.totalPlanned, data.budget.totalSpent)
    : 1;
  const projectionSupportMessage = data ? getProjectionSupportMessage(data) : "";

  useEffect(() => {
    const accounts = activeAccounts;

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
  }, [activeAccounts]);

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
        const nextData = await loadDashboardData(accessToken, monthAndYear);

        if (!isCancelled) {
          setLoadError("");
          setData(nextData);
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
  }, [logout, monthAndYear, reloadKey, session]);

  useEffect(() => {
    setIsNotificationsOpen(false);
  }, [monthValue, reloadKey]);

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

    if (activeAccounts.length === 0) {
      setQuickEntryError(
        "Reative ao menos uma conta em Contas para registrar novas movimentações.",
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
    setQuickEntryCreatedTransactionId(null);

    try {
      const createdTransaction = await createTransaction(accessToken, {
        financialAccountId: quickEntryForm.financialAccountId,
        categoryId: quickEntryForm.categoryId || undefined,
        type: quickEntryForm.type,
        amount,
        description:
          quickEntryForm.description.trim() ||
          (quickEntryForm.type === 1 ? "Entrada rápida" : "Saída rápida"),
        occurredOn: getCurrentDateInputValue(),
      });

      const nextData = await refreshDashboardData(accessToken, monthAndYear, data);

      setData(nextData);
      setQuickEntryForm((current) => ({
        ...defaultQuickEntryForm,
        financialAccountId: current.financialAccountId,
      }));
      setShowQuickEntryDetails(false);
      setQuickEntryCreatedTransactionId(createdTransaction.id);
      setQuickEntrySuccess(
        "Lançamento registrado. Se precisar corrigir conta, valor, data, categoria ou excluir, abra esta movimentação.",
      );
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
        <div className="flex w-full flex-wrap items-center gap-3">
          <MonthPicker
            label="Mês de referência"
            onChange={setMonthValue}
            value={monthValue}
          />
        </div>
      }
      utilityActions={
        <div className="relative flex items-center">
          <button
            aria-expanded={isNotificationsOpen}
            aria-haspopup="dialog"
            aria-label={
              notificationCount > 0
                ? `Abrir focos do mes (${notificationCount})`
                : "Abrir focos do mes"
            }
            className="relative inline-flex min-h-11 min-w-11 items-center justify-center rounded-full border border-[var(--color-line)] bg-white px-3 py-2 text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
            onClick={() => setIsNotificationsOpen((current) => !current)}
            type="button"
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
          </button>

          {isNotificationsOpen ? (
            <div
              aria-label="Focos do mes"
              className="fixed left-3 right-3 top-24 z-20 max-h-[min(70vh,32rem)] overflow-y-auto rounded-[24px] border border-[var(--color-line)] bg-white p-4 shadow-[0_24px_60px_rgba(17,37,51,0.12)] sm:absolute sm:left-auto sm:right-0 sm:top-14 sm:w-[min(26rem,calc(100vw-2rem))]"
              role="dialog"
            >
              <div className="flex items-center justify-between gap-3 border-b border-[var(--color-line)] pb-3">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                    Focos do mes
                  </div>
                  <div className="mt-1 text-sm text-[var(--color-muted)]">
                    Veja o que pede atencao e siga para a acao certa.
                  </div>
                </div>
                <button
                  aria-label="Fechar focos do mes"
                  className="rounded-full border border-[var(--color-line)] px-3 py-2 text-xs font-semibold text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                  onClick={() => setIsNotificationsOpen(false)}
                  type="button"
                >
                  Fechar
                </button>
              </div>

              {notificationItems.length === 0 ? (
                <div className="mt-4 rounded-[20px] border border-dashed border-[var(--color-line)] px-4 py-5 text-sm text-[var(--color-muted)]">
                  Nenhum foco ativo no momento.
                </div>
              ) : (
                <div className="mt-4 space-y-3">
                  {notificationItems.map((item) => (
                    <Link
                      aria-label={`Abrir foco do mes: ${item.message}`}
                      className="block rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] px-4 py-4 transition hover:border-[var(--color-accent)] hover:bg-white"
                      href={item.href}
                      key={item.id}
                      onClick={() => setIsNotificationsOpen(false)}
                    >
                      <div className="flex items-center justify-between gap-3">
                        <span
                          className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[item.severity]}`}
                        >
                          {item.title}
                        </span>
                        <span className="text-xs font-medium text-[var(--color-muted)]">
                          Abrir
                        </span>
                      </div>
                      <div className="mt-3 text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                        {item.message}
                      </div>
                    </Link>
                  ))}
                </div>
              )}
            </div>
          ) : null}
        </div>
      }
      description="Acompanhe o mês, veja o que pede atenção e registre o que entrou ou saiu sem sair da página."
      onLogout={logout}
      session={session}
      title="Visão do Mês"
    >
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
          <section className={dashboardHeroGridClass} data-testid="dashboard-hero-grid">
            <article
              className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-5 xl:p-6"
              id="quick-account"
            >
            <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Ação rápida
                </div>
                <h2 className="mt-2 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                  O que você fez hoje com seu dinheiro?
                </h2>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
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
              <div className="mt-4 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
                {quickEntryError}
              </div>
            ) : null}

            {quickEntrySuccess ? (
              <div className="mt-4 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
                <div>{quickEntrySuccess}</div>
                {quickEntryCreatedTransactionId ? (
                  <div className="mt-3 flex flex-wrap gap-3">
                    <Link
                      className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
                      href={`/transactions?edit=${quickEntryCreatedTransactionId}`}
                    >
                      Revisar ou corrigir lançamento
                    </Link>
                  </div>
                ) : null}
              </div>
            ) : null}

            {data.accounts.length === 0 ? (
              <div className="mt-5 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-5 text-sm leading-6 text-[var(--color-muted)]">
                Você ainda não tem uma conta cadastrada. Para usar a ação rápida,
                crie sua primeira conta em{" "}
                <Link className="font-semibold text-[var(--color-accent)]" href="/accounts">
                  Contas
                </Link>{" "}
                e depois volte para registrar o que entrou ou saiu.
                </div>
            ) : (
              <div
                className="mt-5"
                data-testid="quick-entry-layout"
              >
                {activeAccounts.length === 0 ? (
                  <div className="self-start rounded-[24px] border border-dashed border-[var(--color-line)] bg-white px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                    Todas as suas contas estao inativas. Reative ao menos uma em{" "}
                    <Link className="font-semibold text-[var(--color-accent)]" href="/accounts">
                      Contas
                    </Link>{" "}
                    para registrar novas movimentacoes ou importar dados.
                  </div>
                ) : (
                  <form className="self-start space-y-4 rounded-[24px] border border-[var(--color-line)] bg-white p-5" onSubmit={handleQuickEntrySubmit}>
                    <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)]">
                      <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                        <span>Conta financeira</span>
                        <select
                          className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-4 text-base font-medium text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)] disabled:cursor-not-allowed disabled:bg-[var(--color-panel)]"
                          disabled={activeAccounts.length === 1}
                          onChange={(event) =>
                            setQuickEntryForm((current) => ({
                              ...current,
                              financialAccountId: event.target.value,
                            }))
                          }
                          value={quickEntryForm.financialAccountId}
                        >
                          {activeAccounts.length > 1 ? (
                            <option value="">Escolha a conta para registrar</option>
                          ) : null}
                          {activeAccounts.map((account) => (
                            <option key={account.id} value={account.id}>
                              {account.name}
                            </option>
                          ))}
                        </select>
                      </label>

                      <div className="rounded-[24px] border border-[var(--color-line)] bg-[var(--color-panel)] px-5 py-4 text-sm leading-6 text-[var(--color-muted)]">
                        {activeAccounts.length === 1
                          ? "Essa movimentação será registrada na sua conta ativa disponível."
                          : "Escolha explicitamente a conta ativa para evitar lançar a movimentação no lugar errado."}
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

                    <div className="space-y-2.5">
                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <div className="text-sm font-medium text-[var(--color-foreground)]">
                          Categoria
                        </div>
                        <div className="text-xs text-[var(--color-muted)]">
                          As opções mudam quando você troca entre entrada e saída.
                        </div>
                      </div>
                      <div className="text-xs leading-5 text-[var(--color-muted)]">
                        Categoria responde ao motivo da movimentação. PIX, boleto e cartão ficam para um campo futuro de meio de pagamento.
                      </div>
                      <div className="flex gap-2 overflow-x-auto pb-1">
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

              </div>
            )}
            </article>

            <article className="min-w-0 self-start rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4 xl:p-5">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                Resumo do mês
              </div>
              <h2 className="mt-2 text-xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Detalhes do mês
              </h2>

              <div className="mt-3 grid gap-3" data-testid="dashboard-month-details-content">
                <article className="rounded-[20px] border border-[var(--color-line)] bg-white px-4 py-3">
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Entradas x saídas
                  </div>
                  <div className="mt-3 space-y-3">
                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Entradas</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.freeMoney.totalIncome)}
                        </span>
                      </div>
                      <div className="mt-2 h-2 rounded-full bg-[color:rgba(15,118,110,0.08)]">
                        <div
                          className="h-2 rounded-full bg-[var(--color-accent)]"
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
                      <div className="mt-2 h-2 rounded-full bg-[color:rgba(217,119,6,0.12)]">
                        <div
                          className="h-2 rounded-full bg-[var(--color-warm)]"
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

                <article className="rounded-[20px] border border-[var(--color-line)] bg-white px-4 py-3">
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Planejado x gasto
                  </div>
                  <div className="mt-3 space-y-3">
                    <div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="text-[var(--color-muted)]">Planejado</span>
                        <span className="font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(data.budget.totalPlanned)}
                        </span>
                      </div>
                      <div className="mt-2 h-2 rounded-full bg-[color:rgba(15,118,110,0.08)]">
                        <div
                          className="h-2 rounded-full bg-[var(--color-accent)]"
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
                      <div className="mt-2 h-2 rounded-full bg-[color:rgba(217,119,6,0.12)]">
                        <div
                          className="h-2 rounded-full bg-[var(--color-warm)]"
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

                <article className="rounded-[20px] border border-[var(--color-line)] bg-white px-4 py-3">
                  <div className="flex items-baseline justify-between gap-3">
                    <div className="text-sm font-semibold text-[var(--color-foreground)]">
                      Próximas contas
                    </div>
                    <div className="text-[11px] leading-5 text-[var(--color-muted)]">
                      Até 5 vencimentos
                    </div>
                  </div>

                  <div className="mt-3 space-y-2.5">
                    {data.billsSummary.upcoming.length === 0 ? (
                      <div className="rounded-[18px] border border-dashed border-[var(--color-line)] px-3 py-4 text-sm text-[var(--color-muted)]">
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
                          className="rounded-[18px] border border-[var(--color-line)] bg-[var(--color-panel)] px-3 py-3"
                          key={bill.id}
                        >
                          <div className="flex items-start justify-between gap-3">
                            <div className="min-w-0">
                              <div className="text-sm font-semibold text-[var(--color-foreground)]">
                                {bill.description}
                              </div>
                              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-[var(--color-muted)]">
                                <span>Vence em {formatDate(bill.dueOn)}</span>
                                {getPredictableBillLabel(bill) ? (
                                  <span className="rounded-full border border-[var(--color-line)] bg-white px-2 py-1 font-semibold text-[var(--color-foreground)]">
                                    {getPredictableBillLabel(bill)}
                                  </span>
                                ) : null}
                              </div>
                            </div>
                            <div className="flex shrink-0 flex-col items-end gap-2">
                              <span className="rounded-full border border-[color:rgba(217,119,6,0.14)] bg-[color:rgba(217,119,6,0.12)] px-2.5 py-1 text-[11px] font-semibold text-[var(--color-warm)]">
                                Pendente
                              </span>
                              <div className="text-sm font-semibold text-[var(--color-foreground)]">
                                {formatCurrency(bill.amount)}
                              </div>
                            </div>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </article>
              </div>
            </article>
          </section>

          {firstUseState && activationMessage ? (
            <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className={dashboardStandardGridClass}>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                      Primeiro uso
                    </div>
                    <span className="rounded-full border border-[color:rgba(15,118,110,0.16)] bg-[var(--color-accent-soft)] px-3 py-1 text-xs font-semibold text-[var(--color-accent)]">
                      Ativação guiada
                    </span>
                  </div>

                  <h2 className="mt-4 text-3xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                    {activationMessage.message}
                  </h2>
                  <p className="mt-3 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                    {activationMessage.cause}
                  </p>
                </div>

                <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-5">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                    O que fazer agora
                  </div>
                  <div className="mt-3 text-sm leading-6 text-[var(--color-foreground)]">
                    {activationMessage.action}
                  </div>
                  <div className="mt-4 flex flex-wrap gap-3">
                    {!data.onboarding.hasAccount ? (
                      <Link
                        className="rounded-full border border-[var(--color-line)] bg-white px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                        href="/accounts"
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
                </div>
              </div>
            </section>
          ) : null}

          {showFallbackAlertHighlights ? (
            <section
              className="rounded-[28px] border border-[color:rgba(217,119,6,0.18)] bg-[color:rgba(255,247,237,0.92)] p-6"
              id="alertas-do-mes"
            >
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="min-w-0">
                  <div className="text-sm font-semibold uppercase tracking-[0.18em] text-[var(--color-warm)]">
                    Alertas do mês
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.04em] text-[var(--color-foreground)]">
                    O dashboard já encontrou sinais que pedem atenção.
                  </h2>
                  <p className="mt-3 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                    Mesmo sem a leitura completa do mês, estes alertas continuam visíveis
                    aqui para você não depender só do sino para entender o que precisa ver.
                  </p>
                </div>
                <div className="rounded-full border border-[color:rgba(217,119,6,0.16)] bg-white px-4 py-2 text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-warm)]">
                  {visibleAlertHighlights.length} alerta
                  {visibleAlertHighlights.length > 1 ? "s" : ""} ativo
                  {visibleAlertHighlights.length > 1 ? "s" : ""}
                </div>
              </div>

              <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {visibleAlertHighlights.map((alert) => (
                  <article
                    className="rounded-[22px] border border-[color:rgba(217,119,6,0.12)] bg-white px-5 py-4"
                    key={`${alert.type}-${alert.message}`}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <span
                        className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[alert.severity]}`}
                      >
                        Prioridade {alertSeverityLabels[alert.severity]}
                      </span>
                      {alert.amount > 0 ? (
                        <span className="text-sm font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(alert.amount)}
                        </span>
                      ) : null}
                    </div>
                    <div className="mt-4 text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                      {alert.message}
                    </div>
                    {alert.actionUrl ? (
                      <div className="mt-4">
                        <Link
                          className="inline-flex rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                          href={alert.actionUrl}
                        >
                          Abrir alerta
                        </Link>
                      </div>
                    ) : null}
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          {isCriticalHealth && monthHealth ? (
                <section
                  className="rounded-[28px] border border-[color:rgba(185,28,28,0.3)] bg-[color:rgba(254,226,226,0.82)] p-6"
                  id="foco-do-mes"
                >
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="min-w-0">
                      <div className="text-sm font-semibold uppercase tracking-[0.18em] text-red-700">
                        Risco financeiro crítico
                      </div>
                      <div className="mt-3 text-2xl font-bold tracking-[-0.04em] text-red-800">
                        {monthHealth.message ?? monthHealth.summary.message}
                      </div>
                    </div>
                    {notificationCount > 0 ? (
                      <div className="rounded-full border border-[color:rgba(185,28,28,0.14)] bg-white px-4 py-2 text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                        {notificationCount} foco
                        {notificationCount > 1 ? "s" : ""} ativo
                        {notificationCount > 1 ? "s" : ""}
                      </div>
                    ) : null}
                  </div>

                  <div className={dashboardStandardGridClass}>
                    <div className="min-w-0">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                        O que isso significa no mes
                      </div>
                      <p className="max-w-3xl text-sm leading-6 text-[var(--color-foreground)]">
                        {monthHealth.summary.cause}
                      </p>

                      {criticalReasons.length > 0 ? (
                        <div className="mt-5">
                          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                            O que esta pesando agora
                          </div>
                          <div className="mt-3 grid gap-3 md:grid-cols-2">
                            {criticalReasons.map((reason, index) => (
                              <div
                                className="rounded-[20px] border border-[color:rgba(185,28,28,0.12)] bg-white px-4 py-4 text-sm leading-6 text-[var(--color-foreground)]"
                                key={`reason-${index}`}
                              >
                                {reason}
                              </div>
                            ))}
                          </div>
                        </div>
                      ) : null}
                    </div>

                    <div className="rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-white/80 px-5 py-5">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                        Comece por aqui
                      </div>
                      <div className="mt-3 text-base font-semibold leading-7 text-[var(--color-foreground)]">
                        {monthHealth.summary.action}
                      </div>
                      {criticalPriorityReason ? (
                        <div className="mt-4 rounded-[18px] border border-[color:rgba(185,28,28,0.12)] bg-[color:rgba(254,242,242,0.9)] px-4 py-4">
                          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                            Por que comecar por isso
                          </div>
                          <p className="mt-2 text-sm leading-6 text-[var(--color-foreground)]">
                            {criticalPriorityReason}
                          </p>
                        </div>
                      ) : null}
                      {criticalActions.length > 0 ? (
                        <div className="mt-4 space-y-2 text-sm leading-6 text-[var(--color-muted)]">
                          {criticalActions.slice(0, 2).map((action, index) => (
                            <p key={`action-${index}`}>• {action}</p>
                          ))}
                        </div>
                      ) : null}
                      <div className="mt-5">
                        <div className="flex flex-wrap gap-3">
                          <Link
                            href={primaryRecommendedAction?.target ?? dashboardFocusHref}
                            className="inline-flex rounded-xl bg-red-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-red-800"
                          >
                            {primaryRecommendedAction?.label ?? "Ver resumo do mes"}
                          </Link>
                          <Link
                            className="inline-flex rounded-xl border border-[color:rgba(185,28,28,0.14)] bg-white px-4 py-2 text-sm font-semibold text-red-700 transition hover:bg-[color:rgba(254,242,242,0.9)]"
                            href={`/analysis?month=${monthValue}`}
                          >
                            Ver analise do mes
                          </Link>
                        </div>
                      </div>
                      {criticalSupportMessage ? (
                        <p className="mt-4 text-sm leading-6 text-[var(--color-muted)]">
                          {criticalSupportMessage}
                        </p>
                      ) : null}
                    </div>
                  </div>

                  {visibleAlertHighlights.length > 0 ? (
                    <div className="mt-5" id="alertas-do-mes">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-700">
                        Alertas visíveis no mês
                      </div>
                      <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                        {visibleAlertHighlights.map((alert) => (
                          <article
                            className="rounded-[22px] border border-[color:rgba(185,28,28,0.12)] bg-white px-5 py-4"
                            key={`${alert.type}-${alert.message}`}
                          >
                            <div className="flex flex-wrap items-center justify-between gap-3">
                              <span
                                className={`rounded-full border px-3 py-1 text-xs font-semibold ${alertSeverityStyles[alert.severity]}`}
                              >
                                Prioridade {alertSeverityLabels[alert.severity]}
                              </span>
                              {alert.amount > 0 ? (
                                <span className="text-sm font-semibold text-[var(--color-foreground)]">
                                  {formatCurrency(alert.amount)}
                                </span>
                              ) : null}
                            </div>
                            <div className="mt-4 text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                              {alert.message}
                            </div>
                            {alert.actionUrl ? (
                              <div className="mt-4">
                                <Link
                                  className="inline-flex rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                                  href={alert.actionUrl}
                                >
                                  Abrir alerta
                                </Link>
                              </div>
                            ) : null}
                          </article>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </section>
              
          ) : null}

          {showSecondarySections ? (
            <>
              <section className={dashboardSectionStackClass}>
            <article
              className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6"
              id="resumo-financeiro"
            >
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Resumo financeiro
              </div>
              <div className="mt-4 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-muted)]">
                    Estado do mês
                  </div>
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

              <div className="mt-6 grid gap-4 md:grid-cols-3">
                {monthStateCards.map((item) => (
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
                    {item.composition?.length ? (
                      <div className="mt-4 rounded-[18px] border border-[var(--color-line)] bg-[var(--color-panel)] p-3">
                        <div className="space-y-2">
                          {item.composition.map((compositionItem) => (
                            <div
                              className="flex items-center justify-between gap-3 text-xs"
                              key={compositionItem.label}
                            >
                              <span className="text-[var(--color-muted)]">
                                {compositionItem.label}
                              </span>
                              <span className="font-semibold text-[var(--color-foreground)]">
                                {formatCurrency(compositionItem.value)}
                              </span>
                            </div>
                          ))}
                        </div>
                        {item.note ? (
                          <div className="mt-3 text-xs leading-5 text-[var(--color-muted)]">
                            {item.note}
                          </div>
                        ) : null}
                      </div>
                    ) : null}
                  </article>
                ))}
              </div>

              {projectionSupportMessage ? (
                <div className="mt-6 rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-4 text-sm leading-6 text-[var(--color-foreground)]">
                  {projectionSupportMessage}
                </div>
              ) : null}
            </article>

            <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-5 xl:p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                Contas a pagar do mês
              </div>
              <h2 className="mt-2 text-xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                Compromissos do mês
              </h2>
              <p className="mt-2 max-w-xl text-sm leading-5 text-[var(--color-muted)]">
                Leitura rápida do que ainda pressiona o caixa neste mês.
              </p>

              <div
                className="mt-4 rounded-[24px] border border-[var(--color-line)] bg-white p-3"
                data-testid="dashboard-bills-compact-block"
              >
                <div className="grid gap-3 md:grid-cols-2">
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
                  ].map((item) => (
                    <article
                      className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] px-4 py-3"
                      key={item.label}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <div className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${item.tone}`}>
                            {item.label}
                          </div>
                          <div className="mt-3 text-xs text-[var(--color-muted)]">
                            {item.count} {item.count === 1 ? "conta" : "contas"}
                          </div>
                        </div>
                        <div className="text-right text-lg font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                          {formatCurrency(item.total)}
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </div>

              {data.billsSummary.countPredictable > 0 ? (
                <details className="mt-4 rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                  <summary className="cursor-pointer list-none text-sm font-semibold text-[var(--color-foreground)]">
                    Ver detalhe de compromissos previsiveis
                  </summary>
                  <div className="mt-3 flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
                    <div>
                      <div className="text-sm font-semibold text-[var(--color-foreground)]">
                        Compromissos previsiveis em aberto
                      </div>
                      <div className="mt-1 text-xs leading-5 text-[var(--color-muted)]">
                        Contas recorrentes e parcelas ja previstas neste mes que ainda seguem abertas.
                      </div>
                    </div>
                    <div className="text-sm font-semibold text-[var(--color-foreground)]">
                      {formatCurrency(data.billsSummary.predictableTotal)}
                    </div>
                  </div>

                  <div className="mt-3 grid gap-3 md:grid-cols-3">
                    {[
                      {
                        label: "Previsiveis",
                        total: data.billsSummary.predictableTotal,
                        count: data.billsSummary.countPredictable,
                      },
                      {
                        label: "Recorrentes",
                        total: data.billsSummary.recurringTotal,
                        count: data.billsSummary.countRecurring,
                      },
                      {
                        label: "Parceladas",
                        total: data.billsSummary.installmentTotal,
                        count: data.billsSummary.countInstallment,
                      },
                    ].map((item) => (
                      <div
                        className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] px-3 py-3"
                        key={item.label}
                      >
                        <div className="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--color-muted)]">
                          {item.label}
                        </div>
                        <div className="mt-2 text-lg font-semibold text-[var(--color-foreground)]">
                          {formatCurrency(item.total)}
                        </div>
                        <div className="mt-1 text-xs text-[var(--color-muted)]">
                          {item.count} {item.count === 1 ? "conta" : "contas"}
                        </div>
                      </div>
                    ))}
                  </div>
                </details>
              ) : null}
            </article>
              </section>

              {data.summary.byCategory.length > 0 ? (
              <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Leitura por categoria
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Onde o mês está concentrado
            </h2>

            <div className="mt-6 grid gap-3 md:grid-cols-2">
              {data.summary.byCategory.map((item, index) => (
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
                ))}
            </div>
              </section>
              ) : null}
            </>
          ) : null}
        </div>
      )}
    </AppShell>
  );
}
