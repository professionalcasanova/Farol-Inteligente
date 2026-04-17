import {
  getAlerts,
  getBillsSummary,
  getMonthHealth,
  getMonthlyBudget,
  getMonthlySummary,
  getFreeMoney,
  isUnauthorizedApiError,
  listAccounts,
  listCategories,
} from "@/lib/api";
import type { DashboardData } from "./dashboard-types";

type DashboardMonthReference = {
  month: number;
  year: number;
};

function buildOnboardingState(
  summary: DashboardData["summary"],
  billsSummary: DashboardData["billsSummary"],
  accounts: DashboardData["accounts"],
) {
  return {
    hasAccount: accounts.length > 0,
    hasBill:
      billsSummary.countPending + billsSummary.countOverdue + billsSummary.countPaid > 0,
    hasTransaction:
      summary.byCategory.length > 0 ||
      summary.totalIncome !== 0 ||
      summary.totalExpense !== 0,
  };
}

export async function loadDashboardData(
  accessToken: string,
  monthReference: DashboardMonthReference,
) {
  const monthHealthPromise = getMonthHealth(
    accessToken,
    monthReference.month,
    monthReference.year,
  ).catch((caughtError) => {
    if (isUnauthorizedApiError(caughtError)) {
      throw caughtError;
    }

    return null;
  });
  const categoriesPromise = listCategories(accessToken).catch((caughtError) => {
    if (isUnauthorizedApiError(caughtError)) {
      throw caughtError;
    }

    return [];
  });

  const [monthHealth, summary, alerts, billsSummary, categories, freeMoney, budget, accounts] =
    await Promise.all([
      monthHealthPromise,
      getMonthlySummary(accessToken, monthReference.month, monthReference.year),
      getAlerts(accessToken, monthReference.month, monthReference.year),
      getBillsSummary(accessToken, monthReference.month, monthReference.year),
      categoriesPromise,
      getFreeMoney(accessToken, monthReference.month, monthReference.year),
      getMonthlyBudget(accessToken, monthReference.month, monthReference.year),
      listAccounts(accessToken),
    ]);

  return {
    accounts,
    alerts,
    billsSummary,
    budget,
    categories,
    freeMoney,
    monthHealth,
    onboarding: buildOnboardingState(summary, billsSummary, accounts),
    summary,
  } satisfies DashboardData;
}

export async function refreshDashboardData(
  accessToken: string,
  monthReference: DashboardMonthReference,
  currentData: DashboardData,
) {
  const monthHealthPromise = getMonthHealth(
    accessToken,
    monthReference.month,
    monthReference.year,
  ).catch((caughtError) => {
    if (isUnauthorizedApiError(caughtError)) {
      throw caughtError;
    }

    return null;
  });

  const [monthHealth, summary, alerts, billsSummary, freeMoney, budget] = await Promise.all([
    monthHealthPromise,
    getMonthlySummary(accessToken, monthReference.month, monthReference.year),
    getAlerts(accessToken, monthReference.month, monthReference.year),
    getBillsSummary(accessToken, monthReference.month, monthReference.year),
    getFreeMoney(accessToken, monthReference.month, monthReference.year),
    getMonthlyBudget(accessToken, monthReference.month, monthReference.year),
  ]);

  return {
    ...currentData,
    alerts,
    billsSummary,
    budget,
    freeMoney,
    monthHealth,
    onboarding: buildOnboardingState(summary, billsSummary, currentData.accounts),
    summary,
  } satisfies DashboardData;
}
