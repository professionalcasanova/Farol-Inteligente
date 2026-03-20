import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DashboardPage from "@/app/dashboard/page";
import {
  ApiError,
  getAlerts,
  getBillsSummary,
  getFreeMoney,
  getMonthlyBudget,
  getMonthlySummary,
  listAccounts,
  listBills,
  listTransactions,
} from "@/lib/api";
import { useProtectedSession } from "@/lib/use-protected-session";

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: ComponentProps<"a">) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/components/app-shell", () => ({
  AppShell: ({
    title,
    description,
    children,
  }: {
    title: string;
    description: string;
    children: ReactNode;
  }) => (
    <div>
      <h1>{title}</h1>
      <p>{description}</p>
      {children}
    </div>
  ),
}));

vi.mock("@/components/month-picker", () => ({
  MonthPicker: ({ label }: { label: string }) => <div>{label}</div>,
}));

vi.mock("@/lib/use-protected-session", () => ({
  useProtectedSession: vi.fn(),
}));

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    getAlerts: vi.fn(),
    getBillsSummary: vi.fn(),
    getFreeMoney: vi.fn(),
    getMonthlyBudget: vi.fn(),
    getMonthlySummary: vi.fn(),
    listAccounts: vi.fn(),
    listBills: vi.fn(),
    listTransactions: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedGetAlerts = vi.mocked(getAlerts);
const mockedGetBillsSummary = vi.mocked(getBillsSummary);
const mockedGetFreeMoney = vi.mocked(getFreeMoney);
const mockedGetMonthlyBudget = vi.mocked(getMonthlyBudget);
const mockedGetMonthlySummary = vi.mocked(getMonthlySummary);
const mockedListAccounts = vi.mocked(listAccounts);
const mockedListBills = vi.mocked(listBills);
const mockedListTransactions = vi.mocked(listTransactions);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

function mockDashboardApi(overrides?: {
  accounts?: Array<{
    id: string;
    name: string;
    type: 1 | 2 | 3 | 4;
    isActive: boolean;
    createdAtUtc: string;
  }>;
  bills?: Array<{
    id: string;
    description: string;
    amount: number;
    dueOn: string;
    isPaid: boolean;
    paidAtUtc: string | null;
    createdAtUtc: string;
    status: "pending" | "paid" | "overdue";
  }>;
  transactions?: Array<{
    id: string;
    financialAccountId: string;
    categoryId: string | null;
    type: 1 | 2;
    amount: number;
    description: string;
    occurredOn: string;
    createdAtUtc: string;
  }>;
}) {
  mockedGetMonthlySummary.mockResolvedValue({
    month: 3,
    year: 2026,
    totalIncome: 0,
    totalExpense: 0,
    balance: 0,
    byCategory: [],
  });
  mockedGetAlerts.mockResolvedValue({ alerts: [] });
  mockedGetBillsSummary.mockResolvedValue({
    totalPending: 0,
    totalOverdue: 0,
    totalPaid: 0,
    countPending: 0,
    countOverdue: 0,
    countPaid: 0,
    upcoming: [],
  });
  mockedGetFreeMoney.mockResolvedValue({
    month: 3,
    year: 2026,
    totalIncome: 0,
    totalExpense: 0,
    balance: 0,
    totalPlannedBudget: 0,
    totalBudgetSpent: 0,
    totalBudgetRemaining: 0,
    freeToSpend: 0,
  });
  mockedGetMonthlyBudget.mockResolvedValue({
    month: 3,
    year: 2026,
    totalPlanned: 0,
    totalSpent: 0,
    totalRemaining: 0,
    categories: [],
  });
  mockedListAccounts.mockResolvedValue(overrides?.accounts ?? []);
  mockedListTransactions.mockResolvedValue(overrides?.transactions ?? []);
  mockedListBills.mockResolvedValue(overrides?.bills ?? []);
}

describe("DashboardPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedGetAlerts.mockReset();
    mockedGetBillsSummary.mockReset();
    mockedGetFreeMoney.mockReset();
    mockedGetMonthlyBudget.mockReset();
    mockedGetMonthlySummary.mockReset();
    mockedListAccounts.mockReset();
    mockedListBills.mockReset();
    mockedListTransactions.mockReset();
  });

  it("Dashboard_Loading_ShowsLoadingState", () => {
    mockedUseProtectedSession.mockReturnValue({
      session: null,
      isLoading: true,
      logout: vi.fn(),
    });

    render(<DashboardPage />);

    expect(screen.getByText("Preparando seu painel...")).toBeInTheDocument();
  });

  it("Dashboard_NewUser_ShowsOnboardingChecklist", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockDashboardApi();

    render(<DashboardPage />);

    expect(await screen.findByText("Comece por aqui")).toBeInTheDocument();
    expect(screen.getByText(/Criar sua primeira conta/)).toBeInTheDocument();
    expect(screen.getByText(/Registrar uma entrada \(salário\)/)).toBeInTheDocument();
    expect(screen.getByText(/Adicionar uma conta a pagar/)).toBeInTheDocument();
  });

  it("Dashboard_UserWithCompletedOnboarding_HidesChecklist", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockDashboardApi({
      accounts: [
        {
          id: "account-1",
          name: "Conta principal",
          type: 2,
          isActive: true,
          createdAtUtc: "2026-03-01T00:00:00Z",
        },
      ],
      transactions: [
        {
          id: "transaction-1",
          financialAccountId: "account-1",
          categoryId: null,
          type: 1,
          amount: 3000,
          description: "Salario",
          occurredOn: "2026-03-01",
          createdAtUtc: "2026-03-01T00:00:00Z",
        },
      ],
      bills: [
        {
          id: "bill-1",
          description: "Aluguel",
          amount: 900,
          dueOn: "2026-03-10",
          isPaid: false,
          paidAtUtc: null,
          createdAtUtc: "2026-03-01T00:00:00Z",
          status: "pending",
        },
      ],
    });

    render(<DashboardPage />);

    expect(await screen.findByText("Ações rápidas")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.queryByText("Comece por aqui")).not.toBeInTheDocument();
    });
  });

  it("Dashboard_LoadError_ShowsReadableErrorState", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedGetMonthlySummary.mockRejectedValue(
      new ApiError(
        "Npgsql.PostgresException: relation \"bills\" does not exist at DashboardController.cs:117",
        500,
      ),
    );
    mockedGetAlerts.mockResolvedValue({ alerts: [] });
    mockedGetBillsSummary.mockResolvedValue({
      totalPending: 0,
      totalOverdue: 0,
      totalPaid: 0,
      countPending: 0,
      countOverdue: 0,
      countPaid: 0,
      upcoming: [],
    });
    mockedGetFreeMoney.mockResolvedValue({
      month: 3,
      year: 2026,
      totalIncome: 0,
      totalExpense: 0,
      balance: 0,
      totalPlannedBudget: 0,
      totalBudgetSpent: 0,
      totalBudgetRemaining: 0,
      freeToSpend: 0,
    });
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    expect(await screen.findByText("Falha ao carregar")).toBeInTheDocument();
    expect(screen.getByText("Não foi possível abrir o dashboard")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Não foi possível carregar o dashboard agora. Confira se a API local está ativa e tente novamente.",
      ),
    ).toBeInTheDocument();
  });

  it("Dashboard_WhenSessionExpires_TriggersConsistentLogout", async () => {
    const logout = vi.fn();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout,
    });
    mockedGetMonthlySummary.mockRejectedValue(
      new ApiError("Invalid access token.", 401),
    );
    mockedGetAlerts.mockResolvedValue({ alerts: [] });
    mockedGetBillsSummary.mockResolvedValue({
      totalPending: 0,
      totalOverdue: 0,
      totalPaid: 0,
      countPending: 0,
      countOverdue: 0,
      countPaid: 0,
      upcoming: [],
    });
    mockedGetFreeMoney.mockResolvedValue({
      month: 3,
      year: 2026,
      totalIncome: 0,
      totalExpense: 0,
      balance: 0,
      totalPlannedBudget: 0,
      totalBudgetSpent: 0,
      totalBudgetRemaining: 0,
      freeToSpend: 0,
    });
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    await waitFor(() => {
      expect(logout).toHaveBeenCalledWith("session-expired");
    });
  });
});
