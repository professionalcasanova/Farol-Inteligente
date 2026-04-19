"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { BoundedList } from "@/components/bounded-list";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import { PrimaryActionCard } from "@/components/primary-action-card";
import { SecondarySupportPanel } from "@/components/secondary-support-panel";
import {
  getFriendlyApiMessage,
  getMonthHealth,
  isUnauthorizedApiError,
  type AlertSeverity,
  type MonthHealthResponse,
} from "@/lib/api";
import { getCurrentMonthInputValue, parseMonthInputValue } from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

const monthHealthStatusLabels = {
  healthy: "Sob controle",
  attention: "Pede atencao",
  critical: "Precisa de acao agora",
} as const;

const monthHealthStatusStyles = {
  healthy:
    "border-[color:rgba(29,130,93,0.18)] bg-[color:rgba(220,252,231,0.82)] text-[var(--color-success)]",
  attention:
    "border-[color:rgba(217,119,6,0.16)] bg-[color:rgba(255,247,237,0.9)] text-[var(--color-warm)]",
  critical:
    "border-[color:rgba(185,28,28,0.16)] bg-[color:rgba(254,226,226,0.82)] text-red-700",
} as const;

const insightSeverityLabels: Record<AlertSeverity, string> = {
  high: "Alto",
  medium: "Medio",
};

const insightSeverityStyles: Record<AlertSeverity, string> = {
  high: "bg-[color:rgba(185,28,28,0.1)] text-red-700 border-[color:rgba(185,28,28,0.14)]",
  medium:
    "bg-[color:rgba(217,119,6,0.12)] text-[var(--color-warm)] border-[color:rgba(217,119,6,0.14)]",
};

function resolveMonthValueFromQuery(search: string) {
  const requestedMonth = new URLSearchParams(search).get("month");

  if (requestedMonth && /^\d{4}-\d{2}$/.test(requestedMonth)) {
    return requestedMonth;
  }

  return getCurrentMonthInputValue();
}

function getPrimaryAction(monthHealth: MonthHealthResponse) {
  return monthHealth.recommendedActions?.[0] ?? null;
}

export default function AnalysisPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [monthHealth, setMonthHealth] = useState<MonthHealthResponse | null>(null);
  const [loadError, setLoadError] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(() => parseMonthInputValue(monthValue), [monthValue]);

  useEffect(() => {
    const requestedMonth = resolveMonthValueFromQuery(window.location.search);
    setMonthValue((current) => (current === requestedMonth ? current : requestedMonth));
  }, []);

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
        const response = await getMonthHealth(
          accessToken,
          monthAndYear.month,
          monthAndYear.year,
        );

        if (!isCancelled) {
          setMonthHealth(response);
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
              "Nao foi possivel carregar a analise do mes agora. Tente novamente em alguns instantes.",
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

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  const primaryAction = monthHealth ? getPrimaryAction(monthHealth) : null;
  const reasons = monthHealth?.reasons ?? [];
  const actions = monthHealth?.actions ?? [];

  return (
    <AppShell
      actions={
        <MonthPicker
          label="Mes analisado"
          onChange={setMonthValue}
          value={monthValue}
        />
      }
      description="Leia com mais contexto o estado do mes, entenda o que esta pesando agora e siga para a acao certa sem depender do dashboard para tudo."
      onLogout={logout}
      session={session}
      title="Analise do mes"
    >
      {isFetching ? (
        <LoadingScreen message="Montando a analise do mes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel abrir a analise do mes"
        />
      ) : monthHealth ? (
        <div className="space-y-6">
          <PrimaryActionCard
            description="Este bloco resume o estado do mes e aponta a proxima decisao recomendada sem recalcular nada no frontend."
            eyebrow="Estado do mes"
            footer={
              <div className="flex flex-wrap gap-3">
                {primaryAction ? (
                  <Link
                    className="inline-flex rounded-2xl bg-[var(--color-foreground)] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)]"
                    href={primaryAction.target}
                  >
                    {primaryAction.label}
                  </Link>
                ) : null}
                <Link
                  className="inline-flex rounded-2xl border border-[var(--color-line)] px-5 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                  href={`/dashboard?month=${monthValue}`}
                >
                  Voltar ao dashboard
                </Link>
              </div>
            }
            title={monthHealth.summary.message}
          >
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-3">
                  <span
                    className={`rounded-full border px-3 py-1 text-xs font-semibold ${monthHealthStatusStyles[monthHealth.status]}`}
                  >
                    {monthHealthStatusLabels[monthHealth.status]}
                  </span>
                  {typeof monthHealth.score === "number" ? (
                    <span className="rounded-full border border-[var(--color-line)] bg-white px-3 py-1 text-xs font-semibold text-[var(--color-foreground)]">
                      Score {monthHealth.score}
                    </span>
                  ) : null}
                  <span className="rounded-full border border-[var(--color-line)] bg-white px-3 py-1 text-xs font-semibold text-[var(--color-muted)]">
                    {monthAndYear.month.toString().padStart(2, "0")}/{monthAndYear.year}
                  </span>
                </div>
                <p className="mt-4 max-w-3xl text-sm leading-6 text-[var(--color-muted)]">
                  {monthHealth.summary.action}
                </p>
              </div>
            </div>
          </PrimaryActionCard>

          <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,0.95fr)_minmax(320px,1.05fr)]">
            <SecondarySupportPanel
              description="Aqui fica a explicacao principal do que esta acontecendo no periodo, em linguagem direta e orientada a decisao."
              eyebrow="Explicacao"
              title="O que esta acontecendo"
            >
              <div className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-5 text-sm leading-7 text-[var(--color-foreground)]">
                {monthHealth.summary.cause}
              </div>
            </SecondarySupportPanel>

            <SecondarySupportPanel
              description="Os motivos aparecem em ordem de importancia para reduzir leitura dispersa."
              eyebrow="Fatores"
              title="Razoes principais"
            >
              <BoundedList
                emptyState={
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                    Nenhuma razao adicional foi destacada para este periodo.
                  </div>
                }
                hasItems={reasons.length > 0}
                maxHeightClassName="max-h-[20rem]"
                testId="analysis-reasons-list"
              >
                {reasons.map((reason, index) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                    key={`${index}-${reason}`}
                  >
                    <div className="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--color-muted)]">
                      Razao {index + 1}
                    </div>
                    <div className="mt-2 text-sm leading-6 text-[var(--color-foreground)]">
                      {reason}
                    </div>
                  </article>
                ))}
              </BoundedList>
            </SecondarySupportPanel>
          </div>

          <div className="grid items-start gap-6 xl:grid-cols-[minmax(320px,0.9fr)_minmax(0,1.1fr)]">
            <SecondarySupportPanel
              description="Estas acoes traduzem a leitura do mes em proximos passos concretos."
              eyebrow="Decisao"
              title="Acoes sugeridas"
            >
              <BoundedList
                emptyState={
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                    Nenhuma acao adicional foi sugerida para este periodo.
                  </div>
                }
                hasItems={actions.length > 0}
                maxHeightClassName="max-h-[20rem]"
                testId="analysis-actions-list"
              >
                {actions.map((action, index) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                    key={`${index}-${action}`}
                  >
                    <div className="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--color-muted)]">
                      Acao {index + 1}
                    </div>
                    <div className="mt-2 text-sm leading-6 text-[var(--color-foreground)]">
                      {action}
                    </div>
                  </article>
                ))}
              </BoundedList>
            </SecondarySupportPanel>

            <SecondarySupportPanel
              description="Quando o backend trouxer insights detalhados, eles ficam aqui como apoio e nao disputam com a mensagem principal."
              eyebrow="Apoio"
              title="Detalhes adicionais"
            >
              <BoundedList
                emptyState={
                  <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                    Nenhum detalhe adicional foi retornado para este mes.
                  </div>
                }
                hasItems={monthHealth.insights.length > 0}
                maxHeightClassName="max-h-[24rem]"
                testId="analysis-insights-list"
              >
                {monthHealth.insights.map((insight) => (
                  <article
                    className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                    key={`${insight.type}-${insight.priority}`}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <span
                        className={`rounded-full border px-3 py-1 text-xs font-semibold ${insightSeverityStyles[insight.severity]}`}
                      >
                        Prioridade {insightSeverityLabels[insight.severity]}
                      </span>
                      <span className="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--color-muted)]">
                        #{insight.priority}
                      </span>
                    </div>
                    <div className="mt-4 text-sm font-semibold leading-6 text-[var(--color-foreground)]">
                      {insight.message}
                    </div>
                    <div className="mt-2 text-sm leading-6 text-[var(--color-muted)]">
                      {insight.cause}
                    </div>
                    <div className="mt-3 rounded-[18px] border border-[var(--color-line)] bg-[var(--color-panel)] px-4 py-3 text-sm leading-6 text-[var(--color-foreground)]">
                      {insight.action}
                    </div>
                  </article>
                ))}
              </BoundedList>
            </SecondarySupportPanel>
          </div>
        </div>
      ) : null}
    </AppShell>
  );
}
