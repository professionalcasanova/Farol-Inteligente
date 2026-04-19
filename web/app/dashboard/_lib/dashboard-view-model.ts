import type {
  AlertsResponse,
  BillsSummaryResponse,
  MonthHealthResponse,
} from "@/lib/api";
import type {
  ActivationMessage,
  DashboardData,
  NotificationItem,
} from "./dashboard-types";

export function getFinancialSupportText(data: DashboardData) {
  if (data.freeMoney.isProjection) {
    return "Este mes ainda esta em projecao. Reservas e contas previstas ajudam no preparo, mas ainda nao contam como gasto realizado nem viram saldo carregado automaticamente.";
  }

  if (
    data.summary.totalIncome === 0 &&
    data.summary.totalExpense === 0 &&
    data.billsSummary.countPending === 0 &&
    data.budget.totalPlanned === 0
  ) {
    return "Este painel mostra a base do mês. Conforme você registrar movimentações, vencimentos e planejamento, a leitura fica mais precisa.";
  }

  return "Entradas, saídas, saldo e dinheiro livre ajudam a confirmar o contexto do mês antes de agir. Aqui, o dinheiro livre já considera o que ficou reservado no planejamento e as contas em aberto do mês.";
}

export function isFirstUseState(data: DashboardData) {
  const hasActionableMonthHealth = Boolean(
    data.monthHealth &&
      (data.monthHealth.status !== "healthy" ||
        data.monthHealth.insights.length > 0 ||
        (data.monthHealth.recommendedActions?.length ?? 0) > 0),
  );
  const hasFinancialActivity =
    data.summary.totalIncome > 0 ||
    data.summary.totalExpense > 0 ||
    data.freeMoney.totalIncome > 0 ||
    data.freeMoney.totalExpense > 0 ||
    data.freeMoney.balance !== 0 ||
    data.freeMoney.plannedReserve > 0 ||
    data.freeMoney.unpaidBillsReserve > 0;

  return (
    !hasActionableMonthHealth &&
    !hasFinancialActivity &&
    data.billsSummary.countPending === 0 &&
    data.billsSummary.countOverdue === 0 &&
    data.budget.totalPlanned === 0 &&
    data.accounts.length <= 1 &&
    !data.onboarding.hasTransaction &&
    !data.onboarding.hasBill
  );
}

export function getProjectionSupportMessage(data: DashboardData) {
  if (!data.freeMoney.isProjection) {
    return "";
  }

  return "Se voce reserva dinheiro para um objetivo, isso continua como compromisso planejado do mes escolhido. Nao acumula sozinho para o mes seguinte sem registro ou ajuste do planejamento.";
}

export function getActivationMessage(data: DashboardData): ActivationMessage {
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

export function getCriticalPriorityReason(
  monthHealth: MonthHealthResponse | null | undefined,
) {
  if (!monthHealth) {
    return "";
  }

  const insightTypes = new Set(monthHealth.insights.map((insight) => insight.type));

  if (
    insightTypes.has("negative_balance_high_variable_expense") ||
    insightTypes.has("negative_free_money")
  ) {
    return "Revisar as maiores saidas primeiro mostra o que pode ser cortado ou adiado antes de faltar para o essencial.";
  }

  if (insightTypes.has("overdue_bills_long") || insightTypes.has("overdue_bills")) {
    return "Comecar pelas contas vencidas reduz juros, evita mais pressao no caixa e protege o restante do mes.";
  }

  if (insightTypes.has("short_term_bills_pressure")) {
    return "Organizar os proximos vencimentos agora evita que a pressao de poucos dias vire atraso em cadeia.";
  }

  return "O primeiro passo precisa proteger sua rotina e abrir espaco para o restante do mes.";
}

export function getCriticalSupportMessage(
  monthHealth: MonthHealthResponse | null | undefined,
) {
  if (!monthHealth || monthHealth.status !== "critical") {
    return "";
  }

  return "Voce nao precisa resolver tudo hoje. Comece pelo que protege sua rotina e seu caixa neste mes.";
}

export function getComparisonBarWidth(value: number, max: number) {
  if (value <= 0 || max <= 0) {
    return "0%";
  }

  return `${Math.max(10, Math.min(100, (value / max) * 100))}%`;
}

export function isActionNavigableFromDashboard(target?: string | null) {
  return Boolean(target && target.trim() && target !== "/dashboard");
}

export function getPredictableBillLabel(
  bill: BillsSummaryResponse["upcoming"][number],
) {
  if (bill.seriesKind === "installment" && bill.occurrenceNumber && bill.totalOccurrences) {
    return `Parcela ${bill.occurrenceNumber}/${bill.totalOccurrences}`;
  }

  if (bill.seriesKind === "recurring") {
    return "Recorrente";
  }

  return "";
}

export function buildNotificationItems(
  monthHealth: MonthHealthResponse | null | undefined,
  alerts: AlertsResponse["alerts"],
  fallbackHref: string,
) {
  const items: NotificationItem[] = [];
  const seen = new Set<string>();

  const addItem = (item: NotificationItem) => {
    const normalizedHref = isActionNavigableFromDashboard(item.href) ? item.href : fallbackHref;
    const dedupeKey = `${normalizedHref}::${item.message}`;

    if (seen.has(dedupeKey)) {
      return;
    }

    seen.add(dedupeKey);
    items.push({
      ...item,
      href: normalizedHref,
    });
  };

  if (monthHealth && monthHealth.status !== "healthy") {
    const summaryMessage = monthHealth.message ?? monthHealth.summary.message;
    const summaryHref =
      monthHealth.recommendedActions?.find((action) =>
        isActionNavigableFromDashboard(action.target),
      )?.target ?? fallbackHref;

    addItem({
      id: "month-health-summary",
      href: summaryHref,
      title: monthHealth.status === "critical" ? "Foco principal" : "Leitura do mes",
      message: summaryMessage,
      severity: monthHealth.status === "critical" ? "high" : "medium",
    });
  }

  alerts.forEach((alert, index) => {
    addItem({
      id: `${alert.type}-${index}`,
      href: alert.actionUrl ?? fallbackHref,
      title: "Alerta do mes",
      message: alert.message,
      severity: alert.severity,
    });
  });

  return items.slice(0, 5);
}
