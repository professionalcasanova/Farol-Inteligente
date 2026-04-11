import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DashboardPage from "@/app/dashboard/page";
import {
  ApiError,
  createAccount,
  createTransaction,
  getAlerts,
  getBillsSummary,
  getFreeMoney,
  getMonthHealth,
  getMonthlyBudget,
  getMonthlySummary,
  listAccounts,
  listBills,
  listCategories,
  listTransactions,
  type BillsSummaryResponse,
  type FreeMoneyResponse,
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
    actions,
    utilityActions,
    children,
  }: {
    title: string;
    description: string;
    actions?: ReactNode;
    utilityActions?: ReactNode;
    children: ReactNode;
  }) => (
    <div>
      <h1>{title}</h1>
      <p>{description}</p>
      {actions}
      {utilityActions}
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
    createAccount: vi.fn(),
    createTransaction: vi.fn(),
    getAlerts: vi.fn(),
    getBillsSummary: vi.fn(),
    getFreeMoney: vi.fn(),
    getMonthHealth: vi.fn(),
    getMonthlyBudget: vi.fn(),
    getMonthlySummary: vi.fn(),
    listAccounts: vi.fn(),
    listBills: vi.fn(),
    listCategories: vi.fn(),
    listTransactions: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedCreateAccount = vi.mocked(createAccount);
const mockedCreateTransaction = vi.mocked(createTransaction);
const mockedGetAlerts = vi.mocked(getAlerts);
const mockedGetBillsSummary = vi.mocked(getBillsSummary);
const mockedGetFreeMoney = vi.mocked(getFreeMoney);
const mockedGetMonthHealth = vi.mocked(getMonthHealth);
const mockedGetMonthlyBudget = vi.mocked(getMonthlyBudget);
const mockedGetMonthlySummary = vi.mocked(getMonthlySummary);
const mockedListAccounts = vi.mocked(listAccounts);
const mockedListBills = vi.mocked(listBills);
const mockedListCategories = vi.mocked(listCategories);
const mockedListTransactions = vi.mocked(listTransactions);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

function createFreeMoneyResponse(
  overrides?: Partial<FreeMoneyResponse>,
): FreeMoneyResponse {
  return {
    month: 3,
    year: 2026,
    totalIncome: 0,
    totalExpense: 0,
    balance: 0,
    totalPlannedBudget: 0,
    totalBudgetSpent: 0,
    totalBudgetRemaining: 0,
    plannedReserve: 0,
    unpaidBillsReserve: 0,
    predictableObligationsReserve: 0,
    recurringBillsReserve: 0,
    installmentBillsReserve: 0,
    predictableObligationsCount: 0,
    recurringBillsCount: 0,
    installmentBillsCount: 0,
    freeToSpend: 0,
    ...overrides,
  };
}

function createBillsSummaryResponse(
  overrides?: Partial<BillsSummaryResponse>,
): BillsSummaryResponse {
  return {
    totalPending: 0,
    totalOverdue: 0,
    totalPaid: 0,
    predictableTotal: 0,
    recurringTotal: 0,
    installmentTotal: 0,
    countPending: 0,
    countOverdue: 0,
    countPaid: 0,
    countPredictable: 0,
    countRecurring: 0,
    countInstallment: 0,
    upcoming: [],
    ...overrides,
  };
}

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
  categories?: Array<{
    id: string;
    name: string;
    type: 1 | 2;
    isSystem: boolean;
  }>;
  monthHealth?: {
    status: "healthy" | "attention" | "critical";
    message?: string;
    reasons?: string[];
    actions?: string[];
    priority?: number;
    recommendedActions?: Array<{
      id: string;
      label: string;
      target: string;
    }>;
    summary: {
      message: string;
      cause: string;
      action: string;
    };
    insights: Array<{
      type: string;
      severity: "high" | "medium";
      priority: number;
      message: string;
      cause: string;
      action: string;
    }>;
  } | null;
  alerts?: {
    alerts: Array<{
      type: string;
      severity: "high" | "medium";
      message: string;
      amount: number;
      actionUrl?: string | null;
    }>;
  };
  billsSummary?: Partial<BillsSummaryResponse>;
  freeMoney?: Partial<FreeMoneyResponse>;
}) {
  mockedGetMonthHealth.mockResolvedValue(
    overrides?.monthHealth ?? {
      status: "healthy",
      summary: {
        message: "Seu mes esta sob controle ate aqui.",
        cause: "Voce mantem folga no mes e sem sinais fortes de pressao imediata.",
        action:
          "Continue acompanhando orcamento e vencimentos para manter a margem.",
      },
      insights: [],
    },
  );
  mockedGetMonthlySummary.mockResolvedValue({
    month: 3,
    year: 2026,
    totalIncome: 0,
    totalExpense: 0,
    balance: 0,
    byCategory: [],
  });
  mockedGetAlerts.mockResolvedValue(overrides?.alerts ?? { alerts: [] });
  mockedGetBillsSummary.mockResolvedValue(
    createBillsSummaryResponse(overrides?.billsSummary),
  );
  mockedGetFreeMoney.mockResolvedValue(createFreeMoneyResponse(overrides?.freeMoney));
  mockedGetMonthlyBudget.mockResolvedValue({
    month: 3,
    year: 2026,
    totalPlanned: 0,
    totalSpent: 0,
    totalRemaining: 0,
    categories: [],
  });
  mockedListAccounts.mockResolvedValue(overrides?.accounts ?? []);
  mockedListCategories.mockResolvedValue(overrides?.categories ?? []);
  mockedListTransactions.mockResolvedValue(overrides?.transactions ?? []);
  mockedListBills.mockResolvedValue(overrides?.bills ?? []);
}

describe("DashboardPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedCreateAccount.mockReset();
    mockedCreateTransaction.mockReset();
    mockedGetAlerts.mockReset();
    mockedGetBillsSummary.mockReset();
    mockedGetFreeMoney.mockReset();
    mockedGetMonthHealth.mockReset();
    mockedGetMonthlyBudget.mockReset();
    mockedGetMonthlySummary.mockReset();
    mockedListAccounts.mockReset();
    mockedListBills.mockReset();
    mockedListCategories.mockReset();
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

  it("Dashboard_NewUser_ShowsFirstAccountPathInsideQuickEntry", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockDashboardApi();

    render(<DashboardPage />);

    expect(
      await screen.findByText("Comece criando sua primeira conta no Farol."),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Sem uma conta financeira/i),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /criar primeira conta/i })).toBeInTheDocument();
    expect(await screen.findAllByText("Criar primeira conta")).toHaveLength(2);
    expect(screen.getByLabelText(/nome da conta/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^tipo$/i)).toBeInTheDocument();
    expect(screen.queryByText("Resumo financeiro")).not.toBeInTheDocument();
    expect(screen.queryByText("Contas a pagar do mês")).not.toBeInTheDocument();
    expect(screen.queryByText("Leitura por categoria")).not.toBeInTheDocument();
  });

  it("Dashboard_CriticalHealth_ShowsProminentCriticalBlock", async () => {
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
      monthHealth: {
        status: "critical",
        message: "Saldo negativo e contas atrasadas.",
        reasons: [
          "Conta de energia vencida há 8 dias",
          "Saldo do mês está em -R$ 1.200,00",
        ],
        actions: [
          "Priorize o pagamento das contas fixas vencidas",
          "Suspenda despesas não essenciais até estabilizar o saldo",
        ],
        priority: 110,
        summary: {
          message: "Risco financeiro crítico detectado.",
          cause: "A análise mostra alta pressão de caixa e dívidas em atraso.",
          action: "Execute imediatamente um plano de pagamento prioritário.",
        },
        recommendedActions: [
          {
            id: "review_expenses",
            label: "Ver saídas do mês",
            target: "/transactions",
          },
          {
            id: "review_overdue_bills",
            label: "Ver contas vencidas",
            target: "/bills?status=overdue",
          },
        ],
        insights: [
          {
            type: "negative_free_money",
            severity: "high",
            priority: 90,
            message: "Seu dinheiro livre ficou negativo neste mes.",
            cause: "Depois dos gastos e do que ainda esta reservado, faltou folga para fechar o mes.",
            action: "Segure novos gastos e revise as maiores saidas para abrir espaco para o essencial.",
          },
        ],
      },
      alerts: {
        alerts: [
          {
            type: "overdue_bills",
            severity: "high",
            message: "Conta de energia vencida há 8 dias",
            amount: 240,
            actionUrl: "/bills?status=overdue",
          },
          {
            type: "low_balance",
            severity: "medium",
            message: "Saldo do mês está em -R$ 1.200,00",
            amount: 1200,
            actionUrl: "/transactions",
          },
        ],
      },
    });

    render(<DashboardPage />);
    const user = userEvent.setup();

    expect(await screen.findByText("Risco financeiro crítico")).toBeInTheDocument();
    expect(screen.getByText("Saldo negativo e contas atrasadas.")).toBeInTheDocument();
    expect(screen.getByText("O que isso significa no mes")).toBeInTheDocument();
    expect(screen.getByText("O que esta pesando agora")).toBeInTheDocument();
    expect(screen.getAllByText("Conta de energia vencida há 8 dias")).toHaveLength(2);
    expect(screen.getByText("Comece por aqui")).toBeInTheDocument();
    expect(screen.getByText("Por que comecar por isso")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Revisar as maiores saidas primeiro mostra o que pode ser cortado ou adiado antes de faltar para o essencial.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Voce nao precisa resolver tudo hoje. Comece pelo que protege sua rotina e seu caixa neste mes.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Alertas visíveis no mês")).toBeInTheDocument();
    expect(screen.getByText(/240,00/)).toBeInTheDocument();
    expect(screen.getByText(/Priorize o pagamento das contas fixas vencidas/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ver saídas do mês" })).toHaveAttribute(
      "href",
      "/transactions",
    );
    expect(screen.getAllByRole("link", { name: "Abrir alerta" })).toHaveLength(2);

    await user.click(
      screen.getByRole("button", {
        name: /Abrir focos do mes \(3\)/i,
      }),
    );

    expect(
      screen.getByRole("link", {
        name: /Abrir foco do mes: Saldo do mês está em -R\$ 1.200,00/i,
      }),
    ).toHaveAttribute("href", "/transactions");
  });

  it("Dashboard_FirstUseWithAccount_ShowsPathToAddOrImportData", async () => {
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
    });

    render(<DashboardPage />);

    expect(
      await screen.findByText("Seu mês ainda não tem dados suficientes."),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/ainda faltam movimenta/i),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /registrar agora/i }),
    ).toBeInTheDocument();
    expect(
      screen.getAllByRole("link", { name: /importar (dados de )?arquivo/i }),
    ).not.toHaveLength(0);
    expect(screen.getByText("Suas contas financeiras")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /^adicionar conta$/i }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Resumo financeiro")).not.toBeInTheDocument();
    expect(screen.queryByText("Contas a pagar do mês")).not.toBeInTheDocument();
    expect(screen.queryByText("Leitura por categoria")).not.toBeInTheDocument();
  });

  it("Dashboard_WithExistingAccount_AllowsCreatingAdditionalAccount", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    const primaryAccount = {
      id: "account-1",
      name: "Conta principal",
      type: 2 as const,
      isActive: true,
      createdAtUtc: "2026-03-01T00:00:00Z",
    };
    const secondaryAccount = {
      id: "account-2",
      name: "Cartão do dia a dia",
      type: 3 as const,
      isActive: true,
      createdAtUtc: "2026-03-02T00:00:00Z",
    };

    mockDashboardApi({
      accounts: [primaryAccount],
    });
    mockedListAccounts.mockResolvedValueOnce([primaryAccount]).mockResolvedValue([
      primaryAccount,
      secondaryAccount,
    ]);
    mockedCreateAccount.mockResolvedValue(secondaryAccount);

    render(<DashboardPage />);

    expect(await screen.findByText("Suas contas financeiras")).toBeInTheDocument();
    expect(screen.getAllByText("Conta principal").length).toBeGreaterThan(0);

    await user.type(screen.getByLabelText(/nome da conta/i), "Cartão do dia a dia");
    await user.selectOptions(screen.getByLabelText(/tipo da conta/i), "3");
    await user.click(screen.getByRole("button", { name: /^adicionar conta$/i }));

    await waitFor(() => {
      expect(mockedCreateAccount).toHaveBeenCalledWith("token", {
        name: "Cartão do dia a dia",
        type: 3,
      });
    });

    expect(
      await screen.findByText(
        "Conta adicionada com sucesso. Agora escolha em qual conta deseja registrar a próxima movimentação ou importação.",
      ),
    ).toBeInTheDocument();
    expect(screen.getAllByText("Cartão do dia a dia").length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/conta financeira/i)).not.toBeDisabled();
  });

  it("Dashboard_RegisterNow_SubmitSuccess_RefreshesDataAndClearsForm", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedCreateTransaction.mockResolvedValue({
      id: "transaction-2",
      financialAccountId: "account-1",
      categoryId: null,
      type: 2,
      amount: 85,
      description: "Saída rápida",
      occurredOn: "2026-03-21",
      createdAtUtc: "2026-03-21T00:00:00Z",
    });
    mockedGetMonthHealth
      .mockResolvedValueOnce({
        status: "healthy",
        summary: {
          message: "Seu mês ainda não tem dados suficientes.",
          cause:
            "Você já tem conta, mas ainda faltam movimentações ou vencimentos para o Farol montar seu primeiro estado do mês.",
          action:
            "Registre uma entrada agora ou importe um arquivo para chegar ao primeiro insight do mês.",
        },
        insights: [],
      })
      .mockResolvedValue({
        status: "attention",
        summary: {
          message: "Seu mês pede alguns ajustes agora.",
          cause: "As contas pendentes já consomem a maior parte da sua folga.",
          action: "Organize a ordem de pagamento e preserve caixa para o essencial.",
        },
        insights: [],
      });
    mockedGetMonthlySummary
      .mockResolvedValueOnce({
        month: 3,
        year: 2026,
        totalIncome: 0,
        totalExpense: 0,
        balance: 0,
        byCategory: [],
      })
      .mockResolvedValue({
        month: 3,
        year: 2026,
        totalIncome: 0,
        totalExpense: 85,
        balance: -85,
        byCategory: [],
      });
    mockedGetAlerts.mockResolvedValue({ alerts: [] });
    mockedGetBillsSummary.mockResolvedValue(createBillsSummaryResponse());
    mockedGetFreeMoney.mockResolvedValue({
      ...createFreeMoneyResponse({
        totalExpense: 85,
        balance: -85,
        freeToSpend: -85,
      }),
    });
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([
      {
        id: "account-1",
        name: "Conta principal",
        type: 2,
        isActive: true,
        createdAtUtc: "2026-03-01T00:00:00Z",
      },
    ]);
    mockedListCategories.mockResolvedValue([
      {
        id: "category-1",
        name: "Mercado",
        type: 2,
        isSystem: true,
      },
    ]);
    mockedListTransactions
      .mockResolvedValueOnce([])
      .mockResolvedValue([
        {
          id: "transaction-2",
          financialAccountId: "account-1",
          categoryId: null,
          type: 2,
          amount: 85,
          description: "Saída rápida",
          occurredOn: "2026-03-21",
          createdAtUtc: "2026-03-21T00:00:00Z",
        },
      ]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    expect(await screen.findByLabelText(/conta financeira/i)).toBeDisabled();
    await user.type(await screen.findByLabelText(/quanto foi/i), "85");
    await user.click(screen.getByRole("button", { name: /adicionar descrição/i }));
    await user.type(screen.getByLabelText(/descrição \(se quiser\)/i), "Mercado");
    await user.click(screen.getByRole("button", { name: /registrar saída/i }));

    await waitFor(() => {
      expect(mockedCreateTransaction).toHaveBeenCalledWith("token", {
        financialAccountId: "account-1",
        categoryId: undefined,
        type: 2,
        amount: 85,
        description: "Mercado",
        occurredOn: expect.any(String),
      });
    });

    expect(await screen.findByText("Registrado.")).toBeInTheDocument();
    expect(screen.getByLabelText(/quanto foi/i)).toHaveValue(null);
    expect(
      screen.queryByLabelText(/descrição \(se quiser\)/i),
    ).not.toBeInTheDocument();
    expect(mockedGetMonthlySummary).toHaveBeenCalledTimes(2);
  });

  it("Dashboard_RegisterNow_WithEntryShortcut_UsesSelectedTypeAndDefaultDescription", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedCreateTransaction.mockResolvedValue({
      id: "transaction-3",
      financialAccountId: "account-1",
      categoryId: null,
      type: 1,
      amount: 1200,
      description: "Entrada rápida",
      occurredOn: "2026-03-21",
      createdAtUtc: "2026-03-21T00:00:00Z",
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
      categories: [
        {
          id: "category-1",
          name: "Salário",
          type: 1,
          isSystem: true,
        },
        {
          id: "category-2",
          name: "Mercado",
          type: 2,
          isSystem: true,
        },
      ],
    });

    render(<DashboardPage />);

    await user.click(await screen.findByRole("button", { name: /entrou dinheiro/i }));
    await user.type(screen.getByLabelText(/quanto foi/i), "1200");
    await user.click(screen.getByRole("button", { name: /registrar entrada/i }));

    await waitFor(() => {
      expect(mockedCreateTransaction).toHaveBeenCalledWith("token", {
        financialAccountId: "account-1",
        categoryId: undefined,
        type: 1,
        amount: 1200,
        description: "Entrada rápida",
        occurredOn: expect.any(String),
      });
    });
  });

  it("Dashboard_RegisterNow_SwitchingTypeUpdatesVisibleCategories", async () => {
    const user = userEvent.setup();

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
      categories: [
        {
          id: "category-1",
          name: "Salário",
          type: 1,
          isSystem: true,
        },
        {
          id: "category-2",
          name: "Mercado",
          type: 2,
          isSystem: true,
        },
      ],
    });

    render(<DashboardPage />);

    expect(await screen.findByRole("button", { name: "Mercado" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Salário" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /entrou dinheiro/i }));

    expect(await screen.findByRole("button", { name: "Salário" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mercado" })).not.toBeInTheDocument();
  });

  it("Dashboard_RegisterNow_WithMultipleAccounts_RequiresExplicitSelection", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedCreateTransaction.mockResolvedValue({
      id: "transaction-4",
      financialAccountId: "account-2",
      categoryId: null,
      type: 2,
      amount: 220,
      description: "Mercado",
      occurredOn: "2026-03-21",
      createdAtUtc: "2026-03-21T00:00:00Z",
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
        {
          id: "account-2",
          name: "Cartão do dia a dia",
          type: 3,
          isActive: true,
          createdAtUtc: "2026-03-02T00:00:00Z",
        },
      ],
    });

    render(<DashboardPage />);

    const accountSelect = await screen.findByLabelText(/conta financeira/i);

    expect(accountSelect).not.toBeDisabled();

    await user.type(screen.getByLabelText(/quanto foi/i), "220");
    await user.click(screen.getByRole("button", { name: /registrar saída/i }));

    expect(
      await screen.findByText(
        "Escolha a conta em que essa movimentação deve ser registrada.",
      ),
    ).toBeInTheDocument();
    expect(mockedCreateTransaction).not.toHaveBeenCalled();

    await user.selectOptions(accountSelect, "account-2");
    await user.click(screen.getByRole("button", { name: /registrar saída/i }));

    await waitFor(() => {
      expect(mockedCreateTransaction).toHaveBeenCalledWith("token", {
        financialAccountId: "account-2",
        categoryId: undefined,
        type: 2,
        amount: 220,
        description: "Saída rápida",
        occurredOn: expect.any(String),
      });
    });
  });

  it("Dashboard_RegisterNow_ShowsCategoryChoicesAndKeepsDescriptionCollapsed", async () => {
    const user = userEvent.setup();

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
    });

    render(<DashboardPage />);

    expect(await screen.findByLabelText(/quanto foi/i)).toHaveFocus();
    expect(
      screen.getByRole("button", { name: /registrar saída/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sem categoria/i })).toBeInTheDocument();
    expect(
      screen.queryByLabelText(/descrição \(se quiser\)/i),
    ).not.toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: /adicionar descrição/i }),
    );

    expect(screen.getByLabelText(/descrição \(se quiser\)/i)).toBeInTheDocument();
  });

  it("Dashboard_WithMonthHealth_ShowsSummaryCauseAndAction", async () => {
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
      monthHealth: {
        status: "attention",
        summary: {
          message: "Seu mes pede alguns ajustes agora.",
          cause: "As contas pendentes ja consomem a maior parte da sua folga.",
          action: "Organize a ordem de pagamento e preserve caixa para o essencial.",
        },
        insights: [],
      },
    });

    render(<DashboardPage />);

    expect(
      await screen.findByText("Seu mes pede alguns ajustes agora."),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "As contas pendentes ja consomem a maior parte da sua folga.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Organize a ordem de pagamento e preserve caixa para o essencial.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Resumo financeiro")).toBeInTheDocument();
    expect(screen.getByText("Entradas x saídas")).toBeInTheDocument();
    expect(screen.getByText("Planejado x gasto")).toBeInTheDocument();
    expect(screen.queryByText("Alertas do mês")).not.toBeInTheDocument();
  });

  it("Dashboard_FreeMoneyComposition_ShowsPlannedAndUnpaidReservesBelowDinheiroLivre", async () => {
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
      freeMoney: {
        totalIncome: 3000,
        balance: 3000,
        totalPlannedBudget: 1200,
        totalBudgetRemaining: 1200,
        plannedReserve: 1200,
        unpaidBillsReserve: 850,
        freeToSpend: 1800,
      },
    });

    render(<DashboardPage />);

    expect(await screen.findByText("Resumo financeiro")).toBeInTheDocument();
    expect(screen.getByText("Dinheiro livre")).toBeInTheDocument();
    expect(screen.getByText("Reservado no planejamento")).toBeInTheDocument();
    expect(screen.getByText("Em contas em aberto")).toBeInTheDocument();
    expect(screen.getByText(/1\.200,00/)).toBeInTheDocument();
    expect(screen.getByText(/850,00/)).toBeInTheDocument();
    expect(
      screen.getByText("Esse valor já desconta as contas em aberto do mês."),
    ).toBeInTheDocument();
  });

  it("Dashboard_PredictableObligations_ShowsRecurringAndInstallmentPressure", async () => {
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
          amount: 4200,
          description: "Salario",
          occurredOn: "2026-03-01",
          createdAtUtc: "2026-03-01T00:00:00Z",
        },
      ],
      billsSummary: {
        totalPending: 1680,
        predictableTotal: 1460,
        recurringTotal: 960,
        installmentTotal: 500,
        countPending: 4,
        countPredictable: 3,
        countRecurring: 2,
        countInstallment: 1,
        upcoming: [
          {
            id: "bill-1",
            description: "Internet fibra",
            amount: 160,
            dueOn: "2026-03-12",
            status: "pending",
            seriesKind: "recurring",
          },
          {
            id: "bill-2",
            description: "Notebook",
            amount: 500,
            dueOn: "2026-03-15",
            status: "pending",
            seriesKind: "installment",
            occurrenceNumber: 3,
            totalOccurrences: 12,
          },
        ],
      },
      freeMoney: {
        totalIncome: 4200,
        balance: 4200,
        unpaidBillsReserve: 1680,
        predictableObligationsReserve: 1460,
        recurringBillsReserve: 960,
        installmentBillsReserve: 500,
        predictableObligationsCount: 3,
        recurringBillsCount: 2,
        installmentBillsCount: 1,
        freeToSpend: 1520,
      },
    });

    render(<DashboardPage />);

    expect(await screen.findByText("Compromissos previsiveis em aberto")).toBeInTheDocument();
    expect(screen.getByText("Recorrentes")).toBeInTheDocument();
    expect(screen.getByText("Parceladas")).toBeInTheDocument();
    expect(screen.getAllByText(/1\.460,00/).length).toBeGreaterThan(0);
    expect(
      screen.getByText(
        "Desse total em contas abertas, R$ 1.460,00 ja vem de contas recorrentes e parcelas.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("Recorrente")).toBeInTheDocument();
    expect(screen.getByText("Parcela 3/12")).toBeInTheDocument();
  });

  it("Dashboard_WithRecommendedActions_ShowsPrioritizedLinks", async () => {
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
      monthHealth: {
        status: "attention",
        summary: {
          message: "Seu mes pede alguns ajustes agora.",
          cause: "As contas pendentes ja consomem a maior parte da sua folga.",
          action: "Organize a ordem de pagamento e preserve caixa para o essencial.",
        },
        recommendedActions: [
          {
            id: "review_overdue_bills",
            label: "Ver contas vencidas",
            target: "/bills?status=overdue",
          },
          {
            id: "review_expenses",
            label: "Revisar gastos do mes",
            target: "/transactions",
          },
          {
            id: "adjust_budget",
            label: "Ajustar orcamento",
            target: "/budget",
          },
          {
            id: "keep_tracking",
            label: "Continuar acompanhando",
            target: "/dashboard",
          },
        ],
        insights: [],
      },
    });

    render(<DashboardPage />);

    expect(await screen.findByText("Prioridades do momento")).toBeInTheDocument();

    const actionLinks = screen.getAllByRole("link").filter((link) =>
      /^\d+\./i.test(link.textContent ?? ""),
    );

    expect(actionLinks).toHaveLength(3);
    expect(screen.getByRole("link", { name: /1\. ver contas vencidas/i })).toHaveAttribute(
      "href",
      "/bills?status=overdue",
    );
    expect(screen.getByRole("link", { name: /2\. revisar gastos do mes/i })).toHaveAttribute(
      "href",
      "/transactions",
    );
    expect(screen.getByRole("link", { name: /3\. ajustar orcamento/i })).toHaveAttribute(
      "href",
      "/budget",
    );
    expect(screen.queryByRole("link", { name: /continuar acompanhando/i })).not.toBeInTheDocument();
  });

  it("Dashboard_WithMonthHealth_ShowsActiveInsights", async () => {
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
      monthHealth: {
        status: "critical",
        summary: {
          message: "Voce esta no vermelho neste mes.",
          cause: "Depois das despesas e compromissos, seu dinheiro livre ficou negativo.",
          action: "Pause novos gastos e revise as maiores saidas do mes.",
        },
        insights: [
          {
            type: "negative_free_money",
            severity: "high",
            priority: 90,
            message: "Voce esta no vermelho neste mes.",
            cause:
              "Depois das despesas e compromissos, seu dinheiro livre ficou negativo.",
            action: "Pause novos gastos e revise as maiores saidas do mes.",
          },
          {
            type: "budget_overspent",
            severity: "medium",
            priority: 70,
            message: "Seu orcamento do mes ja saiu do plano.",
            cause: "Voce gastou mais do que planejou nas categorias acompanhadas.",
            action: "Reduza gastos ajustaveis e reavalie o restante do mes.",
          },
        ],
      },
    });

    render(<DashboardPage />);

    expect(
      (await screen.findAllByText("Voce esta no vermelho neste mes.")).length,
    ).toBeGreaterThanOrEqual(2);
    expect(
      screen.getByText("Seu orcamento do mes ja saiu do plano."),
    ).toBeInTheDocument();
    expect(screen.getByText("#90")).toBeInTheDocument();
    expect(screen.getByText("#70")).toBeInTheDocument();
  });

  it("Dashboard_UserWithCompletedOnboarding_RemovesOldGuidanceBlocks", async () => {
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

    expect(await screen.findByText("O que você fez hoje com seu dinheiro?")).toBeInTheDocument();
    expect(screen.queryByText("Comece por aqui")).not.toBeInTheDocument();
    expect(screen.queryByText("O que fazer em seguida")).not.toBeInTheDocument();
    expect(screen.queryByText("Reserva e execução")).not.toBeInTheDocument();
    expect(screen.queryByText("Base para movimentações e importação")).not.toBeInTheDocument();
  });

  it("Dashboard_LoadError_ShowsReadableErrorState", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedGetMonthHealth.mockResolvedValue({
      status: "healthy",
      summary: {
        message: "Seu mes esta sob controle ate aqui.",
        cause: "Voce mantem folga no mes e sem sinais fortes de pressao imediata.",
        action:
          "Continue acompanhando orcamento e vencimentos para manter a margem.",
      },
      insights: [],
    });
    mockedGetMonthlySummary.mockRejectedValue(
      new ApiError(
        "Npgsql.PostgresException: relation \"bills\" does not exist at DashboardController.cs:117",
        500,
      ),
    );
    mockedGetAlerts.mockResolvedValue({ alerts: [] });
    mockedGetBillsSummary.mockResolvedValue(createBillsSummaryResponse());
    mockedGetFreeMoney.mockResolvedValue(createFreeMoneyResponse());
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListCategories.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    expect(await screen.findByText("Falha ao carregar")).toBeInTheDocument();
    expect(screen.getByText("Não foi possível abrir o dashboard")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Nao foi possivel carregar o dashboard agora. Tente novamente em alguns instantes.",
      ),
    ).toBeInTheDocument();
  });

  it("Dashboard_WhenMonthHealthFails_DegradesWithoutBreakingThePage", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedGetMonthHealth.mockRejectedValue(
      new ApiError(
        "Npgsql.PostgresException: relation \"month_health\" does not exist at InsightsController.cs:117",
        500,
      ),
    );
    mockedGetMonthlySummary.mockResolvedValue({
      month: 3,
      year: 2026,
      totalIncome: 0,
      totalExpense: 0,
      balance: 0,
      byCategory: [],
    });
    mockedGetAlerts.mockResolvedValue({
      alerts: [
        {
          type: "overdue_bills",
          severity: "high",
          message: "Voce tem contas vencidas que precisam de atencao imediata.",
          amount: 300,
          actionUrl: "/bills?status=overdue",
        },
      ],
    });
    mockedGetBillsSummary.mockResolvedValue(createBillsSummaryResponse());
    mockedGetFreeMoney.mockResolvedValue(createFreeMoneyResponse());
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListCategories.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    expect(await screen.findByText("Resumo financeiro")).toBeInTheDocument();
    expect(screen.queryByText("Visão do mês")).not.toBeInTheDocument();
    expect(screen.getByText("Alertas do mês")).toBeInTheDocument();
    expect(
      screen.getByText("O dashboard já encontrou sinais que pedem atenção."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Voce tem contas vencidas que precisam de atencao imediata."),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Abrir alerta" })).toHaveAttribute(
      "href",
      "/bills?status=overdue",
    );
    expect(screen.getByText("Base do mês")).toBeInTheDocument();
    expect(
      screen.queryByText(/month_health|Npgsql|PostgresException/i),
    ).not.toBeInTheDocument();
  });

  it("Dashboard_WhenMonthHealthIsUnavailable_UsesAlertActionAsNotificationFallback", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedGetMonthHealth.mockRejectedValue(new ApiError("month health unavailable", 500));
    mockedGetMonthlySummary.mockResolvedValue({
      month: 3,
      year: 2026,
      totalIncome: 0,
      totalExpense: 0,
      balance: 0,
      byCategory: [],
    });
    mockedGetAlerts.mockResolvedValue({
      alerts: [
        {
          type: "low_balance",
          severity: "medium",
          message: "Seu dinheiro livre para o mes esta baixo.",
          amount: 150,
          actionUrl: "/transactions",
        },
      ],
    });
    mockedGetBillsSummary.mockResolvedValue(createBillsSummaryResponse());
    mockedGetFreeMoney.mockResolvedValue(createFreeMoneyResponse());
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListCategories.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    expect(await screen.findByText("Resumo financeiro")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: /Abrir focos do mes \(1\)/i,
      }),
    );

    expect(screen.getByRole("dialog", { name: "Focos do mes" })).toBeInTheDocument();
    expect(
      screen.getByRole("link", {
        name: /Abrir foco do mes: Seu dinheiro livre para o mes esta baixo\./i,
      }),
    ).toHaveAttribute("href", "/transactions");
  });

  it("Dashboard_WhenSessionExpires_TriggersConsistentLogout", async () => {
    const logout = vi.fn();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout,
    });
    mockedGetMonthHealth.mockResolvedValue({
      status: "healthy",
      summary: {
        message: "Seu mes esta sob controle ate aqui.",
        cause: "Voce mantem folga no mes e sem sinais fortes de pressao imediata.",
        action:
          "Continue acompanhando orcamento e vencimentos para manter a margem.",
      },
      insights: [],
    });
    mockedGetMonthlySummary.mockRejectedValue(
      new ApiError("Invalid access token.", 401),
    );
    mockedGetAlerts.mockResolvedValue({ alerts: [] });
    mockedGetBillsSummary.mockResolvedValue(createBillsSummaryResponse());
    mockedGetFreeMoney.mockResolvedValue(createFreeMoneyResponse());
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedListAccounts.mockResolvedValue([]);
    mockedListCategories.mockResolvedValue([]);
    mockedListTransactions.mockResolvedValue([]);
    mockedListBills.mockResolvedValue([]);

    render(<DashboardPage />);

    await waitFor(() => {
      expect(logout).toHaveBeenCalledWith("session-expired");
    });
  });
});
