import type {
  AccountResponse,
  AlertsResponse,
  BillsSummaryResponse,
  CategoryResponse,
  FreeMoneyResponse,
  MonthHealthResponse,
  MonthlyBudgetResponse,
  MonthlySummaryResponse,
  TransactionType,
} from "@/lib/api";

export type AccountFormState = {
  name: string;
  type: 1 | 2 | 3 | 4;
};

export type DashboardData = {
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

export type QuickEntryFormState = {
  financialAccountId: string;
  amount: string;
  type: TransactionType;
  description: string;
  categoryId: string;
};

export type NotificationItem = {
  id: string;
  href: string;
  title: string;
  message: string;
  severity: "high" | "medium";
};

export type ActivationMessage = {
  message: string;
  cause: string;
  action: string;
};
