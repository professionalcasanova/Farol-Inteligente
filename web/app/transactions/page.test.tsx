import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import TransactionsPage from "@/app/transactions/page";
import {
  ApiError,
  createTransaction,
  deleteTransaction,
  listAccounts,
  listCategories,
  listTransactions,
  updateTransaction,
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

vi.mock("@/lib/use-protected-session", () => ({
  useProtectedSession: vi.fn(),
}));

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    createTransaction: vi.fn(),
    deleteTransaction: vi.fn(),
    listAccounts: vi.fn(),
    listCategories: vi.fn(),
    listTransactions: vi.fn(),
    updateTransaction: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedCreateTransaction = vi.mocked(createTransaction);
const mockedDeleteTransaction = vi.mocked(deleteTransaction);
const mockedListAccounts = vi.mocked(listAccounts);
const mockedListCategories = vi.mocked(listCategories);
const mockedListTransactions = vi.mocked(listTransactions);
const mockedUpdateTransaction = vi.mocked(updateTransaction);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

function mockTransactionsApi(overrides?: {
  accounts?: Array<{
    id: string;
    name: string;
    type: 1 | 2 | 3 | 4;
    isActive: boolean;
    createdAtUtc: string;
  }>;
  categories?: Array<{
    id: string;
    name: string;
    type: 1 | 2;
    isSystem: boolean;
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
  mockedListAccounts.mockResolvedValue(
    overrides?.accounts ?? [
      {
        id: "account-1",
        name: "Conta principal",
        type: 2,
        isActive: true,
        createdAtUtc: "2026-03-01T00:00:00Z",
      },
    ],
  );

  mockedListCategories.mockResolvedValue(
    overrides?.categories ?? [
      {
        id: "category-1",
        name: "Mercado",
        type: 2,
        isSystem: true,
      },
      {
        id: "category-2",
        name: "Salario",
        type: 1,
        isSystem: true,
      },
    ],
  );

  mockedListTransactions.mockResolvedValue(
    overrides?.transactions ?? [
      {
        id: "transaction-1",
        financialAccountId: "account-1",
        categoryId: "category-1",
        type: 2,
        amount: 85,
        description: "Mercado",
        occurredOn: "2026-03-21",
        createdAtUtc: "2026-03-21T00:00:00Z",
      },
    ],
  );
}

describe("TransactionsPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedCreateTransaction.mockReset();
    mockedDeleteTransaction.mockReset();
    mockedListAccounts.mockReset();
    mockedListCategories.mockReset();
    mockedListTransactions.mockReset();
    mockedUpdateTransaction.mockReset();
    window.history.replaceState({}, "", "/transactions");
  });

  it("Transactions_EditTransaction_PopulatesFormAndSavesUpdate", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockTransactionsApi({
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
          name: "Carteira do dia a dia",
          type: 1,
          isActive: true,
          createdAtUtc: "2026-03-02T00:00:00Z",
        },
      ],
      transactions: [
        {
          id: "transaction-1",
          financialAccountId: "account-2",
          categoryId: "category-1",
          type: 2,
          amount: 85,
          description: "Mercado",
          occurredOn: "2026-03-21",
          createdAtUtc: "2026-03-21T00:00:00Z",
        },
      ],
    });

    mockedUpdateTransaction.mockResolvedValue({
      id: "transaction-1",
      financialAccountId: "account-2",
      categoryId: "category-1",
      type: 2,
      amount: 120,
      description: "Mercado da semana",
      occurredOn: "2026-03-21",
      createdAtUtc: "2026-03-21T00:00:00Z",
    });

    mockedListTransactions.mockReset();
    mockedListTransactions
      .mockResolvedValueOnce([
        {
          id: "transaction-1",
          financialAccountId: "account-2",
          categoryId: "category-1",
          type: 2,
          amount: 85,
          description: "Mercado",
          occurredOn: "2026-03-21",
          createdAtUtc: "2026-03-21T00:00:00Z",
        },
      ])
      .mockResolvedValue([
        {
          id: "transaction-1",
          financialAccountId: "account-2",
          categoryId: "category-1",
          type: 2,
          amount: 120,
          description: "Mercado da semana",
          occurredOn: "2026-03-21",
          createdAtUtc: "2026-03-21T00:00:00Z",
        },
      ]);

    render(<TransactionsPage />);

    await user.click(await screen.findByRole("button", { name: "Editar" }));

    expect(await screen.findByText(/Editando transa/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/conta financeira/i)).toHaveValue("account-2");
    expect(screen.getByLabelText(/descri/i)).toHaveValue("Mercado");

    await user.clear(screen.getByLabelText(/valor/i));
    await user.type(screen.getByLabelText(/valor/i), "120");
    await user.clear(screen.getByLabelText(/descri/i));
    await user.type(screen.getByLabelText(/descri/i), "Mercado da semana");
    await user.click(screen.getByRole("button", { name: /salvar altera/i }));

    await waitFor(() => {
      expect(mockedUpdateTransaction).toHaveBeenCalledWith("token", "transaction-1", {
        financialAccountId: "account-2",
        categoryId: "category-1",
        type: 2,
        amount: 120,
        description: "Mercado da semana",
        occurredOn: "2026-03-21",
      });
    });

    expect(await screen.findByText(/atualizada com sucesso/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /criar transa/i })).toBeInTheDocument();
  });

  it("Transactions_EditTransaction_CancelRestoresCreateMode", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockTransactionsApi();

    render(<TransactionsPage />);

    await user.click(await screen.findByRole("button", { name: "Editar" }));

    expect(await screen.findByText(/Editando transa/i)).toBeInTheDocument();

    await user.clear(screen.getByLabelText(/descri/i));
    await user.type(screen.getByLabelText(/descri/i), "Texto temporario");
    await user.click(screen.getByRole("button", { name: /cancelar edi/i }));

    expect(screen.getByText(/Nova transa/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /criar transa/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/descri/i)).toHaveValue("");
    expect(
      screen.queryByRole("button", { name: /cancelar edi/i }),
    ).not.toBeInTheDocument();
    expect(mockedUpdateTransaction).not.toHaveBeenCalled();
  });

  it("Transactions_QueryEdit_OpensRequestedTransactionForCorrection", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    window.history.replaceState({}, "", "/transactions?edit=transaction-1");

    mockTransactionsApi();

    render(<TransactionsPage />);

    expect(await screen.findByText(/Editando transa/i)).toBeInTheDocument();
    expect(
      screen.getByText(/Este lan.* veio do dashboard\. Corrija aqui ou exclua/i),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/descri/i)).toHaveValue("Mercado");
  });

  it("Transactions_DeleteTransaction_RemovesManualEntryWithConfirmation", async () => {
    const user = userEvent.setup();
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedDeleteTransaction.mockResolvedValue(undefined);
    window.history.replaceState({}, "", "/transactions?edit=transaction-1");
    mockTransactionsApi();
    mockedListTransactions
      .mockResolvedValueOnce([
        {
          id: "transaction-1",
          financialAccountId: "account-1",
          categoryId: "category-1",
          type: 2,
          amount: 85,
          description: "Mercado",
          occurredOn: "2026-03-21",
          createdAtUtc: "2026-03-21T00:00:00Z",
        },
      ])
      .mockResolvedValue([]);

    render(<TransactionsPage />);

    await screen.findByText(/Editando transa/i);
    await user.click(screen.getByRole("button", { name: /excluir transa/i }));

    await waitFor(() => {
      expect(mockedDeleteTransaction).toHaveBeenCalledWith("token", "transaction-1");
    });

    expect(await screen.findByText(/exclu.* com sucesso/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /criar transa/i })).toBeInTheDocument();

    confirmSpy.mockRestore();
  });

  it("Transactions_EditTransaction_WhenUpdateFails_ShowsFriendlyError", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedUpdateTransaction.mockRejectedValue(
      new ApiError("Category was not found.", 404),
    );

    mockTransactionsApi();

    render(<TransactionsPage />);

    await user.click(await screen.findByRole("button", { name: "Editar" }));
    await user.click(screen.getByRole("button", { name: /salvar altera/i }));

    expect(await screen.findByText(/categoria selecionada/i)).toBeInTheDocument();
  });

  it("Transactions_CreateWithMultipleAccounts_RequiresExplicitAccountSelection", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockTransactionsApi({
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
          name: "Carteira do dia a dia",
          type: 1,
          isActive: true,
          createdAtUtc: "2026-03-02T00:00:00Z",
        },
      ],
    });

    render(<TransactionsPage />);

    const accountSelect = await screen.findByLabelText(/conta financeira/i);

    expect(accountSelect).toHaveValue("");
    expect(
      screen.getByText(/PIX, boleto e cartão ficam para um campo futuro de meio de pagamento/i),
    ).toBeInTheDocument();

    await user.type(screen.getByLabelText(/valor/i), "90");
    await user.type(screen.getByLabelText(/descri/i), "Mercado");
    await user.click(screen.getByRole("button", { name: /criar transa/i }));

    expect(
      await screen.findByText(/Escolha a conta em que essa transa/i),
    ).toBeInTheDocument();
    expect(mockedCreateTransaction).not.toHaveBeenCalled();
    expect(mockedUpdateTransaction).not.toHaveBeenCalled();
  });
});
