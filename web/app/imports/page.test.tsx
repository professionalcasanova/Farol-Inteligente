import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ImportsPage from "@/app/imports/page";
import { importTransactionsCsv, listAccounts } from "@/lib/api";
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
    importTransactionsCsv: vi.fn(),
    listAccounts: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedImportTransactionsCsv = vi.mocked(importTransactionsCsv);
const mockedListAccounts = vi.mocked(listAccounts);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("ImportsPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedImportTransactionsCsv.mockReset();
    mockedListAccounts.mockReset();
  });

  it("Imports_SubmitWithoutFile_ShowsFriendlyValidation", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
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

    render(<ImportsPage />);

    await user.click(await screen.findByRole("button", { name: /enviar csv/i }));

    expect(
      await screen.findByText("Selecione um arquivo CSV antes de importar."),
    ).toBeInTheDocument();
  });

  it("Imports_HeaderOnlySuccess_ShowsHelpfulSummary", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
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

    mockedImportTransactionsCsv.mockResolvedValue({
      totalRows: 0,
      importedRows: 0,
      skippedRows: 0,
      errors: [],
    });

    render(<ImportsPage />);

    const input = (await screen.findByLabelText(/arquivo csv/i)) as HTMLInputElement;
    const file = new File(
      ["occurredOn,description,amount,type,categoryName\n"],
      "transactions.csv",
      { type: "text/csv" },
    );

    await user.upload(input, file);
    await user.click(screen.getByRole("button", { name: /enviar csv/i }));

    expect(await screen.findByText(/importar dados reais/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(mockedImportTransactionsCsv).toHaveBeenCalledTimes(1);
    });
  });

  it("Imports_WithMultipleAccounts_AllowsChoosingDestinationAccount", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListAccounts.mockResolvedValue([
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
    ]);

    mockedImportTransactionsCsv.mockResolvedValue({
      totalRows: 2,
      importedRows: 2,
      skippedRows: 0,
      errors: [],
    });

    render(<ImportsPage />);

    const fileInput = (await screen.findByLabelText(/arquivo csv/i)) as HTMLInputElement;
    const accountSelect = screen.getByLabelText(/conta de destino/i);
    const file = new File(
      ["occurredOn,description,amount,type,categoryName\n2026-03-01,Salario,3000.00,Income,Salario\n"],
      "transactions.csv",
      { type: "text/csv" },
    );

    await user.selectOptions(accountSelect, "account-2");
    await user.upload(fileInput, file);
    await user.click(screen.getByRole("button", { name: /enviar csv/i }));

    await waitFor(() => {
      expect(mockedImportTransactionsCsv).toHaveBeenCalledWith("token", {
        financialAccountId: "account-2",
        file,
      });
    });
  });

  it("Imports_OnlyActiveAccounts_AppearAsDestinations", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListAccounts.mockResolvedValue([
      {
        id: "account-1",
        name: "Conta principal",
        type: 2,
        isActive: true,
        createdAtUtc: "2026-03-01T00:00:00Z",
      },
      {
        id: "account-2",
        name: "Cartao antigo",
        type: 3,
        isActive: false,
        createdAtUtc: "2026-03-02T00:00:00Z",
      },
    ]);

    render(<ImportsPage />);

    const accountSelect = await screen.findByLabelText(/conta de destino/i);

    expect(accountSelect).toHaveValue("account-1");
    expect(screen.getByRole("option", { name: "Conta principal" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Cartao antigo" })).not.toBeInTheDocument();
  });

  it("Imports_WhenAllAccountsInactive_ShowsManagementGuidance", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListAccounts.mockResolvedValue([
      {
        id: "account-2",
        name: "Cartao antigo",
        type: 3,
        isActive: false,
        createdAtUtc: "2026-03-02T00:00:00Z",
      },
    ]);

    render(<ImportsPage />);

    expect(await screen.findByText(/Todas as suas contas estao inativas/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Contas" })).toHaveAttribute("href", "/accounts");
  });
});
