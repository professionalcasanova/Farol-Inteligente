import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AccountsPage from "@/app/accounts/page";
import { createAccount, listAccounts, updateAccount, type AccountResponse } from "@/lib/api";
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
    createAccount: vi.fn(),
    listAccounts: vi.fn(),
    updateAccount: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedCreateAccount = vi.mocked(createAccount);
const mockedListAccounts = vi.mocked(listAccounts);
const mockedUpdateAccount = vi.mocked(updateAccount);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

function createAccountResponse(overrides?: Partial<AccountResponse>) {
  return {
    id: "account-1",
    name: "Conta principal",
    type: 2 as const,
    isActive: true,
    createdAtUtc: "2026-03-01T00:00:00Z",
    ...overrides,
  };
}

describe("AccountsPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedCreateAccount.mockReset();
    mockedListAccounts.mockReset();
    mockedUpdateAccount.mockReset();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
  });

  it("Accounts_Load_ShowsActiveAndInactiveSummary", async () => {
    mockedListAccounts.mockResolvedValue([
      createAccountResponse(),
      createAccountResponse({
        id: "account-2",
        name: "Cartao do dia a dia",
        type: 3,
        isActive: false,
      }),
    ]);

    render(<AccountsPage />);

    expect(await screen.findByText("Visao das contas")).toBeInTheDocument();
    expect(screen.getByText("Total")).toBeInTheDocument();
    expect(screen.getByText("Ativas")).toBeInTheDocument();
    expect(screen.getByText("Inativas")).toBeInTheDocument();
    expect(screen.getByTestId("accounts-bounded-list")).toBeInTheDocument();
    expect(screen.getByText("Conta principal")).toBeInTheDocument();
    expect(screen.getByText("Cartao do dia a dia")).toBeInTheDocument();
    expect(screen.getByText("Ativa")).toBeInTheDocument();
    expect(screen.getByText("Inativa")).toBeInTheDocument();
    expect(screen.getAllByText("1")).toHaveLength(2);
  });

  it("Accounts_CreateAccount_AddsNewAccountAndShowsSuccess", async () => {
    const user = userEvent.setup();
    const createdAccount = createAccountResponse({
      id: "account-2",
      name: "Reserva",
      type: 1,
    });

    mockedListAccounts
      .mockResolvedValueOnce([createAccountResponse()])
      .mockResolvedValue([
        createAccountResponse(),
        createdAccount,
      ]);
    mockedCreateAccount.mockResolvedValue(createdAccount);

    render(<AccountsPage />);

    await user.type(await screen.findByLabelText(/nome da conta/i), "Reserva");
    await user.selectOptions(screen.getByLabelText(/tipo da conta/i), "1");
    await user.click(screen.getByRole("button", { name: /criar conta/i }));

    await waitFor(() => {
      expect(mockedCreateAccount).toHaveBeenCalledWith("token", {
        name: "Reserva",
        type: 1,
      });
    });

    expect(await screen.findByText(/Conta criada com sucesso/i)).toBeInTheDocument();
    expect(screen.getByText("Reserva")).toBeInTheDocument();
  });

  it("Accounts_EditAccount_UpdatesDetailsAndInactiveState", async () => {
    const user = userEvent.setup();
    const account = createAccountResponse();
    const updatedAccount = createAccountResponse({
      name: "Conta da casa",
      type: 4,
      isActive: false,
    });

    mockedListAccounts
      .mockResolvedValueOnce([account])
      .mockResolvedValue([updatedAccount]);
    mockedUpdateAccount.mockResolvedValue(updatedAccount);

    render(<AccountsPage />);

    await user.click(await screen.findByRole("button", { name: "Editar" }));
    await user.clear(screen.getByLabelText(/nome da conta/i));
    await user.type(screen.getByLabelText(/nome da conta/i), "Conta da casa");
    await user.selectOptions(screen.getByLabelText(/tipo da conta/i), "4");
    await user.click(screen.getByRole("checkbox", { name: /manter esta conta ativa/i }));
    await user.click(screen.getByRole("button", { name: /salvar conta/i }));

    await waitFor(() => {
      expect(mockedUpdateAccount).toHaveBeenCalledWith("token", "account-1", {
        name: "Conta da casa",
        type: 4,
        isActive: false,
      });
    });

    expect(await screen.findByText(/Conta atualizada com sucesso/i)).toBeInTheDocument();
  });

  it("Accounts_ToggleStatus_ReactivatesInactiveAccount", async () => {
    const user = userEvent.setup();
    const inactiveAccount = createAccountResponse({
      id: "account-2",
      name: "Cartao antigo",
      type: 3,
      isActive: false,
    });
    const reactivatedAccount = {
      ...inactiveAccount,
      isActive: true,
    };

    mockedListAccounts
      .mockResolvedValueOnce([inactiveAccount])
      .mockResolvedValue([reactivatedAccount]);
    mockedUpdateAccount.mockResolvedValue(reactivatedAccount);

    render(<AccountsPage />);

    await user.click(await screen.findByRole("button", { name: "Reativar" }));

    await waitFor(() => {
      expect(mockedUpdateAccount).toHaveBeenCalledWith("token", "account-2", {
        name: "Cartao antigo",
        type: 3,
        isActive: true,
      });
    });

    expect(
      await screen.findByText(
        "Conta reativada. Ela volta a aparecer em novos lancamentos e importacoes.",
      ),
    ).toBeInTheDocument();
  });
});
